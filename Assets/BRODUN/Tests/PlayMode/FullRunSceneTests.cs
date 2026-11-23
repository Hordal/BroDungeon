using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Brodun.Flow;
using Brodun.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace Brodun.Tests.PlayMode
{
    public sealed class FullRunSceneTests
    {
        private GameFlow Flow=>GameFlow.Instance;
        private BattleController Battle=>UnityEngine.Object.FindFirstObjectByType<BattleController>();
        [UnitySetUp] public IEnumerator Boot()
        {
            if(Flow!=null){UnityEngine.Object.Destroy(Flow.gameObject);yield return null;}
            yield return SceneManager.LoadSceneAsync("BrodunBoot");yield return Ready(FlowScreen.Title);
        }
        [UnityTearDown] public IEnumerator Clean(){if(Flow!=null)UnityEngine.Object.Destroy(Flow.gameObject);yield return null;}
        private IEnumerator Ready(FlowScreen screen)
        {
            float deadline=Time.realtimeSinceStartup+15;
            while((Flow==null||Flow.Session.Screen!=screen||Flow.Session.IsTransitioning)&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.NotNull(Flow);Assert.AreEqual(screen,Flow.Session.Screen);Assert.IsFalse(Flow.Session.IsTransitioning);yield return null;
        }
        private static void Pointer(GameObject target)=>ExecuteEvents.Execute(target,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
        private static StyledButton Button(string name)=>UnityEngine.Object.FindObjectsByType<StyledButton>(FindObjectsSortMode.None).Single(b=>b.name==name);
        private static void Click(string name){var b=Button(name);Assert.IsTrue(b.IsInteractable(),name);Pointer(b.gameObject);}
        private GeneratedDungeon Fixture(uint seed)
        {
            // Integration fixture, not a capture mode: real rules/data and connected choices, no auto-win/stat overrides.
            var ids=new[]{"start","fight","shop","rest","altar","puzzle","elite","boss"};var nodes=new List<RoomNode>();
            for(int i=0;i<ids.Length;i++)nodes.Add(new RoomNode(ids[i],i==0?RoomType.Start:i>1&&i<5?RoomType.Event:i==5?RoomType.Puzzle:i==7?RoomType.Boss:RoomType.Battle,
                i,50,i==7?Array.Empty<string>():new[]{ids[i+1]},i==2?EventKind.Shop:i==3?EventKind.RestSite:i==4?EventKind.Altar:EventKind.None,
                floor:1,encounter:i==6?EnemyType.Elite:i==7?EnemyType.Boss:EnemyType.Normal,previous:i==0?Array.Empty<string>():new[]{ids[i-1]}));
            return new GeneratedDungeon(null,nodes.AsReadOnly());
        }
        private IEnumerator StartFixture(uint seed=17,Func<uint,GeneratedDungeon> map=null)
        {
            // Legacy room/economy regression fixture. Formal generated runs are tested separately in PHASE 9.
            var d=Flow.Session.Definitions;
            var legacyEconomy=new GameDefinitions(d.AllCards,d.Characters,d.Enemies,d.Config,new RewardDefinition(d.Reward.Gold,d.Reward.CandidateCount),d.Elements);
            var f=new FlowSession(legacyEconomy,map??Fixture);f.FinishTransition(FlowScreen.Title);
            typeof(GameFlow).GetProperty("Session").SetValue(Flow,f); // Test assembly only; no runtime QA API.
            Assert.IsTrue(Flow.StartNewRun(seed));yield return Ready(FlowScreen.CharacterSelect);Click("Character_Priest");yield return Ready(FlowScreen.DungeonMap);
        }
        private IEnumerator Settled()
        {
            float deadline=Time.realtimeSinceStartup+12;
            while(Flow.Session.IsTransitioning || (Flow.Session.Screen==FlowScreen.Battle&&
                (Flow.Session.Battle.Phase==BattlePhase.ResolvingPlayerAction||Flow.Session.Battle.Phase==BattlePhase.EnemyTurn||Flow.Session.Battle.Phase==BattlePhase.Victory)))
            {Assert.Less(Time.realtimeSinceStartup,deadline);yield return null;}
            yield return null;
        }
        private IEnumerator Win()
        {
            int steps=0;
            while(Flow.Session.Screen==FlowScreen.Battle&&Flow.Session.Battle.Phase==BattlePhase.PlayerTurn&&steps++<260)
            {
                var runtime=Flow.Session.Battle.Runtime;var available=Battle.View.Cards.Where(c=>Flow.Session.Battle.CanPlay(c.InstanceId)).ToArray();
                var c=available.FirstOrDefault(x=>x.Definition.Cost==0)??
                    available.FirstOrDefault(x=>x.Definition.Effects.Any(e=>e.Type==EffectType.Strength))??
                    available.FirstOrDefault(x=>x.Definition.Block>0&&runtime.Block<runtime.CurrentIntent.ExpectedHPDamage(0))??
                    available.OrderByDescending(x=>x.Definition.BaseDamage+x.Definition.Effects.Where(e=>e.Type==EffectType.Poison).Sum(e=>e.Amount*3)).FirstOrDefault();
                if(c!=null)Pointer(c.gameObject);else Click("EndTurn");yield return Settled();
            }
            Assert.IsTrue(Flow.Session.Screen==FlowScreen.RunResult&&Flow.Session.Summary.Status==RunStatus.Complete ||
                Flow.Session.Battle.Phase==BattlePhase.Reward,"Actual card strategy must win, not edit HP.");
        }
        private IEnumerator Reward(RewardType kind)
        {
            if(kind==RewardType.Gold)Click("RewardGold");
            else
            {
                var r=Flow.Session.Battle.Runtime;string id=r.Reward.Candidates.OrderByDescending(c=>r.Data.Card(c).BaseDamage).First();
                Pointer(Battle.View.Root.GetComponentsInChildren<CardView>().Single(c=>c.name=="Reward_"+id).gameObject);
            }
            yield return Ready(FlowScreen.DungeonMap);
        }
        [UnityTest] public IEnumerator AltarBlindSwapRevealsBothActualCardsAndKeepsResultsOnReload()
        {
            var dungeon=new GeneratedDungeon(null,new[]{
                new RoomNode("start",RoomType.Start,0,50,new[]{"altar"},EventKind.None),
                new RoomNode("altar",RoomType.Event,1,50,Array.Empty<string>(),EventKind.Altar,previous:new[]{"start"})
            });
            var data=Flow.Session.Definitions;
            uint seed=Enumerable.Range(0,64).Select(i=>(uint)i).First(i=>
            {
                var run=new RunState(data,CharacterId.Priest,i,dungeon:dungeon);
                var altar=new RoomRuntime(data,run,DungeonRuntime.Enter(run,"altar"));
                return altar.Visit.AltarTrades.Contains(AltarTrade.BlindSwap);
            });
            yield return StartFixture(seed,_=>dungeon);
            Click("Node_altar");
            yield return Ready(FlowScreen.Room);
            var before=Flow.Session.Run.Deck.ToArray();
            Click("TradeTab_4");yield return null;
            Click("Altar_BlindSwap");
            Assert.IsNull(Flow.Session.Room.Visit.SwappedInCardId);
            Assert.IsEmpty(UnityEngine.Object.FindObjectsByType<CardView>(FindObjectsSortMode.None));
            Click("AltarConfirm");
            var visit=Flow.Session.Room.Visit;
            var after=Flow.Session.Run.Deck;
            Assert.AreEqual(visit.SwappedOutCardId,before.Except(after).Single().CardId);
            Assert.AreEqual(visit.SwappedInCardId,after.Except(before).Single().CardId);
            Assert.IsFalse(Button("RoomLeave").IsInteractable());
            AssertCards();
            var gained=UnityEngine.Object.FindObjectsByType<CardView>(FindObjectsSortMode.None).Single(c=>c.name=="AltarGainedCard");
            Assert.Less(gained.ink.alpha,1);
            float deadline=Time.realtimeSinceStartup+2;
            while(!Button("RoomLeave").IsInteractable()&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.AreEqual(1,gained.ink.alpha);
            Assert.IsTrue(Flow.ReloadRoom());
            yield return Ready(FlowScreen.Room);
            AssertCards();
            Click("RoomLeave");
            yield return Ready(FlowScreen.DungeonMap);

            void AssertCards()
            {
                var cards=UnityEngine.Object.FindObjectsByType<CardView>(FindObjectsSortMode.None);
                var removed=cards.Single(card=>card.name=="AltarRemovedCard");
                var added=cards.Single(card=>card.name=="AltarGainedCard");
                Assert.AreEqual(visit.SwappedOutCardId,removed.Definition.Id);
                Assert.AreEqual(visit.SwappedInCardId,added.Definition.Id);
                Assert.NotNull(removed.artwork.sprite);
                Assert.NotNull(added.artwork.sprite);
            }
        }

        [UnityTest] public IEnumerator AltarHidesBlindRewardsUntilConfirmedAndRestoresTheChoice()
        {
            var nodes=new[]{
                new RoomNode("start",RoomType.Start,0,50,new[]{"altar"},EventKind.None),
                new RoomNode("altar",RoomType.Event,1,50,Array.Empty<string>(),EventKind.Altar,previous:new[]{"start"})
            };
            var dungeon=new GeneratedDungeon(null,nodes);
            var data=Flow.Session.Definitions;
            uint seed=Enumerable.Range(0,64).Select(i=>(uint)i).First(i=>
            {
                var run=new RunState(data,CharacterId.Priest,i,dungeon:dungeon);
                var altar=new RoomRuntime(data,run,DungeonRuntime.Enter(run,"altar"));
                return altar.Visit.AltarTrades.Contains(AltarTrade.BlindDoubleDraft);
            });
            yield return StartFixture(seed,_=>dungeon);
            Click("Node_altar");yield return Ready(FlowScreen.Room);
            int deckCount=Flow.Session.Run.Deck.Count;
            Click("TradeTab_4");yield return null;
            Click("Altar_BlindDoubleDraft");
            Assert.IsEmpty(Flow.Session.Room.Visit.PendingAltarCards);
            Assert.AreEqual(deckCount,Flow.Session.Run.Deck.Count);
            Click("AltarConfirm");
            Assert.AreEqual(deckCount-2,Flow.Session.Run.Deck.Count);
            Assert.IsFalse(Button("RoomLeave").IsInteractable());
            Assert.IsTrue(Flow.ReloadRoom());yield return Ready(FlowScreen.Room);
            Click("AltarCard_0");
            Assert.AreEqual(deckCount-1,Flow.Session.Run.Deck.Count);
            Click("RoomLeave");yield return Ready(FlowScreen.DungeonMap);
        }

        [UnityTest] public IEnumerator SanctuaryItemAndCardBuySellTabsPreserveTransactionsOnReload()
        {
            GeneratedDungeon Map(uint seed)=>new GeneratedDungeon(null,new[]{
                new RoomNode("start",RoomType.Start,0,50,new[]{"altar"},EventKind.None),
                new RoomNode("altar",RoomType.Event,1,50,Array.Empty<string>(),EventKind.Altar,previous:new[]{"start"})
            });
            yield return StartFixture(map:Map);var run=Flow.Session.Run;run.AddGold(500);
            Click("Node_altar");yield return Ready(FlowScreen.Room);var room=Flow.Session.Room;
            for(int i=0;i<5;i++)Assert.NotNull(Button("TradeTab_"+i));
            var offer=room.Visit.ItemOffers[0];int itemBuy=offer.Price,itemSell=Item40Catalog.Find(offer.ItemId).SellPrice;
            Click("ItemBuy_0");yield return null;Assert.IsTrue(run.HasItem(offer.ItemId));Assert.AreEqual(500-itemBuy,run.Gold);
            Click("TradeTab_1");yield return null;Click("ItemSell_"+offer.ItemId);yield return null;Assert.IsFalse(run.HasItem(offer.ItemId));
            Click("TradeTab_2");yield return null;var before=new HashSet<string>(run.Deck.Select(c=>c.InstanceId));int cardBuy=room.Visit.Offers[0].Price;
            Click("ShopBuy_0");yield return null;var acquired=run.Deck.Single(c=>!before.Contains(c.InstanceId));int cardSell=room.CardSalePrice(acquired.InstanceId);
            Click("TradeTab_3");yield return null;Click("CardSell_"+acquired.InstanceId);yield return null;
            Assert.IsFalse(run.Deck.Any(c=>c.InstanceId==acquired.InstanceId));Assert.AreEqual(500-itemBuy+itemSell-cardBuy+cardSell,run.Gold);
            Assert.IsTrue(Flow.ReloadRoom());yield return Ready(FlowScreen.Room);Assert.IsFalse(Button("ItemBuy_0").IsInteractable());
            Click("TradeTab_2");yield return null;Assert.IsFalse(Button("ShopBuy_0").IsInteractable());Assert.AreEqual(1,Flow.Session.Room.Visit.CardsSold);
            Click("TradeTab_4");yield return null;Assert.AreEqual(3,Flow.Session.Room.Visit.AltarTrades.Count);
        }

        [UnityTest] public IEnumerator AllRoomTypesResultResetAndResolutionScreensUseRealUi()
        {
            foreach(var size in new[]{new Vector2Int(1920,1080)})
            {
#if UNITY_EDITOR
                UnityEditor.PlayModeWindow.SetCustomRenderingResolution((uint)size.x,(uint)size.y,"BRODUN PHASE6 "+size.x+"x"+size.y);
#endif
                yield return null;yield return null;
                if(Flow.Session.Screen!=FlowScreen.Title){Flow.ReturnToTitle();yield return Ready(FlowScreen.Title);}
                yield return StartFixture();var run=Flow.Session.Run;
                Click("Node_fight");yield return Ready(FlowScreen.Battle);yield return Win();yield return Reward(RewardType.Gold);Assert.AreEqual(50,run.Gold);
                int hp=run.CurrentHP;Click("Node_shop");yield return Ready(FlowScreen.Room);Assert.AreSame(run,Flow.Session.Room.Run);yield return Capture("Shop",size);
                Click("TradeTab_2");yield return null;run.AddGold(200);int shopBudget=run.Gold;
                int index=Flow.Session.Room.Visit.Offers.Select((o,i)=>new{o,i}).OrderByDescending(x=>Flow.Session.Definitions.Card(x.o.CardId).BaseDamage).First().i;
                string acquired=Flow.Session.Room.Visit.Offers[index].CardId;int expectedGold=shopBudget-Flow.Session.Room.Visit.Offers[index].Price;Click("ShopBuy_"+index);Assert.AreEqual(expectedGold,run.Gold);Assert.AreEqual(11,run.Deck.Count);Assert.IsFalse(Flow.Session.Room.Buy(index));
                Assert.IsTrue(Flow.ReloadRoom());yield return Ready(FlowScreen.Room);Click("TradeTab_2");yield return null;Assert.IsFalse(Button("ShopBuy_"+index).interactable);Click("RoomLeave");yield return Ready(FlowScreen.DungeonMap);
                Click("Node_rest");yield return Ready(FlowScreen.Room);yield return Capture("Rest",size);Click("RestChoice");Assert.AreEqual(Math.Min(60,hp+8),run.CurrentHP);Assert.IsFalse(Flow.Session.Room.Choose(RoomChoice.Rest));Click("RoomLeave");yield return Ready(FlowScreen.DungeonMap);
                Click("Node_altar");yield return Ready(FlowScreen.Room);yield return Capture("Altar",size);Assert.AreEqual(3,Flow.Session.Room.Visit.AltarTrades.Count);Assert.AreEqual(expectedGold,run.Gold);Click("RoomLeave");yield return Ready(FlowScreen.DungeonMap);
                Click("Node_puzzle");yield return Ready(FlowScreen.Room);yield return Capture("Puzzle",size);Click("PassChoice");Click("RoomLeave");yield return Ready(FlowScreen.DungeonMap);
                Click("Node_elite");yield return Ready(FlowScreen.Battle);Assert.AreSame(run,Flow.Session.Battle.Run);Assert.AreEqual(EnemyType.Elite,Flow.Session.Battle.Runtime.Enemy.Definition.Type);
                var piles=Flow.Session.Battle.Runtime.Deck;Assert.IsTrue(piles.Hand.Concat(piles.DrawPile).Any(c=>c.CardId==acquired));yield return Win();yield return Reward(RewardType.Card);
                Click("Node_boss");yield return Ready(FlowScreen.Battle);Assert.AreEqual(EnemyType.Boss,Flow.Session.Battle.Runtime.Enemy.Definition.Type);yield return Win();yield return Ready(FlowScreen.RunResult);
                Assert.AreEqual(RunStatus.Complete,run.Status);Assert.AreEqual(7,run.CompletedNodes.Count);Assert.AreEqual(0,DungeonRuntime.Available(run).Count);Assert.IsFalse(Flow.Session.Battle.OpenReward());yield return Capture("RunClear",size);
                Click("ResultNewRun");Assert.IsNull(Flow.Session.Run);Assert.IsNull(Flow.Session.Battle);Assert.IsNull(Flow.Session.Room);yield return Ready(FlowScreen.CharacterSelect);Click("Character_Warrior");yield return Ready(FlowScreen.DungeonMap);
                Assert.AreEqual(0,Flow.Session.Run.Gold);Assert.AreEqual(10,Flow.Session.Run.Deck.Count);Assert.AreEqual(0,Flow.Session.Run.CompletedNodes.Count);Assert.AreEqual("start",Flow.Session.Run.CurrentNode);
                Assert.AreNotSame(run,Flow.Session.Run);Assert.AreEqual(60,Flow.Session.Run.CurrentHP);
                Click("Node_fight");yield return Ready(FlowScreen.Battle);
                for(int n=0;n<40&&Flow.Session.Screen==FlowScreen.Battle&&Flow.Session.Battle.Phase==BattlePhase.PlayerTurn;n++){Click("EndTurn");yield return Settled();}
                yield return Ready(FlowScreen.RunResult);Assert.AreEqual(RunStatus.Failed,Flow.Session.Summary.Status);Assert.IsNull(Flow.Session.Battle.Runtime.Reward);Assert.AreEqual(0,Flow.Session.Run.CompletedNodes.Count);
                yield return Capture("RunFail",size);Click("DefeatTitle");yield return Ready(FlowScreen.Title);Assert.IsNull(Flow.Session.Run);
            }
        }
        private IEnumerator Capture(string name,Vector2Int size)
        {
            foreach(var resolution in new[]{new Vector2Int(1280,720),new Vector2Int(1600,900),new Vector2Int(1920,1080),new Vector2Int(2560,1440)})
            {
#if UNITY_EDITOR
                UnityEditor.PlayModeWindow.SetCustomRenderingResolution((uint)resolution.x,(uint)resolution.y,"BRODUN PHASE6 "+resolution.x+"x"+resolution.y);
#endif
                yield return null;yield return null;yield return CaptureAt(name,resolution);
            }
        }
        private IEnumerator CaptureAt(string name,Vector2Int size)
        {
            yield return null;Canvas.ForceUpdateCanvases();Assert.AreEqual(size.x,Screen.width);Assert.AreEqual(size.y,Screen.height);
            var view=UnityEngine.Object.FindFirstObjectByType<GameScreenView>();Assert.NotNull(view);
            foreach(var t in view.Root.GetComponentsInChildren<RectTransform>().Where(t=>t.GetComponent<StyledButton>()!=null||t.GetComponent<CardView>()!=null||t.name=="RoomHUD"||t.name=="RunResultStats"))
            {
                var corners=new Vector3[4];t.GetWorldCorners(corners);foreach(var p in corners){Assert.That(p.x,Is.InRange(-2,Screen.width+2),t.name);Assert.That(p.y,Is.InRange(-2,Screen.height+2),t.name);}
            }
            foreach(var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))Assert.IsFalse(t.GetComponents<Component>().Any(c=>c==null),"Missing script "+t.name);
            string output=Environment.GetEnvironmentVariable("BRODUN_PHASE6_CAPTURE_DIR")??Path.GetFullPath(Path.Combine(Application.dataPath,"../../../work/phase6/screenshots"));
            Directory.CreateDirectory(output);string path=Path.Combine(output,name+"_"+size.x+"x"+size.y+".png");ScreenCapture.CaptureScreenshot(path);yield return null;yield return null;
            float deadline=Time.realtimeSinceStartup+5;while(!File.Exists(path)&&Time.realtimeSinceStartup<deadline)yield return null;Assert.IsTrue(File.Exists(path));
        }
    }
}

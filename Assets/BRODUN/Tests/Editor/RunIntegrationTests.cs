using System;
using System.Collections.Generic;
using System.Linq;
using Brodun.Flow;
using NUnit.Framework;
namespace Brodun.Tests
{
    public sealed class RunIntegrationTests : LegacyRulesTest
    {
        private GeneratedDungeon Fixture(uint seed)
        {
            var ids=new[]{"start","rest","shop","altar","puzzle","normal","elite","boss"};
            var nodes=new List<RoomNode>();
            for(int i=0;i<ids.Length;i++)nodes.Add(new RoomNode(ids[i],i==0?RoomType.Start:i<4?RoomType.Event:i==4?RoomType.Puzzle:i==7?RoomType.Boss:RoomType.Battle,
                i,50,i==7?Array.Empty<string>():new[]{ids[i+1]},i==1?EventKind.RestSite:i==2?EventKind.Shop:i==3?EventKind.Altar:EventKind.None,
                floor:1,encounter:i==6?EnemyType.Elite:i==7?EnemyType.Boss:EnemyType.Normal,previous:i==0?Array.Empty<string>():new[]{ids[i-1]}));
            return new GeneratedDungeon(null,nodes.AsReadOnly());
        }
        private RunState New(uint seed=17)=>new RunState(Data,CharacterId.Warrior,seed,dungeon:Fixture(seed));
        private RoomRuntime Enter(RunState r,string id)
        {return new RoomRuntime(Data,r,DungeonRuntime.Enter(r,id));}
        private void Pass(RunState r,string id)
        {var room=Enter(r,id);room.Choose(RoomChoice.Leave);Assert.IsTrue(room.Finish());}
        [Test] public void RestUsesWebHealClampsAndCannotRepeatAfterReload()
        {
            var r=New();r.CurrentHP=50;var room=Enter(r,"rest");Assert.IsFalse(room.Finish());Assert.IsFalse(room.Choose(RoomChoice.Explore));
            Assert.IsTrue(room.Choose(RoomChoice.Rest));Assert.AreEqual(58,r.CurrentHP);Assert.IsFalse(room.Choose(RoomChoice.Rest));
            var restored=new RoomRuntime(Data,r,room.Node);Assert.AreSame(room.Visit,restored.Visit);Assert.IsFalse(restored.Choose(RoomChoice.Rest));
            Assert.IsTrue(restored.Finish());Assert.IsFalse(room.Finish());Assert.IsNull(DungeonRuntime.Enter(r,"rest"));
            var full=New();full.CurrentHP=59;var near=Enter(full,"rest");near.Choose(RoomChoice.Rest);Assert.AreEqual(60,full.CurrentHP);
        }
        [Test] public void ShopPurchaseIsAtomicPersistsAndDoesNotAllowDuplicateOrOverspending()
        {
            var r=New();Pass(r,"rest");r.AddGold(50);var shop=Enter(r,"shop");var ids=shop.Visit.Offers.Select(o=>o.CardId).ToArray();
            Assert.AreEqual(3,ids.Distinct().Count());Assert.IsFalse(shop.Buy(-1));Assert.IsFalse(shop.Buy(3));
            Assert.IsTrue(shop.Buy(0));Assert.AreEqual(20,r.Gold);Assert.AreEqual(11,r.Deck.Count);Assert.AreEqual(ids[0],r.Deck.Last().CardId);
            Assert.IsFalse(shop.Buy(0));Assert.IsFalse(shop.Buy(1));var restored=new RoomRuntime(Data,r,shop.Node);Assert.IsFalse(restored.Buy(0));
            CollectionAssert.AreEqual(ids,restored.Visit.Offers.Select(o=>o.CardId));Assert.IsTrue(restored.Finish());Assert.IsFalse(shop.Buy(1));
        }
        [Test] public void ShopCardsAndHpSurviveIntoNextBattleDrawPile()
        {
            var r=New();Pass(r,"rest");r.CurrentHP=38;r.AddGold(50);var shop=Enter(r,"shop");shop.Buy(0);var card=r.Deck.Last();shop.Finish();Pass(r,"altar");Pass(r,"puzzle");
            var battle=new BattleRuntime(Data,r,DungeonRuntime.Enter(r,"normal"));battle.Deck.Draw(30);
            Assert.AreEqual(38,r.CurrentHP);Assert.AreEqual(20,r.Gold);Assert.IsTrue(battle.Deck.Hand.Any(c=>c.InstanceId==card.InstanceId));
            Assert.IsTrue(battle.PlayCard(card.InstanceId));
        }
        private RoomRuntime FindAltar(AltarTrade trade)
        {
            for(uint seed=0;seed<64;seed++)
            {
                var run=New(seed);
                Pass(run,"rest");
                Pass(run,"shop");
                var altar=Enter(run,"altar");
                if(altar.Visit.AltarTrades.Contains(trade))return altar;
            }
            throw new InvalidOperationException("No seed offered "+trade);
        }

        [Test]
        public void AltarOffersPersistAndAllowExactlyOneAffordableTrade()
        {
            var altar=FindAltar(AltarTrade.GoldForHealth);
            var run=altar.Run;
            var reopened=new RoomRuntime(Data,run,altar.Node);
            Assert.AreEqual(3,altar.Visit.AltarTrades.Distinct().Count());
            CollectionAssert.AreEqual(altar.Visit.AltarTrades,reopened.Visit.AltarTrades);
            run.CurrentHP=8;
            Assert.IsFalse(altar.TradeAtAltar(AltarTrade.GoldForHealth));
            Assert.AreEqual(8,run.CurrentHP);
            run.CurrentHP=9;
            Assert.IsTrue(reopened.TradeAtAltar(AltarTrade.GoldForHealth));
            Assert.AreEqual(1,run.CurrentHP);
            Assert.AreEqual(25,run.Gold);
            Assert.IsFalse(altar.TradeAtAltar(AltarTrade.GoldForHealth));
            Assert.IsTrue(altar.Finish());
        }

        [Test]
        public void BlindDraftRevealsOnlyAfterPaymentAndMustBeClaimedOnce()
        {
            var altar=FindAltar(AltarTrade.BlindDoubleDraft);
            var run=altar.Run;
            int count=run.Deck.Count;
            Assert.IsEmpty(altar.Visit.PendingAltarCards);
            Assert.IsTrue(altar.TradeAtAltar(AltarTrade.BlindDoubleDraft));
            Assert.AreEqual(count-2,run.Deck.Count);
            var reopened=new RoomRuntime(Data,run,altar.Node);
            var candidates=reopened.Visit.PendingAltarCards.ToArray();
            Assert.AreEqual(3,candidates.Distinct().Count());
            Assert.IsFalse(reopened.Finish());
            Assert.IsFalse(reopened.TradeAtAltar(AltarTrade.BlindDoubleDraft));
            Assert.IsFalse(reopened.ClaimAltarCard("없는 카드"));
            Assert.IsTrue(reopened.ClaimAltarCard(candidates[0]));
            Assert.AreEqual(count-1,run.Deck.Count);
            Assert.AreEqual(candidates[0],run.Deck.Last().CardId);
            Assert.IsFalse(altar.ClaimAltarCard(candidates[0]));
            Assert.IsTrue(altar.Finish());
        }

        [Test]
        public void CoinRequiresAGuessAndChargesOnce()
        {
            var altar=FindAltar(AltarTrade.Coin);
            var run=altar.Run;
            run.AddGold(10);
            Assert.IsFalse(altar.TradeAtAltar(AltarTrade.Coin));
            Assert.AreEqual(10,run.Gold);
            Assert.IsTrue(altar.TradeAtAltar(AltarTrade.Coin,guess:CoinFace.Heads));
            Assert.That(run.Gold,Is.EqualTo(0).Or.EqualTo(25));
            Assert.IsFalse(altar.TradeAtAltar(AltarTrade.Coin,guess:CoinFace.Tails));
        }
        [Test] public void PuzzleOutcomeIsSeededAndChoiceCannotRepeat()
        {
            foreach(uint seed in new[]{0u,17u,uint.MaxValue})
            {
                var a=New(seed);var b=New(seed);foreach(var r in new[]{a,b}){Pass(r,"rest");Pass(r,"shop");Pass(r,"altar");}
                var x=Enter(a,"puzzle");var y=Enter(b,"puzzle");Assert.AreEqual(x.Visit.PuzzleTreasure,y.Visit.PuzzleTreasure);
                Assert.IsTrue(x.Choose(RoomChoice.Explore));Assert.IsFalse(x.Choose(RoomChoice.Explore));Assert.IsTrue(y.Choose(RoomChoice.Explore));
                Assert.AreEqual(a.Gold,b.Gold);Assert.AreEqual(a.CurrentHP,b.CurrentHP);Assert.IsTrue(x.Finish());Assert.IsFalse(x.Finish());
            }
        }
        [Test] public void LethalPuzzleNeverCompletesRoomOrAllowsMoreChoices()
        {
            for(uint seed=0;seed<100;seed++)
            {
                var r=New(seed);Pass(r,"rest");Pass(r,"shop");Pass(r,"altar");var p=Enter(r,"puzzle");if(p.Visit.PuzzleTreasure)continue;
                r.CurrentHP=1;Assert.IsTrue(p.Choose(RoomChoice.Explore));Assert.AreEqual(RunStatus.Failed,r.Status);Assert.AreEqual(0,r.CurrentHP);
                Assert.IsFalse(p.Finish());Assert.IsFalse(p.Choose(RoomChoice.Leave));Assert.IsFalse(DungeonRuntime.Complete(r));return;
            }
            Assert.Fail("No trap fixture found");
        }
        [Test] public void AllTwelveEnemiesAreInAreaRankPoolsAndSelectionIsReproducible()
        {
            var all=new List<string>();
            for(int area=1;area<=3;area++)foreach(EnemyType rank in Enum.GetValues(typeof(EnemyType)))all.AddRange(EncounterPool.ForArea(Data,area,rank).Select(e=>e.Id));
            CollectionAssert.AreEquivalent(Data.Enemies.Select(e=>e.Id),all);Assert.AreEqual(12,all.Count);
            var used=new HashSet<string>();for(uint seed=0;seed<64;seed++)
            {
                var a=New(seed);var b=New(seed);var node=a.Map.Single(n=>n.Id=="normal");
                var x=EncounterPool.Select(Data,a,node);Assert.AreEqual(x.Id,EncounterPool.Select(Data,b,node).Id);Assert.AreEqual(1,x.Area);used.Add(x.Id);
            }
            Assert.AreEqual(2,used.Count);
        }
        private FlowSession Map()
        {var f=new FlowSession(Data,Fixture);f.FinishTransition(FlowScreen.Title);f.StartNewRun(17);f.FinishTransition(FlowScreen.CharacterSelect);f.ChooseCharacter(CharacterId.Warrior);f.FinishTransition(FlowScreen.DungeonMap);return f;}
        [Test] public void ContentTransitionRequiresMatchingRoomAndCompletesOnlyOnce()
        {
            var f=Map();Assert.IsTrue(f.SelectRoom("rest"));Assert.IsFalse(f.BeginBattle());Assert.IsTrue(f.BeginRoom());Assert.IsFalse(f.BeginRoom());
            f.FinishTransition(FlowScreen.Room);Assert.IsFalse(f.ReturnFromRoom());f.Room.Choose(RoomChoice.Rest);Assert.IsTrue(f.ReturnFromRoom());Assert.IsFalse(f.ReturnFromRoom());
            f.FinishTransition(FlowScreen.DungeonMap);Assert.AreEqual(NodeVisualState.Current,f.NodeState("rest"));Assert.IsFalse(f.SelectRoom("rest"));Assert.IsFalse(f.SelectRoom("boss"));Assert.IsTrue(f.SelectRoom("shop"));
        }
        [Test] public void BossVictoryBypassesRewardAndNewRunClearsEveryContext()
        {
            var f=Map();foreach(var id in new[]{"rest","shop","altar","puzzle"}){f.SelectRoom(id);f.BeginRoom();f.FinishTransition(FlowScreen.Room);if(id!="shop")f.Room.Choose(RoomChoice.Leave);f.ReturnFromRoom();f.FinishTransition(FlowScreen.DungeonMap);}
            foreach(var id in new[]{"normal","elite"}){f.SelectRoom(id);DungeonRuntime.Complete(f.Run);}
            Assert.IsTrue(f.SelectRoom("boss"));Assert.IsTrue(f.BeginBattle());f.FinishTransition(FlowScreen.Battle);var context=f.Battle;context.Runtime.Enemy.CurrentHP=1;
            context.Play(context.Runtime.Deck.Hand.First(c=>Data.Card(c.CardId).BaseDamage>0).InstanceId);context.FinishActionPresentation();
            Assert.IsFalse(context.OpenReward());Assert.IsFalse(context.ResolveReward(RewardType.Gold));Assert.IsTrue(f.FinishRun());Assert.IsFalse(f.FinishRun());
            Assert.AreEqual(RunStatus.Complete,f.Run.Status);Assert.Contains("boss",f.Run.CompletedNodes.ToList());Assert.AreEqual(0,f.Run.Gold);Assert.AreEqual(0,DungeonRuntime.Available(f.Run).Count);
            Assert.IsFalse(f.ReturnFromBattle());Assert.IsFalse(context.CompleteBoss());
            f.FinishTransition(FlowScreen.RunResult);var snapshot=f.Summary;Assert.AreEqual(7,snapshot.ClearedNodes);Assert.IsTrue(f.StartNewRun(29));Assert.IsFalse(f.StartNewRun(17));
            Assert.IsNull(f.Run);Assert.IsNull(f.Battle);Assert.IsNull(f.Room);Assert.IsNull(f.Summary);Assert.IsNull(f.PendingRoom);Assert.IsFalse(f.DeckOpen);
            f.FinishTransition(FlowScreen.CharacterSelect);f.ChooseCharacter(CharacterId.Priest);f.FinishTransition(FlowScreen.DungeonMap);
            Assert.AreEqual(60,f.Run.CurrentHP);Assert.AreEqual(0,f.Run.Gold);Assert.AreEqual(29u,f.Run.Seed);Assert.AreEqual("start",f.Run.CurrentNode);Assert.AreEqual(0,f.Run.CompletedNodes.Count);Assert.AreEqual(0,f.Run.RoomVisits.Count);Assert.AreEqual(0,f.Run.ResolvedRewards.Count);Assert.AreEqual(10,f.Run.Deck.Count);
            Assert.AreEqual(RunStatus.Complete,snapshot.Status);
        }
        [Test] public void BattleDeathRoutesToFrozenFailureWithoutRewardCompletion()
        {
            var f=new FlowSession(Data);f.FinishTransition(FlowScreen.Title);f.StartNewRun(17);f.FinishTransition(FlowScreen.CharacterSelect);f.ChooseCharacter(CharacterId.Warrior);f.FinishTransition(FlowScreen.DungeonMap);
            f.SelectRoom("r1");f.BeginBattle();f.FinishTransition(FlowScreen.Battle);f.Run.CurrentHP=1;f.Battle.EndTurn();f.Battle.FinishActionPresentation();
            Assert.IsFalse(f.Battle.OpenReward());Assert.IsNull(f.Battle.Runtime.Reward);Assert.IsTrue(f.FinishRun());f.FinishTransition(FlowScreen.RunResult);
            Assert.AreEqual(RunStatus.Failed,f.Summary.Status);Assert.AreEqual(0,f.Summary.HP);Assert.AreEqual(0,f.Summary.ClearedNodes);Assert.IsFalse(f.Battle.EndTurn());
            Assert.IsFalse(DungeonRuntime.Complete(f.Run));Assert.IsTrue(f.ReturnToTitle());Assert.IsNull(f.Run);Assert.IsNull(f.Battle);Assert.IsNull(f.Summary);Assert.AreEqual(0u,f.PendingSeed);
        }
    }
}

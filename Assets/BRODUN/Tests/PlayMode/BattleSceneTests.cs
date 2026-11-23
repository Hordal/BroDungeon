using System;
using System.Collections;
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
    public sealed class BattleSceneTests
    {
        private GameFlow Flow=>GameFlow.Instance;
        private BattleController Controller=>UnityEngine.Object.FindFirstObjectByType<BattleController>();
        private BattleContext Context=>Flow.Session.Battle;
        [UnitySetUp] public IEnumerator Boot()
        {
            if(Flow!=null){UnityEngine.Object.Destroy(Flow.gameObject);yield return null;}
            yield return SceneManager.LoadSceneAsync("BrodunBoot");yield return Ready(FlowScreen.Title);
        }
        [UnityTearDown] public IEnumerator Clean(){if(Flow!=null)UnityEngine.Object.Destroy(Flow.gameObject);yield return null;}
        private IEnumerator Ready(FlowScreen screen)
        {
            float end=Time.realtimeSinceStartup+15;
            while((Flow==null||Flow.Session.Screen!=screen||Flow.Session.IsTransitioning)&&Time.realtimeSinceStartup<end)yield return null;
            Assert.NotNull(Flow);Assert.AreEqual(screen,Flow.Session.Screen);Assert.IsFalse(Flow.Session.IsTransitioning);yield return null;
        }
        private static void Click(GameObject target)
        {ExecuteEvents.Execute(target,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);}
        private void Button(string name)=>Click(UnityEngine.Object.FindObjectsByType<StyledButton>(FindObjectsSortMode.None).Single(b=>b.name==name).gameObject);
        // Scripted card-effect checks use a stable target so balance updates do not alter their sequence.
        private static EnemyDefinition CardEffectTestEnemy(EnemyDefinition enemy)
        {
            if(enemy.Area!=1 || enemy.Type!=EnemyType.Normal)return enemy;
            return new EnemyDefinition(enemy.Id,enemy.Name,enemy.Type,enemy.Area,38,enemy.AreaHPStep,enemy.AssetId,
                new[]{new IntentData(IntentType.Attack,8),new IntentData(IntentType.Attack,10),new IntentData(IntentType.Block,5)},enemy.IsLegacy);
        }

        private IEnumerator LegacyMap(uint seed=17,CharacterId hero=CharacterId.Warrior)
        {
            var d=Flow.Session.Definitions;
            var snapshot=JsonUtility.FromJson<Brodun.Data.WebSnapshot>(File.ReadAllText("Assets/BRODUN/Data/WebRulesSnapshot.json"));
            var heroes=d.Characters.Select(h=>
            {
                string prefix=h.Id==CharacterId.Warrior?"B-W":h.Id==CharacterId.Mage?"B-M":"B-P";
                var legacy=snapshot.characters.Single(c=>c.id==h.Id.ToString().ToLowerInvariant()).starter;
                return new CharacterDefinition(h.Id,h.Name,h.Description,h.MaxHP,h.Energy,
                    Enumerable.Repeat(prefix+"01",3).Concat(Enumerable.Repeat(prefix+"02",3)).Concat(legacy),legacy);
            });
            var legacyData=new GameDefinitions(d.AllCards,heroes,d.Enemies,d.Config,new RewardDefinition(d.Reward.Gold,d.Reward.CandidateCount),d.Elements);
            Assert.IsTrue(Flow.StartEditorPreviewRun(legacyData,seed));yield return Ready(FlowScreen.CharacterSelect);Button("Character_"+hero);yield return Ready(FlowScreen.DungeonMap);}
        private IEnumerator Enter(string node)
        {Button("Node_"+node);yield return Ready(FlowScreen.Battle);Assert.NotNull(Controller);Assert.AreEqual(node,Context.Room.Id);}
        private IEnumerator Settled()
        {
            float end=Time.realtimeSinceStartup+10;
            while((Context.Phase==BattlePhase.ResolvingPlayerAction||Context.Phase==BattlePhase.EnemyTurn||Context.Phase==BattlePhase.Victory)&&Time.realtimeSinceStartup<end)yield return null;
            Assert.Less(Time.realtimeSinceStartup,end,"Battle visual sequence stalled");yield return null;
        }
        private CardView Card(string id)
        {
            // Legacy-effect probes ask for a particular card; the normal draw tests inspect the hand untouched.
            var view=Controller.View;
            var card=view.Cards.FirstOrDefault(c=>c.Definition.Id==id);
            if(card!=null)return card;
            var deck=Context.Runtime.Deck;
            while(!deck.Hand.Any(c=>c.CardId==id)&&(deck.DrawPile.Count>0||deck.DiscardPile.Count>0))
                deck.Draw(1);
            view.RefreshHand(c=>Controller.PlayCard(c.InstanceId));view.Refresh();
            return view.Cards.First(c=>c.Definition.Id==id);
        }
        private IEnumerator Play(string id){Click(Card(id).gameObject);yield return Settled();}
        // Test-only strategy. Uses public input handlers; never edits HP/damage/intent or auto-wins.
        private IEnumerator Win()
        {
            int actions=0;
            while(Context.Phase==BattlePhase.PlayerTurn && actions++<240)
            {
                var b=Context.Runtime;
                var playable=Controller.View.Cards.Where(c=>Context.CanPlay(c.InstanceId)).ToArray();
                var card=playable.FirstOrDefault(c=>c.Definition.Cost==0)??
                    playable.FirstOrDefault(c=>c.Definition.Block>0&&b.Block<b.CurrentIntent.ExpectedHPDamage(0))??
                    playable.OrderByDescending(c=>c.Definition.BaseDamage+c.Definition.Effects.Where(e=>e.Type==EffectType.Poison).Sum(e=>e.Amount*3)).FirstOrDefault();
                if(card!=null){Click(card.gameObject);yield return Settled();}
                else {Button("EndTurn");yield return Settled();}
            }
            Assert.AreEqual(BattlePhase.Reward,Context.Phase,"Test strategy must actually win the encounter");
        }
        [UnityTest] public IEnumerator RealSceneCardInputIntentAndGoldReturnLoop()
        {
            yield return LegacyMap();var run=Flow.Session.Run;yield return Enter("r2");
            Assert.AreEqual("BrodunBattle",SceneManager.GetActiveScene().name);Assert.AreSame(run,Context.Run);
            int hp=Context.Runtime.Enemy.CurrentHP;var strike=Card("earth_strike");Click(strike.gameObject);
            Assert.AreEqual(hp-5,Context.Runtime.Enemy.CurrentHP);Assert.AreEqual(4,Context.Runtime.Energy);
            Assert.IsFalse(Controller.PlayCard(strike.InstanceId));Assert.IsFalse(Controller.TryEndTurn());yield return Settled();
            var announced=Context.Runtime.CurrentIntent;
            Assert.AreEqual(9,announced.TotalDamage);
            Assert.AreEqual("9",Controller.View.Enemy.intentLabel.text);
            Button("EndTurn");Assert.IsFalse(Controller.TryEndTurn());yield return Settled();
            Assert.AreEqual(announced.Amount,Context.Runtime.LastEnemyAction.Intent.Amount);Assert.AreEqual(announced.Hits,Context.Runtime.LastEnemyAction.Intent.Hits);
            yield return Win();Assert.AreEqual(0,run.CompletedNodes.Count);Assert.AreEqual(0,run.Gold);
            Button("RewardGold");Assert.IsFalse(Controller.TryChooseReward(RewardType.Gold));Assert.AreEqual(50,run.Gold);
            yield return Ready(FlowScreen.DungeonMap);Assert.AreSame(run,Flow.Session.Run);Assert.Contains("r2",run.CompletedNodes.ToList());
            CollectionAssert.AreEquivalent(DungeonRuntime.Node(run,"r2").Next,DungeonRuntime.Available(run));
            Assert.IsTrue(UnityEngine.Object.FindFirstObjectByType<GameScreenView>().Root.GetComponentsInChildren<Text>().Any(t=>t.text=="GOLD  50"));
        }
        [UnityTest] public IEnumerator CardRewardReloadCannotDuplicateAndAcquiredCardAppearsNextBattle()
        {
            yield return LegacyMap();yield return Enter("r2");yield return Win();var battle=Context;var reward=battle.Runtime.Reward;var run=battle.Run;
            Assert.IsTrue(Flow.ReloadBattle());yield return Ready(FlowScreen.Battle);Assert.AreSame(battle,Context);Assert.AreSame(reward,Context.Runtime.Reward);
            Assert.AreEqual(SpriteMotion.Death,Controller.View.Enemy.Motion,"A reward-scene reload must not resurrect the enemy view");
            string id=reward.Candidates[0];Click(Controller.View.Root.GetComponentsInChildren<CardView>().Single(c=>c.name=="Reward_"+id).gameObject);
            Assert.AreEqual(11,run.Deck.Count);Assert.IsFalse(Controller.TryChooseReward(RewardType.Card,id));yield return Ready(FlowScreen.DungeonMap);
            Button("DeckButton");yield return null;Assert.AreEqual(11,UnityEngine.Object.FindFirstObjectByType<GameScreenView>().Root.GetComponentsInChildren<LayoutElement>().Length);Button("DeckClose");yield return null;
            string next=DungeonRuntime.Available(run).First(n=>DungeonRuntime.Node(run,n).Type==RoomType.Battle);
            yield return Enter(next);var card=Card(id);Assert.AreEqual(id,card.Definition.Id);Assert.IsTrue(Controller.PlayCard(card.InstanceId));yield return Settled();
        }
        [UnityTest] public IEnumerator SkipCompletesRoomWithoutCurrencyOrCardAndDefeatNeverCompletes()
        {
            yield return LegacyMap();yield return Enter("r2");yield return Win();var run=Context.Run;Button("RewardSkip");yield return Ready(FlowScreen.DungeonMap);
            Assert.AreEqual(0,run.Gold);Assert.AreEqual(10,run.Deck.Count);Assert.AreEqual(1,run.ResolvedRewards.Count);
            Flow.ReturnToTitle();yield return Ready(FlowScreen.Title);yield return LegacyMap();yield return Enter("r2");var failed=Context.Run;
            for(int i=0;i<30&&Context.Phase==BattlePhase.PlayerTurn;i++){Button("EndTurn");yield return Settled();}
            Assert.AreEqual(BattlePhase.Defeat,Context.Phase);Assert.AreEqual(0,failed.CurrentHP);Assert.AreEqual(0,failed.CompletedNodes.Count);Assert.IsNull(Context.Runtime.Reward);
            // PHASE 6 replaces the old defeat overlay with the shared run-result screen.
            Assert.IsFalse(Context.EndTurn());yield return Ready(FlowScreen.RunResult);
            Assert.AreEqual(RunStatus.Failed,Flow.Session.Summary.Status);Button("DefeatTitle");yield return Ready(FlowScreen.Title);Assert.IsNull(Flow.Session.Run);
        }
        [UnityTest] public IEnumerator NormalAndEliteCanBeReachedAndWonThroughConnectedMapRooms()
        {
            uint seed=0;string[] route=null;
            for(;seed<1000&&route==null;seed++)
            {
                var map=LayeredMapGenerator.Generate(seed).Rooms.ToDictionary(n=>n.Id);
                foreach(string a in map["start"].Next)foreach(string b in map[a].Next)foreach(string e in map[b].Next)
                    if(map[b].Type==RoomType.Battle&&map[e].Encounter==EnemyType.Elite){route=new[]{a,b,e};break;}
                if(route!=null)break;
            }
            Assert.NotNull(route);yield return LegacyMap(seed);
            foreach(string node in route)
            {
                yield return Enter(node);yield return Win();string reward=Context.Runtime.Reward.Candidates.OrderByDescending(id=>Context.Runtime.Data.Card(id).BaseDamage).First();
                if(node==route.Last())Assert.AreEqual(EnemyType.Elite,Context.Runtime.Enemy.Definition.Type);
                Click(Controller.View.Root.GetComponentsInChildren<CardView>().Single(c=>c.name=="Reward_"+reward).gameObject);yield return Ready(FlowScreen.DungeonMap);
            }
            Assert.AreEqual(3,Flow.Session.Run.CompletedNodes.Count);Assert.AreEqual(13,Flow.Session.Run.Deck.Count);
        }
        [UnityTest] public IEnumerator CardTypesCostZeroStatusExhaustMultiHitAndConditionalFeedbackUseRealUi()
        {
            foreach(string id in new[]{"earth_crush","poison_touch","fire_strength","lightning_chain","lightning_charge","stone_retaliation"})
            {
                if(Flow.Session.Screen!=FlowScreen.Title){Flow.ReturnToTitle();yield return Ready(FlowScreen.Title);}
                yield return LegacyMap();Flow.Session.Run.AddCard(id);yield return Enter("r2");
                // Fixture-only extra draw exposes both the sampled card and the starter guard;
                // production initial hand size remains the PHASE 2 value (4).
                Context.Runtime.Deck.Draw(1);Controller.View.RefreshHand(c=>Controller.PlayCard(c.InstanceId));Controller.View.Refresh();
                var card=Card(id);Assert.AreEqual(card.Definition.Cost.ToString(),card.cost.text);Assert.AreEqual(card.Definition.Description,card.description.text);
                if(id=="stone_retaliation"){yield return Play("stone_guard");card=Card(id);}
                Click(card.gameObject);yield return Settled();
                Assert.IsTrue((card.Definition.Exhausts?Context.Runtime.Deck.ExhaustPile:Context.Runtime.Deck.DiscardPile).Any(c=>c.InstanceId==card.InstanceId));
                if(id=="lightning_chain")Assert.AreEqual(6,Context.Runtime.LastCardResult.Damage);
                if(id=="poison_touch")Assert.AreEqual(3,Context.Runtime.Enemy.Status.Poison);
                if(id=="fire_strength")Assert.AreEqual(2,Context.Runtime.Strength);
                if(id=="stone_retaliation")Assert.IsTrue(Context.Runtime.LastCardResult.ConditionalTriggered);
            }
        }
        [UnityTest] public IEnumerator WaterIceComboAndInsufficientEnergyUseRuntimeResults()
        {
            yield return LegacyMap();Flow.Session.Run.AddCard("water_wave");Flow.Session.Run.AddCard("ice_link");Flow.Session.Run.AddCard("earth_blessing");yield return Enter("r2");
            Context.Runtime.Deck.Draw(3);Controller.View.RefreshHand(c=>Controller.PlayCard(c.InstanceId));Controller.View.Refresh();
            yield return Play("water_wave");Assert.AreEqual(ElementType.Water,Context.Runtime.Enemy.Element);
            yield return Play("ice_link");Assert.IsTrue(Context.Runtime.LastCardResult.ConditionalTriggered);Assert.AreEqual(11,Context.Runtime.LastCardResult.Damage);
            Assert.IsTrue(Context.Runtime.LastCardResult.HitEvents.Any(e=>e.Cause==CombatCause.Conditional&&e.Hit.Result.HPDamage==7));
            yield return Play("stone_guard");yield return Play("earth_blessing");
            yield return Play("earth_strike");Assert.AreEqual(0,Context.Runtime.Energy);int hp=Context.Runtime.Enemy.CurrentHP;
            var cut=Card("wind_cut");Assert.IsFalse(cut.CanPlay);Click(cut.gameObject);yield return null;
            Assert.AreEqual(hp,Context.Runtime.Enemy.CurrentHP);Assert.AreEqual(0,Context.Runtime.Energy);Assert.IsTrue(Context.Runtime.Deck.Hand.Any(c=>c.CardId=="wind_cut"));
        }
        [UnityTest] public IEnumerator BattleScreenshotsHoverAndBoundsAtThreeResolutions()
        {
            foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1600,900),new Vector2Int(1280,720),new Vector2Int(2560,1440)})
            {
#if UNITY_EDITOR
                UnityEditor.PlayModeWindow.SetCustomRenderingResolution((uint)size.x,(uint)size.y,"BRODUN Battle QA "+size.x);
#endif
                yield return null;yield return null;
                if(Flow.Session.Screen!=FlowScreen.Title){Flow.ReturnToTitle();yield return Ready(FlowScreen.Title);}
                yield return LegacyMap();yield return Enter("r2");yield return Capture("BattleStart",size);
                var strike=Card("earth_strike");strike.OnPointerEnter(new PointerEventData(EventSystem.current));yield return new WaitForSecondsRealtime(.25f);yield return Capture("CardHover",size);
                strike.OnPointerExit(new PointerEventData(EventSystem.current));
                var hover=Controller.View.Enemy.GetComponentInChildren<IntentHover>();hover.OnPointerEnter(new PointerEventData(EventSystem.current));yield return Capture("EnemyIntent",size);hover.OnPointerExit(new PointerEventData(EventSystem.current));
                Click(strike.gameObject);yield return new WaitForSecondsRealtime(.24f);yield return Capture("EnemyHurt",size);yield return Settled();
                Button("EndTurn");yield return new WaitForSecondsRealtime(.12f);yield return Capture("EnemyTurn",size);yield return Settled();
                yield return Win();yield return Capture("VictoryCardReward",size);
                Assert.AreEqual(SpriteMotion.Death,Controller.View.Enemy.Motion);yield return new WaitForSecondsRealtime(.4f);Assert.AreEqual(SpriteMotion.Death,Controller.View.Enemy.Motion);
                Button("RewardGold");yield return Capture("GoldReward",size);yield return Ready(FlowScreen.DungeonMap);
                yield return Capture("DungeonReturn",size);
            }
        }
        private IEnumerator Capture(string label,Vector2Int size)
        {
            if(label=="VictoryCardReward"||label=="GoldReward")yield return new WaitForSecondsRealtime(.22f);
            yield return null;Canvas.ForceUpdateCanvases();Assert.AreEqual(size.x,Screen.width);Assert.AreEqual(size.y,Screen.height);
            var root=Controller!=null?Controller.View.Root:UnityEngine.Object.FindFirstObjectByType<GameScreenView>().Root;
            foreach(var r in root.GetComponentsInChildren<RectTransform>().Where(r=>r.GetComponent<CardView>()!=null||r.name=="EndTurn"||r.name=="Intent"||r.name=="Energy"))
            {var corners=new Vector3[4];r.GetWorldCorners(corners);foreach(var p in corners){Assert.That(p.x,Is.InRange(-2f,Screen.width+2f));Assert.That(p.y,Is.InRange(-2f,Screen.height+2f));}}
            string directory=Environment.GetEnvironmentVariable("BRODUN_BATTLE_CAPTURE_DIR")??Path.GetFullPath(Path.Combine(Application.dataPath,"../../../work/phase4/screenshots"));Directory.CreateDirectory(directory);
            string path=Path.Combine(directory,label+"_"+size.x+"x"+size.y+".png");ScreenCapture.CaptureScreenshot(path);yield return null;yield return null;
            float deadline=Time.realtimeSinceStartup+5;while(!File.Exists(path)&&Time.realtimeSinceStartup<deadline)yield return null;Assert.IsTrue(File.Exists(path));
        }
        [UnityTest] public IEnumerator PlayedCardFadesWithSeparateSparksBeforeLeavingHand()
        {
            yield return LegacyMap();
            yield return Enter("r2");
            var card=Card("earth_strike");
            Assert.IsTrue(Controller.PlayCard(card.InstanceId));
            Assert.IsTrue(card.IsPresenting);
            var sparks=card.transform.Find("Card use effect").GetComponent<BattleVfx>();
            Assert.AreNotSame(card.ink.transform,sparks.transform.parent);
            Assert.IsFalse(sparks.raycastTarget);
            float deadline=Time.realtimeSinceStartup+1;
            while(card.ink.alpha>=.9f&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.Less(card.ink.alpha,.9f);
            yield return Settled();
            Assert.IsFalse(Context.Runtime.Deck.Hand.Any(c=>c.CardId=="earth_strike"));
        }

        [UnityTest] public IEnumerator HoverIsIdempotentAndUsedCardOwnsItsVisualUntilDiscard()
        {
            yield return LegacyMap();yield return Enter("r2");var card=Card("earth_strike");
            var hitRect=(RectTransform)card.transform;var position=hitRect.anchoredPosition;
            var pointer=new PointerEventData(EventSystem.current);
            card.OnPointerEnter(pointer);card.OnPointerEnter(pointer);yield return new WaitForSecondsRealtime(.2f);
            Assert.IsTrue(card.Hovered);Assert.AreEqual(position,hitRect.anchoredPosition);
            Assert.Greater(card.visual.anchoredPosition.y,35);Assert.Greater(card.visual.localScale.x,1);
            card.OnPointerExit(pointer);yield return new WaitForSecondsRealtime(.2f);
            Assert.IsFalse(card.Hovered);Assert.Less(card.visual.anchoredPosition.y,5);
            Click(card.gameObject);yield return new WaitForSecondsRealtime(.12f);
            Assert.IsTrue(card.IsPresenting);Assert.Greater(card.visual.anchoredPosition.y,45);
            Assert.AreEqual("HP  60 / 60",Controller.View.Root.GetComponentsInChildren<Text>().Single(t=>t.name=="Player HP").text);
            yield return Settled();Assert.IsFalse(Context.Runtime.Deck.Hand.Any(c=>c.CardId=="earth_strike"));
        }
        [UnityTest] public IEnumerator LargeHandUsesPagesAndEveryCardRemainsAccessible()
        {
            yield return LegacyMap();for(int i=0;i<6;i++)Flow.Session.Run.AddCard("earth_strike");yield return Enter("r2");
            Context.Runtime.Deck.Draw(20);Controller.View.RefreshHand(c=>Controller.PlayCard(c.InstanceId));Controller.View.Refresh();
            Assert.AreEqual(16,Controller.View.Cards.Count);
            for(int page=0;page<3;page++)
            {
                Controller.View.HandLayout.ShowPage(page);yield return null;Canvas.ForceUpdateCanvases();
                var visible=Controller.View.Cards.Where(c=>c.gameObject.activeSelf).ToArray();
                Assert.AreEqual(page<2?6:4,visible.Length);
                foreach(var card in visible)
                {var corners=new Vector3[4];((RectTransform)card.transform).GetWorldCorners(corners);Assert.GreaterOrEqual(corners[0].x,0);Assert.LessOrEqual(corners[2].x,Screen.width);}
            }
            var target=Controller.View.Cards.Last();Assert.IsTrue(target.gameObject.activeSelf);
            Click(target.gameObject);yield return Settled();Assert.AreEqual(15,Controller.View.Cards.Count);
        }
        private static string ImpactName(CombatEvent entry)=>$"Contact VFX {entry.ActionId}:{entry.Index} {entry.TargetId}";
        private BattleVfx Impact(CombatEvent entry)=>Controller.View.Root.Find(ImpactName(entry))?.GetComponent<BattleVfx>();
        private static void AssertImpactAt(BattleVfx effect,Image sprite)
        {
            Assert.NotNull(effect);Assert.IsFalse(effect.raycastTarget);
            Canvas.ForceUpdateCanvases();
            Assert.Greater(effect.canvasRenderer.GetMesh().vertexCount,0);
            var center=sprite.rectTransform.InverseTransformPoint(effect.rectTransform.position);
            var bounds=sprite.rectTransform.rect;
            Assert.That(center.x,Is.InRange(bounds.xMin,bounds.xMax));
            Assert.That(center.y,Is.InRange(bounds.yMin,bounds.yMax));
        }
        private static void CaptureImpact(string label)
        {
            string directory=Environment.GetEnvironmentVariable("BRODUN_PHASE3_IMPACT_CAPTURE_DIR");
            if(string.IsNullOrEmpty(directory))return;
            Directory.CreateDirectory(directory);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,label+".png"));
        }
        [UnityTest] public IEnumerator AttackFramesVfxAndImpactStayPresentationOnly()
        {
#if UNITY_EDITOR
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1920,1080,"BRODUN Phase 3 player contact");
#endif
            yield return null;yield return LegacyMap();yield return Enter("r2");
            var motions=Resources.Load<CombatMotionAssets>("BrodunCombatMotion");Assert.AreEqual(3,motions.heroes.Length);
            foreach(var hero in motions.heroes)foreach(var sequence in hero.animations)Assert.AreEqual(4,sequence.frames.Length);

            var view=Controller.View;var player=view.PlayerActor.GetComponent<Image>();
            var origin=player.rectTransform.anchoredPosition;
            foreach(string id in new[]{"earth_strike","wind_cut"})
            {
                int hp=Context.Runtime.Enemy.CurrentHP;
                bool capture=id=="earth_strike"&&!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BRODUN_PHASE3_IMPACT_CAPTURE_DIR"));
                int frame=0;float nextCapture=0;
                if(capture){CaptureImpact($"A-{frame++:000}");yield return null;}
                Click(Card(id).gameObject);
                var hit=Context.Runtime.LastCardResult.HitEvents.Single();
                Assert.That(Context.Runtime.Energy,Is.EqualTo(id=="earth_strike"?4:3));
                bool sawAttack=false,sawContact=false;
                float deadline=Time.realtimeSinceStartup+5;
                while(Context.Phase==BattlePhase.ResolvingPlayerAction&&Time.realtimeSinceStartup<deadline)
                {
                    Assert.AreEqual(origin,player.rectTransform.anchoredPosition);
                    if(view.PlayerActor.IsAttacking)
                    {
                        Assert.That(player.sprite.name,Does.StartWith("attack_"));sawAttack=true;
                    }
                    var impact=Impact(hit);
                    if(!sawContact&&impact==null)Assert.AreEqual($"{hp} / {Context.Runtime.Enemy.MaxHP}",view.Enemy.hp.text);
                    if(!sawContact&&impact!=null)
                    {
                        AssertImpactAt(impact,view.Enemy.sprite);
                        Assert.That(player.sprite.name,Does.StartWith("attack_02"));
                        Assert.AreEqual(SpriteMotion.Hurt,view.Enemy.Motion);
                        Assert.AreEqual($"{hit.Hit.HPAfter} / {Context.Runtime.Enemy.MaxHP}",view.Enemy.hp.text);
                        Assert.IsNull(view.Root.Find("Combat stage/Card VFX earth_strike"));
                        sawContact=true;
                        if(capture){CaptureImpact("A-contact");yield return null;}
                    }
                    if(capture&&Time.realtimeSinceStartup>=nextCapture)
                    {CaptureImpact($"A-{frame++:000}");nextCapture=Time.realtimeSinceStartup+.055f;}
                    yield return null;
                }
                Assert.IsTrue(sawAttack);Assert.IsTrue(sawContact);
                Assert.AreEqual(BattlePhase.PlayerTurn,Context.Phase);
                Assert.AreEqual(hit.Hit.HPAfter,Context.Runtime.Enemy.CurrentHP,"The presentation must not apply damage a second time.");
                Assert.AreEqual(origin,player.rectTransform.anchoredPosition);
                Assert.IsFalse(view.PlayerActor.IsAttacking);
                if(capture)
                {
                    yield return new WaitForSecondsRealtime(.25f);
                    CaptureImpact($"A-{frame++:000}");yield return null;CaptureImpact("A-after");yield return null;
                }
            }
        }
        [UnityTest] public IEnumerator EnemyAttacksInPlaceAndPlayerVitalsFollowRecordedContact()
        {
#if UNITY_EDITOR
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1920,1080,"BRODUN Phase 3 enemy contact");
#endif
            yield return null;yield return LegacyMap();yield return Enter("r2");yield return Play("stone_guard");
            var view=Controller.View;var origin=view.Enemy.sprite.rectTransform.anchoredPosition;
            var hp=view.Root.GetComponentsInChildren<Text>().Single(t=>t.name=="Player HP");
            string before=hp.text;
            Button("EndTurn");
            var hit=Context.Runtime.LastTurnResult.Events.Single(e=>e.Hit!=null&&e.TargetId==CombatTrace.PlayerId);
            Assert.Greater(hit.Hit.Result.Blocked,0);Assert.Greater(hit.Hit.Result.HPDamage,0);
            bool sawAttack=false,sawContact=false;
            float deadline=Time.realtimeSinceStartup+4;
            while(Context.Phase==BattlePhase.EnemyTurn&&Time.realtimeSinceStartup<deadline)
            {
                if(view.Enemy.Motion==SpriteMotion.Attack){sawAttack=true;Assert.AreEqual(origin,view.Enemy.sprite.rectTransform.anchoredPosition);}
                if(!sawContact)
                {
                    var effect=Impact(hit);
                    if(effect==null)Assert.AreEqual(before,hp.text);
                    else
                    {
                        AssertImpactAt(effect,view.PlayerActor.GetComponent<Image>());
                        Assert.AreEqual($"{hit.Hit.HPAfter} / {Context.Run.MaxHP}",hp.text);
                        var labels=view.Root.GetComponentsInChildren<Text>().Where(t=>t.name=="Floating feedback player").ToArray();
                        var absorbed=labels.Single(t=>t.text.Contains("방어 −"));
                        var damage=labels.Single(t=>t.text.Contains("피해 "));
                        Assert.Greater(Mathf.Abs(absorbed.rectTransform.anchoredPosition.y-damage.rectTransform.anchoredPosition.y),40);
                        CaptureImpact("B-contact");sawContact=true;
                    }
                }
                yield return null;
            }
            Assert.IsTrue(sawAttack);Assert.IsTrue(sawContact);
            Assert.AreEqual(hit.Hit.HPAfter,Context.Run.CurrentHP);
            Assert.AreEqual(BattlePhase.PlayerTurn,Context.Phase);
        }
#if UNITY_EDITOR
        [UnityTest] public IEnumerator AreaAndReactionContactsUseInstanceIdsEvenWhenFirstEnemyDies()
        {
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1280,720,"BRODUN Phase 3 area contact");
            yield return null;
            const string id="phase3_impact_probe";
            var card=new CardDefinition(id,"감전 검증","모든 적에게 피해 2.",1,ElementType.Lightning,
                new[]{new CardEffect(EffectType.Damage,2)},CardTargetScope.AllEnemies,ElementApplication.SharedAttack);
            yield return PreviewTwoWithCard(card);
            var enemies=Context.Runtime.Enemies;
            enemies[0].CurrentHP=2;enemies[0].ElementMark=ElementType.Water;
            Controller.View.Refresh();
            Click(Card(id).gameObject);
            var result=Context.Runtime.LastCardResult;
            var hits=result.HitEvents.Where(e=>e.Hit.Result.HPDamage>0||e.Hit.Result.Blocked>0).ToArray();
            Assert.AreEqual(3,hits.Length);
            CollectionAssert.AreEquivalent(enemies.Select(e=>e.TargetId),hits.Select(e=>e.TargetId).Distinct());
            Assert.IsFalse(enemies[0].IsAlive);Assert.IsTrue(enemies[1].IsAlive);
            Assert.IsTrue(hits.Any(e=>e.Cause==CombatCause.Spread&&e.TargetId==enemies[1].TargetId));
            var shown=new System.Collections.Generic.HashSet<string>();
            bool captured=false;
            float deadline=Time.realtimeSinceStartup+5;
            while(Context.Phase==BattlePhase.ResolvingPlayerAction&&Time.realtimeSinceStartup<deadline)
            {
                foreach(var hit in hits)
                {
                    var effect=Impact(hit);
                    if(effect==null||!shown.Add(effect.name))continue;
                    var model=enemies.Single(enemy=>enemy.TargetId==hit.TargetId);
                    var target=Controller.View.EnemyFor(model);
                    AssertImpactAt(effect,target.sprite);
                    Assert.AreEqual($"{hit.Hit.HPAfter} / {model.MaxHP}",target.hp.text);
                    Assert.AreEqual(SpriteMotion.Hurt,target.Motion);
                }
                if(!captured&&Controller.View.Root.GetComponentsInChildren<BattleVfx>().Count(v=>v.name.StartsWith("Contact VFX "))>=2)
                {CaptureImpact("C-multi-contact");captured=true;}
                yield return null;
            }
            Assert.AreEqual(hits.Length,shown.Count);
            Assert.IsTrue(captured,"Effects for separate instances must coexist without sharing an anchor.");
            Assert.AreEqual(BattlePhase.PlayerTurn,Context.Phase);
            Assert.AreEqual(SpriteMotion.Death,Controller.View.EnemyFor(enemies[0]).Motion);
        }
        [UnityTest] public IEnumerator ChainContactsStartAtTheSelectedEnemy()
        {
            const string id="phase3_chain_probe";
            var card=new CardDefinition(id,"연쇄 검증","선택한 적과 다음 적에게 피해 2.",1,ElementType.Lightning,
                new[]{new CardEffect(EffectType.Damage,2)},CardTargetScope.Chain,ElementApplication.SharedAttack);
            yield return PreviewTwoWithCard(card);
            var enemies=Context.Runtime.Enemies;
            var view=Controller.View;
            Click(Card(id).gameObject);
            Click(view.EnemyFor(enemies[1]).gameObject);
            var hits=Context.Runtime.LastCardResult.HitEvents;
            CollectionAssert.AreEqual(new[]{enemies[1].TargetId,enemies[0].TargetId},hits.Select(e=>e.TargetId));
            int next=0;float deadline=Time.realtimeSinceStartup+5;
            while(next<hits.Count&&Time.realtimeSinceStartup<deadline)
            {
                var effect=Impact(hits[next]);
                if(effect!=null)
                {
                    AssertImpactAt(effect,view.EnemyFor(enemies[1-next]).sprite);
                    if(next==0)Assert.IsNull(Impact(hits[1]));
                    next++;
                }
                yield return null;
            }
            Assert.AreEqual(hits.Count,next);
            yield return Settled();
        }

        private IEnumerator PreviewTwoWithCard(CardDefinition card)
        {
            var data=Flow.Session.Definitions;
            var definitions=new GameDefinitions(data.AllCards.Concat(new[]{card}),data.Characters,data.Enemies.Select(CardEffectTestEnemy),data.Config,data.Reward,data.Elements);
            Assert.IsTrue(Flow.StartEditorPreviewRun(definitions,17,CardEffectTestEnemy(data.Enemy("ruin_gargoyle"))));
            yield return Ready(FlowScreen.CharacterSelect);Button("Character_Warrior");yield return Ready(FlowScreen.DungeonMap);
            Flow.Session.Run.AddCard(card.Id);yield return Enter("r2");
            var enemies=Context.Runtime.Enemies;
            Assert.AreEqual(2,enemies.Count);Assert.AreEqual(enemies[0].Definition.Id,enemies[1].Definition.Id);
            Context.Runtime.Deck.Draw(1);Controller.View.RefreshHand(c=>Controller.PlayCard(c.InstanceId));Controller.View.Refresh();
        }
#endif
        [UnityTest] public IEnumerator Phase4OpeningHandsFitAndStarterActionsUseRealScene()
        {
#if UNITY_EDITOR
            foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,720)})
            {
                UnityEditor.PlayModeWindow.SetCustomRenderingResolution((uint)size.x,(uint)size.y,"BRODUN starter decks");
                yield return null;
                foreach(CharacterId hero in Enum.GetValues(typeof(CharacterId)))
                {
                    if(Flow.Session.Screen!=FlowScreen.Title){Flow.ReturnToTitle();yield return Ready(FlowScreen.Title);}
                    var data=Flow.Session.Definitions;
                    uint seed=Enumerable.Range(17,100).Select(i=>(uint)i).First(s=>
                    {
                        var room=LayeredMapGenerator.Generate(s).Rooms.Single(r=>r.Id=="r2");
                        var opening=Shuffling.Shuffle(data.Character(hero).StartingDeck,new SeededRandom(s+(uint)room.Column))
                            .Take(5).Select(data.Card).Where(c=>c.StarterOnly).ToArray();
                        return room.Type==RoomType.Battle&&opening.Any(c=>c.Type==CardType.Attack)&&opening.Any(c=>c.Type==CardType.Guard);
                    });
                    yield return LegacyMap(seed,hero);yield return Enter("r2");
                    yield return new WaitForSecondsRealtime(.55f);
                    Canvas.ForceUpdateCanvases();
                    Assert.AreEqual(size.x,Screen.width);Assert.AreEqual(size.y,Screen.height);
                    var view=Controller.View;
                    Assert.AreEqual(10,Context.Run.Deck.Count);
                    Assert.AreEqual(5,view.Cards.Count);
                    Assert.AreEqual(5,Context.Runtime.Energy);
                    var corners=new Vector3[4];
                    float right=0;
                    foreach(var card in view.Cards)
                    {
                        ((RectTransform)card.transform).GetWorldCorners(corners);
                        Assert.GreaterOrEqual(corners[0].x+.5f,right,"The five-card hand must not overlap.");
                        Assert.GreaterOrEqual(corners[0].y,-.5f);
                        Assert.LessOrEqual(corners[2].x,Screen.width+.5f);
                        Assert.LessOrEqual(corners[2].y,Screen.height+.5f);
                        right=corners[2].x;
                        Assert.AreEqual(card.Definition.Name,card.title.text);
                        Assert.AreEqual(card.Definition.Cost.ToString(),card.cost.text);
                        Assert.AreEqual(card.Definition.Description,card.description.text);
                        Assert.NotNull(card.artwork.sprite);
                        if(card.Definition.StarterOnly)
                        {
                            Assert.AreEqual("무속성",card.element.text);
                            Assert.That(card.kind.text,Does.StartWith("기본"));
                            Assert.Greater(card.kind.canvasRenderer.GetMesh().vertexCount,0,"The starter grade must render.");
                        }
                    }
                    var provisional=data.Character(hero).ProvisionalStartingCards;
                    foreach(var card in view.Cards.Where(c=>provisional.Contains(c.Definition.Id)))
                    {
                        Assert.That(card.kind.text,Does.StartWith("임시"));
                        Assert.Greater(card.kind.canvasRenderer.GetMesh().vertexCount,0,"The provisional label must render.");
                    }
                    var directory=Environment.GetEnvironmentVariable("BRODUN_PHASE4_STARTER_CAPTURE_DIR");
                    if(size.x==1920&&!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                        var path=Path.Combine(directory,hero+".png");
                        ScreenCapture.CaptureScreenshot(path);yield return null;
                        float limit=Time.realtimeSinceStartup+5;
                        while(!File.Exists(path)&&Time.realtimeSinceStartup<limit)yield return null;
                        Assert.IsTrue(File.Exists(path));
                    }

                    var attack=view.Cards.First(c=>c.Definition.StarterOnly&&c.Definition.Type==CardType.Attack);
                    var guard=view.Cards.First(c=>c.Definition.StarterOnly&&c.Definition.Type==CardType.Guard);
                    int hp=Context.Runtime.Enemy.CurrentHP;
                    var actor=view.PlayerActor.GetComponent<Image>();
                    var home=actor.rectTransform.anchoredPosition;
                    Click(attack.gameObject);
                    var hit=Context.Runtime.LastCardResult.HitEvents.Single();
                    bool sawAttack=false,sawImpact=false;
                    float deadline=Time.realtimeSinceStartup+5;
                    while(Context.Phase==BattlePhase.ResolvingPlayerAction&&Time.realtimeSinceStartup<deadline)
                    {
                        if(view.PlayerActor.IsAttacking){sawAttack=true;Assert.AreEqual(home,actor.rectTransform.anchoredPosition);}
                        var impact=Impact(hit);
                        if(!sawImpact&&impact!=null){AssertImpactAt(impact,view.Enemy.sprite);sawImpact=true;}
                        yield return null;
                    }
                    Assert.IsTrue(sawAttack&&sawImpact);
                    Assert.AreEqual(hp-6,Context.Runtime.Enemy.CurrentHP);
                    Assert.AreEqual(4,Context.Runtime.Energy);
                    Assert.AreEqual(4,view.Cards.Count);
                    Click(guard.gameObject);yield return Settled();
                    Assert.AreEqual(5,Context.Runtime.Block);
                    Assert.AreEqual(3,Context.Runtime.Energy);
                    Assert.AreEqual(3,view.Cards.Count);
                    Button("EndTurn");yield return Settled();
                    Assert.AreEqual(5,Context.Runtime.Energy);
                    Assert.AreEqual(5,view.Cards.Count);
                    if(size.x==1920&&hero==CharacterId.Warrior)
                    {
                        Context.Runtime.Deck.Draw(5);
                        view.RefreshHand(c=>Controller.PlayCard(c.InstanceId));view.Refresh();
                        yield return new WaitForSecondsRealtime(.4f);
                        Assert.AreEqual(10,view.Cards.Count);
                        var scroll=view.Root.GetComponentsInChildren<Scrollbar>().Single();
                        Assert.IsTrue(scroll.interactable);
                        scroll.value=1;
                        yield return new WaitForSecondsRealtime(.4f);
                        var last=view.Cards.Last();
                        var position=RectTransformUtility.WorldToScreenPoint(null,last.transform.position);
                        Assert.AreSame(last,view.HandLayout.PointerOwner(position,null));
                        Click(last.gameObject);yield return Settled();
                        Assert.AreEqual(9,view.Cards.Count);
                    }
                }
            }
#else
            yield break;
#endif
        }
#if UNITY_EDITOR
        private IEnumerator WarriorTestDeck(params string[] cards)
        {
            var data=Flow.Session.Definitions;
            var heroes=data.Characters.Select(hero=>hero.Id==CharacterId.Warrior
                ?new CharacterDefinition(hero.Id,hero.Name,hero.Description,hero.MaxHP,hero.Energy,cards):hero);
            var fixture=new GameDefinitions(data.AllCards,heroes,data.Enemies.Select(CardEffectTestEnemy),data.Config,data.Reward,data.Elements);
            Assert.IsTrue(Flow.StartEditorPreviewRun(fixture,17));
            yield return Ready(FlowScreen.CharacterSelect);Button("Character_Warrior");yield return Ready(FlowScreen.DungeonMap);
            yield return Enter("r2");
        }
        [UnityTest] public IEnumerator FormalEarthStrikePlaysPaintedFramesAtContact()
        {
            yield return WarriorTestDeck(Enumerable.Repeat("W17",10).ToArray());
            Click(Controller.View.Cards.First().gameObject);
            var frames=new System.Collections.Generic.HashSet<Sprite>();
            while(Context.Phase==BattlePhase.ResolvingPlayerAction)
            {
                var image=Controller.View.Root.GetComponentsInChildren<UnityEngine.UI.Image>()
                    .FirstOrDefault(i=>i.name=="Contact earth_strike");
                if(image!=null&&image.sprite!=null)frames.Add(image.sprite);
                yield return null;
            }
            Assert.GreaterOrEqual(frames.Count,2);
        }
        [UnityTest] public IEnumerator DragAttackInDuelAndCancelOutsideEnemy()
        {
            yield return WarriorTestDeck(Enumerable.Repeat("B-W01",10).ToArray());
            var card=Controller.View.Cards.First();
            var enemy=Controller.View.Enemy;
            Assert.AreEqual(1,Context.Runtime.Enemies.Count(e=>e.IsAlive));
            var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left};
            card.OnPointerDown(pointer);card.OnBeginDrag(pointer);
            var arrow=Controller.View.Root.GetComponentInChildren<BattleTargetArrow>();
            Assert.NotNull(arrow);
            int energy=Context.Runtime.Energy;
            card.OnEndDrag(pointer);
            Assert.IsFalse(arrow.gameObject.activeSelf);
            Assert.AreEqual(energy,Context.Runtime.Energy);
            pointer.pointerCurrentRaycast=new RaycastResult{gameObject=enemy.sprite.gameObject};
            card.OnPointerDown(pointer);card.OnBeginDrag(pointer);card.OnDrag(pointer);card.OnEndDrag(pointer);
            Assert.AreEqual(card.InstanceId,Context.Runtime.LastCardResult.CardInstanceId);
            Assert.AreEqual(energy-1,Context.Runtime.Energy);
        }
        [UnityTest] public IEnumerator DragAreaOntoEitherEnemyKeepsAllTargets()
        {
            yield return ElementTestDeck(true,CharacterId.Warrior,Enumerable.Repeat("W23",10).ToArray());
            var card=Controller.View.Cards.First();
            var enemies=Context.Runtime.Enemies;
            var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,
                pointerCurrentRaycast=new RaycastResult{gameObject=Controller.View.EnemyFor(enemies[1]).sprite.gameObject}};
            card.OnPointerDown(pointer);card.OnBeginDrag(pointer);card.OnDrag(pointer);
            Assert.NotNull(Controller.View.Root.GetComponentInChildren<BattleTargetArrow>());
            card.OnEndDrag(pointer);
            CollectionAssert.AreEquivalent(enemies.Select(e=>e.TargetId),
                Context.Runtime.LastCardResult.HitEvents.Select(e=>e.TargetId));
        }
        private IEnumerator WarriorCapture(string name)
        {
            var directory=Environment.GetEnvironmentVariable("BRODUN_PHASE5_WARRIOR_CAPTURE_DIR");
            if(string.IsNullOrEmpty(directory))yield break;
            Directory.CreateDirectory(directory);
            var path=Path.Combine(directory,name+".png");
            ScreenCapture.CaptureScreenshot(path);yield return null;
            float deadline=Time.realtimeSinceStartup+5;
            while(!File.Exists(path)&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsTrue(File.Exists(path));
        }
        private IEnumerator CapturedWarriorHit(string id,string label,int damage)
        {
            var hero=Controller.View.PlayerActor.GetComponent<Image>();
            var home=hero.rectTransform.anchoredPosition;
            int before=Context.Runtime.Enemy.CurrentHP;
            Click(Card(id).gameObject);
            var result=Context.Runtime.LastCardResult;
            var hit=result.HitEvents.First();
            bool captured=false;
            float deadline=Time.realtimeSinceStartup+5;
            while(Context.Phase==BattlePhase.ResolvingPlayerAction&&Time.realtimeSinceStartup<deadline)
            {
                Assert.AreEqual(home,hero.rectTransform.anchoredPosition);
                var effect=Impact(hit);
                if(!captured&&effect!=null)
                {
                    AssertImpactAt(effect,Controller.View.Enemy.sprite);
                    yield return WarriorCapture(label);
                    captured=true;
                }
                yield return null;
            }
            Assert.IsTrue(captured);
            Assert.AreEqual(BattlePhase.PlayerTurn,Context.Phase);
            Assert.AreEqual(before-damage,Context.Runtime.Enemy.CurrentHP);
        }
        [UnityTest] public IEnumerator WarriorFortressUsesRealCardsAndKeepsReferencedBlock()
        {
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1920,1080,"BRODUN warrior fortress");
            yield return null;
            yield return WarriorTestDeck("W01","W03","W08","W05","W07","B-W01","B-W01","B-W02","B-W02","B-W02");
            yield return Play("W01");yield return Play("W03");
            Assert.AreEqual(13,Context.Runtime.Block);
            yield return CapturedWarriorHit("W08","Fortress-W08",19);
            Assert.AreEqual(0,Context.Runtime.Energy);
            Assert.AreEqual(13,Context.Runtime.Block);
        }
        [UnityTest] public IEnumerator WarriorCounterDisplaysBothCountersAndTheAdditionalDraw()
        {
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1920,1080,"BRODUN warrior counter");
            yield return null;
            yield return WarriorTestDeck("W11","W14","W16","W13","W15","B-W01","B-W01","B-W02","B-W02","B-W02");
            yield return Play("W11");yield return Play("W14");
            Assert.AreEqual(13,Context.Runtime.Block);
            Button("EndTurn");
            var counters=Context.Runtime.LastTurnResult.Events.Where(e=>e.Cause==CombatCause.Counter&&e.Hit!=null).ToArray();
            Assert.AreEqual(2,counters.Length);
            bool captured=false;
            float deadline=Time.realtimeSinceStartup+5;
            while(Context.Phase==BattlePhase.EnemyTurn&&Time.realtimeSinceStartup<deadline)
            {
                var effect=Impact(counters[1]);
                if(!captured&&effect!=null)
                {
                    AssertImpactAt(effect,Controller.View.Enemy.sprite);
                    yield return WarriorCapture("Counter-W11-W14");
                    captured=true;
                }
                yield return null;
            }
            Assert.IsTrue(captured);
            Assert.AreEqual(33,Context.Runtime.Enemy.CurrentHP);
            Assert.AreEqual(6,Context.Runtime.Deck.Hand.Count);
            Assert.IsTrue(Context.Runtime.CounteredLastEnemyTurn);
            Assert.AreEqual(5,Context.Runtime.Energy);
        }
        [UnityTest] public IEnumerator WarriorSpendingShowsRealDamageRestorationAndOneDraw()
        {
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1920,1080,"BRODUN warrior spending");
            yield return null;
            yield return WarriorTestDeck("W24","W18","W20","W22","W23","B-W01","B-W01","B-W02","B-W02","B-W02");
            yield return Play("W24");yield return Play("W18");
            int hand=Context.Runtime.Deck.Hand.Count;
            yield return CapturedWarriorHit("W20","BlockSpend-W20",16);
            Assert.AreEqual(0,Context.Runtime.Energy);
            Assert.AreEqual(3,Context.Runtime.Block);
            Assert.AreEqual(7,Context.Runtime.BlockSpentThisTurn);
            Assert.AreEqual(3,Context.Runtime.RestoredBlockThisTurn);
            Assert.AreEqual(hand,Context.Runtime.Deck.Hand.Count,"Using one card and triggering one draw must balance.");
        }
        [UnityTest] public IEnumerator WarriorAreaAndCounterKillsPreserveRewardAndMapFlow()
        {
            yield return WarriorTestDeck("W11","W17","W17","W17","W17","W17","W17","B-W01","B-W01","B-W02");
            yield return Play("W17");yield return Play("W17");yield return Play("W17");
            yield return Play("W17");yield return Play("W17");Button("EndTurn");yield return Settled();
            yield return Play("W11");
            Button("EndTurn");yield return Settled();
            Assert.AreEqual(BattlePhase.Reward,Context.Phase);
            Assert.IsTrue(Context.Runtime.LastTurnResult.Events.Any(e=>e.Cause==CombatCause.Counter&&e.Hit?.Killed==true));
            Assert.AreEqual(SpriteMotion.Death,Controller.View.Enemy.Motion);
            var run=Context.Run;
            Button("RewardGold");yield return Ready(FlowScreen.DungeonMap);
            Assert.Contains("r2",run.CompletedNodes.ToList());Assert.AreEqual(25,run.Gold);
            Flow.ReturnToTitle();yield return Ready(FlowScreen.Title);

            var data=Flow.Session.Definitions;
            var heroes=data.Characters.Select(hero=>hero.Id==CharacterId.Warrior
                ?new CharacterDefinition(hero.Id,hero.Name,hero.Description,hero.MaxHP,hero.Energy,
                    new[]{"W01","W23","B-W01","B-W01","B-W02","B-W02","B-W02","W17","W17","W17"}):hero);
            Assert.IsTrue(Flow.StartEditorPreviewRun(new GameDefinitions(data.AllCards,heroes,data.Enemies.Select(CardEffectTestEnemy),data.Config,data.Reward,data.Elements),
                17,CardEffectTestEnemy(data.Enemy("ruin_gargoyle"))));
            yield return Ready(FlowScreen.CharacterSelect);Button("Character_Warrior");yield return Ready(FlowScreen.DungeonMap);
            yield return Enter("r2");yield return Play("W01");Click(Card("W23").gameObject);
            var hits=Context.Runtime.LastCardResult.HitEvents;
            Assert.AreEqual(2,hits.Count);
            bool both=false;float deadline=Time.realtimeSinceStartup+5;
            while(Context.Phase==BattlePhase.ResolvingPlayerAction&&Time.realtimeSinceStartup<deadline)
            {
                if(!both&&hits.All(hit=>Impact(hit)!=null))
                {
                    foreach(var hit in hits)
                        AssertImpactAt(Impact(hit),Controller.View.EnemyFor(Context.Runtime.Enemies.Single(e=>e.TargetId==hit.TargetId)).sprite);
                    both=true;
                }
                yield return null;
            }
            Assert.IsTrue(both);
            Assert.IsTrue(hits.All(hit=>hit.Hit.Amount==18));
        }
#endif
#if UNITY_EDITOR
        [UnityTest] public IEnumerator WarriorRuleTextAndGradeRenderForEveryCard()
        {
            yield return LegacyMap();yield return Enter("r2");
            var art=Resources.Load<BattlePresentationAssets>("BrodunBattle");
            var row=BattleUi.Rect(Controller.View.Root,"Hand",new Vector2(.44f,.01f),new Vector2(.56f,.32f));
            var card=UnityEngine.Object.Instantiate(art.cardPrefab,row,false);
            foreach(var definition in Flow.Session.Definitions.AllCards.Where(c=>c.Build!=CardBuild.None))
            {
                card.Bind(definition,definition.Id,art,Flow.Session.Definitions,_=>{});
                yield return null;Canvas.ForceUpdateCanvases();
                Assert.AreEqual(definition.Cost.ToString(),card.cost.text);
                Assert.AreEqual("무속성",card.element.text);
                Assert.IsTrue(card.kind.text.StartsWith("하급")||card.kind.text.StartsWith("중급")||card.kind.text.StartsWith("상급"));
                Assert.Greater(card.kind.canvasRenderer.GetMesh().vertexCount,0,definition.Id);
                Assert.GreaterOrEqual(card.description.cachedTextGenerator.characterCountVisible,
                    definition.Description.Length-1,definition.Id+" must render the complete rule text.");
                Assert.NotNull(card.artwork.sprite,definition.Id);
            }
            UnityEngine.Object.Destroy(row.gameObject);
        }
#endif
#if UNITY_EDITOR
        private IEnumerator PriestTestDeck(bool twoEnemies,params string[] cards)
        {
            var data=Flow.Session.Definitions;
            var heroes=data.Characters.Select(hero=>hero.Id==CharacterId.Priest
                ?new CharacterDefinition(hero.Id,hero.Name,hero.Description,hero.MaxHP,hero.Energy,cards):hero);
            Assert.IsTrue(Flow.StartEditorPreviewRun(new GameDefinitions(data.AllCards,heroes,data.Enemies.Select(CardEffectTestEnemy),data.Config,data.Reward,data.Elements),
                17,twoEnemies?CardEffectTestEnemy(data.Enemy("ruin_gargoyle")):null));
            yield return Ready(FlowScreen.CharacterSelect);Button("Character_Priest");yield return Ready(FlowScreen.DungeonMap);
            yield return Enter("r2");
        }
        private IEnumerator PriestCapture(string name)
        {
            var directory=Environment.GetEnvironmentVariable("BRODUN_PHASE6_PRIEST_CAPTURE_DIR");
            if(string.IsNullOrEmpty(directory))yield break;
            Directory.CreateDirectory(directory);
            var path=Path.Combine(directory,name+".png");
            ScreenCapture.CaptureScreenshot(path);yield return null;
            float deadline=Time.realtimeSinceStartup+5;
            while(!File.Exists(path)&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsTrue(File.Exists(path));
        }
        private IEnumerator CapturePriestHit(string id,string name,CombatEvent hit)
        {
            bool captured=false;float deadline=Time.realtimeSinceStartup+6;
            while(Context.Phase==BattlePhase.ResolvingPlayerAction&&Time.realtimeSinceStartup<deadline)
            {
                var effect=Impact(hit);
                if(!captured&&effect!=null)
                {
                    var target=Context.Runtime.Enemies.Single(e=>e.TargetId==hit.TargetId);
                    AssertImpactAt(effect,Controller.View.EnemyFor(target).sprite);
                    Assert.AreEqual(hit.Hit.HPAfter+" / "+target.MaxHP,Controller.View.EnemyFor(target).hp.text);
                    yield return PriestCapture(name);captured=true;
                }
                yield return null;
            }
            Assert.IsTrue(captured,id+" actual trace contact must render");
        }
        private IEnumerator MageTestDeck(bool pair,params string[] cards)
        {
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1920,1080,"BRODUN mage QA");yield return null;
            yield return ElementTestDeck(pair,CharacterId.Mage,cards);
        }
        private IEnumerator MageCapture(string name)
        {
            var directory=Environment.GetEnvironmentVariable("BRODUN_PHASE8_MAGE_CAPTURE_DIR");if(string.IsNullOrEmpty(directory))yield break;
            Directory.CreateDirectory(directory);var path=Path.Combine(directory,name+".png");ScreenCapture.CaptureScreenshot(path);yield return null;
            float deadline=Time.realtimeSinceStartup+5;while(!File.Exists(path)&&Time.realtimeSinceStartup<deadline)yield return null;Assert.IsTrue(File.Exists(path));
        }
        private IEnumerator MageContact(CombatEvent hit,string capture)
        {
            bool saw=false;float deadline=Time.realtimeSinceStartup+7;
            while(Context.Phase==BattlePhase.ResolvingPlayerAction&&Time.realtimeSinceStartup<deadline)
            {
                var fx=Impact(hit);
                if(!saw&&fx!=null&&fx.canvasRenderer.GetMesh()!=null)
                {
                    var target=Context.Runtime.Enemies.Single(e=>e.TargetId==hit.TargetId);AssertImpactAt(fx,Controller.View.EnemyFor(target).sprite);
                    Assert.AreEqual(hit.Hit.HPAfter+" / "+target.MaxHP,Controller.View.EnemyFor(target).hp.text);
                    yield return MageCapture(capture);saw=true;
                }
                yield return null;
            }
            Assert.IsTrue(saw,capture+" actual contact");
        }
        private IEnumerator MagePlayOn(string id,int target=0)
        {Click(Card(id).gameObject);if(Context.Phase==BattlePhase.PlayerTurn)Click(Controller.View.EnemyFor(Context.Runtime.Enemies[target]).gameObject);yield return Settled();}
        [UnityTest] public IEnumerator MageSniperUsesRealCardsAndPreservesRewardAndMapFlow()
        {
            yield return MageTestDeck(false,"M01","M02","M03","M04","M05","M06","M07","M08","B-M01","B-M02");
            Click(Card("M04").gameObject);var r=Context.Runtime.LastCardResult;Assert.AreEqual(27,r.Damage);
            yield return MageContact(r.HitEvents.Last(),"Sniper-M04");Assert.AreEqual(11,Context.Runtime.Enemy.CurrentHP);Assert.AreEqual(2,Context.Runtime.Energy);
            yield return Play("M05");Assert.AreEqual(BattlePhase.Reward,Context.Phase);var run=Context.Run;
            Button("RewardGold");yield return Ready(FlowScreen.DungeonMap);Assert.AreEqual(25,run.Gold);Assert.Contains("r2",run.CompletedNodes.ToList());
        }
        [UnityTest] public IEnumerator MageResonanceDisplaysEffectiveCostsAndAutomaticallyRecoversTheSingleCandidate()
        {
            yield return MageTestDeck(false,"M12","E01","E09","M13","M16","E13","E17","M09","M14","M10");
            yield return Play("M12");var wind=Card("E01");string instance=wind.InstanceId;var ice=Card("E09");
            Assert.AreEqual("0",wind.cost.text);Assert.AreEqual("1",ice.cost.text);Assert.AreEqual(3,Context.Runtime.Energy);
            Assert.IsTrue(Controller.View.Root.GetComponentsInChildren<Text>().Any(t=>t.name=="Player effect status"&&t.text.Contains("원소 비용")));
            yield return new WaitForSecondsRealtime(.12f);yield return MageCapture("Resonance-M12-discount");
            Click(wind.gameObject);yield return Settled();Assert.AreEqual(3,Context.Runtime.Energy);
            Assert.AreEqual(0,Context.Runtime.LastCardResult.PaidCost);Assert.AreEqual(1,Context.Runtime.LastCardResult.BaseCost);
            yield return Play("M14");Assert.AreEqual(instance,Card("E01").InstanceId);Assert.AreEqual("1",Card("E01").cost.text);
            Assert.IsTrue(Context.Runtime.RecoveredSharedThisTurn);
        }
        [UnityTest] public IEnumerator MageRecoveryChoiceCancelsBeforeDamageAndMovesOnlyTheSelectedInstance()
        {
            yield return MageTestDeck(false,"E05","E13","M14","M14","M09","M12","M13","M16","B-M01","B-M02");
            yield return Play("E05");string fire=Card("E13").InstanceId;yield return Play("E13");
            var cycle=Card("M14");int hp=Context.Runtime.Enemy.CurrentHP,energy=Context.Runtime.Energy;
            Click(cycle.gameObject);Assert.AreEqual(energy,Context.Runtime.Energy);Assert.AreEqual(hp,Context.Runtime.Enemy.CurrentHP);Assert.IsFalse(Controller.TryEndTurn());
            Button("RecoveryCancel");yield return null;Assert.AreEqual(energy,Context.Runtime.Energy);
            Click(cycle.gameObject);Button("Recover_"+fire);yield return Settled();
            Assert.AreEqual(hp-10,Context.Runtime.Enemy.CurrentHP);Assert.AreEqual(energy-2,Context.Runtime.Energy);
            Assert.AreEqual(fire,Card("E13").InstanceId);var b=Context.Runtime;
            var zones=b.Deck.Hand.Concat(b.Deck.DrawPile).Concat(b.Deck.DiscardPile).Concat(b.Deck.ExhaustPile);
            Assert.AreEqual(Context.Run.Deck.Count,zones.Count());Assert.AreEqual(Context.Run.Deck.Count,zones.Select(c=>c.InstanceId).Distinct().Count());
        }
        [UnityTest] public IEnumerator MageAnnihilationShowsTheRealKillThenOneAreaFollowUp()
        {
            yield return MageTestDeck(true,"M04","M01","M22","M17","M18","M19","M20","M21","M23","M24");
            yield return MagePlayOn("M04");yield return MagePlayOn("M01");Assert.AreEqual(2,Context.Runtime.Enemy.CurrentHP);
            Button("EndTurn");yield return Settled();Click(Card("M22").gameObject);var r=Context.Runtime.LastCardResult;
            Assert.AreEqual(2,r.HitEvents.Count(e=>e.Cause==CombatCause.DirectAttack));Assert.AreEqual(1,r.HitEvents.Count(e=>e.Cause==CombatCause.Conditional));
            yield return MageContact(r.HitEvents.Last(),"Annihilation-M22");
            Assert.AreEqual(0,Context.Runtime.Enemies[0].CurrentHP);Assert.AreEqual(26,Context.Runtime.Enemies[1].CurrentHP);
            Assert.AreEqual(SpriteMotion.Death,Controller.View.Enemy.Motion);
        }
        [UnityTest] public IEnumerator MageCatalystAndCommandReactOncePerCardAndFirstReactionInTheRealScene()
        {
            yield return MageTestDeck(true,"M13","M16","E04","E18","M09","M12","M14","E13","E17","M10");
            yield return MagePlayOn("M13");yield return MagePlayOn("M16");Button("EndTurn");yield return Settled();yield return Play("E04");
            Assert.AreEqual(1,Context.Runtime.LastCardResult.Events.Count(e=>e.Kind==CombatEventKind.Draw&&e.EffectId=="M13"));
            Assert.IsTrue(Controller.View.Root.GetComponentsInChildren<Text>().Any(t=>t.name=="Player effect status"&&t.text.Contains("공명 촉매")));
            yield return MagePlayOn("E18");var r=Context.Runtime.LastCardResult;Assert.AreEqual(1,r.HitEvents.Count(e=>e.Cause==CombatCause.Resonance));
            Assert.AreEqual(2,r.Events.Count(e=>e.Reaction.HasValue));Assert.IsFalse(r.Events.Any(e=>e.Kind==CombatEventKind.Draw&&e.EffectId=="M13"));
            CollectionAssert.AreEqual(new[]{0,21},Context.Runtime.Enemies.Select(e=>e.CurrentHP));
        }
        [UnityTest] public IEnumerator MageAllTwentyFourDescriptionsGradesAndArtFallbackRender()
        {
            // Reproduce the smaller viewport left by the preceding hand-layout test.
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1280,720,"BRODUN mage rule text");yield return null;
            yield return LegacyMap(hero:CharacterId.Mage);yield return Enter("r2");
            var art=Resources.Load<BattlePresentationAssets>("BrodunBattle");var row=BattleUi.Rect(Controller.View.Root,"Mage rule view",new Vector2(.44f,.01f),new Vector2(.56f,.32f));
            var card=UnityEngine.Object.Instantiate(art.cardPrefab,row,false);
            foreach(var c in Flow.Session.Definitions.AllCards.Where(c=>c.Owner==CharacterId.Mage&&!c.StarterOnly))
            {
                card.Bind(c,c.Id,art,Flow.Session.Definitions,_=>{});yield return null;Canvas.ForceUpdateCanvases();
                Assert.AreEqual(c.Cost.ToString(),card.cost.text);Assert.AreEqual("무속성",card.element.text);Assert.NotNull(card.artwork.sprite,c.Id);
                Assert.GreaterOrEqual(card.description.cachedTextGenerator.characterCountVisible,c.Description.Length-1,c.Id+" full rules text");
            }
            UnityEngine.Object.Destroy(row.gameObject);
        }
        private IEnumerator ElementTestDeck(bool pair,CharacterId hero,params string[] cards)
        {
            var data=Flow.Session.Definitions;
            var heroes=data.Characters.Select(h=>h.Id==hero?new CharacterDefinition(h.Id,h.Name,h.Description,h.MaxHP,h.Energy,cards):h);
            Assert.IsTrue(Flow.StartEditorPreviewRun(new GameDefinitions(data.AllCards,heroes,data.Enemies.Select(CardEffectTestEnemy),data.Config,data.Reward,data.Elements),17,
                pair?CardEffectTestEnemy(data.Enemy("ruin_gargoyle")):null));
            yield return Ready(FlowScreen.CharacterSelect);Button("Character_"+hero);yield return Ready(FlowScreen.DungeonMap);yield return Enter("r2");
        }
        private IEnumerator CaptureElementContact(string name,CombatEvent hit,string caption)
        {
            bool shown=false;float deadline=Time.realtimeSinceStartup+7;
            while(Context.Phase==BattlePhase.ResolvingPlayerAction&&Time.realtimeSinceStartup<deadline)
            {
                var effect=Impact(hit);
                if(!shown&&effect!=null&&effect.canvasRenderer.GetMesh()!=null)
                {
                    var enemy=Context.Runtime.Enemies.Single(e=>e.TargetId==hit.TargetId);
                    AssertImpactAt(effect,Controller.View.EnemyFor(enemy).sprite);
                    Assert.AreEqual(hit.Hit.HPAfter+" / "+enemy.MaxHP,Controller.View.EnemyFor(enemy).hp.text);
                    Assert.IsTrue(Controller.View.Root.GetComponentsInChildren<Text>().Any(t=>t.text==caption));
                    var directory=Environment.GetEnvironmentVariable("BRODUN_PHASE7_ELEMENT_CAPTURE_DIR");
                    if(!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);var path=Path.Combine(directory,name+".png");
                        ScreenCapture.CaptureScreenshot(path);yield return null;
                        float saved=Time.realtimeSinceStartup+5;while(!File.Exists(path)&&Time.realtimeSinceStartup<saved)yield return null;
                        Assert.IsTrue(File.Exists(path));
                    }
                    shown=true;
                }
                yield return null;
            }
            Assert.IsTrue(shown,name+" actual reaction contact must render");
        }
        [UnityTest] public IEnumerator ElementOverloadUsesRealCardsAndPreservesTheRewardLoop()
        {
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1920,1080,"BRODUN elements overload");yield return null;
            yield return ElementTestDeck(false,CharacterId.Warrior,"E13","E17","E15","E19","E18","E14","E16","E13","B-W01","B-W02");
            yield return Play("E13");Click(Card("E17").gameObject);var r=Context.Runtime.LastCardResult;
            Assert.AreEqual(ElementReaction.Overload,r.Events.Single(e=>e.Reaction.HasValue).Reaction);Assert.AreEqual(14,r.Damage);
            yield return CaptureElementContact("Overload-E13-E17",r.HitEvents.Last(),"과부하");
            Assert.AreEqual(17,Context.Runtime.Enemy.CurrentHP);
            Assert.AreEqual(ElementType.Lightning,Context.Runtime.Enemy.ElementMark);
            Assert.IsTrue(Controller.View.Root.GetComponentsInChildren<Text>(true).Any(t=>t.name=="Enemy element mark"&&t.text.Contains("번개")));
            yield return Play("E18");Button("EndTurn");yield return Settled();yield return Play("B-W01");
            Assert.AreEqual(BattlePhase.Reward,Context.Phase);var run=Context.Run;
            Button("RewardGold");yield return Ready(FlowScreen.DungeonMap);Assert.AreEqual(25,run.Gold);Assert.Contains("r2",run.CompletedNodes.ToList());
        }
        [UnityTest] public IEnumerator ElementRapidFreezeUsesTargetContactAndTheSameReducedIntent()
        {
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1920,1080,"BRODUN elements freeze");yield return null;
            yield return ElementTestDeck(false,CharacterId.Warrior,"E05","E08","E07","E09","E06","E08","E05","E07","B-W01","B-W02");
            yield return Play("E05");Click(Card("E08").gameObject);var r=Context.Runtime.LastCardResult;
            Assert.AreEqual(ElementReaction.RapidFreeze,r.Events.Single(e=>e.Reaction.HasValue).Reaction);
            yield return CaptureElementContact("RapidFreeze-E05-E08",r.HitEvents.Last(),"급속 빙결");
            Assert.AreEqual(4,Context.Runtime.Enemy.Status.RapidFreeze);Assert.AreEqual(22,Context.Runtime.Enemy.CurrentHP);
            var intent=Context.Runtime.CurrentIntent;int hp=Context.Run.CurrentHP,block=Context.Runtime.Block;
            Button("EndTurn");yield return Settled();
            Assert.AreEqual(intent.TotalDamage,Context.Runtime.LastEnemyAction.Intent.TotalDamage);
            Assert.AreEqual(hp-Math.Max(0,intent.TotalDamage-block),Context.Run.CurrentHP);Assert.AreEqual(0,Context.Runtime.Enemy.Status.RapidFreeze);
        }
        [UnityTest] public IEnumerator ElementShockChainDisplaysEachDirectAndSpreadTarget()
        {
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1920,1080,"BRODUN elements shock");yield return null;
            yield return ElementTestDeck(true,CharacterId.Warrior,"E04","E18","E02","E12","E14","E16","E17","E01","B-W01","B-W02");
            yield return Play("E04");Click(Card("E18").gameObject);Click(Controller.View.Enemy.gameObject);
            var r=Context.Runtime.LastCardResult;Assert.AreEqual(2,r.Events.Count(e=>e.Reaction.HasValue));
            Assert.AreEqual(1,r.HitEvents.Count(e=>e.Cause==CombatCause.Spread));
            yield return CaptureElementContact("Shock-E04-E18",r.HitEvents.Last(),"감전");
            CollectionAssert.AreEqual(new[]{19,21},Context.Runtime.Enemies.Select(e=>e.CurrentHP));
            Assert.IsTrue(Context.Runtime.Enemies.All(e=>e.ElementMark==ElementType.Lightning));
        }
        [UnityTest] public IEnumerator ElementCleanseChoiceCanCancelBeforeSpendingAPAndClearsOneWholeStatus()
        {
            yield return ElementTestDeck(false,CharacterId.Warrior,"E04","E18","E02","E12","E14","E16","E17","E01","B-W01","B-W02");
            // Test-only incoming statuses; production enemies are not given new debuff patterns.
            Context.Runtime.PlayerStatus.AddPoison(4);Context.Runtime.PlayerStatus.ApplyWeak(3);
            Click(Card("E04").gameObject);Assert.AreEqual(5,Context.Runtime.Energy);Assert.IsFalse(Controller.TryEndTurn());
            Button("CleanseCancel");Assert.AreEqual(5,Context.Runtime.Energy);Assert.AreEqual(38,Context.Runtime.Enemy.CurrentHP);
            Click(Card("E04").gameObject);Button("CleansePoison");yield return Settled();
            Assert.AreEqual(3,Context.Runtime.Energy);Assert.AreEqual(0,Context.Runtime.PlayerStatus.Poison);
            Assert.AreEqual(3,Context.Runtime.PlayerStatus.Weak);Assert.AreEqual(33,Context.Runtime.Enemy.CurrentHP);
        }
        [UnityTest] public IEnumerator ElementHealingTriggersPriestOathAndAllLivingBrandsThroughActualRecovery()
        {
            yield return ElementTestDeck(true,CharacterId.Priest,"P06","P23","E06","E05","E17","E06","P20","P19","B-P01","B-P02");
            yield return Play("P06");yield return Play("P23");Button("EndTurn");yield return Settled();
            Assert.AreEqual(52,Context.Run.CurrentHP);
            Click(Card("E06").gameObject);Click(Controller.View.Enemy.gameObject);yield return Settled();
            Assert.AreEqual(55,Context.Run.CurrentHP);Assert.AreEqual(4,Context.Runtime.Block);
            CollectionAssert.AreEqual(new[]{29,33},Context.Runtime.Enemies.Select(e=>e.CurrentHP));
            var r=Context.Runtime.LastCardResult;Assert.AreEqual(2,r.Events.Count(e=>e.Kind==CombatEventKind.BrandTriggered));
            Assert.IsTrue(Context.Runtime.Deck.ExhaustPile.Any(c=>c.CardId=="E06"));
        }
        [UnityTest] public IEnumerator PriestBlessingsHealExactlyAndPersistIntoTheNextTurn()
        {
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1920,1080,"BRODUN priest blessing");
            yield return null;
            yield return PriestTestDeck(false,"P06","P08","P02","P04","P03","P07","B-P01","B-P01","B-P02","B-P02");
            yield return Play("P06");yield return Play("P08");
            Assert.AreEqual(0,Context.Runtime.Block);
            Button("EndTurn");yield return Settled();
            Assert.AreEqual(52,Context.Run.CurrentHP);Assert.AreEqual(4,Context.Runtime.Block);
            Click(Card("P02").gameObject);
            Assert.AreEqual(5,Context.Runtime.LastCardResult.Events.Single(e=>e.Kind==CombatEventKind.Heal).ActualHealing);
            var hp=Controller.View.Root.GetComponentsInChildren<Text>().Single(t=>t.name=="Player HP");
            bool captured=false;float deadline=Time.realtimeSinceStartup+5;
            while(Context.Phase==BattlePhase.ResolvingPlayerAction&&Time.realtimeSinceStartup<deadline)
            {
                if(!captured&&hp.text=="57 / 60"&&Controller.View.Root.GetComponentsInChildren<Text>().Any(t=>t.text.Contains(DamageNumber.Caption(DamageNumber.Kind.Heal,5))))
                {yield return PriestCapture("Blessing-P06-P02");captured=true;}
                yield return null;
            }
            Assert.IsTrue(captured);
            Assert.AreEqual(8,Context.Runtime.Block);Assert.AreEqual(57,Context.Run.CurrentHP);
            Assert.IsTrue(Context.Runtime.Deck.ExhaustPile.Any(c=>c.CardId=="P02"));
            yield return Play("P04");yield return Play("P03");
            Assert.AreEqual(59,Context.Run.CurrentHP);Assert.AreEqual(15,Context.Runtime.Block);
            var heal=Context.Runtime.LastCardResult.Events.Single(e=>e.Kind==CombatEventKind.Heal);
            Assert.AreEqual(2,heal.RequestedAmount);Assert.AreEqual(2,heal.ActualHealing);
        }
        [UnityTest] public IEnumerator PriestWeaknessUsesPreCardStacksAndLethalDamageReturnsToMap()
        {
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1920,1080,"BRODUN priest weakness");
            yield return null;
            yield return PriestTestDeck(false,"P10","P11","P14","P16","P09","P12","P15","P13","B-P01","B-P02");
            yield return Play("P10");yield return Play("P11");
            Assert.AreEqual(2,Context.Runtime.Enemy.Status.Weak);Assert.AreEqual(6,Context.Runtime.Block);
            Click(Card("P14").gameObject);
            var result=Context.Runtime.LastCardResult;
            Assert.AreEqual(19,result.Damage);
            yield return CapturePriestHit("P14","Weakness-P14",result.HitEvents.Last());
            Assert.AreEqual(19,Context.Runtime.Enemy.CurrentHP);
            Assert.That(Controller.View.Enemy.status.text,Does.Contain("약화 2"));
            Button("EndTurn");yield return Settled();
            Assert.AreEqual(60,Context.Run.CurrentHP);Assert.AreEqual(1,Context.Runtime.Enemy.Status.Weak);
            yield return Play("P16");Assert.AreEqual(BattlePhase.Reward,Context.Phase);
            Assert.AreEqual(1,Context.Runtime.LastCardResult.HitEvents.Count(e=>e.Hit.Killed));
            Assert.AreEqual(SpriteMotion.Death,Controller.View.Enemy.Motion);
            var run=Context.Run;Button("RewardGold");yield return Ready(FlowScreen.DungeonMap);
            Assert.AreEqual(25,run.Gold);Assert.Contains("r2",run.CompletedNodes.ToList());
        }
        [UnityTest] public IEnumerator PriestRippleUsesSeparateEnemyBrandsAndRealBlockGains()
        {
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1920,1080,"BRODUN priest brands");
            yield return null;
            yield return PriestTestDeck(true,"P23","P24","P19","P18","P20","P22","B-P01","B-P01","B-P02","B-P02");
            yield return Play("P23");
            Assert.IsTrue(Context.Runtime.Enemies.All(e=>e.Status.Brand==1&&e.CurrentHP==38));
            Assert.AreEqual(8,Context.Runtime.Block);
            Assert.IsTrue(Context.Runtime.Enemies.All(e=>Controller.View.EnemyFor(e).status.text.Contains("낙인 1")));
            Click(Card("P24").gameObject);
            var result=Context.Runtime.LastCardResult;
            Assert.AreEqual(2,result.Events.Count(e=>e.Kind==CombatEventKind.BrandTriggered));
            yield return CapturePriestHit("P24","Brand-P23-P24",result.HitEvents.Last());
            Assert.AreEqual(12,Context.Runtime.Block);
            Assert.IsTrue(Context.Runtime.Enemies.All(e=>e.CurrentHP==26&&e.Status.Brand==0));
            Assert.IsTrue(Context.Runtime.Enemies.All(e=>!Controller.View.EnemyFor(e).status.text.Contains("낙인")));
        }
        [UnityTest] public IEnumerator PriestHealingHitsEveryBrandedEnemyWithoutAnAttackMotion()
        {
            yield return PriestTestDeck(true,"P23","P20","P19","P22","P18","P21","B-P01","B-P01","B-P02","B-P02");
            yield return Play("P23");Button("EndTurn");yield return Settled();
            Assert.AreEqual(52,Context.Run.CurrentHP);
            Click(Card("P20").gameObject);
            var result=Context.Runtime.LastCardResult;
            Assert.AreEqual(2,result.HitEvents.Count);
            Assert.IsTrue(result.HitEvents.All(e=>e.Cause==CombatCause.Brand));
            bool sawFirst=false,sawSecond=false;float deadline=Time.realtimeSinceStartup+5;
            while(Context.Phase==BattlePhase.ResolvingPlayerAction&&Time.realtimeSinceStartup<deadline)
            {
                Assert.IsFalse(Controller.View.PlayerActor.IsAttacking);
                var first=Impact(result.HitEvents[0]);var second=Impact(result.HitEvents[1]);
                if(first!=null&&first.canvasRenderer.GetMesh()!=null){AssertImpactAt(first,Controller.View.EnemyFor(Context.Runtime.Enemies[0]).sprite);sawFirst=true;}
                if(second!=null&&second.canvasRenderer.GetMesh()!=null){AssertImpactAt(second,Controller.View.EnemyFor(Context.Runtime.Enemies[1]).sprite);sawSecond=true;}
                yield return null;
            }
            Assert.IsTrue(sawFirst&&sawSecond);
            Assert.AreEqual(56,Context.Run.CurrentHP);Assert.AreEqual(9,Context.Runtime.Block);
            Assert.IsTrue(Context.Runtime.Enemies.All(e=>e.CurrentHP==33&&e.Status.Brand==0));
        }
#endif
        [UnityTest] public IEnumerator EveryCardVfxHasRenderableGeometry()
        {
#if UNITY_EDITOR
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1920,1080,"BRODUN all card VFX");
#endif
            yield return null;yield return LegacyMap();yield return Enter("r2");
            var run=Context.Run;int hp=run.CurrentHP,energy=Context.Runtime.Energy,enemyHP=Context.Runtime.Enemy.CurrentHP;
            var stage=Controller.View.Root.Find("Combat stage");
            string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../work/phase6_5/all-card-vfx"));Directory.CreateDirectory(output);
            Assert.AreEqual(27,Context.Runtime.Data.Cards.Count);
            foreach(var definition in Context.Runtime.Data.Cards)
            {
                var rect=BattleUi.Rect(stage,"VFX visibility "+definition.Id,Vector2.zero,Vector2.one);
                var fx=rect.gameObject.AddComponent<BattleVfx>();int contacts=0;
                var animation=fx.StartCoroutine(fx.Present(definition,()=>contacts++));
                if(definition.Id=="fire_inferno")
                {
                    bool visibleBeforeContact=false;
                    while(contacts==0)
                    {
                        yield return null;Canvas.ForceUpdateCanvases();
                        var flight=fx.canvasRenderer.GetMesh();
                        if(contacts==0&&flight!=null&&flight.colors.Any(c=>c.a>.2f)){visibleBeforeContact=true;break;}
                    }
                    Assert.IsTrue(visibleBeforeContact,"An unrecorded contact must show the incoming flame before impact.");
                    yield return new WaitUntil(()=>contacts==1);
                    yield return new WaitForSecondsRealtime(.15f);
                }
                else yield return new WaitForSecondsRealtime(.50f);
                Canvas.ForceUpdateCanvases();
                Assert.NotNull(fx.GetComponent<CanvasRenderer>(),definition.Id);
                var mesh=fx.canvasRenderer.GetMesh();Assert.NotNull(mesh,definition.Id);Assert.Greater(mesh.vertexCount,0,definition.Id);
                Assert.Greater(mesh.bounds.size.sqrMagnitude,100,definition.Id+" must have a visible size");
                Assert.IsTrue(mesh.colors.Any(c=>c.a>.2f),definition.Id+" must not be transparent");
                string path=Path.Combine(output,definition.Id+".png");ScreenCapture.CaptureScreenshot(path);
                yield return animation;Assert.AreEqual(1,contacts,definition.Id);
                float deadline=Time.realtimeSinceStartup+3;while(!File.Exists(path)&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.IsTrue(File.Exists(path),definition.Id+" rendered capture");
            }
            Assert.AreEqual(hp,run.CurrentHP);Assert.AreEqual(energy,Context.Runtime.Energy);Assert.AreEqual(enemyHP,Context.Runtime.Enemy.CurrentHP);
        }
    }
}

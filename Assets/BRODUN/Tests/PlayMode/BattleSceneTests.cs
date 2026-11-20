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
        private IEnumerator Map(uint seed=17)
        {Assert.IsTrue(Flow.StartNewRun(seed));yield return Ready(FlowScreen.CharacterSelect);Button("Character_Warrior");yield return Ready(FlowScreen.DungeonMap);}
        private IEnumerator Enter(string node)
        {Button("Node_"+node);yield return Ready(FlowScreen.Battle);Assert.NotNull(Controller);Assert.AreEqual(node,Context.Room.Id);}
        private IEnumerator Settled()
        {
            float end=Time.realtimeSinceStartup+10;
            while((Context.Phase==BattlePhase.ResolvingPlayerAction||Context.Phase==BattlePhase.EnemyTurn||Context.Phase==BattlePhase.Victory)&&Time.realtimeSinceStartup<end)yield return null;
            Assert.Less(Time.realtimeSinceStartup,end,"Battle visual sequence stalled");yield return null;
        }
        private CardView Card(string id)=>Controller.View.Cards.Single(c=>c.Definition.Id==id);
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
            yield return Map();var run=Flow.Session.Run;yield return Enter("r2");
            Assert.AreEqual("BrodunBattle",SceneManager.GetActiveScene().name);Assert.AreSame(run,Context.Run);
            int hp=Context.Runtime.Enemy.CurrentHP;var strike=Card("earth_strike");Click(strike.gameObject);
            Assert.AreEqual(hp-5,Context.Runtime.Enemy.CurrentHP);Assert.AreEqual(2,Context.Runtime.Energy);
            Assert.IsFalse(Controller.PlayCard(strike.InstanceId));Assert.IsFalse(Controller.TryEndTurn());yield return Settled();
            var announced=Context.Runtime.CurrentIntent;Button("EndTurn");Assert.IsFalse(Controller.TryEndTurn());yield return Settled();
            Assert.AreEqual(announced.Amount,Context.Runtime.LastEnemyAction.Intent.Amount);Assert.AreEqual(announced.Hits,Context.Runtime.LastEnemyAction.Intent.Hits);
            yield return Win();Assert.AreEqual(0,run.CompletedNodes.Count);Assert.AreEqual(0,run.Gold);
            Button("RewardGold");Assert.IsFalse(Controller.TryChooseReward(RewardType.Gold));Assert.AreEqual(50,run.Gold);
            yield return Ready(FlowScreen.DungeonMap);Assert.AreSame(run,Flow.Session.Run);Assert.Contains("r2",run.CompletedNodes.ToList());
            CollectionAssert.AreEquivalent(DungeonRuntime.Node(run,"r2").Next,DungeonRuntime.Available(run));
            Assert.IsTrue(UnityEngine.Object.FindFirstObjectByType<GameScreenView>().Root.GetComponentsInChildren<Text>().Any(t=>t.text=="GOLD  50"));
        }
        [UnityTest] public IEnumerator CardRewardReloadCannotDuplicateAndAcquiredCardAppearsNextBattle()
        {
            yield return Map();yield return Enter("r2");yield return Win();var battle=Context;var reward=battle.Runtime.Reward;var run=battle.Run;
            Assert.IsTrue(Flow.ReloadBattle());yield return Ready(FlowScreen.Battle);Assert.AreSame(battle,Context);Assert.AreSame(reward,Context.Runtime.Reward);
            Assert.AreEqual(SpriteMotion.Death,Controller.View.Enemy.Motion,"A reward-scene reload must not resurrect the enemy view");
            string id=reward.Candidates[0];Click(Controller.View.Root.GetComponentsInChildren<CardView>().Single(c=>c.name=="Reward_"+id).gameObject);
            Assert.AreEqual(5,run.Deck.Count);Assert.IsFalse(Controller.TryChooseReward(RewardType.Card,id));yield return Ready(FlowScreen.DungeonMap);
            Button("DeckButton");yield return null;Assert.AreEqual(5,UnityEngine.Object.FindFirstObjectByType<GameScreenView>().Root.GetComponentsInChildren<LayoutElement>().Length);Button("DeckClose");yield return null;
            string next=DungeonRuntime.Available(run).First(n=>DungeonRuntime.Node(run,n).Type==RoomType.Battle);
            yield return Enter(next);var card=Card(id);Assert.AreEqual(id,card.Definition.Id);Assert.IsTrue(Controller.PlayCard(card.InstanceId));yield return Settled();
        }
        [UnityTest] public IEnumerator SkipCompletesRoomWithoutCurrencyOrCardAndDefeatNeverCompletes()
        {
            yield return Map();yield return Enter("r2");yield return Win();var run=Context.Run;Button("RewardSkip");yield return Ready(FlowScreen.DungeonMap);
            Assert.AreEqual(0,run.Gold);Assert.AreEqual(4,run.Deck.Count);Assert.AreEqual(1,run.ResolvedRewards.Count);
            Flow.ReturnToTitle();yield return Ready(FlowScreen.Title);yield return Map();yield return Enter("r2");var failed=Context.Run;
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
            Assert.NotNull(route);yield return Map(seed);
            foreach(string node in route)
            {
                yield return Enter(node);yield return Win();string reward=Context.Runtime.Reward.Candidates.OrderByDescending(id=>Context.Runtime.Data.Card(id).BaseDamage).First();
                if(node==route.Last())Assert.AreEqual(EnemyType.Elite,Context.Runtime.Enemy.Definition.Type);
                Click(Controller.View.Root.GetComponentsInChildren<CardView>().Single(c=>c.name=="Reward_"+reward).gameObject);yield return Ready(FlowScreen.DungeonMap);
            }
            Assert.AreEqual(3,Flow.Session.Run.CompletedNodes.Count);Assert.AreEqual(7,Flow.Session.Run.Deck.Count);
        }
        [UnityTest] public IEnumerator CardTypesCostZeroStatusExhaustMultiHitAndConditionalFeedbackUseRealUi()
        {
            foreach(string id in new[]{"earth_crush","poison_touch","fire_strength","lightning_chain","lightning_charge","stone_retaliation"})
            {
                if(Flow.Session.Screen!=FlowScreen.Title){Flow.ReturnToTitle();yield return Ready(FlowScreen.Title);}
                yield return Map();Flow.Session.Run.AddCard(id);yield return Enter("r2");
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
            yield return Map();Flow.Session.Run.AddCard("water_wave");Flow.Session.Run.AddCard("ice_link");yield return Enter("r2");
            Context.Runtime.Deck.Draw(2);Controller.View.RefreshHand(c=>Controller.PlayCard(c.InstanceId));Controller.View.Refresh();
            yield return Play("water_wave");Assert.AreEqual(ElementType.Water,Context.Runtime.Enemy.Element);
            yield return Play("ice_link");Assert.IsTrue(Context.Runtime.LastCardResult.ConditionalTriggered);Assert.AreEqual(11,Context.Runtime.LastCardResult.Damage);
            Assert.IsTrue(Controller.View.Root.GetComponentsInChildren<Text>().Any(t=>t.text.StartsWith("COMBO")));
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
                yield return Map();yield return Enter("r2");yield return Capture("BattleStart",size);
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
        [UnityTest] public IEnumerator HoverIsIdempotentAndUsedCardOwnsItsVisualUntilDiscard()
        {
            yield return Map();yield return Enter("r2");var card=Card("earth_strike");
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
            yield return Map();for(int i=0;i<12;i++)Flow.Session.Run.AddCard("earth_strike");yield return Enter("r2");
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
        [UnityTest] public IEnumerator AttackFramesVfxAndImpactStayPresentationOnly()
        {
#if UNITY_EDITOR
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1920,1080,"BRODUN VFX visibility");
#endif
            yield return null;yield return null;
            yield return Map();yield return Enter("r2");
            string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../work/phase6_5/motion-check"));Directory.CreateDirectory(output);
            var motions=Resources.Load<CombatMotionAssets>("BrodunCombatMotion");Assert.AreEqual(3,motions.heroes.Length);
            foreach(var hero in motions.heroes)foreach(var sequence in hero.animations)Assert.AreEqual(4,sequence.frames.Length);
            foreach(string id in new[]{"earth_strike","wind_cut"})
            {
                int before=Context.Runtime.Enemy.CurrentHP;Click(Card(id).gameObject);
                float deadline=Time.realtimeSinceStartup+3;
                while(!Controller.View.PlayerActor.IsAttacking&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.IsTrue(Controller.View.PlayerActor.IsAttacking);yield return new WaitForSecondsRealtime(.12f);
                var image=Controller.View.PlayerActor.GetComponent<Image>();Assert.That(image.sprite.name,Does.StartWith("attack_"));
                Assert.AreNotEqual(Vector2.zero,image.rectTransform.anchoredPosition);
                var fx=Controller.View.Root.GetComponentInChildren<BattleVfx>();Assert.NotNull(fx);Assert.IsFalse(fx.raycastTarget);
                Canvas.ForceUpdateCanvases();var mesh=fx.canvasRenderer.GetMesh();Assert.NotNull(mesh);
                Debug.Log("VFX_RENDER "+id+" rect="+fx.rectTransform.rect+" vertices="+mesh.vertexCount+" bounds="+mesh.bounds+" depth="+fx.depth);
                Assert.Greater(mesh.vertexCount,0,"Effect object existence alone does not prove rendering.");
                Assert.AreEqual(id=="earth_strike"?BattleVfx.Style.Ground:BattleVfx.Style.Slash,fx.CurrentStyle);
                Assert.AreEqual(before+" / "+Context.Runtime.Enemy.MaxHP,Controller.View.Enemy.hp.text,"HP display must wait for impact, not change at click.");
                ScreenCapture.CaptureScreenshot(Path.Combine(output,id+"_attack.png"));
                while(Controller.View.Enemy.Motion!=SpriteMotion.Hurt&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.AreEqual(SpriteMotion.Hurt,Controller.View.Enemy.Motion);yield return null;
                Assert.AreEqual(Context.Runtime.Enemy.CurrentHP+" / "+Context.Runtime.Enemy.MaxHP,Controller.View.Enemy.hp.text);
                string impactPath=Path.Combine(output,id+"_impact.png");ScreenCapture.CaptureScreenshot(impactPath);yield return Settled();
                float captureDeadline=Time.realtimeSinceStartup+5;while(!File.Exists(impactPath)&&Time.realtimeSinceStartup<captureDeadline)yield return null;
                Assert.IsTrue(File.Exists(impactPath),"Must inspect a rendered effect, not only its running coroutine.");
                Assert.IsNull(Controller.View.Root.GetComponentInChildren<BattleVfx>());Assert.IsFalse(Controller.View.PlayerActor.IsAttacking);
                Assert.AreEqual(Vector2.zero,image.rectTransform.anchoredPosition);
            }
        }
        [UnityTest] public IEnumerator EveryCardVfxHasRenderableGeometry()
        {
#if UNITY_EDITOR
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1920,1080,"BRODUN all card VFX");
#endif
            yield return null;yield return Map();yield return Enter("r2");
            var run=Context.Run;int hp=run.CurrentHP,energy=Context.Runtime.Energy,enemyHP=Context.Runtime.Enemy.CurrentHP;
            var stage=Controller.View.Root.Find("Combat stage");
            string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../work/phase6_5/all-card-vfx"));Directory.CreateDirectory(output);
            Assert.AreEqual(27,Context.Runtime.Data.Cards.Count);
            foreach(var definition in Context.Runtime.Data.Cards)
            {
                var rect=BattleUi.Rect(stage,"VFX visibility "+definition.Id,Vector2.zero,Vector2.one);
                var fx=rect.gameObject.AddComponent<BattleVfx>();int contacts=0;
                var animation=fx.StartCoroutine(fx.Present(definition,()=>contacts++));
                yield return new WaitForSecondsRealtime(.50f);Canvas.ForceUpdateCanvases();
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

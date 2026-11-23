using System;
using System.Collections;
using System.IO;
using System.Linq;
using Brodun.Flow;
using Brodun.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace Brodun.Tests.PlayMode
{
    public sealed class ScreenFlowTests
    {
        private GameFlow Flow => GameFlow.Instance;
        private GameScreenView View => UnityEngine.Object.FindFirstObjectByType<GameScreenView>();
        [UnitySetUp] public IEnumerator Boot()
        {
            if(GameFlow.Instance!=null){UnityEngine.Object.Destroy(GameFlow.Instance.gameObject);yield return null;}
            yield return SceneManager.LoadSceneAsync("BrodunBoot");
            yield return WaitFor(FlowScreen.Title);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(GameFlow.Instance!=null)UnityEngine.Object.Destroy(GameFlow.Instance.gameObject);
            yield return null;
        }
        private IEnumerator WaitFor(FlowScreen screen)
        {
            float deadline=Time.realtimeSinceStartup+12;
            while((Flow==null || Flow.Session.Screen!=screen || Flow.Session.IsTransitioning) && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.NotNull(Flow);Assert.IsFalse(Flow.Session.IsTransitioning);Assert.AreEqual(screen,Flow.Session.Screen);
            Assert.NotNull(View);yield return null;Canvas.ForceUpdateCanvases();
        }
        private static StyledButton Button(string name) =>
            UnityEngine.Object.FindObjectsByType<StyledButton>(FindObjectsSortMode.None).Single(b=>b.name==name);
        private static void Click(string name)
        {
            var button=Button(name);Assert.IsTrue(button.interactable,name);
            // Drive the real uGUI pointer-click handler, rather than invoking a controller directly.
            ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
        }
        private IEnumerator Map(CharacterId hero=CharacterId.Warrior)
        {
            Click("GameStart");Assert.IsTrue(Flow.Session.IsTransitioning);
            Assert.IsFalse(Flow.StartNewRun());yield return WaitFor(FlowScreen.CharacterSelect);
            Click("Character_"+hero);Assert.IsFalse(Flow.ChooseCharacter(CharacterId.Mage));
            yield return WaitFor(FlowScreen.ThemeSelect);Click("Theme_ruins");
            yield return WaitFor(FlowScreen.DungeonMap);
        }
        [UnityTest] public IEnumerator SelectionCardsOnlyHighlightAfterPointerEnters()
        {
            Click("GameStart");
            yield return WaitFor(FlowScreen.CharacterSelect);
            AssertNeutralUntilHover("Character_Warrior");
            Click("Character_Warrior");
            yield return WaitFor(FlowScreen.ThemeSelect);
            AssertNeutralUntilHover("Theme_ruins");
        }

        private static void AssertNeutralUntilHover(string name)
        {
            var button=Button(name);
            var pointer=new PointerEventData(EventSystem.current);
            var image=button.GetComponent<Image>();
            Assert.AreSame(button.normal,image.sprite);
            button.OnPointerEnter(pointer);
            Assert.AreSame(button.hover,image.sprite);
            button.OnPointerExit(pointer);
            Assert.AreSame(button.normal,image.sprite);
        }

        [UnityTest] public IEnumerator FoundrySelectionLoadsThemedMapAndBattle()
        {
            Assert.IsTrue(Flow.StartNewRun(7));
            yield return WaitFor(FlowScreen.CharacterSelect);
            Click("Character_Warrior");
            yield return WaitFor(FlowScreen.ThemeSelect);
            Assert.IsNull(Flow.Session.Run);
            Click("Back");
            yield return WaitFor(FlowScreen.CharacterSelect);
            Click("Character_Mage");
            yield return WaitFor(FlowScreen.ThemeSelect);
            Click("Theme_fire");
            yield return WaitFor(FlowScreen.DungeonMap);
            var run=Flow.Session.Run;
            Assert.AreEqual("fire",run.ThemeId);
            Assert.AreEqual(3,run.AreaCount);
            Assert.AreEqual(CharacterId.Mage,run.SelectedCharacter);
            Assert.AreEqual(7u,run.Seed);
            Assert.AreEqual("03_foundry_normal",View.Root.Find("Dungeon background").GetComponent<Image>().sprite.name);

            var boss=DungeonRuntime.Node(run,"boss");
            Assert.AreEqual("fire_crowned_dragon",EncounterPool.Select(Flow.Session.Definitions,run,boss).Id);
            var elite=run.Map.First(node=>node.Encounter==EnemyType.Elite);
            Assert.That(EncounterPool.Select(Flow.Session.Definitions,run,elite).Id,Does.StartWith("fire_"));

            Click("Node_r2");
            float deadline=Time.realtimeSinceStartup+12;
            while(Flow.Session.IsTransitioning&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsFalse(Flow.Session.IsTransitioning);
            Assert.AreEqual(FlowScreen.Battle,Flow.Session.Screen);
            var battle=Flow.Session.Battle;
            Assert.That(battle.Runtime.Enemy.Definition.Id,Does.StartWith("fire_"));
            var enemy=UnityEngine.Object.FindObjectsByType<EnemyView>(FindObjectsSortMode.None).Single();
            Assert.AreEqual(battle.Runtime.Enemy.Definition.Id,enemy.sprite.sprite.name);
            var background=UnityEngine.Object.FindObjectsByType<Image>(FindObjectsSortMode.None)
                .Single(image=>image.name=="Battle background");
            Assert.AreEqual("03_foundry_normal",background.sprite.name);
            float peakMove=0;
            var home=enemy.sprite.rectTransform.anchoredPosition;
            yield return ObserveEnemyAction();
            Assert.Greater(peakMove,1);
            yield return enemy.Play(SpriteMotion.Death);
            Assert.Less(enemy.sprite.color.a,.05f);

            IEnumerator ObserveEnemyAction()
            {
                var motion=enemy.PresentAction(new IntentData(IntentType.Attack,1),1);
                while(motion.MoveNext())
                {
                    var rect=enemy.sprite.rectTransform;
                    peakMove=Mathf.Max(peakMove,Vector2.Distance(home,rect.anchoredPosition),
                        Mathf.Abs(rect.localScale.x-1)*100,Quaternion.Angle(rect.localRotation,Quaternion.identity));
                    yield return motion.Current;
                }
            }
        }

        [UnityTest] public IEnumerator ThemedRunContinuesThroughThreeDistinctAreas()
        {
            Assert.IsTrue(Flow.StartNewRun(7));
            yield return WaitFor(FlowScreen.CharacterSelect);
            Click("Character_Warrior");
            yield return WaitFor(FlowScreen.ThemeSelect);
            Click("Theme_frost");
            yield return WaitFor(FlowScreen.DungeonMap);

            var run=Flow.Session.Run;
            var visited=new System.Collections.Generic.HashSet<string>();
            run.AddGold(23);
            run.CurrentHP-=7;
            var extra=run.AddCard("W01");
            int rooms=0;
            var chosenThemes=new[]{"frost","fire","void"};
            for(int area=1;area<=3;area++)
            {
                Assert.AreSame(run,Flow.Session.Run);
                Assert.AreEqual(area,run.CurrentArea);
                Assert.AreEqual("start",run.CurrentNode);
                Assert.IsEmpty(run.CompletedNodes);
                Assert.IsTrue(visited.Add(run.ThemeId));
                Assert.AreEqual(chosenThemes[area-1],run.ThemeId);
                var theme=DungeonThemes.Find(run.ThemeId);
                var mapArt=View.Root.Find("Dungeon background").GetComponent<Image>().sprite;
                Assert.AreEqual(theme.BackgroundKey+"_normal",mapArt.name);

                // Skip non-boss rooms to exercise the three live scene transitions without a balance run.
                while(!DungeonRuntime.Available(run).Contains("boss"))
                {
                    Assert.IsNotNull(DungeonRuntime.Enter(run,DungeonRuntime.Available(run).First()));
                    Assert.IsTrue(DungeonRuntime.Complete(run));
                    rooms++;
                }

                Assert.IsTrue(Flow.SelectRoom("boss"));
                float deadline=Time.realtimeSinceStartup+15;
                while(Flow.Session.IsTransitioning&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.AreEqual(FlowScreen.Battle,Flow.Session.Screen);
                var battle=Flow.Session.Battle;
                Assert.Contains(battle.Runtime.Enemy.Definition.Id,theme.EnemyIds.ToArray());
                var background=UnityEngine.Object.FindObjectsByType<Image>(FindObjectsSortMode.None)
                    .Single(image=>image.name=="Battle background");
                Assert.AreEqual(theme.BackgroundKey+"_boss",background.sprite.name);
                var controller=UnityEngine.Object.FindFirstObjectByType<BattleController>();
                battle.Runtime.Enemy.CurrentHP=1;
                battle.Runtime.Deck.Draw(10);
                controller.View.RefreshHand(card=>controller.PlayCard(card.InstanceId));
                var attack=battle.Runtime.Deck.Hand.First(card=>battle.Runtime.Data.Card(card.CardId).Type==CardType.Attack);
                Assert.IsTrue(controller.PlayCard(attack.InstanceId));
                yield return WaitFor(area==3?FlowScreen.RunResult:FlowScreen.ThemeSelect);
                Assert.AreEqual(area,run.CurrentArea);
                rooms++;
                Assert.AreEqual(rooms,run.TotalClearedNodes);
                Assert.AreEqual(53,run.CurrentHP);
                Assert.Greater(run.Gold,23);
                Assert.Contains(extra,run.Deck.ToArray());
                if(area<3)
                {
                    var options=View.Root.GetComponentsInChildren<StyledButton>()
                        .Where(button=>button.name.StartsWith("Theme_")).Select(button=>button.name);
                    CollectionAssert.AreEquivalent(DungeonThemes.All.Where(theme=>!visited.Contains(theme.Id))
                        .Select(theme=>"Theme_"+theme.Id),options);
                    Assert.IsFalse(Flow.ChooseTheme(run.ThemeId));
                    Assert.IsFalse(Flow.ReturnToCharacterSelect());
                    Click("Theme_"+chosenThemes[area]);
                    Assert.IsFalse(Flow.ChooseTheme("ruins"));
                    yield return WaitFor(FlowScreen.DungeonMap);
                }
            }
            Assert.AreEqual(3,visited.Count);
            Assert.AreEqual(RunStatus.Complete,run.Status);
            Assert.AreEqual(3,Flow.Session.Summary.Area);
            Assert.AreEqual(rooms,Flow.Session.Summary.ClearedNodes);
        }

        [UnityTest] public IEnumerator BootTitleCharacterSelectionAndRealSceneLoad()
        {
            Assert.AreEqual("BrodunTitle",SceneManager.GetActiveScene().name);
            yield return Map();Assert.AreEqual("BrodunDungeonMap",SceneManager.GetActiveScene().name);
            Assert.AreEqual(CharacterId.Warrior,Flow.Session.Run.SelectedCharacter);
            Assert.AreEqual(60,Flow.Session.Run.CurrentHP);Assert.AreEqual(10,Flow.Session.Run.Deck.Count);
        }
#if UNITY_EDITOR
        [UnityTest] public IEnumerator EditorCaptureRunDoesNotLeakIntoNormalNewGame()
        {
            var original=Flow.Session.Definitions;
            var preview=new GameDefinitions(original.AllCards,original.Characters,
                original.Enemies.Select(e=>e.Area==1&&e.Type==EnemyType.Normal&&!e.IsLegacy
                    ?new EnemyDefinition(e.Id,e.Name,e.Type,e.Area,14,e.AreaHPStep,e.AssetId,e.Pattern,e.IsLegacy):e),
                original.Config,original.Reward,original.Elements);
            Assert.IsTrue(Flow.StartEditorPreviewRun(preview,7));
            yield return WaitFor(FlowScreen.CharacterSelect);Click("Character_Warrior");
            yield return WaitFor(FlowScreen.DungeonMap);Click("Node_r2");
            while(Flow.Session.IsTransitioning)yield return null;
            Assert.AreEqual(14,Flow.Session.Battle.Runtime.Enemy.MaxHP);
            Flow.ReturnToTitle();yield return WaitFor(FlowScreen.Title);
            Assert.IsTrue(Flow.StartNewRun(7));yield return WaitFor(FlowScreen.CharacterSelect);
            Click("Character_Warrior");yield return WaitFor(FlowScreen.ThemeSelect);Click("Theme_ruins");
            yield return WaitFor(FlowScreen.DungeonMap);Click("Node_r2");
            while(Flow.Session.IsTransitioning)yield return null;
            Assert.AreEqual(44,Flow.Session.Battle.Runtime.Enemy.MaxHP);
            Assert.AreEqual(44,original.Enemy("ruin_gargoyle").BaseHP);
        }
#endif
        [UnityTest] public IEnumerator AllThreeCharactersUseCatalogStatsAndDeck()
        {
            foreach(CharacterId hero in Enum.GetValues(typeof(CharacterId)))
            {
                if(Flow.Session.Screen!=FlowScreen.Title){Flow.ReturnToTitle();yield return WaitFor(FlowScreen.Title);}
                yield return Map(hero);var definition=Flow.Session.Definitions.Character(hero);
                Assert.AreEqual(definition.MaxHP,Flow.Session.Run.CurrentHP);
                CollectionAssert.AreEqual(definition.StartingDeck,Flow.Session.Run.Deck.Select(c=>c.CardId));
            }
        }
        [UnityTest] public IEnumerator EscapeMenuBlocksBattleAndRestoresInputAfterAudioSettings()
        {
            yield return Map();
            Flow.HandleEscape();
            Assert.IsTrue(Flow.IsPauseMenuOpen);
            Assert.IsFalse(View.Root.GetComponent<CanvasGroup>().interactable);
            Click("PauseResume");
            Assert.IsTrue(View.Root.GetComponent<CanvasGroup>().interactable);

            Click("Node_r1");
            while(Flow.Session.IsTransitioning)yield return null;
            Assert.AreEqual(FlowScreen.Battle,Flow.Session.Screen);
            var battle=UnityEngine.Object.FindFirstObjectByType<BattleController>();
            var turn=battle.Context.Runtime.Turn;
            Flow.HandleEscape();
            Assert.IsTrue(Flow.IsPauseMenuOpen);
            Assert.IsFalse(battle.TryEndTurn());
            Click("PauseAudio");
            Assert.IsTrue(GameAudio.Ensure().IsSettingsOpen);
            Flow.HandleEscape();
            Assert.IsFalse(GameAudio.Ensure().IsSettingsOpen);
            Assert.IsTrue(Flow.IsPauseMenuOpen);
            Click("PauseTitle");
            Flow.HandleEscape();
            Assert.IsTrue(Flow.IsPauseMenuOpen);
            Assert.AreEqual(turn,battle.Context.Runtime.Turn);
            Flow.HandleEscape();
            Assert.IsFalse(Flow.IsPauseMenuOpen);
            Assert.IsTrue(battle.View.EndTurn.IsInteractable());
            Assert.IsTrue(battle.TryEndTurn());
        }

        [UnityTest] public IEnumerator RuinsAndVoidActorsAndVitalsUseTheirFloorLine()
        {
            foreach(var item in new[]{(Theme:"ruins",Floor:.375f),(Theme:"void",Floor:.401f)})
            {
                Assert.IsTrue(Flow.StartNewRun(7));
                yield return WaitFor(FlowScreen.CharacterSelect);
                Click("Character_Mage");
                yield return WaitFor(FlowScreen.ThemeSelect);
                Click("Theme_"+item.Theme);
                yield return WaitFor(FlowScreen.DungeonMap);
                Click("Node_r2");
                while(Flow.Session.IsTransitioning)yield return null;
                Assert.AreEqual(FlowScreen.Battle,Flow.Session.Screen);
                var battle=UnityEngine.Object.FindFirstObjectByType<BattleController>();
                var root=battle.View.Root;
                var stage=root.Find("Combat stage");
                var player=(RectTransform)stage.Find("Player sprite");
                var shadow=(RectTransform)stage.Find("Player contact shadow");
                Assert.AreEqual(item.Floor,player.anchorMin.y);
                Assert.AreEqual(player.anchorMin,shadow.anchorMin);
                foreach(var enemy in root.GetComponentsInChildren<EnemyView>())
                    Assert.AreEqual(item.Floor,enemy.Home.y);
                Assert.Less(((RectTransform)root.Find("Player vitals")).anchorMax.y,player.anchorMin.y);
                Flow.ReturnToTitle();
                yield return WaitFor(FlowScreen.Title);
            }
        }

        [UnityTest] public IEnumerator LockedNodesAndBattleEntryRemainUncompletedAcrossReload()
        {
            yield return Map();Assert.IsFalse(Button("Node_boss").interactable);Assert.IsFalse(Flow.SelectRoom("boss"));
            Click("Node_r1");Assert.NotNull(Flow.Session.PendingRoom);Assert.AreEqual("r1",Flow.Session.Run.CurrentNode);
            Assert.AreEqual(0,Flow.Session.Run.CompletedNodes.Count);
            while(Flow.Session.IsTransitioning)yield return null;
            Assert.AreEqual(FlowScreen.Battle,Flow.Session.Screen);Assert.AreEqual("BrodunBattle",SceneManager.GetActiveScene().name);
            var run=Flow.Session.Run;var battle=Flow.Session.Battle;
            Assert.IsTrue(Flow.ReloadBattle());while(Flow.Session.IsTransitioning)yield return null;
            Assert.AreSame(run,Flow.Session.Run);Assert.AreSame(battle,Flow.Session.Battle);
            Assert.IsFalse(Flow.SelectRoom("boss"));Assert.AreEqual(0,run.CompletedNodes.Count);
        }
        [UnityTest] public IEnumerator TitleCodexShowsOwnershipGroupsAndEmptyItems()
        {
            Click("CodexButton");
            Assert.IsTrue(Flow.IsCardLibraryOpen);
            Assert.IsNull(Flow.Session.Run);
            foreach(var hero in Flow.Session.Definitions.Characters)
            {
                Click("Codex_"+hero.Id);
                AssertRows(hero.Id);
            }
            Click("Codex_Shared");
            AssertRows(null);
            Click("Codex_Items");
            Assert.IsEmpty(CatalogRows());
            Assert.IsTrue(View.Root.GetComponentsInChildren<Text>().Any(t=>t.name=="Item empty state"));
            Flow.HandleEscape();
            Assert.IsFalse(Flow.IsCardLibraryOpen);
            Assert.IsTrue(Button("GameStart").IsInteractable());
            Click("AudioSettings");
            Assert.IsTrue(GameAudio.Ensure().IsSettingsOpen);
            Flow.HandleEscape();
            Assert.IsFalse(GameAudio.Ensure().IsSettingsOpen);
            yield return null;

            void AssertRows(CharacterId? owner)
            {
                var expected=Flow.Session.Definitions.GameplayCards.Where(card=>card.Owner==owner)
                    .Select(card=>"CodexCard_"+card.Id);
                CollectionAssert.AreEquivalent(expected,CatalogRows());
            }
            string[] CatalogRows()=>View.Root.GetComponentsInChildren<LayoutElement>()
                .Where(row=>row.name.StartsWith("CodexCard_")).Select(row=>row.name).ToArray();
        }

        [UnityTest] public IEnumerator BattleCanInspectCodexAndRunDeckWithoutAdvancingTurn()
        {
            yield return Map();
            Click("CodexButton");
            Assert.IsTrue(Flow.IsCardLibraryOpen);
            Assert.IsFalse(Flow.SelectRoom("r1"));
            Click("CodexClose");
            Click("Node_r1");
            while(Flow.Session.IsTransitioning)yield return null;
            Assert.AreEqual(FlowScreen.Battle,Flow.Session.Screen);
            var battle=UnityEngine.Object.FindFirstObjectByType<BattleController>();
            int turn=battle.Context.Runtime.Turn;
            var deck=Flow.Session.Run.Deck.Select(card=>card.InstanceId).ToArray();
            Click("CodexButton");
            Assert.IsTrue(Flow.IsCardLibraryOpen);
            Assert.IsFalse(battle.TryEndTurn());
            Click("CodexClose");
            Click("DeckButton");
            Assert.IsTrue(Flow.IsCardLibraryOpen);
            Assert.IsFalse(battle.TryEndTurn());
            var rows=battle.View.Root.GetComponentsInChildren<LayoutElement>().Where(row=>row.name.StartsWith("DeckCard_"));
            CollectionAssert.AreEquivalent(Flow.Session.Run.Deck.Select(card=>"DeckCard_"+card.CardId).Distinct(),
                rows.Select(row=>row.name));
            Flow.HandleEscape();
            Assert.IsFalse(Flow.IsCardLibraryOpen);
            Assert.AreEqual(turn,battle.Context.Runtime.Turn);
            CollectionAssert.AreEqual(deck,Flow.Session.Run.Deck.Select(card=>card.InstanceId));
            Assert.IsTrue(battle.View.EndTurn.IsInteractable());
        }

        [UnityTest] public IEnumerator ReworkedMageDeckPlaysCardsAndClaimsAnOwnedReward()
        {
            Assert.IsTrue(Flow.StartNewRun(7));
            yield return WaitFor(FlowScreen.CharacterSelect);
            Click("Character_Mage");
            yield return WaitFor(FlowScreen.ThemeSelect);
            Click("Theme_fire");
            yield return WaitFor(FlowScreen.DungeonMap);

            var data=Flow.Session.Definitions;
            Assert.AreEqual(70,data.GameplayCards.Count);
            Assert.AreEqual(64,data.RewardCards.Count);
            var run=Flow.Session.Run;
            CollectionAssert.AreEquivalent(new[]{"M01","M01","M01","M02","M02","M02","M03","M04","N01","N03"},
                run.Deck.Select(card=>card.CardId));

            Click("Node_r2");
            yield return new WaitUntil(()=>!Flow.Session.IsTransitioning);
            Assert.AreEqual(FlowScreen.Battle,Flow.Session.Screen);
            var controller=UnityEngine.Object.FindFirstObjectByType<BattleController>();
            var battle=controller.Context.Runtime;
            Assert.AreEqual(3,battle.Energy);
            Assert.AreEqual(4,battle.Deck.Hand.Count);

            // Guarantee access to the two cards under test regardless of shuffle order.
            battle.Deck.Draw(10);
            controller.View.RefreshHand(card=>controller.PlayCard(card.InstanceId));
            var sort=battle.Deck.Hand.Single(card=>card.CardId=="N01");
            Assert.IsTrue(controller.PlayCard(sort.InstanceId));
            Assert.IsTrue(battle.PendingDiscard);
            Assert.IsFalse(controller.TryEndTurn());
            var discard=battle.Deck.Hand.First(card=>card.CardId=="M02");
            string choice="Choose "+discard.InstanceId;
            yield return new WaitUntil(()=>controller.View.Root.Find("Card choice/"+choice)!=null);
            Click(choice);
            yield return new WaitUntil(()=>controller.Context.Phase==BattlePhase.PlayerTurn);
            Assert.IsFalse(battle.PendingDiscard);
            Assert.Contains(discard,battle.Deck.DiscardPile.ToArray());

            var attack=battle.Deck.Hand.Single(card=>card.CardId=="M03");
            battle.Enemy.CurrentHP=1;
            Assert.IsTrue(controller.PlayCard(attack.InstanceId));
            Assert.AreEqual(1,battle.Resonance);
            Assert.AreEqual(battle.Enemy.TargetId,battle.LastCardResult.HitEvents.Single().TargetId);
            yield return new WaitUntil(()=>controller.Context.Phase==BattlePhase.Reward);
            var choices=battle.Reward.Candidates.Select(data.Card).ToArray();
            Assert.AreEqual(3,choices.Length);
            Assert.IsTrue(choices.All(card=>!card.StarterOnly&&(!card.Owner.HasValue||card.Owner==CharacterId.Mage)));
            var reward=choices.First(card=>card.Owner==CharacterId.Mage);
            Assert.IsTrue(controller.TryChooseReward(RewardType.Card,reward.Id));
            yield return WaitFor(FlowScreen.DungeonMap);
            Assert.AreEqual(11,run.Deck.Count);
            Assert.AreEqual(reward.Id,run.Deck.Last().CardId);
        }

        [UnityTest] public IEnumerator DeckOverlayOpensClosesAndUsesCurrentDeck()
        {
            yield return Map();Flow.Session.Run.AddCard("W16");Click("DeckButton");yield return null;
            Assert.IsTrue(View.IsDeckVisible);Assert.IsTrue(Flow.Session.DeckOpen);
            Assert.AreEqual(Flow.Session.Run.Deck.Select(c=>c.CardId).Distinct().Count(),View.Root.GetComponentsInChildren<LayoutElement>().Length);
            Assert.IsFalse(Flow.SelectRoom("r1"));
            var keyboard=Keyboard.current??InputSystem.AddDevice<Keyboard>("BRODUN QA Keyboard");
            var cancel=UnityEngine.Object.FindFirstObjectByType<InputSystemUIInputModule>().cancel.action;
            Assert.IsTrue(cancel.enabled,"UI/Cancel must be enabled.");
            Assert.IsTrue(cancel.controls.Contains(keyboard.escapeKey),"Escape must resolve to UI/Cancel.");
            // Test the real uGUI cancel receiver. Native keyboard focus is a separate interactive check.
            Assert.IsTrue(ExecuteEvents.Execute(Button("DeckClose").gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.cancelHandler));
            yield return null;
            Assert.IsFalse(View.IsDeckVisible,"UI/Cancel must close the deck.");
            Click("DeckButton");yield return null;Click("DeckClose");yield return null;
            Assert.IsFalse(View.IsDeckVisible);Assert.IsFalse(Flow.Session.DeckOpen);
        }
        [UnityTest] public IEnumerator BackAndExplicitAbandonment()
        {
            Click("GameStart");yield return WaitFor(FlowScreen.CharacterSelect);View.Back();yield return WaitFor(FlowScreen.Title);
            yield return Map();View.Back();yield return null;Click("ModalCancel");yield return null;
            Assert.NotNull(Flow.Session.Run);View.Back();yield return null;Click("ModalAccept");yield return WaitFor(FlowScreen.Title);
            Assert.IsNull(Flow.Session.Run);
        }
        [UnityTest] public IEnumerator GameViewResolutionScreenshotsAndLayoutBounds()
        {
            foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1600,900),new Vector2Int(1280,720)})
            {
#if UNITY_EDITOR
                UnityEditor.PlayModeWindow.SetCustomRenderingResolution((uint)size.x,(uint)size.y,"BRODUN QA "+size.x+"x"+size.y);
#else
                Screen.SetResolution(size.x,size.y,false);
#endif
                yield return null;yield return null;
                if(Flow.Session.Screen!=FlowScreen.Title){Flow.ReturnToTitle();yield return WaitFor(FlowScreen.Title);}
                yield return Capture("Title",size);
                Click("GameStart");yield return WaitFor(FlowScreen.CharacterSelect);yield return Capture("CharacterSelect",size);
                Click("Character_Warrior");yield return WaitFor(FlowScreen.ThemeSelect);Click("Theme_ruins");
                yield return WaitFor(FlowScreen.DungeonMap);yield return Capture("DungeonMap",size);
                Click("DeckButton");yield return null;yield return Capture("Deck",size);Click("DeckClose");
            }
        }
        private IEnumerator Capture(string name,Vector2Int size)
        {
            yield return null;Canvas.ForceUpdateCanvases();
            Assert.AreEqual(size.x,Screen.width);Assert.AreEqual(size.y,Screen.height);
            foreach(var button in View.Root.GetComponentsInChildren<StyledButton>().Where(b=>b.gameObject.activeInHierarchy))
            {
                if(button.name.StartsWith("Node_"))
                {
                    // PHASE 3.5 nodes intentionally live in a scrollable, clipped content rect.
                    var local=(RectTransform)button.transform;
                    Assert.IsTrue(View.MapContent.rect.Contains(local.anchoredPosition+new Vector2(local.anchorMin.x*View.MapContent.rect.width,local.anchorMin.y*View.MapContent.rect.height)+View.MapContent.rect.min));
                    continue;
                }
                var corners=new Vector3[4];((RectTransform)button.transform).GetWorldCorners(corners);
                foreach(var point in corners)
                { Assert.GreaterOrEqual(point.x,-2,button.name);Assert.LessOrEqual(point.x,Screen.width+2,button.name);
                  Assert.GreaterOrEqual(point.y,-2,button.name);Assert.LessOrEqual(point.y,Screen.height+2,button.name); }
            }
            foreach(var image in View.Root.GetComponentsInChildren<Image>())
                if(image.name.Contains("icon") || image.name.Contains("portrait") || image.name.Contains("background"))Assert.NotNull(image.sprite,image.name);
            foreach(var transform in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                Assert.IsFalse(transform.GetComponents<Component>().Any(c=>c==null),"Missing script: "+transform.name);
            string output=Environment.GetEnvironmentVariable("BRODUN_PHASE3_CAPTURE_DIR") ??
                Path.GetFullPath(Path.Combine(Application.dataPath,"../../../work/phase3/screenshots"));
            Directory.CreateDirectory(output);string path=Path.Combine(output,name+"_"+size.x+"x"+size.y+".png");
            ScreenCapture.CaptureScreenshot(path);
            yield return null;yield return null; // Let the new capture replace any earlier QA output.
            float deadline=Time.realtimeSinceStartup+5;
            while(!File.Exists(path) && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsTrue(File.Exists(path),path);Debug.Log("BRODUN Game View captured: "+name+" "+Screen.width+"x"+Screen.height);
        }
    }
}

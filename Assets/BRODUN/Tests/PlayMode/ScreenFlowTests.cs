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
            yield return WaitFor(FlowScreen.DungeonMap);
        }
        [UnityTest] public IEnumerator BootTitleCharacterSelectionAndRealSceneLoad()
        {
            Assert.AreEqual("BrodunTitle",SceneManager.GetActiveScene().name);
            yield return Map();Assert.AreEqual("BrodunDungeonMap",SceneManager.GetActiveScene().name);
            Assert.AreEqual(CharacterId.Warrior,Flow.Session.Run.SelectedCharacter);
            Assert.AreEqual(60,Flow.Session.Run.CurrentHP);Assert.AreEqual(4,Flow.Session.Run.Deck.Count);
        }
#if UNITY_EDITOR
        [UnityTest] public IEnumerator EditorCaptureRunDoesNotLeakIntoNormalNewGame()
        {
            var original=Flow.Session.Definitions;
            var preview=new GameDefinitions(original.Cards,original.Characters,
                original.Enemies.Select(e=>e.Id=="ruin_gargoyle"
                    ?new EnemyDefinition(e.Id,e.Name,e.Type,e.Area,14,e.AreaHPStep,e.AssetId,e.Pattern,e.IsLegacy):e),
                original.Config,original.Reward,original.Elements);
            Assert.IsTrue(Flow.StartEditorPreviewRun(preview,7));
            yield return WaitFor(FlowScreen.CharacterSelect);Click("Character_Warrior");
            yield return WaitFor(FlowScreen.DungeonMap);Click("Node_r2");
            while(Flow.Session.IsTransitioning)yield return null;
            Assert.AreEqual(14,Flow.Session.Battle.Runtime.Enemy.MaxHP);
            Flow.ReturnToTitle();yield return WaitFor(FlowScreen.Title);
            Assert.IsTrue(Flow.StartNewRun(7));yield return WaitFor(FlowScreen.CharacterSelect);
            Click("Character_Warrior");yield return WaitFor(FlowScreen.DungeonMap);Click("Node_r2");
            while(Flow.Session.IsTransitioning)yield return null;
            Assert.AreEqual(38,Flow.Session.Battle.Runtime.Enemy.MaxHP);
            Assert.AreEqual(38,original.Enemy("ruin_gargoyle").BaseHP);
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
        [UnityTest] public IEnumerator ThemeButtonsEnterTheirOwnBattleAndBackground()
        {
            foreach(var theme in DungeonThemes.All)
            {
                if(Flow.Session.Screen!=FlowScreen.Title){Flow.ReturnToTitle();yield return WaitFor(FlowScreen.Title);}
                Click("GameStart");yield return WaitFor(FlowScreen.CharacterSelect);
                Click("Theme_"+theme.Id);Click("Character_Warrior");
                yield return WaitFor(FlowScreen.DungeonMap);
                Assert.AreEqual(theme.Id,Flow.Session.Run.ThemeId);
                var node=Flow.Session.Run.Map.First(n=>n.Layer==1&&n.Type==RoomType.Battle);
                Click("Node_"+node.Id);
                while(Flow.Session.IsTransitioning)yield return null;
                var controller=UnityEngine.Object.FindFirstObjectByType<BattleController>();
                Assert.AreEqual(FlowScreen.Battle,Flow.Session.Screen);
                Assert.Contains(controller.Context.Runtime.Enemy.Definition.Id,theme.EnemyIds.ToArray());
                var background=controller.View.Root.GetComponentsInChildren<Image>().Single(i=>i.name=="Battle background");
                Assert.AreSame(BattleBackgrounds.Resolve(theme.Id,false,null),background.sprite);
                Assert.NotNull(controller.View.Enemy.sprite.sprite);
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
        [UnityTest] public IEnumerator DeckOverlayOpensClosesAndUsesCurrentDeck()
        {
            yield return Map();Flow.Session.Run.AddCard("earth_crush");Click("DeckButton");yield return null;
            Assert.IsTrue(View.IsDeckVisible);Assert.IsTrue(Flow.Session.DeckOpen);
            Assert.AreEqual(5,View.Root.GetComponentsInChildren<LayoutElement>().Length);
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
                Click("Character_Warrior");yield return WaitFor(FlowScreen.DungeonMap);yield return Capture("DungeonMap",size);
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

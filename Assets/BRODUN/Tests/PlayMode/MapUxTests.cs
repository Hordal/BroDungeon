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
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace Brodun.Tests.PlayMode
{
    public sealed class MapUxTests
    {
        private GameFlow Flow=>GameFlow.Instance;
        private GameScreenView View=>UnityEngine.Object.FindFirstObjectByType<GameScreenView>();
        [UnitySetUp] public IEnumerator Boot()
        {
            if(GameFlow.Instance!=null){UnityEngine.Object.Destroy(GameFlow.Instance.gameObject);yield return null;}
            yield return SceneManager.LoadSceneAsync("BrodunBoot");yield return Ready(FlowScreen.Title);
        }
        [UnityTearDown] public IEnumerator Clean()
        {if(GameFlow.Instance!=null)UnityEngine.Object.Destroy(GameFlow.Instance.gameObject);yield return null;}
        private IEnumerator Ready(FlowScreen screen)
        {
            float deadline=Time.realtimeSinceStartup+15;
            while((Flow==null || Flow.Session.Screen!=screen || Flow.Session.IsTransitioning)&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.NotNull(Flow);Assert.AreEqual(screen,Flow.Session.Screen);Assert.IsFalse(Flow.Session.IsTransitioning);
            yield return null;Canvas.ForceUpdateCanvases();
        }
        private StyledButton Button(string name)=>View.Root.GetComponentsInChildren<StyledButton>().Last(b=>b.name==name);
        private void Click(string name)
        {
            var button=Button(name);Assert.IsTrue(button.interactable);
            ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
        }
        private IEnumerator Map(uint seed)
        {Assert.IsTrue(Flow.StartNewRun(seed));yield return Ready(FlowScreen.CharacterSelect);Click("Character_Warrior");yield return Ready(FlowScreen.DungeonMap);}
        [UnityTest] public IEnumerator ProductionUsesRandomMapAndRetainsProgressOnReload()
        {
            yield return Map(17);var run=Flow.Session.Run;
            Assert.AreEqual("layered-v2",run.MapGeneratorVersion);Assert.Greater(run.Map.Count,11);
            Assert.Greater(View.MapContent.rect.width,View.MapScroller.viewport.rect.width);
            Assert.IsTrue(View.Root.GetComponentsInChildren<Text>().Any(t=>t.text=="1구역 1층"));
            // Isolate the map reload contract. The real scene entry is covered by ScreenFlow/BattleSlice tests.
            Assert.IsFalse(Flow.SelectRoom("boss"));Assert.IsTrue(Flow.Session.SelectRoom("r1"));View.RefreshMap();Assert.AreEqual(0,run.CompletedNodes.Count);
            run.AddGold(25);run.AddCard("earth_crush");Flow.ReloadMap();yield return Ready(FlowScreen.DungeonMap);
            Assert.AreSame(run,Flow.Session.Run);Assert.AreEqual("r1",run.CurrentNode);Assert.AreEqual(25,run.Gold);Assert.AreEqual(5,run.Deck.Count);
            Click("ModalAccept");yield return null;
            // Simulate only a future room-result callback in the test, not a gameplay clear button.
            DungeonRuntime.Complete(run);View.RefreshMap();
            CollectionAssert.AreEquivalent(DungeonRuntime.Node(run,"r1").Next,View.NodeButtons.Where(p=>p.Value.interactable).Select(p=>p.Key));
            Assert.Contains("r1",run.CompletedNodes.ToList());
            Assert.IsTrue(Flow.Session.SelectRoom(DungeonRuntime.Node(run,"r1").Next[0]));View.RefreshMap();
            Assert.AreEqual(Flow.Assets.completed,View.NodeButtons["r1"].GetComponentsInChildren<Image>().Single(i=>i.name=="Room icon").sprite);
        }
        [UnityTest] public IEnumerator RealScrollRectDragPansWithoutSelectingRoomAndFocusReturns()
        {
            yield return Map(29);var scroll=View.MapScroller;
            var corners=new Vector3[4];scroll.viewport.GetWorldCorners(corners);
            var start=new Vector2(corners[2].x-100,(corners[0].y+corners[2].y)*.5f);
            var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=start,pressPosition=start};
            scroll.OnInitializePotentialDrag(pointer);scroll.OnBeginDrag(pointer);pointer.position=start-new Vector2(500,0);scroll.OnDrag(pointer);scroll.OnEndDrag(pointer);
            yield return null;Assert.Greater(scroll.horizontalNormalizedPosition,.1f);Assert.AreEqual("start",Flow.Session.Run.CurrentNode);
            scroll.horizontalNormalizedPosition=1;scroll.StopMovement();yield return null;Click("FocusCurrent");yield return new WaitForSecondsRealtime(.4f);
            Assert.AreEqual(0,scroll.horizontalNormalizedPosition,.01f);
        }
        [UnityTest] public IEnumerator InputSystemMouseDragReachesTheScrollRectThroughUiRaycasts()
        {
            yield return Map(29);var scroll=View.MapScroller;
            var corners=new Vector3[4];scroll.viewport.GetWorldCorners(corners);
            var start=new Vector2(corners[2].x-120,(corners[0].y+corners[2].y)*.5f);
            var originalBackground=InputSystem.settings.backgroundBehavior;
            var originalEditorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var mouse=InputSystem.AddDevice<Mouse>("BRODUN QA mouse");
            try
            {
                // Frame-separated input tests the actual InputSystemUIInputModule/raycast/drag path,
                // unlike calling ScrollRect.OnDrag directly or injecting a whole drag in one frame.
                SendMouse(mouse,new MouseState{position=start});yield return null;yield return null;
                Assert.AreEqual(start,mouse.position.ReadValue(),"Virtual mouse input must be processed");
                var hits=new System.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=start},hits);
                Assert.IsTrue(hits.Any(h=>h.gameObject.name=="DungeonMapViewport"),"Viewport must be raycastable: "+string.Join(",",hits.Select(h=>h.gameObject.name)));
                SendMouse(mouse,new MouseState{position=start}.WithButton(MouseButton.Left));yield return null;yield return null;
                Assert.IsTrue(mouse.leftButton.isPressed);
                for(int step=1;step<=12;step++)
                {SendMouse(mouse,new MouseState{position=start-new Vector2(step*35,0),delta=new Vector2(-35,0)}.WithButton(MouseButton.Left));yield return null;}
                SendMouse(mouse,new MouseState{position=start-new Vector2(420,0)});yield return null;yield return null;
                Assert.Greater(scroll.horizontalNormalizedPosition,.1f,"Mouse drag must pan through the real UI input module");
                Assert.AreEqual("start",Flow.Session.Run.CurrentNode,"Dragging must not select a room");
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
                InputSystem.settings.backgroundBehavior=originalBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode=originalEditorInput;
            }
        }
        private static void SendMouse(Mouse mouse,MouseState state)
        {
            InputSystem.QueueStateEvent(mouse,state);
            // Batch test players do not necessarily receive the Editor's native input update.
            InputSystem.Update();
        }
        [UnityTest] public IEnumerator EveryCommonButtonKeepsSpriteAndContrastAcrossStates()
        {
            yield return Map(17);
            var guard=new GameObject("Initialization guard",typeof(RectTransform),typeof(Image));var image=guard.GetComponent<Image>();image.sprite=Flow.Assets.buttonNormal;
            var unconfigured=guard.AddComponent<StyledButton>();Assert.NotNull(image.sprite,"OnEnable must not clear an already valid sprite");UnityEngine.Object.Destroy(guard);
            foreach(string name in new[]{"LeaveRun","DeckButton","FocusCurrent"})
            {
                var button=Button(name);var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left};
                Assert.NotNull(button.image.sprite,name);
                button.OnPointerEnter(pointer);Assert.AreEqual(button.hover,button.image.sprite,name);
                Assert.Less(button.image.color.r,1.0f,name);Assert.Less(button.image.color.g,1.0f,name);
                button.OnPointerDown(pointer);Assert.AreEqual(button.pressed,button.image.sprite,name);
                button.OnPointerUp(pointer);button.OnPointerExit(pointer);button.interactable=false;
                Assert.AreEqual(button.disabled,button.image.sprite,name);button.interactable=true;button.RefreshStyle();
            }
            yield return null;
        }
        [UnityTest] public IEnumerator AbandonmentModalBlocksOutsideAndRepeatedInput()
        {
            yield return Map(17);var run=Flow.Session.Run;Click("LeaveRun");yield return null;
            Click("Node_r1");Assert.AreEqual("start",run.CurrentNode);
            Click("LeaveRun");Assert.AreEqual(1,View.Root.GetComponentsInChildren<Image>().Count(i=>i.name=="RoomOverlay"));
            Click("ModalCancel");yield return null;Assert.AreSame(run,Flow.Session.Run);
            Click("LeaveRun");yield return null;Click("ModalAccept");Assert.IsTrue(Flow.Session.IsTransitioning);
            Assert.IsFalse(Flow.ReturnToTitle());yield return Ready(FlowScreen.Title);Assert.IsNull(Flow.Session.Run);
        }
        [UnityTest] public IEnumerator MapScreenshotsForTwoSeedsAndThreeResolutions()
        {
            foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1600,900),new Vector2Int(1280,720)})
            foreach(uint seed in new[]{17u,29u})
            {
#if UNITY_EDITOR
                UnityEditor.PlayModeWindow.SetCustomRenderingResolution((uint)size.x,(uint)size.y,"BRODUN Map QA "+size.x);
#else
                Screen.SetResolution(size.x,size.y,false);
#endif
                yield return null;yield return null;
                if(Flow.Session.Screen!=FlowScreen.Title){Flow.ReturnToTitle();yield return Ready(FlowScreen.Title);}
                yield return Map(seed);yield return Capture("Start",seed,size);
                View.MapScroller.StopMovement();View.MapScroller.horizontalNormalizedPosition=.5f;yield return Capture("Middle",seed,size);
                View.MapScroller.horizontalNormalizedPosition=1;yield return Capture("Boss",seed,size);
                View.MapScroller.FocusLayer(0,false);
                var leave=Button("LeaveRun");leave.OnPointerEnter(new PointerEventData(EventSystem.current));yield return Capture("LeaveHover",seed,size);
                Click("LeaveRun");yield return null;
                Button("ModalAccept").OnPointerEnter(new PointerEventData(EventSystem.current));yield return Capture("LeaveModalHover",seed,size);
                Click("ModalCancel");yield return null;Click("DeckButton");yield return null;Click("DeckClose");
            }
        }
        private IEnumerator Capture(string name,uint seed,Vector2Int size)
        {
            yield return null;Canvas.ForceUpdateCanvases();Assert.AreEqual(size.x,Screen.width);Assert.AreEqual(size.y,Screen.height);
            foreach(var label in View.MapContent.GetComponentsInChildren<Text>().Where(t=>!string.IsNullOrEmpty(t.text)))
                Assert.GreaterOrEqual(label.rectTransform.rect.height+.1f,label.preferredHeight,"Map label must fit its font: "+label.text);
            var lines=View.MapContent.GetComponentInChildren<MapPathsGraphic>();var mesh=lines.canvasRenderer.GetMesh();
            Assert.Greater(mesh.vertexCount,0,"The map must render its actual connections");
            foreach(var vertex in mesh.vertices)
            {
                Assert.That(vertex.x,Is.InRange(lines.rectTransform.rect.xMin,lines.rectTransform.rect.xMax));
                Assert.That(vertex.y,Is.InRange(lines.rectTransform.rect.yMin,lines.rectTransform.rect.yMax));
            }
            foreach(var button in View.Root.GetComponentsInChildren<StyledButton>().Where(b=>!b.name.StartsWith("Node_")))
            {
                var corners=new Vector3[4];((RectTransform)button.transform).GetWorldCorners(corners);
                foreach(var corner in corners){Assert.That(corner.x,Is.InRange(-2f,Screen.width+2f));Assert.That(corner.y,Is.InRange(-2f,Screen.height+2f));}
                Assert.NotNull(button.image.sprite,button.name);
            }
            string output=Environment.GetEnvironmentVariable("BRODUN_MAP_CAPTURE_DIR")??Path.GetFullPath(Path.Combine(Application.dataPath,"../../../work/phase35/screenshots"));Directory.CreateDirectory(output);
            string path=Path.Combine(output,name+"_Seed"+seed+"_"+size.x+"x"+size.y+".png");ScreenCapture.CaptureScreenshot(path);
            yield return null;yield return null;float deadline=Time.realtimeSinceStartup+5;
            while(!File.Exists(path)&&Time.realtimeSinceStartup<deadline)yield return null;Assert.IsTrue(File.Exists(path));
        }
    }
}

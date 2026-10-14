using System;
using System.Collections;
using Brodun.Data;
using Brodun.Flow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace Brodun.Presentation
{
    // Only this scene-service owns the persistent session and SceneManager calls.
    public sealed class GameFlow : MonoBehaviour
    {
        public static GameFlow Instance { get; private set; }
        [SerializeField] private GameCatalogData catalog;
        [SerializeField] private PresentationAssets assets;
        public FlowSession Session { get; private set; }
        public PresentationAssets Assets => assets;
        public event Action<PendingRoomContext> BattleEntryRequested;
        private CanvasGroup fade;
        private InputActionAsset actions;
        private GameScreenView view;
#if UNITY_EDITOR
        private bool editorPreviewSession;
        // Editor-only, in-memory definitions. Never edits catalog assets or ships in a player.
        public bool StartEditorPreviewRun(GameDefinitions definitions,uint seed)
        {
            if(Session.IsTransitioning||Session.Screen!=FlowScreen.Title)return false;
            Session=new FlowSession(definitions,s=>LayeredMapGenerator.Generate(s));
            Session.FinishTransition(FlowScreen.Title);
            if(!Session.StartNewRun(seed))return false;
            editorPreviewSession=true;
            StartCoroutine(Load(FlowScreen.CharacterSelect));return true;
        }
#endif
        // Room/result are logical screens on the existing generic view scene, not new battle scenes.
        public static string SceneName(FlowScreen screen) => "Brodun"+(screen==FlowScreen.Room||screen==FlowScreen.RunResult?"DungeonMap":screen.ToString());
        public static void LoadBoot() { SceneManager.LoadScene(SceneName(FlowScreen.Boot)); }
        private void Awake()
        {
            if(Instance!=null && Instance!=this){Destroy(gameObject);return;}
            if(catalog==null || assets==null)throw new InvalidOperationException("BRODUN Boot dependencies missing.");
            Instance=this;DontDestroyOnLoad(gameObject);Session=new FlowSession(catalog.ToDefinitions(),seed=>LayeredMapGenerator.Generate(seed));
            var canvasObject=new GameObject("TransitionOverlay",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform,false);
            var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=1000;
            var scaler=canvasObject.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            fade=canvasObject.GetComponent<CanvasGroup>();fade.alpha=1;fade.blocksRaycasts=true;
            var image=new GameObject("Black",typeof(RectTransform),typeof(Image));image.transform.SetParent(canvasObject.transform,false);
            var rect=(RectTransform)image.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            image.GetComponent<Image>().color=Color.black;
            var eventObject=new GameObject("BRODUN EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
            eventObject.transform.SetParent(transform,false);
            actions=Instantiate(assets.inputActions);actions.name="BRODUN UI Actions (runtime copy)";
            var module=eventObject.GetComponent<InputSystemUIInputModule>();module.actionsAsset=actions;
            module.deselectOnBackgroundClick=false;
            module.point=InputActionReference.Create(actions.FindAction("UI/Point",true));
            module.leftClick=InputActionReference.Create(actions.FindAction("UI/Click",true));
            module.rightClick=InputActionReference.Create(actions.FindAction("UI/RightClick",true));
            module.middleClick=InputActionReference.Create(actions.FindAction("UI/MiddleClick",true));
            module.scrollWheel=InputActionReference.Create(actions.FindAction("UI/ScrollWheel",true));
            module.move=InputActionReference.Create(actions.FindAction("UI/Navigate",true));
            module.submit=InputActionReference.Create(actions.FindAction("UI/Submit",true));
            module.cancel=InputActionReference.Create(actions.FindAction("UI/Cancel",true));
            actions.FindActionMap("UI",true).Enable();
            SceneManager.sceneLoaded+=Loaded;
        }
        private void Start()
        {
            if(Instance==this && Session.Screen==FlowScreen.Boot && Session.BeginTransition())StartCoroutine(Load(FlowScreen.Title));
        }
        public void Attach(GameScreenView screen) { view=screen; }
        public bool StartNewRun(uint? seedOverride=null)
        {
#if UNITY_EDITOR
            if(editorPreviewSession && !Session.IsTransitioning && (Session.Screen==FlowScreen.Title||Session.Screen==FlowScreen.RunResult))
            {
                Session=new FlowSession(catalog.ToDefinitions(),s=>LayeredMapGenerator.Generate(s));
                Session.FinishTransition(FlowScreen.Title);editorPreviewSession=false;
            }
#endif
            uint seed=seedOverride??unchecked((uint)DateTime.UtcNow.Ticks);
            if(!Session.StartNewRun(seed))return false;
            StartCoroutine(Load(FlowScreen.CharacterSelect));return true;
        }
        public bool ChooseCharacter(CharacterId id)
        {
            if(!Session.ChooseCharacter(id))return false;
            StartCoroutine(Load(FlowScreen.DungeonMap));return true;
        }
        public bool ReturnToTitle()
        { if(!Session.ReturnToTitle())return false;StartCoroutine(Load(FlowScreen.Title));return true; }
        public bool SelectRoom(string id)
        {
            if(!Session.SelectRoom(id))return false;
            view?.RefreshMap();
            if(Session.PendingRoom.IsBattle)EnterBattle(Session.PendingRoom);
            else if(Session.BeginRoom())StartCoroutine(Load(FlowScreen.Room));
            return true;
        }
        public void EnterBattle(PendingRoomContext context)
        {
            if(!Session.BeginBattle())return;
            BattleEntryRequested?.Invoke(context);
            StartCoroutine(Load(FlowScreen.Battle));
        }
        public bool ReturnFromBattle()
        {if(!Session.ReturnFromBattle())return false;StartCoroutine(Load(FlowScreen.DungeonMap));return true;}
        public bool ReturnFromRoom()
        {if(!Session.ReturnFromRoom())return false;StartCoroutine(Load(FlowScreen.DungeonMap));return true;}
        public bool FinishRun()
        {if(!Session.FinishRun())return false;StartCoroutine(Load(FlowScreen.RunResult));return true;}
        public bool ReloadRoom()
        {if(Session.Screen!=FlowScreen.Room||Session.Room==null||!Session.BeginTransition())return false;StartCoroutine(Load(FlowScreen.Room));return true;}
        public bool ReloadBattle()
        {if(Session.Screen!=FlowScreen.Battle||Session.Battle==null||!Session.BeginTransition())return false;StartCoroutine(Load(FlowScreen.Battle));return true;}
        public bool ReloadMap()
        {
            if(Session.Screen!=FlowScreen.DungeonMap || Session.Run==null || !Session.BeginTransition())return false;
            StartCoroutine(Load(FlowScreen.DungeonMap));return true;
        }
        private void Loaded(Scene scene,LoadSceneMode mode) { view=null; }
        private IEnumerator Load(FlowScreen target)
        {
            fade.blocksRaycasts=true;
            yield return FadeTo(1,.22f);
            var operation=SceneManager.LoadSceneAsync(SceneName(target),LoadSceneMode.Single);
            while(!operation.isDone)yield return null;
            Session.RestorePendingRoom();
            // Enable rendering with the target state, but keep input locked until fade-in ends.
            view=FindFirstObjectByType<GameScreenView>();
            view?.Bind(this,target);
            var battleView=FindFirstObjectByType<BattleController>();
            if(target==FlowScreen.Battle)battleView.Bind(this,Session.Battle,Resources.Load<BattlePresentationAssets>("BrodunBattle"));
            yield return FadeTo(0,.22f);
            Session.FinishTransition(target);fade.blocksRaycasts=false;
            view?.Ready();
            battleView?.Ready();
        }
        private IEnumerator FadeTo(float alpha,float duration)
        {
            float start=fade.alpha;
            for(float t=0;t<duration;t+=Time.unscaledDeltaTime){fade.alpha=Mathf.Lerp(start,alpha,t/duration);yield return null;}
            fade.alpha=alpha;
        }
        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit();
#endif
        }
        private void OnDestroy()
        {
            if(Instance!=this)return;
            SceneManager.sceneLoaded-=Loaded;
            if(actions!=null){actions.Disable();Destroy(actions);}
            Instance=null;
        }
    }
}

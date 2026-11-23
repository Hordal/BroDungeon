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
        public Sprite CardArtwork(string id) => catalog.CardArtwork(id);
        public Sprite CardArtwork(CardDefinition card)
        {
            if(!card.IsReworked)return CardArtwork(card.Id);
            if(!card.Owner.HasValue)return card.Type==CardType.Guard?assets.altar:assets.battle;
            string existing=card.Id switch
            {
                "W01"=>"B-W01","W02"=>"B-W02","M01"=>"B-M01","M02"=>"B-M02","P01"=>"B-P01","P02"=>"B-P02",
                "W05"=>"stone_guard","M03"=>"fire_bolt","M04"=>"ice_shard",
                _=>null
            };
            return existing==null?null:CardArtwork(existing);
        }
        public event Action<PendingRoomContext> BattleEntryRequested;
        private CanvasGroup fade;
        private InputActionAsset actions;
        private GameScreenView view;
        private PauseMenuView pauseMenu;
        private CardLibraryView cardLibrary;
        public bool IsCardLibraryOpen => cardLibrary!=null;
        public bool IsPauseMenuOpen => pauseMenu!=null;
#if UNITY_EDITOR
        private bool editorPreviewSession;
        // Editor-only, in-memory definitions. Never edits catalog assets or ships in a player.
        public bool StartEditorPreviewRun(GameDefinitions definitions,uint seed,EnemyDefinition firstBattleCompanion=null)
        {
            if(Session.IsTransitioning||Session.Screen!=FlowScreen.Title)return false;
            Session=new FlowSession(definitions,s=>LayeredMapGenerator.Generate(s),firstBattleCompanion);
            Session.FinishTransition(FlowScreen.Title);
            if(!Session.StartNewRun(seed))return false;
            editorPreviewSession=true;
            StartCoroutine(Load(FlowScreen.CharacterSelect));return true;
        }
#endif
        // Room/result are logical screens on the existing generic view scene, not new battle scenes.
        public static string SceneName(FlowScreen screen) => "Brodun"+(screen==FlowScreen.ThemeSelect?"CharacterSelect":screen==FlowScreen.Room||screen==FlowScreen.RunResult?"DungeonMap":screen.ToString());
        public static void LoadBoot() { SceneManager.LoadScene(SceneName(FlowScreen.Boot)); }
        private static GeneratedDungeon GenerateRunMap(uint seed) =>
            LayeredMapGenerator.Generate(seed,MapGenerationConfig.Campaign);

        private FlowSession CreateProductionSession() =>
            new FlowSession(Card70Catalog.Apply(DungeonThemes.WithThemeEnemies(catalog.ToDefinitions())),GenerateRunMap,chooseTheme:true);

        private void Awake()
        {
            if(Instance!=null && Instance!=this){Destroy(gameObject);return;}
            if(catalog==null || assets==null)throw new InvalidOperationException("BRODUN Boot dependencies missing.");
            Instance=this;DontDestroyOnLoad(gameObject);Session=CreateProductionSession();
            GameAudio.Ensure();
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
                Session=CreateProductionSession();
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
            StartCoroutine(Load(Session.RequiresThemeSelection?FlowScreen.ThemeSelect:FlowScreen.DungeonMap));return true;
        }
        public bool ChooseTheme(string theme)
        {
            if(!Session.ChooseTheme(theme))return false;
            StartCoroutine(Load(FlowScreen.DungeonMap));return true;
        }
        public bool ReturnToCharacterSelect()
        {
            if(!Session.ReturnToCharacterSelect())return false;
            StartCoroutine(Load(FlowScreen.CharacterSelect));return true;
        }
        public bool ReturnToTitle()
        { if(!Session.ReturnToTitle())return false;StartCoroutine(Load(FlowScreen.Title));return true; }
        public bool SelectRoom(string id)
        {
            if(IsCardLibraryOpen||!Session.SelectRoom(id))return false;
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
        {
            if(!Session.FinishRun())return false;
            StartCoroutine(Load(Session.Run.Status==RunStatus.InProgress?FlowScreen.ThemeSelect:FlowScreen.RunResult));
            return true;
        }
        public bool ReloadRoom()
        {if(Session.Screen!=FlowScreen.Room||Session.Room==null||!Session.BeginTransition())return false;StartCoroutine(Load(FlowScreen.Room));return true;}
        public bool ReloadBattle()
        {if(Session.Screen!=FlowScreen.Battle||Session.Battle==null||!Session.BeginTransition())return false;StartCoroutine(Load(FlowScreen.Battle));return true;}
        public bool ReloadMap()
        {
            if(Session.Screen!=FlowScreen.DungeonMap || Session.Run==null || !Session.BeginTransition())return false;
            StartCoroutine(Load(FlowScreen.DungeonMap));return true;
        }
        public bool OpenCardLibrary(RectTransform underlying,bool deck=false)
        {
            if(underlying==null||IsCardLibraryOpen||IsPauseMenuOpen||Session.IsTransitioning||
                GameAudio.Ensure().IsSettingsOpen)return false;
            bool map=Session.Screen==FlowScreen.DungeonMap;
            if(deck&&Session.Run==null)return false;
            if(Session.Screen==FlowScreen.Battle)
            {
                var phase=Session.Battle.Phase;
                if(phase!=BattlePhase.PlayerTurn&&phase!=BattlePhase.Reward)return false;
                FindFirstObjectByType<BattleController>()?.TryCancelSelection();
            }
            else if(!map&&Session.Screen!=FlowScreen.Room&&Session.Screen!=FlowScreen.Title)return false;
            if(deck&&map&&!Session.OpenDeck())return false;

            cardLibrary=BattleUi.Rect(underlying,"Card library",Vector2.zero,Vector2.one)
                .gameObject.AddComponent<CardLibraryView>();
            cardLibrary.Build(this,underlying,deck);
            return true;
        }

        public bool CloseCardLibrary()
        {
            if(cardLibrary==null)return false;
            var closing=cardLibrary;cardLibrary=null;
            Session.CloseDeck();
            closing.ReleaseInput();
            Destroy(closing.gameObject);
            return true;
        }

        private void Update()
        {
            if(Keyboard.current?.escapeKey.wasPressedThisFrame==true)HandleEscape();
        }

        public void HandleEscape()
        {
            if(Session==null||Session.IsTransitioning)return;
            var audio=GameAudio.Ensure();
            if(audio.IsSettingsOpen){audio.CloseSettings();return;}
            if(CloseCardLibrary())return;
            if(IsPauseMenuOpen)
            {
                if(!pauseMenu.DismissConfirmation())ClosePauseMenu();
                return;
            }

            RectTransform underlying=view?.Root;
            if(Session.Screen==FlowScreen.Battle)
            {
                var battle=FindFirstObjectByType<BattleController>();
                if(battle==null||battle.TryCancelSelection())return;
                // Finish the current card/turn presentation before showing the menu.
                var phase=Session.Battle.Phase;
                if(phase!=BattlePhase.PlayerTurn&&phase!=BattlePhase.Reward)return;
                underlying=battle.View.Root;
            }
            else if(Session.Screen==FlowScreen.DungeonMap)
            {
                if(view!=null&&view.TryDismissOverlay())return;
            }
            else if(Session.Screen!=FlowScreen.Room&&(Session.Screen!=FlowScreen.ThemeSelect||Session.Run==null))
            {
                view?.Back();
                return;
            }

            if(underlying==null)return;
            pauseMenu=new GameObject("Pause menu",typeof(RectTransform)).AddComponent<PauseMenuView>();
            pauseMenu.transform.SetParent(transform,false);
            pauseMenu.Build(this,underlying);
        }

        internal void ClosePauseMenu()
        {
            if(pauseMenu==null)return;
            var closing=pauseMenu;pauseMenu=null;
            closing.ReleaseInput();
            Destroy(closing.gameObject);
        }

        private void Loaded(Scene scene,LoadSceneMode mode) { view=null; }
        private IEnumerator Load(FlowScreen target)
        {
            GameAudio.Ensure().CloseSettings();
            ClosePauseMenu();
            CloseCardLibrary();
            fade.blocksRaycasts=true;
            yield return FadeTo(1,.22f);
            var operation=SceneManager.LoadSceneAsync(SceneName(target),LoadSceneMode.Single);
            while(!operation.isDone)yield return null;
            Session.RestorePendingRoom();
            GameAudio.Ensure().SetScreen(target,target==FlowScreen.Battle&&Session.Battle.Room.Type==RoomType.Boss);
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

using System;
using System.Collections.Generic;
using System.Linq;
using Brodun.Flow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Brodun.Presentation
{
    // Runtime composition keeps map/deck views data driven. No copy of gameplay data lives here.
    public sealed class GameScreenView : MonoBehaviour
    {
        public GameFlow Flow { get; private set; }
        public FlowScreen Screen { get; private set; }
        public RectTransform Root { get; private set; }
        public IReadOnlyDictionary<string,StyledButton> NodeButtons => nodes;
        public bool IsDeckVisible => deckOverlay!=null && deckOverlay.activeSelf;
        private readonly Dictionary<string,StyledButton> nodes=new Dictionary<string,StyledButton>();
        private readonly Dictionary<string,GameObject> nodeStatus=new Dictionary<string,GameObject>();
        private readonly Dictionary<string,Text> nodeLabels=new Dictionary<string,Text>();
        private readonly Dictionary<string,Image> nodeIcons=new Dictionary<string,Image>();
        private readonly Dictionary<string,StyledButton> themeButtons=new Dictionary<string,StyledButton>();
        private Image screenBackground;
        private MapPathsGraphic paths;
        private GameObject modal,deckOverlay;
        private CanvasGroup content;
        private Text hpHud,goldHud,areaHud,deckHud;
        public DungeonMapScroller MapScroller { get; private set; }
        public RectTransform MapContent { get; private set; }
        private string focusedNode;
        private PresentationAssets Art => Flow.Assets;
        private static readonly Color Gold=new Color(.89f,.75f,.43f),Pale=new Color(.88f,.88f,.80f),Muted=new Color(.56f,.66f,.65f);
        private void Start() { if(GameFlow.Instance==null)GameFlow.LoadBoot(); }
        public void Bind(GameFlow flow,FlowScreen screen)
        {
            Flow=flow;Screen=screen;flow.Attach(this);
            var canvasObject=new GameObject("BRODUN Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform,false);Root=(RectTransform)canvasObject.transform;
            var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvasObject.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            content=canvasObject.GetComponent<CanvasGroup>();content.interactable=false;
            var backdrop=screen==FlowScreen.Title?Art.titleBackground:Art.mapBackground;
            if(screen==FlowScreen.DungeonMap&&flow.Session.Run!=null)
                backdrop=BattleBackgrounds.Resolve(flow.Session.Run.ThemeId,false,backdrop);
            if(screen==FlowScreen.CharacterSelect)
                backdrop=BattleBackgrounds.Resolve(flow.Session.PendingThemeId,false,backdrop);
            Background(backdrop,screen==FlowScreen.Title?.13f:.78f);
            if(screen==FlowScreen.Title)BuildTitle();
            else if(screen==FlowScreen.CharacterSelect)BuildCharacters();
            else if(screen==FlowScreen.DungeonMap)BuildMap();
            else if(screen==FlowScreen.Room)gameObject.AddComponent<RoomView>().Build(flow,Root);
            else if(screen==FlowScreen.RunResult)RunResultView.Build(flow,Root);
        }
        public void Ready()
        {
            content.interactable=true;
            if(Screen==FlowScreen.DungeonMap){RefreshMap();if(Flow.Session.PendingRoom!=null)ShowPendingRoom(Flow.Session.PendingRoom);}
            if(EventSystem.current!=null)
            {
                var first=Root.GetComponentsInChildren<StyledButton>().FirstOrDefault(b=>b.interactable);
                if(first!=null)EventSystem.current.SetSelectedGameObject(first.gameObject);
            }
        }
        private void Background(Sprite sprite,float darkness)
        {
            var bg=Image(Root,"Dungeon background",sprite,Color.white,Vector2.zero,Vector2.one);
            screenBackground=bg;
            // Both supplied backgrounds are 16:9; fill the canvas, no web-like black gutters.
            bg.preserveAspect=false;
            Image(Root,"Veil",null,new Color(.015f,.025f,.03f,darkness),Vector2.zero,Vector2.one);
            Image(Root,"Top iron trim",null,Gold,new Vector2(0,.995f),Vector2.one);
            Image(Root,"Bottom iron trim",null,new Color(.44f,.36f,.21f),Vector2.zero,new Vector2(1,.005f));
        }
        private void BuildTitle()
        {
            Label(Root,"R O G U E L I T E   /   D E C K B U I L D E R",24,Muted,new Vector2(.2f,.86f),new Vector2(.8f,.91f));
            var title=Label(Root,"BRODUN",144,Gold,new Vector2(.14f,.64f),new Vector2(.86f,.85f));
            title.font=Art.logoFont;
            title.fontStyle=FontStyle.Bold;title.gameObject.AddComponent<Shadow>().effectDistance=new Vector2(4,-5);
            Label(Root,"운명을 고르고, 심연으로 내려가라",27,Pale,new Vector2(.25f,.61f),new Vector2(.75f,.67f));
            Button(Root,"GameStart","GAME START  /  게임 시작",new Vector2(.37f,.41f),new Vector2(.63f,.49f),()=>Flow.StartNewRun());
            Button(Root,"Quit","EXIT  /  종료",new Vector2(.40f,.29f),new Vector2(.60f,.36f),()=>Flow.QuitGame());
            Label(Root,"B R O D U N   ·   테마를 선택하고 원정 시작",21,Muted,new Vector2(.25f,.045f),new Vector2(.75f,.09f));
            // No false Continue/Settings promise: neither feature is implemented yet.
        }
        private void BuildCharacters()
        {
            Label(Root,"CHOOSE YOUR FATE",42,Gold,new Vector2(.2f,.91f),new Vector2(.8f,.975f));
            Label(Root,"원정 테마를 고른 뒤 영웅을 선택하세요",22,Pale,new Vector2(.2f,.86f),new Vector2(.8f,.905f));
            for(int i=0;i<DungeonThemes.All.Count;i++)
            {
                var theme=DungeonThemes.All[i];float left=.10f+i*.207f;
                var button=Button(Root,"Theme_"+theme.Id,theme.Name,new Vector2(left,.79f),new Vector2(left+.195f,.85f),()=>SelectTheme(theme.Id));
                button.interactable=DungeonThemes.IsAvailable(Flow.Session.Definitions,theme.Id);
                themeButtons.Add(theme.Id,button);
            }
            RefreshThemes();
            var definitions=Flow.Session.Definitions;
            for(int i=0;i<definitions.Characters.Count;i++)
            {
                var hero=definitions.Characters[i];float left=.10f+i*.275f;
                var card=Button(Root,"Character_"+hero.Id,"",new Vector2(left,.18f),new Vector2(left+.25f,.765f),()=>Flow.ChooseCharacter(hero.Id),true);
                card.normal=Art.nodeNormal;card.hover=Art.nodeHover;card.pressed=Art.nodeSelected;card.disabled=Art.nodeLocked;
                card.GetComponent<Image>().sprite=Art.nodeNormal;
                var rect=(RectTransform)card.transform;
                Image(rect,"Hero portrait",Art.Portrait(hero.Id),Color.white,new Vector2(.20f,.39f),new Vector2(.8f,.82f)).preserveAspect=true;
                Label(rect,hero.Name,38,Gold,new Vector2(.06f,.84f),new Vector2(.94f,.98f));
                Label(rect,"HP  "+hero.MaxHP+"     ENERGY  "+hero.Energy,26,Pale,new Vector2(.05f,.295f),new Vector2(.95f,.36f));
                Label(rect,hero.Description,23,Pale,new Vector2(.06f,.19f),new Vector2(.94f,.29f));
                Label(rect,"시작 덱  "+hero.StartingDeck.Count+"장",20,Muted,new Vector2(.08f,.135f),new Vector2(.92f,.20f));
                Label(rect,"선택",28,Gold,new Vector2(.12f,.025f),new Vector2(.88f,.13f));
            }
            Button(Root,"Back","돌아가기",new Vector2(.04f,.05f),new Vector2(.19f,.12f),()=>Flow.ReturnToTitle());
            Label(Root,"선택한 영웅의 카드로 새로운 여정이 시작됩니다",22,Muted,new Vector2(.27f,.06f),new Vector2(.78f,.11f));
        }
        private void SelectTheme(string id)
        {
            if(!Flow.ChooseTheme(id))return;
            screenBackground.sprite=BattleBackgrounds.Resolve(id,false,Art.mapBackground);
            RefreshThemes();
        }
        private void RefreshThemes()
        {
            foreach(var pair in themeButtons)
            {
                bool selected=pair.Key==Flow.Session.PendingThemeId;
                pair.Value.normal=selected?Art.nodeSelected:Art.buttonNormal;
                pair.Value.GetComponentInChildren<Text>().text=(selected?"✓ ":"")+DungeonThemes.Find(pair.Key).Name;
                pair.Value.RefreshStyle();
            }
        }
        private void BuildMap()
        {
            var run=Flow.Session.Run;
            if(run==null)throw new InvalidOperationException("Map view requires an active RunState.");
            var panel=Image(Root,"Run HUD",null,new Color(.025f,.04f,.045f,.94f),new Vector2(.025f,.865f),new Vector2(.975f,.965f));
            var hud=(RectTransform)panel.transform;
            Label(hud,Flow.Session.Definitions.Character(run.SelectedCharacter).Name,30,Gold,new Vector2(.025f,.12f),new Vector2(.16f,.88f));
            hpHud=Label(hud,"",29,Pale,new Vector2(.17f,.12f),new Vector2(.36f,.88f));
            goldHud=Label(hud,"",29,Gold,new Vector2(.37f,.12f),new Vector2(.54f,.88f));
            areaHud=Label(hud,"",26,Muted,new Vector2(.55f,.12f),new Vector2(.75f,.88f));
            var deckButton=Button(hud,"DeckButton","현재 덱",new Vector2(.79f,.15f),new Vector2(.975f,.85f),OpenDeck);
            deckHud=deckButton.GetComponentInChildren<Text>();
            Label(Root,DungeonThemes.Find(run.ThemeId).Name,40,Gold,new Vector2(.2f,.77f),new Vector2(.8f,.85f));
            var viewportImage=Image(Root,"DungeonMapViewport",null,new Color(.016f,.025f,.035f,.58f),new Vector2(.025f,.205f),new Vector2(.975f,.765f));
            viewportImage.raycastTarget=true;viewportImage.gameObject.AddComponent<RectMask2D>();
            MapScroller=viewportImage.gameObject.AddComponent<DungeonMapScroller>();
            MapScroller.horizontal=true;MapScroller.vertical=false;MapScroller.movementType=ScrollRect.MovementType.Clamped;
            MapScroller.inertia=true;MapScroller.decelerationRate=.08f;MapScroller.scrollSensitivity=95;
            var viewport=(RectTransform)viewportImage.transform;Canvas.ForceUpdateCanvases();
            var board=Rect(viewport,"MapContent",Vector2.zero,new Vector2(0,1));
            board.pivot=new Vector2(0,.5f);
            board.sizeDelta=new Vector2((float)DungeonMapLayout.ContentWidth(run.Map.Max(n=>n.Layer),viewport.rect.width),0);
            MapContent=board;MapScroller.content=board;MapScroller.viewport=viewport;
            paths=new GameObject("Data driven paths",typeof(RectTransform),typeof(CanvasRenderer),typeof(MapPathsGraphic)).GetComponent<MapPathsGraphic>();
            paths.transform.SetParent(board,false);Stretch((RectTransform)paths.transform);
            // Graphic vertices are relative to its own centre pivot, not MapContent's left pivot.
            paths.Bind(Flow.Session,n=>new Vector2((float)DungeonMapLayout.X(n.Layer),(float)(1-n.Y/100)*board.rect.height)+paths.rectTransform.rect.min);
            foreach(var node in run.Map)
            {
                var position=new Vector2((float)DungeonMapLayout.X(node.Layer)/board.rect.width,(float)(1-node.Y/100));
                var button=Button(board,"Node_"+node.Id,"",position,position,()=>Flow.SelectRoom(node.Id),true);
                var rect=(RectTransform)button.transform;rect.sizeDelta=new Vector2(128,100);rect.anchoredPosition=Vector2.zero;
                var outline=button.gameObject.AddComponent<Outline>();outline.effectDistance=new Vector2(2,-2);
                nodes.Add(node.Id,button);
                var icon=Image(rect,"Room icon",node.Type==RoomType.Start?Art.current:Art.RoomIcon(node),Color.white,new Vector2(.24f,.38f),new Vector2(.76f,.85f));icon.preserveAspect=true;
                nodeIcons.Add(node.Id,icon);
                var text=Label(rect,RoomName(node),20,Pale,new Vector2(-.06f,0),new Vector2(1.06f,.36f));nodeLabels.Add(node.Id,text);
                var marker=Label(rect,"",19,Gold,new Vector2(-.15f,1.01f),new Vector2(1.15f,1.35f));nodeStatus.Add(node.Id,marker.gameObject);
            }
            Label(Root,"드래그하여 앞길을 살펴보고, 금빛 경로에서 다음 방을 선택하세요",24,Pale,new Vector2(.18f,.13f),new Vector2(.82f,.185f));
            Label(Root,"● 현재 위치     ◆ 이동 가능     ✓ 완료     ▪ 잠김",22,Muted,new Vector2(.27f,.055f),new Vector2(.85f,.105f));
            var leave=Button(Root,"LeaveRun","여정 종료",new Vector2(.035f,.055f),new Vector2(.20f,.115f),ConfirmLeave);
            leave.Destructive=true;leave.RefreshStyle();
            Button(Root,"FocusCurrent","현재 위치",new Vector2(.79f,.055f),new Vector2(.955f,.115f),()=>MapScroller.FocusLayer(DungeonRuntime.Node(run,run.CurrentNode).Layer,true));
            RefreshMap();
        }
        public void RefreshMap()
        {
            if(Screen!=FlowScreen.DungeonMap)return;
            var run=Flow.Session.Run;
            hpHud.text="HP  "+run.CurrentHP+" / "+run.MaxHP;goldHud.text="GOLD  "+run.Gold;
            areaHud.fontSize=20;areaHud.text=run.CurrentArea+"구역 · "+DungeonThemes.Find(run.ThemeId).Name;deckHud.text="현재 덱  "+run.Deck.Count;
            foreach(var pair in nodes)
            {
                var state=Flow.Session.NodeState(pair.Key);var button=pair.Value;
                button.normal=state==NodeVisualState.Current?Art.nodeSelected:state==NodeVisualState.Locked?Art.nodeLocked:Art.nodeNormal;
                button.hover=Art.nodeHover;button.pressed=Art.nodeSelected;button.disabled=button.normal;
                button.interactable=state==NodeVisualState.Available;button.Highlighted=button.interactable;
                button.GetComponent<Image>().sprite=button.normal;
                // Non-interactable does not mean invisible: current/completed remain legible.
                button.GetComponent<Image>().color=state==NodeVisualState.Locked?new Color(.54f,.58f,.60f):Color.white;
                nodeIcons[pair.Key].color=state==NodeVisualState.Locked?new Color(.66f,.70f,.70f):Color.white;
                var outline=button.GetComponent<Outline>();outline.enabled=state==NodeVisualState.Current||state==NodeVisualState.Available;
                outline.effectColor=state==NodeVisualState.Current?Gold:new Color(.18f,.65f,.58f,.75f);
                var node=DungeonRuntime.Node(run,pair.Key);
                nodeIcons[pair.Key].sprite=run.CompletedNodes.Contains(pair.Key)?Art.completed:node.Type==RoomType.Start?Art.current:Art.RoomIcon(node);
                nodeLabels[pair.Key].color=state==NodeVisualState.Locked?Muted:Pale;
                var marker=nodeStatus[pair.Key].GetComponent<Text>();
                marker.text=state==NodeVisualState.Current?(run.CompletedNodes.Contains(pair.Key)?"현재 · ✓ 완료":"현재 위치"):state==NodeVisualState.Completed?"✓ 완료":state==NodeVisualState.Available?"◆ 이동 가능":"";
                marker.color=state==NodeVisualState.Completed?new Color(.28f,.78f,.65f):Gold;
            }
            paths.SetVerticesDirty();
            if(focusedNode!=run.CurrentNode)
            {
                bool smooth=focusedNode!=null;focusedNode=run.CurrentNode;
                MapScroller.FocusLayer(DungeonRuntime.Node(run,run.CurrentNode).Layer,smooth);
            }
        }
        public void ShowPendingRoom(PendingRoomContext context)
        {
            ShowModal(RoomName(context.Node),context.IsBattle?
                "전투 진입 준비가 완료되었습니다.\n전투 화면은 다음 개발 단계에서 연결됩니다.\n이 방은 아직 완료되지 않았습니다.":
                "이 방의 콘텐츠는 아직 준비 중입니다.\n이 방은 아직 완료되지 않았습니다.","지도 확인",CloseModal);
        }
        public void OpenDeck()
        {
            if(!Flow.Session.OpenDeck())return;
            RefreshMap();
            CloseModal();
            deckOverlay=PanelOverlay("DeckOverlay",out var panel);
            Label(panel,"현재 덱  /  "+Flow.Session.Run.Deck.Count+"장",36,Gold,new Vector2(.1f,.86f),new Vector2(.9f,.96f));
            var viewport=Image(panel,"Deck viewport",null,new Color(.03f,.045f,.055f,.95f),new Vector2(.065f,.16f),new Vector2(.935f,.83f));
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.scrollSensitivity=35;
            var list=Rect((RectTransform)viewport.transform,"Card list",new Vector2(0,1),Vector2.one);list.pivot=new Vector2(.5f,1);list.sizeDelta=new Vector2(0,0);
            var layout=list.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=8;layout.padding=new RectOffset(15,15,12,12);
            layout.childControlHeight=true;layout.childForceExpandHeight=false;layout.childControlWidth=true;layout.childForceExpandWidth=true;
            list.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            scroll.content=list;scroll.viewport=(RectTransform)viewport.transform;
            foreach(var instance in Flow.Session.Run.Deck)
            {
                var card=Flow.Session.Definitions.Card(instance.CardId,instance.UpgradeLevel);
                string element=Flow.Session.Definitions.ElementName(card.Element);
                var row=Image(list,"DeckCard_"+instance.InstanceId,null,new Color(.09f,.13f,.16f,.95f),Vector2.zero,Vector2.one);
                row.gameObject.AddComponent<LayoutElement>().preferredHeight=144;
                var tint=CardView.ElementTint(card.Element);
                Image(row.transform,"Element accent",null,tint,new Vector2(0,.06f),new Vector2(.004f,.94f));
                var illustration=Flow.CardArtwork(card.Id);
                if(illustration!=null)
                {
                    var thumb=Image(row.transform,"Card artwork",illustration,Color.white,new Vector2(.015f,.05f),new Vector2(.155f,.95f));
                    thumb.preserveAspect=true;thumb.raycastTarget=false;
                }
                float start=illustration!=null?.18f:.025f;
                Label((RectTransform)row.transform,card.Name,27,Pale,new Vector2(start,.62f),new Vector2(.58f,.94f),TextAnchor.MiddleLeft);
                string role=card.Type==CardType.Attack?"공격":card.Type==CardType.Guard?"방어":"기술";
                Label((RectTransform)row.transform,"비용 "+card.Cost+"  ·  "+(element.Length>0?element+" / ":"")+role,21,card.Cost==0?BattleUi.Teal:tint,new Vector2(.58f,.63f),new Vector2(.975f,.94f),TextAnchor.MiddleRight);
                var rules=Label((RectTransform)row.transform,card.Description,22,Pale,new Vector2(start,.09f),new Vector2(.965f,.61f),TextAnchor.MiddleLeft);
                rules.resizeTextForBestFit=true;rules.resizeTextMinSize=18;rules.resizeTextMaxSize=22;
            }
            Button(panel,"DeckClose","닫기",new Vector2(.35f,.035f),new Vector2(.65f,.125f),CloseDeck);
            Focus("DeckClose");
        }
        public void CloseDeck()
        {
            Flow.Session.CloseDeck();if(deckOverlay!=null)Destroy(deckOverlay);deckOverlay=null;Focus("DeckButton");
        }
        public void Back()
        {
            if(Flow.Session.IsTransitioning)return;
            if(IsDeckVisible){CloseDeck();return;}
            if(modal!=null){CloseModal();return;}
            if(Screen==FlowScreen.CharacterSelect)Flow.ReturnToTitle();
            else if(Screen==FlowScreen.DungeonMap)ConfirmLeave();
        }
        private void ConfirmLeave()
        {
            ShowModal("현재 여정을 종료할까요?","현재 여정의 진행 정보는 사라집니다.\n저장 기능은 아직 제공되지 않습니다.","여정 종료",()=>Flow.ReturnToTitle(),true);
        }
        private void ShowModal(string title,string body,string accept,Action action,bool cancel=false)
        {
            CloseModal();modal=PanelOverlay("RoomOverlay",out var panel);
            Label(panel,title,38,Gold,new Vector2(.08f,.70f),new Vector2(.92f,.89f));
            Label(panel,body,26,Pale,new Vector2(.08f,.30f),new Vector2(.92f,.69f));
            var acceptButton=Button(panel,"ModalAccept",accept,new Vector2(cancel?.53f:.32f,.10f),new Vector2(cancel?.87f:.68f,.25f),action);
            if(cancel){acceptButton.Destructive=true;acceptButton.RefreshStyle();}
            if(cancel)Button(panel,"ModalCancel","계속하기",new Vector2(.13f,.10f),new Vector2(.47f,.25f),CloseModal);
            Focus(cancel?"ModalCancel":"ModalAccept");
        }
        private void CloseModal(){if(modal!=null)Destroy(modal);modal=null;}
        private GameObject PanelOverlay(string name,out RectTransform panel)
        {
            var overlay=Image(Root,name,null,new Color(0,.012f,.02f,.88f),Vector2.zero,Vector2.one);overlay.raycastTarget=true;
            var frame=Image((RectTransform)overlay.transform,"Panel",Art.nodeNormal,Color.white,new Vector2(.23f,.16f),new Vector2(.77f,.84f));
            panel=(RectTransform)frame.transform;return overlay.gameObject;
        }
        private void Focus(string name)
        {
            var target=Root.GetComponentsInChildren<StyledButton>().LastOrDefault(b=>b.name==name);
            if(target!=null && EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(target.gameObject);
        }
        public static string RoomName(RoomNode node)
        {
            if(node.Type==RoomType.Start)return "입구";
            if(node.Type==RoomType.Boss)return "보스";
            if(node.Type==RoomType.Puzzle)return "퍼즐";
            if(node.Type==RoomType.Event)return node.Event==EventKind.Shop?"상점":node.Event==EventKind.Altar?"제단":"휴식처";
            return node.Encounter==EnemyType.Elite?"엘리트":"전투";
        }
        private RectTransform Rect(Transform parent,string name,Vector2 min,Vector2 max)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);
            rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;return rect;
        }
        private static void Stretch(RectTransform rect){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
        private Image Image(Transform parent,string name,Sprite sprite,Color color,Vector2 min,Vector2 max)
        {
            var rect=Rect(parent,name,min,max);var image=rect.gameObject.AddComponent<Image>();image.sprite=sprite;image.color=color;image.raycastTarget=false;return image;
        }
        private Text Label(Transform parent,string text,int size,Color color,Vector2 min,Vector2 max,TextAnchor alignment=TextAnchor.MiddleCenter)
        {
            var rect=Rect(parent,"Text "+text.Split('\n')[0],min,max);var label=rect.gameObject.AddComponent<Text>();
            label.font=Art.font;label.text=text;label.fontSize=size;label.color=color;label.alignment=alignment;
            label.raycastTarget=false;label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Truncate;
            return label;
        }
        private StyledButton Button(Transform parent,string name,string title,Vector2 min,Vector2 max,Action action,bool card=false)
        {
            var image=Image(parent,name,card?Art.nodeNormal:Art.buttonNormal,Color.white,min,max);image.raycastTarget=true;
            var button=image.gameObject.AddComponent<StyledButton>();button.targetGraphic=image;
            button.Configure(card?Art.nodeNormal:Art.buttonNormal,card?Art.nodeHover:Art.buttonHover,
                card?Art.nodeSelected:Art.buttonPressed,card?Art.nodeLocked:Art.buttonDisabled);
            button.onClick.AddListener(()=>
            {
                if(Flow.Session.IsTransitioning)return;
                if(modal!=null && !button.transform.IsChildOf(modal.transform))return;
                if(deckOverlay!=null && !button.transform.IsChildOf(deckOverlay.transform))return;
                action();
            });
            if(!string.IsNullOrEmpty(title))Label((RectTransform)image.transform,title,27,Gold,new Vector2(.04f,.05f),new Vector2(.96f,.95f));
            return button;
        }
    }
}

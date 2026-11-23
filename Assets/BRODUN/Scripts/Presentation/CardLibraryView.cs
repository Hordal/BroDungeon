using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Brodun.Presentation
{
    internal sealed class CardLibraryView : MonoBehaviour
    {
        private GameFlow flow;
        private RectTransform panel,list;
        private ScrollRect scroll;
        private Text title;
        private CanvasGroup underlyingInput;
        private bool priorInteractable,released;
        private GameObject priorSelection;
        private StyledButton[] tabs;
        private PresentationAssets Art=>flow.Assets;
        private GameDefinitions Data=>flow.Session.Definitions;

        public void Build(GameFlow owner,RectTransform underlying,bool deck)
        {
            flow=owner;
            var root=(RectTransform)transform;
            var overlayInput=gameObject.AddComponent<CanvasGroup>();
            overlayInput.ignoreParentGroups=true;
            if(!underlying.TryGetComponent(out underlyingInput))underlyingInput=underlying.gameObject.AddComponent<CanvasGroup>();
            priorInteractable=underlyingInput.interactable;
            underlyingInput.interactable=false;
            priorSelection=EventSystem.current?.currentSelectedGameObject;

            BattleUi.Image(root,"Library input blocker",null,new Color(.007f,.012f,.022f,.85f),
                Vector2.zero,Vector2.one).raycastTarget=true;
            panel=BattleUi.Surface(root,"Library panel",new Vector2(.15f,.073f),new Vector2(.85f,.93f),
                BattleChrome.Form.Hud,BattleUi.Bronze);
            title=Text(panel,"Library title",deck?"현재 덱":"도감",39,BattleUi.Gold,.06f,.865f,.94f,.97f);
            BuildScroll(deck?.83f:.72f);
            var close=BattleUi.CommandButton(panel,deck?"DeckClose":"CodexClose","닫기",Art,new Vector2(.38f,.034f),
                new Vector2(.62f,.114f),()=>flow.CloseCardLibrary(),BattleChrome.Form.Action);
            if(deck)
            {
                var run=flow.Session.Run;
                var entries=run.Deck.GroupBy(card=>card.CardId).ToArray();
                var hero=Data.Character(run.SelectedCharacter);
                title.text=$"현재 덱  ·  {run.Deck.Count}장 / {entries.Length}종";
                foreach(var entry in entries)AddRow(Data.Card(entry.Key),entry.Count(),hero.ProvisionalStartingCards.Contains(entry.Key),"DeckCard_");
                RefreshScroll();
            }
            else
            {
                var characters=Data.Characters;
                tabs=new StyledButton[characters.Count+2];
                for(int i=0;i<characters.Count;i++)
                {
                    var hero=characters[i];
                    int index=i;
                    tabs[i]=Tab(index,"Codex_"+hero.Id,hero.Name,()=>ShowCards(hero.Id,index));
                }
                int shared=characters.Count;
                tabs[shared]=Tab(shared,"Codex_Shared","공용 카드",()=>ShowCards(null,shared));
                tabs[shared+1]=Tab(shared+1,"Codex_Items","아이템",ShowItems);
                var selected=flow.Session.Run?.SelectedCharacter;
                int initial=selected.HasValue?System.Array.FindIndex(characters.ToArray(),hero=>hero.Id==selected.Value):0;
                if(initial<0)initial=0;
                ShowCards(characters[initial].Id,initial);
            }
            if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(close.gameObject);
        }

        private StyledButton Tab(int index,string key,string name,System.Action click)
        {
            float width=.88f/tabs.Length;
            return BattleUi.CommandButton(panel,key,name,Art,new Vector2(.06f+index*width,.757f),
                new Vector2(.06f+(index+1)*width-.009f,.835f),click,BattleChrome.Form.Action);
        }

        private void BuildScroll(float top)
        {
            var viewport=BattleUi.Image(panel,"Library viewport",null,new Color(.022f,.031f,.041f,.95f),
                new Vector2(.06f,.15f),new Vector2(.915f,top));
            viewport.raycastTarget=true;
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll=viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal=false;scroll.scrollSensitivity=70;scroll.movementType=ScrollRect.MovementType.Clamped;
            list=BattleUi.Rect(viewport.transform,"Card list",new Vector2(0,1),Vector2.one);
            list.pivot=new Vector2(.5f,1);
            var layout=list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing=7;layout.padding=new RectOffset(13,13,10,10);
            layout.childControlHeight=true;layout.childForceExpandHeight=false;
            layout.childControlWidth=true;layout.childForceExpandWidth=true;
            list.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            scroll.content=list;scroll.viewport=viewport.rectTransform;
            var rail=BattleUi.Image(panel,"Library scrollbar",null,new Color(.10f,.14f,.17f),
                new Vector2(.926f,.15f),new Vector2(.943f,top));
            rail.raycastTarget=true;
            var sliding=BattleUi.Rect(rail.transform,"Sliding area",Vector2.zero,Vector2.one);
            sliding.offsetMin=Vector2.one*2;sliding.offsetMax=Vector2.one*-2;
            var handle=BattleUi.Image(sliding,"Handle",null,BattleUi.Gold,Vector2.zero,Vector2.one);
            handle.raycastTarget=true;
            var bar=rail.gameObject.AddComponent<Scrollbar>();
            bar.direction=Scrollbar.Direction.BottomToTop;bar.handleRect=handle.rectTransform;bar.targetGraphic=handle;
            scroll.verticalScrollbar=bar;
            scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
        }

        private void SelectTab(int index)
        {
            for(int i=0;i<tabs.Length;i++)
            {
                var chrome=(BattleChrome)tabs[i].targetGraphic;
                chrome.Accent=i==index?BattleUi.Gold:BattleUi.Steel;
                chrome.SetVerticesDirty();
            }
            for(int i=list.childCount-1;i>=0;i--)
            {
                var row=list.GetChild(i).gameObject;
                row.SetActive(false);Destroy(row);
            }
        }

        private void ShowCards(CharacterId? hero,int tab)
        {
            SelectTab(tab);
            var cards=Data.GameplayCards.Where(card=>card.Owner==hero).ToArray();
            title.text=(hero.HasValue?Data.Character(hero.Value).Name:"공용 카드")+" 도감  ·  "+cards.Length+"종";
            foreach(var card in cards)AddRow(card,1,false,"CodexCard_");
            RefreshScroll();
        }

        private void ShowItems()
        {
            SelectTab(tabs.Length-1);
            title.text="아이템 도감";
            var hint=Text(list,"Item empty state","등록된 아이템이 없습니다.",26,BattleUi.Pale,0,0,1,1);
            hint.gameObject.AddComponent<LayoutElement>().preferredHeight=140;
            RefreshScroll();
        }

        private void AddRow(CardDefinition card,int copies,bool provisional,string prefix)
        {
            var row=BattleUi.Image(list,prefix+card.Id,null,new Color(.063f,.083f,.112f,.98f),Vector2.zero,Vector2.one);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight=128;
            var tint=CardView.ElementTint(card.Element);
            BattleUi.Image(row.transform,"Element accent",null,tint,new Vector2(0,.07f),new Vector2(.004f,.93f));
            var illustration=flow.CardArtwork(card)??CardView.Placeholder(card,Art);
            var thumbnail=BattleUi.Image(row.transform,"Card artwork",illustration,Color.white,new Vector2(.017f,.06f),new Vector2(.151f,.94f));
            thumbnail.preserveAspect=true;
            Text(row.transform,"Card name",copies>1?card.Name+"  ×"+copies:card.Name,27,BattleUi.Pale,.17f,.66f,.58f,.96f,TextAnchor.MiddleLeft);
            string metadata=$"비용 {(card.XCost?"X":card.Cost.ToString())}  ·  {Data.ElementName(card.Element)} / {CardView.Classification(card,provisional)}";
            Text(row.transform,"Card details",metadata,20,card.Cost==0?BattleUi.Teal:tint,.58f,.66f,.97f,.95f,TextAnchor.MiddleRight);
            var rules=Text(row.transform,"Card rules",card.Description,22,BattleUi.Pale,.17f,.08f,.966f,.65f,TextAnchor.MiddleLeft);
            rules.resizeTextForBestFit=true;rules.resizeTextMinSize=17;rules.resizeTextMaxSize=22;
        }

        private Text Text(Transform parent,string name,string label,int size,Color tint,float left,float bottom,float right,float top,
            TextAnchor alignment=TextAnchor.MiddleCenter)
        {
            var text=BattleUi.Text(parent,name,label,Art.font,size,tint,new Vector2(left,bottom),new Vector2(right,top));
            text.alignment=alignment;
            return text;
        }

        private void RefreshScroll()
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(list);
            scroll.StopMovement();
            scroll.verticalNormalizedPosition=1;
        }

        public void ReleaseInput()
        {
            if(released)return;
            released=true;
            if(underlyingInput!=null)underlyingInput.interactable=priorInteractable;
            if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(
                priorSelection!=null&&priorSelection.activeInHierarchy?priorSelection:null);
            gameObject.SetActive(false);
        }

        private void OnDestroy()=>ReleaseInput();
    }
}

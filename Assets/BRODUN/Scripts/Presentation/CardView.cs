using System;
using System.Linq;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Brodun.Presentation
{
    public sealed class CardView : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerClickHandler,IScrollHandler,ICanvasRaycastFilter
    {
        public RectTransform visual;
        public Image frame,artwork;
        public Text title,cost,element,kind,effect,description;
        public CanvasGroup ink;
        public string InstanceId { get; private set; }
        public CardDefinition Definition { get; private set; }
        public bool Hovered { get; private set; }
        public bool CanPlay { get; private set; }
        private Action<CardView> selected;
        private int restingIndex;
        private bool presenting;
        private Image costSeal,costRim,costGem,elementRule,textBed,nameBed;
        private float exposedWidth=HandPresentation.CardWidth;
        private bool battleHand;
        private readonly Vector3[] cardCorners=new Vector3[4];
        private RectTransform canvasSurface;
        private HandPresentation handOwner;
        private Image hoverInput;
        public bool HasHandPosition {get;private set;}
        public bool IsPresenting => presenting;
        public void Bind(CardDefinition card,string id,BattlePresentationAssets art,GameDefinitions data,Action<CardView> click)
        {
            Definition=card;InstanceId=id;selected=click;restingIndex=transform.GetSiblingIndex();
            title.text=card.Name;cost.text=(card.XCost?"X":card.Cost.ToString());element.text=data.Elements.Single(e=>e.Id==card.Element).Name;
            kind.text=card.Type==CardType.Power?"지속":card.Type==CardType.Attack?"공격":card.Type==CardType.Guard?"방어":"기술";
            effect.text=(card.BaseDamage>0?"피해 "+card.BaseDamage+"   ":"")+(card.Block>0?"방어 "+card.Block:"");
            description.text=card.Description;
            // One shared layout for the prefab, generated cards and reward cards.
            battleHand=transform.parent!=null&&transform.parent.name=="Hand";
            var canvas=GetComponentInParent<Canvas>();
            canvasSurface=canvas!=null?(RectTransform)canvas.rootCanvas.transform:null;
            ((RectTransform)transform).sizeDelta=new Vector2(236,354);
            var hue=ElementTint(card.Element);
            element.color=hue;kind.color=hue;kind.text+=" · "+(card.Rarity==CardRarity.High?"상급":card.Rarity==CardRarity.Mid?"중급":card.Rarity==CardRarity.Low?"하급":"기본");cost.color=Color.white;
            frame.sprite=card.Type==CardType.Attack?art.attackFrame:card.Type==CardType.Guard?art.guardFrame:art.skillFrame;
            // Shared cards deliberately use the same existing icons in hand, rewards, shops and deck views.
            bool sharedCard=card.IsReworked&&!card.Owner.HasValue;
            var illustration=!sharedCard&&GameFlow.Instance!=null?GameFlow.Instance.CardArtwork(card.Id):null;
            artwork.sprite=illustration!=null?illustration:card.Type==CardType.Guard?art.common.altar:art.common.battle;
            artwork.preserveAspect=true;
            artwork.raycastTarget=false;
            Slot(artwork.rectTransform,.07f,.415f,.93f,.825f);
            if(costSeal==null)
            {
                textBed=BattleUi.Image(visual,"Obsidian text bed",null,new Color(.025f,.037f,.047f,.96f),new Vector2(.065f,.025f),new Vector2(.935f,.415f));
                textBed.transform.SetSiblingIndex(artwork.transform.GetSiblingIndex()+1);
                costSeal=BattleUi.Image(visual,"Energy seal",null,new Color(.055f,.08f,.10f,.98f),new Vector2(.03f,.795f),new Vector2(.245f,.965f));
                costSeal.transform.SetSiblingIndex(textBed.transform.GetSiblingIndex()+1);
                elementRule=BattleUi.Image(visual,"Element rule",null,hue,new Vector2(.075f,.408f),new Vector2(.925f,.416f));
                elementRule.transform.SetSiblingIndex(costSeal.transform.GetSiblingIndex()+1);
                costRim=BattleUi.Image(costSeal.transform,"Cost crystal rim",null,BattleUi.Gold,new Vector2(.15f,.15f),new Vector2(.85f,.85f));
                costRim.rectTransform.localRotation=Quaternion.Euler(0,0,45);
                costGem=BattleUi.Image(costSeal.transform,"Cost crystal core",null,new Color(.025f,.055f,.07f),new Vector2(.22f,.22f),new Vector2(.78f,.78f));
                costGem.rectTransform.localRotation=Quaternion.Euler(0,0,45);
            }
            elementRule.color=hue;
            // The numeral belongs to its own badge, never beneath art/header siblings.
            costSeal.color=Color.clear;
            Slot(costSeal.rectTransform,.13f,.915f,.13f,.915f);costSeal.rectTransform.sizeDelta=new Vector2(56,56);
            cost.transform.SetParent(costSeal.transform,false);Slot(cost.rectTransform,0,0,1,1);
            cost.gameObject.SetActive(true);cost.enabled=true;cost.text=(card.XCost?"X":card.Cost.ToString());
            cost.fontSize=38;cost.fontStyle=FontStyle.Bold;cost.resizeTextForBestFit=false;
            cost.alignment=TextAnchor.MiddleCenter;cost.lineSpacing=1;
            cost.horizontalOverflow=HorizontalWrapMode.Overflow;cost.verticalOverflow=VerticalWrapMode.Overflow;
            cost.raycastTarget=false;cost.transform.SetAsLastSibling();
            Slot(title.rectTransform,.075f,.32f,.925f,.405f);title.fontSize=25;title.color=BattleUi.Pale;
            title.resizeTextForBestFit=true;title.resizeTextMinSize=21;title.resizeTextMaxSize=25;
            Slot(element.rectTransform,.08f,.03f,.53f,.085f);element.fontSize=18;element.alignment=TextAnchor.MiddleLeft;
            Slot(kind.rectTransform,.53f,.03f,.92f,.085f);kind.fontSize=18;kind.alignment=TextAnchor.MiddleRight;
            // The authoritative description already contains damage/block and conditions.
            // Do not repeat a misleading base-damage summary above the full rules text.
            effect.gameObject.SetActive(false);
            Slot(description.rectTransform,.09f,.10f,.91f,.31f);
            description.fontSize=21;description.resizeTextForBestFit=true;description.resizeTextMinSize=17;description.resizeTextMaxSize=21;
            description.alignment=TextAnchor.MiddleCenter;description.lineSpacing=1.06f;
            if(battleHand)
            {
                ((RectTransform)transform).sizeDelta=new Vector2(HandPresentation.CardWidth,HandPresentation.CardHeight);
                Slot(textBed.rectTransform,.065f,.025f,.935f,.35f);
                Slot(description.rectTransform,.085f,.105f,.915f,.34f);
                description.fontSize=21;description.resizeTextMinSize=12;description.resizeTextMaxSize=20;description.lineSpacing=1.10f;
                Slot(elementRule.rectTransform,.075f,.35f,.925f,.357f);
                // Quiet backing keeps the priority name readable above the full-color art.
                if(nameBed==null)nameBed=BattleUi.Image(visual,"Hand name backing",null,new Color(.025f,.037f,.047f,.96f),new Vector2(.23f,.88f),new Vector2(.94f,.98f));
                nameBed.transform.SetSiblingIndex(title.transform.GetSiblingIndex());
                StyleHandHeader();
            }
            foreach(var label in new[]{cost,title,element,kind,description})BattleUi.Readable(label);
            costSeal.transform.SetAsLastSibling();
            CanPlay=true;SetAvailable(true);
        }
        private static void Slot(RectTransform rect,float x0,float y0,float x1,float y1)
        {rect.anchorMin=new Vector2(x0,y0);rect.anchorMax=new Vector2(x1,y1);rect.offsetMin=rect.offsetMax=Vector2.zero;}
        public void SetHandExposedWidth(float width)
        {exposedWidth=width;if(battleHand)StyleHandHeader();}
        private void StyleHandHeader()
        {
            if(nameBed==null)return;
            bool compact=!Hovered&&exposedWidth<HandPresentation.CardWidth*.80f;
            if(compact)
            {
                // Put the complete name in the exposed left strip, below the cost badge.
                float right=Mathf.Clamp((exposedWidth-8)/HandPresentation.CardWidth,.30f,.94f);
                Slot(title.rectTransform,.045f,.705f,right,.84f);
                Slot(nameBed.rectTransform,.035f,.695f,right+.01f,.845f);
                title.fontSize=20;title.resizeTextMinSize=18;title.resizeTextMaxSize=20;title.alignment=TextAnchor.MiddleLeft;
                Slot(artwork.rectTransform,.065f,.365f,.935f,.69f);
            }
            else
            {
                Slot(title.rectTransform,.26f,.845f,.94f,.98f);
                Slot(nameBed.rectTransform,.25f,.84f,.95f,.985f);
                title.fontSize=26;title.resizeTextMinSize=22;title.resizeTextMaxSize=26;title.alignment=TextAnchor.MiddleCenter;
                Slot(artwork.rectTransform,.065f,.365f,.935f,.81f);
            }
        }
        public static Color ElementTint(ElementType element)
        {
            switch(element)
            {
                case ElementType.Earth:return new Color(.87f,.64f,.34f);
                case ElementType.Stone:return new Color(.72f,.76f,.78f);
                case ElementType.Wind:return new Color(.40f,.87f,.75f);
                case ElementType.Water:return new Color(.35f,.70f,1);
                case ElementType.Ice:return new Color(.68f,.90f,1);
                case ElementType.Poison:return new Color(.75f,.51f,.88f);
                case ElementType.Fire:return new Color(1,.49f,.27f);
                case ElementType.Lightning:return new Color(.92f,.88f,.48f);
                default:return new Color(1,.68f,.32f);
            }
        }
        public void SetAvailable(bool value)
        {
            CanPlay=value;ink.alpha=value?1:Hovered?.94f:.74f;
            frame.color=value?Color.white:new Color(.69f,.72f,.77f);
            var battle=GameFlow.Instance?.Session?.Battle?.Runtime;
            bool lacking=battleHand&&battle!=null&&Definition!=null&&battle.Energy<(Definition.XCost?1:Definition.Cost);
            cost.color=Color.white;
            bool free=Definition!=null&&!Definition.XCost&&Definition.Cost==0;
            if(costRim!=null)costRim.color=lacking?new Color(1,.36f,.24f):free?BattleUi.Teal:BattleUi.Gold;
            if(costGem!=null)costGem.color=lacking?new Color(.26f,.035f,.025f):free?new Color(.015f,.14f,.10f):new Color(.025f,.055f,.07f);
        }
        public void OnPointerEnter(PointerEventData data)
        {
            if(Hovered||presenting)return;
            if(handOwner!=null){handOwner.Hover(this);return;}
            Hovered=true;restingIndex=transform.GetSiblingIndex();transform.SetAsLastSibling();
        }
        public void OnPointerExit(PointerEventData data)
        {
            if(handOwner!=null){handOwner.Leave(this);return;}
            if(presenting)return;Hovered=false;transform.SetSiblingIndex(restingIndex);
        }
        public void AttachHand(HandPresentation owner)
        {
            handOwner=owner;HasHandPosition=true;
            if(hoverInput==null)
            {
                hoverInput=BattleUi.Image(transform,"Raised card input",null,Color.clear,new Vector2(.5f,.5f),new Vector2(.5f,.5f));
                hoverInput.raycastTarget=true;
            }
            hoverInput.gameObject.SetActive(false);
        }
        public void DetachHand(HandPresentation owner)
        {if(handOwner!=owner)return;handOwner=null;Hovered=false;if(hoverInput!=null)hoverInput.gameObject.SetActive(false);}
        public void SetHandHover(bool value)
        {Hovered=value;if(hoverInput!=null)hoverInput.gameObject.SetActive(value);StyleHandHeader();SetAvailable(CanPlay);}
        public bool IsRaycastLocationValid(Vector2 screenPoint,Camera eventCamera)
        {return !presenting&&(handOwner==null||handOwner.PointerOwner(screenPoint,eventCamera)==this);}
        public void OnScroll(PointerEventData data)
        {handOwner?.Scroll(Mathf.Abs(data.scrollDelta.x)>.001f?data.scrollDelta.x:data.scrollDelta.y);}
        private void OnDisable(){handOwner?.Leave(this);}
        public IEnumerator PresentUse(bool exhaust)
        {
            presenting=true;handOwner?.BeginUse(this);Hovered=false;
            if(hoverInput!=null)hoverInput.gameObject.SetActive(false);transform.SetAsLastSibling();
            var origin=visual.anchoredPosition;
            float originalScale=visual.localScale.x;
            for(float t=0;t<.24f;t+=Time.unscaledDeltaTime)
            {
                float u=t/.24f;
                visual.anchoredPosition=Vector2.Lerp(origin,new Vector2(exhaust?0:95,100),u);
                visual.localScale=Vector3.one*Mathf.Lerp(originalScale,.83f,u);
                ink.alpha=1-u*.8f;yield return null;
            }
            // Remain presentation-owned until the hand refresh destroys this used card.
        }
        public void OnPointerClick(PointerEventData data)
        {if(data.button==PointerEventData.InputButton.Left&&CanPlay&&!presenting)selected?.Invoke(this);}
        private void Update()
        {
            if(visual==null||presenting)return;
            float zoom=Hovered?(battleHand?HandPresentation.HoverScale:1.06f):1;
            var offset=Hovered&&battleHand?new Vector2(0,HandPresentation.HoverLift):Vector2.zero;
            // Clamp the animated artwork to its own Canvas, including narrow game views.
            if(Hovered&&canvasSurface!=null)
            {
                var surface=canvasSurface;
                ((RectTransform)transform).GetWorldCorners(cardCorners);
                var low=surface.InverseTransformPoint(cardCorners[0]);var high=surface.InverseTransformPoint(cardCorners[2]);
                float left=surface.rect.xMin+12,right=surface.rect.xMax-12;
                handOwner?.HoverBounds(surface,out left,out right);
                var center=(low+high)*.5f+surface.InverseTransformVector(visual.parent.TransformVector(offset));
                var half=(high-low)*(.5f*zoom);
                float shiftX=Mathf.Max(0,left-(center.x-half.x))-Mathf.Max(0,center.x+half.x-right);
                float top=handOwner!=null?surface.rect.yMin+surface.rect.height*HandPresentation.HoverTop:surface.rect.yMax-12;
                float shiftY=Mathf.Max(0,surface.rect.yMin+12-(center.y-half.y))-Mathf.Max(0,center.y+half.y-top);
                var correction=visual.parent.InverseTransformVector(surface.TransformVector(new Vector3(shiftX,shiftY,0)));
                offset+=new Vector2(correction.x,correction.y);
            }
            float blend=1-Mathf.Exp(-HandPresentation.LayoutSpeed*Time.unscaledDeltaTime);
            visual.anchoredPosition=Vector2.Lerp(visual.anchoredPosition,offset,blend);
            visual.localScale=Vector3.Lerp(visual.localScale,Vector3.one*zoom,blend);
            if(hoverInput!=null&&Hovered)
            {
                hoverInput.rectTransform.anchoredPosition=visual.anchoredPosition;
                hoverInput.rectTransform.sizeDelta=Vector2.Scale(visual.rect.size,new Vector2(visual.localScale.x,visual.localScale.y));
            }
        }
        public static CardView Create(Transform parent,PresentationAssets art)
        {
            var rect=BattleUi.Rect(parent,"CardView",new Vector2(.5f,.5f),new Vector2(.5f,.5f));rect.sizeDelta=new Vector2(220,310);
            var hit=rect.gameObject.AddComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;
            var view=rect.gameObject.AddComponent<CardView>();
            view.visual=BattleUi.Rect(rect,"Card visual",Vector2.zero,Vector2.one);
            view.ink=view.visual.gameObject.AddComponent<CanvasGroup>();view.ink.blocksRaycasts=false;
            view.frame=BattleUi.Image(view.visual,"Frame",null,Color.white,Vector2.zero,Vector2.one);
            // Deliberately generic existing icons: these are placeholders, not invented card artwork.
            view.artwork=BattleUi.Image(view.visual,"Artwork",null,Color.white,new Vector2(.30f,.49f),new Vector2(.70f,.69f));view.artwork.preserveAspect=true;
            view.title=BattleUi.Text(view.visual,"Name","",art.font,24,BattleUi.Gold,new Vector2(.15f,.80f),new Vector2(.94f,.97f));
            view.cost=BattleUi.Text(view.visual,"Cost","",art.font,32,BattleUi.Gold,new Vector2(.02f,.80f),new Vector2(.19f,.99f));
            view.element=BattleUi.Text(view.visual,"Element","",art.font,20,BattleUi.Teal,new Vector2(.10f,.69f),new Vector2(.55f,.81f));
            view.kind=BattleUi.Text(view.visual,"Type","",art.font,19,BattleUi.Pale,new Vector2(.55f,.69f),new Vector2(.90f,.81f));
            view.effect=BattleUi.Text(view.visual,"Effect","",art.font,20,BattleUi.Gold,new Vector2(.05f,.37f),new Vector2(.95f,.49f));
            view.description=BattleUi.Text(view.visual,"Description","",art.font,18,BattleUi.Pale,new Vector2(.07f,.04f),new Vector2(.93f,.37f));
            view.description.resizeTextForBestFit=true;view.description.resizeTextMinSize=14;view.description.resizeTextMaxSize=18;
            return view;
        }
    }
}

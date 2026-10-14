using System;
using System.Linq;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Brodun.Presentation
{
    public sealed class CardView : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerClickHandler
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
        public bool IsPresenting => presenting;
        public void Bind(CardDefinition card,string id,BattlePresentationAssets art,GameDefinitions data,Action<CardView> click)
        {
            Definition=card;InstanceId=id;selected=click;restingIndex=transform.GetSiblingIndex();
            title.text=card.Name;cost.text=card.Cost.ToString();element.text=data.Elements.Single(e=>e.Id==card.Element).Name;
            kind.text=card.Type==CardType.Attack?"공격":card.Type==CardType.Guard?"방어":"기술";
            effect.text=(card.BaseDamage>0?"피해 "+card.BaseDamage+"   ":"")+(card.Block>0?"방어 "+card.Block:"");
            description.text=card.Description;
            title.fontSize=25;description.fontSize=20;description.resizeTextMaxSize=20;description.resizeTextMinSize=17;
            // Re-style the shared prefab at bind time; existing prefab references remain intact.
            var hue=card.Type==CardType.Attack?new Color(.96f,.69f,.36f):card.Type==CardType.Guard?BattleUi.Teal:new Color(.64f,.73f,1);
            element.color=hue;kind.color=hue;cost.color=Color.white;
            title.rectTransform.anchorMin=new Vector2(.20f,.80f);
            effect.fontSize=22;
            frame.sprite=card.Type==CardType.Attack?art.attackFrame:card.Type==CardType.Guard?art.guardFrame:art.skillFrame;
            artwork.sprite=card.Type==CardType.Guard?art.common.altar:art.common.battle;
            CanPlay=true;SetAvailable(true);
        }
        public void SetAvailable(bool value){CanPlay=value;ink.alpha=value?1:.55f;}
        public void OnPointerEnter(PointerEventData data)
        {if(Hovered||presenting)return;Hovered=true;restingIndex=transform.GetSiblingIndex();transform.SetAsLastSibling();}
        public void OnPointerExit(PointerEventData data)
        {Hovered=false;transform.SetSiblingIndex(restingIndex);}
        public IEnumerator PresentUse(bool exhaust)
        {
            presenting=true;Hovered=false;transform.SetAsLastSibling();
            var origin=visual.anchoredPosition;
            for(float t=0;t<.24f;t+=Time.unscaledDeltaTime)
            {
                float u=t/.24f;
                visual.anchoredPosition=Vector2.Lerp(origin,new Vector2(exhaust?0:95,100),u);
                visual.localScale=Vector3.one*Mathf.Lerp(1.10f,.83f,u);
                ink.alpha=1-u*.8f;yield return null;
            }
            // Remain presentation-owned until the hand refresh destroys this used card.
        }
        public void OnPointerClick(PointerEventData data)
        {if(data.button==PointerEventData.InputButton.Left)selected?.Invoke(this);}
        private void Update()
        {
            if(visual==null||presenting)return;
            visual.anchoredPosition=Vector2.Lerp(visual.anchoredPosition,Hovered?new Vector2(0,42):Vector2.zero,Time.unscaledDeltaTime*15);
            visual.localScale=Vector3.Lerp(visual.localScale,Vector3.one*(Hovered?1.10f:1),Time.unscaledDeltaTime*15);
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

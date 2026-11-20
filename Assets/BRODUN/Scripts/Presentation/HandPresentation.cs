using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace Brodun.Presentation
{
    // Presentation only. Model order is never derived from draw/sibling order.
    public sealed class HandPresentation
    {
        public const float CardWidth=260,CardHeight=360;
        public const float NaturalSpacing=274,OverflowSpacing=148;
        public const float HoverScale=1.14f,HoverLift=30,LayoutSpeed=18;
        // Normal cards top out below .36; enlarged inspection stays below the .44 floor.
        public const float HoverTop=.41f;
        public const int FullyVisibleCount=8;
        private readonly IReadOnlyList<CardView> cards;
        private readonly RectTransform hand,viewport,foreground;
        private readonly Scrollbar scrollbar;
        private readonly Vector2[] positions;
        private readonly Vector3[] corners=new Vector3[4];
        private readonly List<CardView> returning=new List<CardView>();
        private CardView hovered;
        private float width,spacing,overflow;
        private bool inputEnabled=true,visible=true,disposed;
        public float ScrollPosition {get;private set;}
        public HandPresentation(Transform root,RectTransform handRoot,PresentationAssets art,IReadOnlyList<CardView> entries,float scrollPosition=0)
        {
            cards=entries;hand=handRoot;ScrollPosition=Mathf.Clamp01(scrollPosition);
            viewport=BattleUi.Rect(hand,"Hand viewport",Vector2.zero,Vector2.one);
            viewport.gameObject.AddComponent<RectMask2D>();
            // Only the inspected/used card leaves the mask. HUDs stay outside hand bounds.
            foreground=BattleUi.Rect(root,"Hand foreground",Vector2.zero,Vector2.one);
            var track=BattleUi.Image(root,"Hand scroll",null,new Color(.06f,.09f,.10f,.94f),new Vector2(.205f,.002f),new Vector2(.795f,.022f));
            track.raycastTarget=true;
            scrollbar=track.gameObject.AddComponent<Scrollbar>();
            var slide=BattleUi.Rect(track.transform,"Scroll area",Vector2.zero,Vector2.one);
            slide.offsetMin=new Vector2(4,3);slide.offsetMax=new Vector2(-4,-3);
            var thumb=BattleUi.Image(slide,"Scroll thumb",null,new Color(.63f,.57f,.39f),Vector2.zero,Vector2.one);
            thumb.raycastTarget=true;scrollbar.handleRect=thumb.rectTransform;scrollbar.targetGraphic=thumb;
            scrollbar.direction=Scrollbar.Direction.LeftToRight;
            scrollbar.onValueChanged.AddListener(value=>{ScrollPosition=value;ClearHover();});
            positions=new Vector2[cards.Count];CalculateLayout();
            for(int i=0;i<cards.Count;i++)
            {
                var card=cards[i];var rect=(RectTransform)card.transform;
                Vector2 previous=rect.anchoredPosition;
                rect.SetParent(viewport,false);rect.localScale=Vector3.one;
                rect.sizeDelta=new Vector2(CardWidth,CardHeight);
                positions[i]=card.HasHandPosition?previous:Destination(i)+Vector2.down*55;
                rect.anchoredPosition=positions[i];
                card.AttachHand(this);card.gameObject.SetActive(true);
            }
        }
        private void CalculateLayout()
        {
            width=Mathf.Max(CardWidth+24,hand.rect.width);
            spacing=cards.Count<=1?0:cards.Count<=FullyVisibleCount?
                Mathf.Min(NaturalSpacing,(width-CardWidth-16)/(cards.Count-1)):OverflowSpacing;
            overflow=Mathf.Max(0,CardWidth+Mathf.Max(0,cards.Count-1)*spacing+16-width);
            if(overflow<=0)ScrollPosition=0;
            scrollbar.gameObject.SetActive(visible&&overflow>0);
            scrollbar.size=Mathf.Clamp01(width/(width+overflow));
            scrollbar.SetValueWithoutNotify(ScrollPosition);
            scrollbar.interactable=inputEnabled&&overflow>0;
            foreach(var card in cards)card.SetHandExposedWidth(spacing>0?Mathf.Min(CardWidth,spacing):CardWidth);
        }
        private Vector2 Destination(int index)
        {
            float x=(index-(cards.Count-1)*.5f)*spacing;
            if(overflow>0)x+=overflow*.5f-ScrollPosition*overflow;
            // Nearly flat arch; no rotation to compromise text or hit testing.
            float arc=cards.Count<=1?0:7*(1-Mathf.Abs(index-(cards.Count-1)*.5f)/Mathf.Max(1,(cards.Count-1)*.5f));
            return new Vector2(x,viewport.rect.yMin+CardHeight*.5f+14+arc);
        }
        public void Tick(float deltaTime)
        {
            if(disposed)return;
            if(Mathf.Abs(width-Mathf.Max(CardWidth+24,hand.rect.width))>.5f)CalculateLayout();
            float blend=1-Mathf.Exp(-LayoutSpeed*deltaTime);
            for(int i=0;i<cards.Count;i++)
            {
                var card=cards[i];if(card==null||card.IsPresenting)continue;
                positions[i]=Vector2.Lerp(positions[i],Destination(i),blend);
                var rect=(RectTransform)card.transform;
                if(card==hovered||returning.Contains(card))rect.position=viewport.TransformPoint(positions[i]);
                else rect.anchoredPosition=positions[i];
            }
            for(int i=returning.Count-1;i>=0;i--)
            {
                var card=returning[i];
                if(card==null){returning.RemoveAt(i);continue;}
                if(card.visual.anchoredPosition.sqrMagnitude>.5f||Mathf.Abs(card.visual.localScale.x-1)>.005f)continue;
                returning.RemoveAt(i);ReturnToViewport(card);
            }
        }
        public CardView PointerOwner(Vector2 screen,Camera camera)
        {
            if(disposed||!visible||!inputEnabled)return null;
            if(RectTransformUtility.RectangleContainsScreenPoint(viewport,screen,camera)&&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport,screen,camera,out var point))
            {
                // Match the visible left-to-right overlap, not nearest centers. A cost
                // badge in the exposed strip must select the card it is drawn on.
                for(int i=cards.Count-1;i>=0;i--)
                {
                    var card=cards[i];if(card==null||!card.gameObject.activeInHierarchy||card.IsPresenting)continue;
                    var delta=point-positions[i];
                    if(Mathf.Abs(delta.x)>CardWidth*.5f||Mathf.Abs(delta.y)>CardHeight*.5f)continue;
                    return card;
                }
            }
            // Raised portion is clickable, but never steals another card's base slot.
            return hovered!=null&&RectTransformUtility.RectangleContainsScreenPoint(hovered.visual,screen,camera)?hovered:null;
        }
        public void Hover(CardView card)
        {
            if(disposed||!visible||!inputEnabled||card.IsPresenting||hovered==card)return;
            ClearHover();returning.Remove(card);hovered=card;card.SetHandHover(true);MoveToForeground(card);
        }
        public void Leave(CardView card){if(hovered==card)ClearHover();}
        private void ClearHover()
        {
            if(hovered==null)return;
            var card=hovered;hovered=null;card.SetHandHover(false);
            if(card==null||card.IsPresenting)return;
            if(!returning.Contains(card))returning.Add(card);
        }
        private void ReturnToViewport(CardView card)
        {
            var rect=(RectTransform)card.transform;rect.SetParent(viewport,true);
            for(int i=0;i<cards.Count;i++)
            {
                if(cards[i]==card)rect.anchoredPosition=positions[i];
                // Several cards may be returning at once; indices alone become ambiguous.
                if(cards[i]!=null&&cards[i].transform.parent==viewport)cards[i].transform.SetAsLastSibling();
            }
        }
        private void MoveToForeground(CardView card)
        {
            var rect=(RectTransform)card.transform;rect.SetParent(foreground,true);rect.SetAsLastSibling();
            for(int i=0;i<cards.Count;i++)if(cards[i]==card){rect.position=viewport.TransformPoint(positions[i]);break;}
        }
        public void BeginUse(CardView card)
        {returning.Remove(card);if(hovered==card){hovered=null;card.SetHandHover(false);}MoveToForeground(card);}
        public void Scroll(float wheel)
        {if(disposed||!inputEnabled||overflow<=0)return;scrollbar.value=Mathf.Clamp01(ScrollPosition-wheel*.13f);}
        public void ScrollToCard(int index)
        {
            if(cards.Count<=1||overflow<=0)return;
            float x=(Mathf.Clamp(index,0,cards.Count-1)-(cards.Count-1)*.5f)*spacing;
            scrollbar.value=Mathf.Clamp01((x+overflow*.5f)/overflow);
        }
        // Compatibility for old capture scripts only: scroll, never hide cards/pages.
        public void ShowPage(int index){ScrollToCard(index*5);}
        public void HoverBounds(RectTransform surface,out float left,out float right)
        {
            viewport.GetWorldCorners(corners);
            left=surface.InverseTransformPoint(corners[0]).x+6;
            right=surface.InverseTransformPoint(corners[2]).x-6;
        }
        public void SetInput(bool enabled)
        {inputEnabled=enabled;scrollbar.interactable=enabled&&overflow>0;if(!enabled)ClearHover();}
        public void SetVisible(bool show)
        {visible=show;if(!show)ClearHover();viewport.gameObject.SetActive(show);foreground.gameObject.SetActive(show);scrollbar.gameObject.SetActive(show&&overflow>0);}
        public void Dispose()
        {
            if(disposed)return;ClearHover();disposed=true;
            foreach(var card in cards)
            {
                if(card==null)continue;
                card.transform.SetParent(hand,true);card.DetachHand(this);
            }
            UnityEngine.Object.Destroy(viewport.gameObject);
            UnityEngine.Object.Destroy(foreground.gameObject);
            UnityEngine.Object.Destroy(scrollbar.gameObject);
        }
    }
}

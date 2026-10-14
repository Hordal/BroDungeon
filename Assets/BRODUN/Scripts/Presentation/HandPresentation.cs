using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace Brodun.Presentation
{
    // Stable hit roots, visual-only hover. Six cards per page prevents unreadable giant hands.
    public sealed class HandPresentation
    {
        public const int PageSize=6;
        public int Page { get; private set; }
        private readonly IReadOnlyList<CardView> cards;
        private readonly StyledButton previous,next;
        private readonly Text indicator;
        public HandPresentation(Transform root,PresentationAssets art,IReadOnlyList<CardView> entries)
        {
            cards=entries;
            previous=BattleUi.Button(root,"HandPrevious","◀",art,new Vector2(.23f,.005f),new Vector2(.27f,.045f),()=>ShowPage(Page-1));
            next=BattleUi.Button(root,"HandNext","▶",art,new Vector2(.74f,.005f),new Vector2(.78f,.045f),()=>ShowPage(Page+1));
            indicator=BattleUi.Text(root,"Hand pages","",art.font,19,BattleUi.Pale,new Vector2(.43f,.005f),new Vector2(.59f,.035f));
            ShowPage(0);
        }
        public void Dispose()
        {UnityEngine.Object.Destroy(previous.gameObject);UnityEngine.Object.Destroy(next.gameObject);UnityEngine.Object.Destroy(indicator.gameObject);}
        public void ShowPage(int page)
        {
            int pages=Math.Max(1,(cards.Count+PageSize-1)/PageSize);Page=Mathf.Clamp(page,0,pages-1);
            int count=Math.Min(PageSize,cards.Count-Page*PageSize);
            float scale=count==6?.82f:1,spacing=count==6?188:count==5?220:235;
            for(int i=0;i<cards.Count;i++)
            {
                int slot=i-Page*PageSize;bool visible=slot>=0&&slot<count;
                cards[i].OnPointerExit(null);cards[i].gameObject.SetActive(visible);
                var rect=(RectTransform)cards[i].transform;rect.localScale=Vector3.one*scale;
                rect.anchoredPosition=new Vector2((slot-(count-1)*.5f)*spacing,0);
            }
            previous.gameObject.SetActive(pages>1);next.gameObject.SetActive(pages>1);
            previous.interactable=Page>0;next.interactable=Page+1<pages;
            indicator.text=pages>1?(Page+1)+" / "+pages:"";
        }
        public void SetInput(bool enabled)
        {
            previous.interactable=enabled&&Page>0;
            next.interactable=enabled&&(Page+1)*PageSize<cards.Count;
        }
    }
}

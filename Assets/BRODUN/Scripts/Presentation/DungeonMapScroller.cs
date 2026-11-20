using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Brodun.Presentation
{
    public sealed class DungeonMapScroller : ScrollRect
    {
        private Coroutine focusing;
        public bool IsAutoFocusing => focusing!=null;
        public void FocusLayer(int layer,bool smooth)
        {
            Canvas.ForceUpdateCanvases();StopFocus();StopMovement();
            float overflow=Mathf.Max(0,content.rect.width-viewport.rect.width);
            float destination=overflow<=0?0:Mathf.Clamp01(((float)DungeonMapLayout.X(layer)-viewport.rect.width*.40f)/overflow);
            if(smooth)focusing=StartCoroutine(MoveTo(destination));else horizontalNormalizedPosition=destination;
        }
        private IEnumerator MoveTo(float destination)
        {
            float start=horizontalNormalizedPosition;
            for(float t=0;t<.28f;t+=Time.unscaledDeltaTime)
            {horizontalNormalizedPosition=Mathf.Lerp(start,destination,Mathf.SmoothStep(0,1,t/.28f));yield return null;}
            horizontalNormalizedPosition=destination;focusing=null;
        }
        private void StopFocus(){if(focusing!=null)StopCoroutine(focusing);focusing=null;}
        public override void OnBeginDrag(PointerEventData eventData){StopFocus();base.OnBeginDrag(eventData);}
        public override void OnScroll(PointerEventData data)
        {
            StopFocus();
            if(!IsActive())return;
            // Mouse wheel also pans horizontally; no gameplay RNG or player movement involved.
            float delta=Mathf.Abs(data.scrollDelta.x)>.001f?data.scrollDelta.x:data.scrollDelta.y;
            float overflow=Mathf.Max(1,content.rect.width-viewport.rect.width);
            horizontalNormalizedPosition=Mathf.Clamp01(horizontalNormalizedPosition-delta*scrollSensitivity/overflow);
        }
    }
}

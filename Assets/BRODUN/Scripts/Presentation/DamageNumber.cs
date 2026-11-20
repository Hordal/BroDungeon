using System.Collections;
using UnityEngine;
using UnityEngine.UI;
namespace Brodun.Presentation
{
    public sealed class DamageNumber:MonoBehaviour
    {
        public IEnumerator Present(Text label,Color tint)
        {
            var outline=label.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.01f,.02f,.03f,1);outline.effectDistance=new Vector2(2,-2);
            var rect=label.rectTransform;
            for(float t=0;t<.65f;t+=Time.unscaledDeltaTime)
            {
                float u=t/.65f;rect.anchoredPosition=new Vector2(0,65*u);
                rect.localScale=Vector3.one*(1+Mathf.Sin(Mathf.Min(1,u*4)*Mathf.PI)*.16f);
                label.color=new Color(tint.r,tint.g,tint.b,Mathf.Min(1,(1-u)*3));yield return null;
            }
            Destroy(gameObject);
        }
    }
}

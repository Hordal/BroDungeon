using System.Collections;
using UnityEngine;
using UnityEngine.UI;
namespace Brodun.Presentation
{
    public sealed class DamageNumber:MonoBehaviour
    {
        public enum Kind { Damage, BlockGain, BlockLoss, Heal, Strength }
        private Vector2 origin;
        private bool positioned;
        public void Place(Vector2 position)
        {
            var rect=(RectTransform)transform;
            rect.anchoredPosition=positioned?rect.anchoredPosition+position-origin:position;
            origin=position;positioned=true;
        }
        public static Color Tint(Kind kind)=>kind==Kind.Damage?new Color(1,.36f,.32f):kind==Kind.BlockGain?new Color(.49f,.85f,1):kind==Kind.BlockLoss?new Color(.89f,.76f,.55f):kind==Kind.Heal?new Color(.45f,1,.61f):new Color(.93f,.60f,1);
        public static string Caption(Kind kind,int amount)=>kind==Kind.Damage?"피해 "+amount:kind==Kind.BlockGain?"방어 +"+amount:kind==Kind.BlockLoss?"방어 −"+amount:kind==Kind.Heal?"회복 +"+amount:"힘 +"+amount;
        public static BattleHudGlyph.Symbol Symbol(Kind kind)=>kind==Kind.BlockGain?BattleHudGlyph.Symbol.Shield:kind==Kind.BlockLoss?BattleHudGlyph.Symbol.BrokenShield:kind==Kind.Heal?BattleHudGlyph.Symbol.Heal:kind==Kind.Strength?BattleHudGlyph.Symbol.Power:BattleHudGlyph.Symbol.Sword;
        public IEnumerator Present(Text label,Color tint)
        {
            BattleUi.Readable(label);
            var rect=label.rectTransform;
            if(!positioned)origin=rect.anchoredPosition;
            var group=gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=false;group.interactable=false;
            var icon=GetComponentInChildren<BattleHudGlyph>();
            label.color=tint;
            for(float t=0;t<.78f;t+=Time.unscaledDeltaTime)
            {
                float u=t/.78f;
                // Keep the target/lane origin. Resetting X/Y to zero sent every result
                // back to Canvas center, erasing both target identity and stack spacing.
                rect.anchoredPosition=origin+Vector2.up*(22*Mathf.SmoothStep(0,1,Mathf.Min(1,t/.22f))+t*12);
                rect.localScale=Vector3.one*(1+Mathf.Sin(Mathf.Min(1,u*4)*Mathf.PI)*.16f);
                if(icon!=null&&icon.Kind==BattleHudGlyph.Symbol.BrokenShield)icon.Fracture=Mathf.SmoothStep(0,1,Mathf.Min(1,t/.20f));
                group.alpha=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.48f,.78f,t));yield return null;
            }
            Destroy(gameObject);
        }
    }
}

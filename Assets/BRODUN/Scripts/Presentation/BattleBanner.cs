using System.Collections;
using UnityEngine;
using UnityEngine.UI;
namespace Brodun.Presentation
{
    // One transient banner; a new announcement cancels only the old visual, never game state.
    public sealed class BattleBanner:MonoBehaviour
    {
        private CanvasGroup group;
        private Text label;
        private Coroutine bannerRoutine;
        public void Build(Transform parent,PresentationAssets art)
        {
            var panel=BattleUi.Surface(parent,"Turn banner",new Vector2(.39f,.85f),new Vector2(.61f,.90f),BattleChrome.Form.Command,BattleUi.Bronze);
            group=panel.gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=false;group.alpha=0;
            label=BattleUi.Text(panel.transform,"Announcement","",art.font,25,BattleUi.Gold,Vector2.zero,Vector2.one);
        }
        public void Show(string text)
        {if(bannerRoutine!=null)StopCoroutine(bannerRoutine);label.text=text;bannerRoutine=StartCoroutine(Animate());}
        private IEnumerator Animate()
        {
            for(float t=0;t<.75f;t+=Time.unscaledDeltaTime)
            {group.alpha=t<.12f?t/.12f:t>.55f?1-(t-.55f)/.20f:1;yield return null;}
            group.alpha=0;bannerRoutine=null;
        }
    }
}

using UnityEngine;
using UnityEngine.EventSystems;
namespace Brodun.Presentation
{
    public sealed class IntentHover : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler
    {
        public GameObject tooltip;
        public void OnPointerEnter(PointerEventData data){if(tooltip!=null)tooltip.SetActive(true);}
        public void OnPointerExit(PointerEventData data){if(tooltip!=null)tooltip.SetActive(false);}
        private void OnDisable(){if(tooltip!=null)tooltip.SetActive(false);}
    }
}

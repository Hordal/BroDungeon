using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Brodun.Presentation
{
    public sealed class StyledButton : Button, ICancelHandler
    {
        public Sprite normal,hover,pressed,disabled;
        public bool Highlighted { get; set; }
        public bool Destructive { get; set; }
        private Vector3 target=Vector3.one;
        public void Configure(Sprite normalSprite,Sprite hoverSprite,Sprite pressedSprite,Sprite disabledSprite)
        {
            normal=normalSprite;hover=hoverSprite;pressed=pressedSprite;disabled=disabledSprite;
            transition=Transition.None;
            RefreshStyle();
        }
        public void RefreshStyle(){DoStateTransition(currentSelectionState,true);}
        public void OnCancel(BaseEventData data)
        {
            var view=GetComponentInParent<GameScreenView>();
            if(view!=null){view.Back();data.Use();}
        }
        protected override void DoStateTransition(SelectionState state,bool instant)
        {
            if(targetGraphic==null)return;
            var image=targetGraphic as Image;
            var sprite=state==SelectionState.Disabled?disabled:state==SelectionState.Pressed?pressed:
                state==SelectionState.Highlighted||state==SelectionState.Selected?hover:normal;
            // AddComponent/OnEnable can run before Configure: never erase the Image's valid sprite.
            if(image!=null && (sprite!=null || normal!=null)){image.overrideSprite=null;image.sprite=sprite!=null?sprite:normal;}
            target=Vector3.one*(state==SelectionState.Pressed?.98f:state==SelectionState.Highlighted||state==SelectionState.Selected?1.025f:1);
            bool emphasis=state==SelectionState.Highlighted||state==SelectionState.Selected;
            targetGraphic.color=state==SelectionState.Disabled?new Color(.42f,.43f,.44f,1):
                Destructive?(emphasis?new Color(.90f,.67f,.49f,1):new Color(.76f,.54f,.43f,1)):
                emphasis?new Color(.96f,.91f,.78f,1):new Color(.90f,.90f,.86f,1);
            if(instant)transform.localScale=target;
        }
        private void Update()
        {
            var scale=target;
            if(Highlighted && IsInteractable() && target==Vector3.one)scale*=1+Mathf.Sin(Time.unscaledTime*2.8f)*.012f;
            transform.localScale=Vector3.Lerp(transform.localScale,scale,Time.unscaledDeltaTime*14);
        }
    }
}

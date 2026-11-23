using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace Brodun.Presentation
{
    public sealed class StyledButton : Button, ICancelHandler
    {
        public Sprite normal,hover,pressed,disabled;
        public bool Highlighted { get; set; }
        public bool Destructive { get; set; }
        private Vector3 target=Vector3.one;
        private bool quietSelection,pointerInside;

        public void FocusQuietly()
        {
            quietSelection=true;
            EventSystem.current.SetSelectedGameObject(gameObject);
        }
        public override void OnDeselect(BaseEventData data)
        {
            quietSelection=false;
            base.OnDeselect(data);
        }
        public override void OnMove(AxisEventData data)
        {
            if(quietSelection){quietSelection=false;RefreshStyle();}
            base.OnMove(data);
        }
        public override void OnPointerEnter(PointerEventData data)
        {
            pointerInside=true;
            base.OnPointerEnter(data);
        }
        public override void OnPointerExit(PointerEventData data)
        {
            pointerInside=false;
            base.OnPointerExit(data);
        }
        public void Configure(Sprite normalSprite,Sprite hoverSprite,Sprite pressedSprite,Sprite disabledSprite)
        {
            normal=normalSprite;hover=hoverSprite;pressed=pressedSprite;disabled=disabledSprite;
            transition=Transition.None;
            RefreshStyle();
        }
        public void RefreshStyle(){DoStateTransition(currentSelectionState,true);}
        public void OnCancel(BaseEventData data)
        {
            // GameFlow handles the keyboard Escape route once, even if no button has focus.
            if(GameFlow.Instance!=null&&Keyboard.current?.escapeKey.wasPressedThisFrame==true){data.Use();return;}
            var view=GetComponentInParent<GameScreenView>();
            if(view!=null){view.Back();data.Use();}
        }
        protected override void DoStateTransition(SelectionState state,bool instant)
        {
            if(targetGraphic==null)return;
            var image=targetGraphic as Image;
            bool emphasis=state==SelectionState.Highlighted||
                (state==SelectionState.Selected&&(!quietSelection||pointerInside));
            var sprite=state==SelectionState.Disabled?disabled:state==SelectionState.Pressed?pressed:emphasis?hover:normal;
            // AddComponent/OnEnable can run before Configure: never erase the Image's valid sprite.
            if(image!=null && (sprite!=null || normal!=null)){image.overrideSprite=null;image.sprite=sprite!=null?sprite:normal;}
            target=Vector3.one*(state==SelectionState.Pressed?.98f:emphasis?1.025f:1);
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

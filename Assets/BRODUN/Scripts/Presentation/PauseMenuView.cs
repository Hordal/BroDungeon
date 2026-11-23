using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Brodun.Presentation
{
    internal sealed class PauseMenuView : MonoBehaviour
    {
        private GameFlow flow;
        private RectTransform root;
        private RectTransform panel;
        private CanvasGroup underlyingInput;
        private bool priorInteractable,released;
        private GameObject priorSelection;
        private bool confirming;

        public void Build(GameFlow owner,RectTransform underlying)
        {
            flow=owner;
            priorSelection=EventSystem.current?.currentSelectedGameObject;
            if(!underlying.TryGetComponent(out underlyingInput))underlyingInput=underlying.gameObject.AddComponent<CanvasGroup>();
            priorInteractable=underlyingInput.interactable;
            underlyingInput.interactable=false;
            var canvas=gameObject.AddComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=880;
            var scaler=gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            root=(RectTransform)transform;
            BattleUi.Image(root,"Pause input blocker",null,new Color(.006f,.012f,.021f,.74f),
                Vector2.zero,Vector2.one).raycastTarget=true;
            Show(false);
        }

        private void Show(bool confirm)
        {
            confirming=confirm;
            if(panel!=null){panel.gameObject.SetActive(false);Destroy(panel.gameObject);}
            panel=BattleUi.Surface(root,"Pause panel",new Vector2(.34f,.30f),new Vector2(.66f,.74f),
                BattleChrome.Form.Hud,BattleUi.Bronze);
            var art=flow.Assets;
            BattleUi.Text(panel,"Pause title",confirm?"여정을 종료할까요?":"게임 메뉴",art.font,42,
                BattleUi.Gold,new Vector2(.08f,.75f),new Vector2(.92f,.94f));
            StyledButton first;
            if(confirm)
            {
                BattleUi.Text(panel,"Abandon explanation","타이틀로 돌아가면 현재 여정의 진행 상황이 사라집니다.",
                    art.font,25,BattleUi.Pale,new Vector2(.10f,.43f),new Vector2(.90f,.69f));
                first=Button("PauseCancel","취소",.25f,()=>Show(false));
                var abandon=Button("PauseConfirmTitle","여정 종료",.075f,()=>flow.ReturnToTitle());
                abandon.Destructive=true;abandon.RefreshStyle();
            }
            else
            {
                first=Button("PauseResume","계속하기",.54f,flow.ClosePauseMenu);
                Button("PauseAudio","음량 설정",.33f,()=>GameAudio.Ensure().OpenSettings(art,root));
                Button("PauseTitle","타이틀로 돌아가기",.12f,()=>Show(true));
            }
            if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        private StyledButton Button(string key,string label,float bottom,System.Action click) =>
            BattleUi.CommandButton(panel,key,label,flow.Assets,new Vector2(.14f,bottom),new Vector2(.86f,bottom+.14f),
                click,BattleChrome.Form.Action);

        public bool DismissConfirmation()
        {
            if(!confirming)return false;
            Show(false);
            return true;
        }

        public void ReleaseInput()
        {
            if(released)return;
            released=true;
            if(underlyingInput!=null)underlyingInput.interactable=priorInteractable;
            if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(
                priorSelection!=null&&priorSelection.activeInHierarchy?priorSelection:null);
            gameObject.SetActive(false);
        }

        private void OnDestroy()=>ReleaseInput();
    }
}

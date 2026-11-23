using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Brodun.Presentation
{
    // Small audio-only modal; lives on its own Canvas, outside the blocked scene UI.
    public sealed class AudioSettingsView : MonoBehaviour
    {
        private GameAudio owner;
        private readonly Slider[] sliders=new Slider[3];
        private readonly Text[] values=new Text[3],muteLabels=new Text[3];
        private CanvasGroup sceneInput;
        private bool priorInteractable,released;
        private GameObject priorSelection;
        public void Build(GameAudio audio,PresentationAssets art,RectTransform underlying)
        {
            owner=audio;
            if(underlying!=null)
            {
                if(!underlying.TryGetComponent(out sceneInput))sceneInput=underlying.gameObject.AddComponent<CanvasGroup>();
                priorInteractable=sceneInput.interactable;sceneInput.interactable=false;
            }
            priorSelection=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
            var canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=900;
            var scaler=gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            var root=(RectTransform)transform;
            var veil=BattleUi.Image(root,"Audio input blocker",null,new Color(.005f,.012f,.02f,.38f),Vector2.zero,Vector2.one);veil.raycastTarget=true;
            // The opaque panel stays between the upper vitals and the lower hand/HUD.
            var panel=BattleUi.Surface(root,"Audio panel",new Vector2(.30f,.34f),new Vector2(.70f,.81f),BattleChrome.Form.Hud,BattleUi.Bronze);
            panel.GetComponent<BattleChrome>().raycastTarget=true;
            BattleUi.Text(panel,"Audio title","오디오 설정",art.font,30,BattleUi.Gold,new Vector2(.08f,.84f),new Vector2(.92f,.98f));
            Row(panel,art,AudioChannel.Master,"전체 음량",.65f);
            Row(panel,art,AudioChannel.Music,"배경음악",.44f);
            Row(panel,art,AudioChannel.Sfx,"효과음",.23f);
            var close=BattleUi.CommandButton(panel,"Close audio settings","닫기",art,new Vector2(.36f,.035f),new Vector2(.64f,.125f),owner.CloseSettings);
            close.GetComponentInChildren<Text>().verticalOverflow=VerticalWrapMode.Overflow;
            owner.SettingsChanged+=Refresh;Refresh();
            if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(close.gameObject);
        }
        private void Row(RectTransform panel,PresentationAssets art,AudioChannel channel,string caption,float y)
        {
            int index=(int)channel;
            var channelLabel=BattleUi.Text(panel,caption,caption,art.font,29,BattleUi.Pale,new Vector2(.08f,y+.042f),new Vector2(.44f,y+.135f));
            channelLabel.alignment=TextAnchor.MiddleLeft;
            channelLabel.verticalOverflow=VerticalWrapMode.Overflow;
            values[index]=BattleUi.Text(panel,caption+" value","",art.font,25,BattleUi.Teal,new Vector2(.48f,y+.042f),new Vector2(.66f,y+.135f));
            values[index].alignment=TextAnchor.MiddleRight;
            values[index].verticalOverflow=VerticalWrapMode.Overflow;
            var mute=BattleUi.CommandButton(panel,caption+" mute","",art,new Vector2(.73f,y+.015f),new Vector2(.93f,y+.135f),()=>owner.SetMuted(channel,!owner.Muted(channel)));
            muteLabels[index]=mute.GetComponentInChildren<Text>();muteLabels[index].fontSize=21;
            var track=BattleUi.Image(panel,caption+" slider",null,new Color(.05f,.085f,.11f),new Vector2(.08f,y-.035f),new Vector2(.66f,y+.025f));track.raycastTarget=true;
            var slider=track.gameObject.AddComponent<Slider>();slider.minValue=0;slider.maxValue=1;slider.wholeNumbers=false;slider.direction=Slider.Direction.LeftToRight;
            var groove=BattleUi.Image(track.transform,"Groove",null,new Color(.13f,.20f,.24f),new Vector2(.035f,.36f),new Vector2(.965f,.64f));
            var fill=BattleUi.Image(groove.transform,"Level",null,BattleUi.Teal,Vector2.zero,Vector2.one);
            var handles=BattleUi.Rect(track.transform,"Handle area",new Vector2(.035f,0),new Vector2(.965f,1));
            var handle=BattleUi.Image(handles,"Handle",null,BattleUi.Gold,new Vector2(0,.10f),new Vector2(0,.90f));
            handle.rectTransform.sizeDelta=new Vector2(16,0);
            slider.fillRect=fill.rectTransform;slider.handleRect=handle.rectTransform;slider.targetGraphic=handle;
            slider.onValueChanged.AddListener(value=>owner.SetVolume(channel,value));sliders[index]=slider;
        }
        private void Refresh()
        {
            for(int i=0;i<sliders.Length;i++)
            {
                var channel=(AudioChannel)i;sliders[i].SetValueWithoutNotify(owner.Volume(channel));
                values[i].text=Mathf.RoundToInt(owner.Volume(channel)*100)+"%";
                muteLabels[i].text=owner.Muted(channel)?"해제":"음소거";
                muteLabels[i].color=owner.Muted(channel)?new Color(1,.63f,.48f):BattleUi.Pale;
            }
        }
        public void ReleaseInput()
        {
            if(released)return;released=true;
            if(owner!=null)owner.SettingsChanged-=Refresh;
            if(sceneInput!=null)sceneInput.interactable=priorInteractable;
            if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(priorSelection!=null&&priorSelection.activeInHierarchy?priorSelection:null);
        }
        private void Update(){if(GameFlow.Instance==null&&!released&&Keyboard.current?.escapeKey.wasPressedThisFrame==true)owner.CloseSettings();}
        private void OnDestroy(){ReleaseInput();}
    }
}

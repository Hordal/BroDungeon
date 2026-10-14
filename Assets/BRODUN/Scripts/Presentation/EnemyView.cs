using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Brodun.Presentation
{
    public sealed class EnemyView : MonoBehaviour
    {
        public Image sprite,hpBar;
        public Text title,hp,status,intentLabel,tooltip;
        public RectTransform damageAnchor;
        private EnemyVisual data;
        private EnemyInstance model;
        private float timer,shownHP;
        private int idleFrame;
        private bool oneShot,dead;
        public SpriteMotion Motion { get; private set; }
        private float pixelsScale;
        private Image intentFrame;
        private bool holdHealth;
        private Vector2 spriteHome;
        public void HoldHealth(bool hold){holdHealth=hold;}
        public void Bind(EnemyInstance enemy,EnemyVisual visual)
        {
            model=enemy;data=visual;shownHP=enemy.CurrentHP;
            pixelsScale=data.displayHeight*1.36f/data.Animation(SpriteMotion.Idle).frames[0].rect.height;
            spriteHome=sprite.rectTransform.anchoredPosition;
            title.fontSize=25;hp.fontSize=23;status.fontSize=21;intentLabel.fontSize=29;
            title.rectTransform.anchorMin=new Vector2(.04f,1.00f);title.rectTransform.anchorMax=new Vector2(.96f,1.09f);
            hp.rectTransform.anchorMin=new Vector2(.1f,.925f);hp.rectTransform.anchorMax=new Vector2(.9f,.995f);
            var track=(RectTransform)hpBar.transform.parent;track.anchorMin=new Vector2(.14f,.91f);track.anchorMax=new Vector2(.86f,.923f);
            status.rectTransform.anchorMin=new Vector2(0,.84f);status.rectTransform.anchorMax=new Vector2(1,.91f);
            intentFrame=intentLabel.GetComponentInParent<Image>();
            BattleUi.Image(transform,"Enemy status panel",null,new Color(.02f,.045f,.06f,.82f),new Vector2(.04f,.84f),new Vector2(.96f,1.10f)).transform.SetAsFirstSibling();
            intentFrame.rectTransform.anchorMin=new Vector2(.27f,1.11f);intentFrame.rectTransform.anchorMax=new Vector2(.67f,1.21f);
            var tip=BattleUi.Panel(tooltip.transform.parent,"Intent tooltip backdrop",new Vector2(-.65f,.70f),new Vector2(0,.925f));
            tooltip.transform.SetParent(tip,false);tooltip.rectTransform.anchorMin=Vector2.zero;tooltip.rectTransform.anchorMax=Vector2.one;
            tooltip.rectTransform.offsetMin=Vector2.one*7;tooltip.rectTransform.offsetMax=Vector2.one*-7;
            tip.gameObject.SetActive(false);tooltip.gameObject.SetActive(true);
            GetComponentInChildren<IntentHover>().tooltip=tip.gameObject;
            title.text=enemy.Definition.Name+"  /  "+(enemy.Definition.Type==EnemyType.Normal?"일반":enemy.Definition.Type==EnemyType.Elite?"엘리트":"보스");Refresh(null,0);
            dead=enemy.CurrentHP<=0;oneShot=false;Motion=dead?SpriteMotion.Death:SpriteMotion.Idle;
            var frames=data.Animation(Motion).frames;Frame(frames[dead?frames.Length-1:0]);
        }
        public void Refresh(IntentData intent,int playerBlock)
        {
            if(!holdHealth)hp.text=model.CurrentHP+" / "+model.MaxHP;
            status.text=(model.Block>0?"방어 "+model.Block+"   ":"")+(model.Status.Strength>0?"힘 +"+model.Status.Strength+"   ":"")+
                (model.Status.Weak>0?"약화 "+model.Status.Weak+"   ":"")+(model.Status.Poison>0?"독 "+model.Status.Poison:"");
            intentLabel.text=BattleUi.Intent(intent);tooltip.text=BattleUi.IntentDescription(intent,playerBlock);
            intentLabel.color=intent==null?BattleUi.Pale:intent.Type==IntentType.Attack?new Color(1,.65f,.40f):intent.Type==IntentType.Block?BattleUi.Teal:BattleUi.Gold;
            if(intentFrame!=null)intentFrame.color=new Color(.83f,.85f,.84f);
        }
        private void Frame(Sprite frame)
        {
            sprite.sprite=frame;var rect=(RectTransform)sprite.transform;
            rect.pivot=new Vector2(frame.pivot.x/frame.rect.width,frame.pivot.y/frame.rect.height);
            rect.sizeDelta=frame.rect.size*pixelsScale;
        }
        public IEnumerator Play(SpriteMotion motion)
        {
            if(dead)yield break;oneShot=true;Motion=motion;var sequence=data.Animation(motion);
            if(motion==SpriteMotion.Death)dead=true;
            float duration=sequence.frames.Length/sequence.fps;
            for(float t=0;t<duration;t+=Time.unscaledDeltaTime)
            {
                float p=t/duration;Frame(sequence.frames[Mathf.Min(sequence.frames.Length-1,(int)(t*sequence.fps))]);
                float offset=motion==SpriteMotion.Attack?-85*Mathf.Sin(p*Mathf.PI):motion==SpriteMotion.Hurt?25*Mathf.Sin(p*Mathf.PI):0;
                sprite.rectTransform.anchoredPosition=spriteHome+new Vector2(offset,0);
                sprite.color=motion==SpriteMotion.Hurt&&t<.10f?new Color(1,.68f,.52f):motion==SpriteMotion.Death?new Color(.7f,.7f,.7f,1-.6f*p):Color.white;
                yield return null;
            }
            sprite.rectTransform.anchoredPosition=spriteHome;
            if(dead){Frame(sequence.frames[sequence.frames.Length-1]);sprite.color=new Color(.7f,.7f,.7f,.4f);}
            else sprite.color=Color.white;
            oneShot=false;
            if(!dead){Motion=SpriteMotion.Idle;idleFrame=0;timer=0;Frame(data.Animation(Motion).frames[0]);}
        }
        private void Update()
        {
            if(model==null)return;
            if(!holdHealth){shownHP=Mathf.MoveTowards(shownHP,model.CurrentHP,Time.unscaledDeltaTime*model.MaxHP*3);hp.text=model.CurrentHP+" / "+model.MaxHP;}
            BattleUi.SetBar(hpBar,shownHP/model.MaxHP);
            if(oneShot||dead)return;
            var idle=data.Animation(SpriteMotion.Idle);timer+=Time.unscaledDeltaTime;
            if(timer>=1f/idle.fps){timer%=1f/idle.fps;idleFrame=(idleFrame+1)%idle.frames.Length;Frame(idle.frames[idleFrame]);}
        }
        public static EnemyView Create(Transform parent,PresentationAssets art)
        {
            var root=BattleUi.Rect(parent,"EnemyView",Vector2.zero,Vector2.one);var view=root.gameObject.AddComponent<EnemyView>();
            view.sprite=BattleUi.Image(root,"Enemy sprite",null,Color.white,new Vector2(.5f,0),new Vector2(.5f,0));
            view.title=BattleUi.Text(root,"Enemy name","",art.font,31,BattleUi.Gold,new Vector2(0,.84f),new Vector2(1,.97f));
            view.hp=BattleUi.Text(root,"Enemy HP","",art.font,25,BattleUi.Pale,new Vector2(0,.73f),new Vector2(1,.84f));
            view.hpBar=BattleUi.Bar(root,"Enemy HP Bar",new Vector2(.15f,.71f),new Vector2(.85f,.735f),new Color(.77f,.22f,.17f));
            view.status=BattleUi.Text(root,"Enemy status","",art.font,20,BattleUi.Teal,new Vector2(-.1f,.61f),new Vector2(1.1f,.71f));
            var intent=BattleUi.Image(root,"Intent",art.buttonNormal,Color.white,new Vector2(.22f,.99f),new Vector2(.78f,1.12f));intent.raycastTarget=true;
            view.intentLabel=BattleUi.Text(intent.transform,"Intent value","",art.font,30,BattleUi.Gold,Vector2.zero,Vector2.one);
            view.tooltip=BattleUi.Text(root,"Intent tooltip","",art.font,21,BattleUi.Pale,new Vector2(-.2f,1.13f),new Vector2(1.2f,1.37f));
            view.tooltip.gameObject.AddComponent<Outline>().effectColor=Color.black;view.tooltip.gameObject.SetActive(false);
            var hover=intent.gameObject.AddComponent<IntentHover>();hover.tooltip=view.tooltip.gameObject;
            view.damageAnchor=BattleUi.Rect(root,"Damage anchor",new Vector2(.5f,.5f),new Vector2(.5f,.5f));
            return view;
        }
    }
}

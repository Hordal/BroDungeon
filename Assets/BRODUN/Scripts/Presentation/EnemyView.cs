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
        private BattleHudGlyph intentIcon;
        private Text blockLabel;
        private int motionVersion;
        private bool holdHealth;
        private Vector2 spriteHome;
        public void HoldHealth(bool hold){holdHealth=hold;}
        public void Bind(EnemyInstance enemy,EnemyVisual visual)
        {
            model=enemy;data=visual;shownHP=enemy.CurrentHP;
            float displayFactor=enemy.Definition.Type==EnemyType.Boss?.90f:.92f;
            pixelsScale=data.displayHeight*1.48f*displayFactor/data.Animation(SpriteMotion.Idle).frames[0].rect.height;
            spriteHome=sprite.rectTransform.anchoredPosition;
            title.fontSize=22;hp.fontSize=40;status.fontSize=20;intentLabel.fontSize=36;
            title.rectTransform.anchorMin=new Vector2(.15f,1.31f);title.rectTransform.anchorMax=new Vector2(.95f,1.38f);
            hp.rectTransform.anchorMin=new Vector2(.14f,.99f);hp.rectTransform.anchorMax=new Vector2(.94f,1.10f);
            var track=(RectTransform)hpBar.transform.parent;track.anchorMin=new Vector2(.14f,1.13f);track.anchorMax=new Vector2(.94f,1.24f);
            track.GetComponent<Image>().color=new Color(.015f,.025f,.035f,.96f);hpBar.color=new Color(.53f,.13f,.12f);
            hp.rectTransform.SetParent(track,false);hp.rectTransform.anchorMin=Vector2.zero;hp.rectTransform.anchorMax=Vector2.one;
            hp.rectTransform.offsetMin=hp.rectTransform.offsetMax=Vector2.zero;
            hp.verticalOverflow=VerticalWrapMode.Overflow;hp.horizontalOverflow=HorizontalWrapMode.Overflow;
            status.rectTransform.anchorMin=new Vector2(.40f,1.245f);status.rectTransform.anchorMax=new Vector2(.97f,1.31f);
            BattleUi.Glyph(transform,"Enemy shield",BattleHudGlyph.Symbol.Shield,BattleUi.Teal,new Vector2(.17f,1.25f),new Vector2(.22f,1.305f));
            blockLabel=BattleUi.Text(transform,"Enemy block","",title.font,25,BattleUi.Teal,new Vector2(.23f,1.245f),new Vector2(.38f,1.31f));
            blockLabel.verticalOverflow=VerticalWrapMode.Overflow;
            intentFrame=intentLabel.GetComponentInParent<Image>();
            intentFrame.sprite=null;intentFrame.overrideSprite=null;
            intentFrame.rectTransform.anchorMin=new Vector2(.20f,1.39f);intentFrame.rectTransform.anchorMax=new Vector2(.68f,1.52f);
            intentLabel.rectTransform.anchorMin=new Vector2(.25f,0);intentLabel.rectTransform.anchorMax=Vector2.one;
            intentIcon=BattleUi.Glyph(intentFrame.transform,"Intent icon",BattleHudGlyph.Symbol.Sword,BattleUi.Gold,new Vector2(.07f,.15f),new Vector2(.24f,.85f));
            foreach(var label in new[]{hp,intentLabel,status,blockLabel})BattleUi.Readable(label);
            var tip=BattleUi.Panel(tooltip.transform.parent,"Intent tooltip backdrop",new Vector2(-.65f,.70f),new Vector2(0,.925f));
            tooltip.transform.SetParent(tip,false);tooltip.rectTransform.anchorMin=Vector2.zero;tooltip.rectTransform.anchorMax=Vector2.one;
            tooltip.rectTransform.offsetMin=Vector2.one*7;tooltip.rectTransform.offsetMax=Vector2.one*-7;
            tip.gameObject.SetActive(false);tooltip.gameObject.SetActive(true);
            GetComponentInChildren<IntentHover>().tooltip=tip.gameObject;
            title.text=enemy.Definition.Name;Refresh(null,0);
            dead=enemy.CurrentHP<=0;oneShot=false;Motion=dead?SpriteMotion.Death:SpriteMotion.Idle;
            var frames=data.Animation(Motion).frames;Frame(frames[dead?frames.Length-1:0]);
        }
        public void Refresh(IntentData intent,int playerBlock)
        {
            if(!holdHealth)hp.text=model.CurrentHP+" / "+model.MaxHP;
            blockLabel.text=model.Block.ToString();
            status.text=(model.Status.Strength>0?"힘 +"+model.Status.Strength+"   ":"")+
                (model.Status.Weak>0?"약화 "+model.Status.Weak+"   ":"")+(model.Status.Poison>0?"독 "+model.Status.Poison:"");
            intentLabel.text=intent==null?"—":intent.Type==IntentType.Attack?intent.Amount+(intent.Hits>1?" × "+intent.Hits:""):"+"+intent.Amount;
            tooltip.text=BattleUi.IntentDescription(intent,playerBlock);
            intentLabel.color=intent==null?BattleUi.Pale:intent.Type==IntentType.Attack?new Color(1,.65f,.40f):intent.Type==IntentType.Block?BattleUi.Teal:BattleUi.Gold;
            if(intentFrame!=null)intentFrame.color=new Color(.025f,.04f,.055f,.80f);
            if(intentIcon!=null){intentIcon.enabled=intent!=null;intentIcon.color=intentLabel.color;intentIcon.Kind=intent!=null&&intent.Type==IntentType.Attack?BattleHudGlyph.Symbol.Sword:intent!=null&&intent.Type==IntentType.Block?BattleHudGlyph.Symbol.Shield:BattleHudGlyph.Symbol.Power;}
        }
        private void Frame(Sprite frame)
        {
            sprite.sprite=frame;var rect=(RectTransform)sprite.transform;
            rect.pivot=new Vector2(frame.pivot.x/frame.rect.width,frame.pivot.y/frame.rect.height);
            rect.sizeDelta=frame.rect.size*pixelsScale;
        }
        public IEnumerator Play(SpriteMotion motion,float hitPause=.035f,float weight=1,string impactStyle=null)
        {
            if(dead)yield break;int version=++motionVersion;oneShot=true;Motion=motion;var sequence=data.Animation(motion);
            if(motion==SpriteMotion.Death)dead=true;
            if(motion==SpriteMotion.Hurt)
            {
                // Local pose hold only: no Time.timeScale or domain state changes.
                Frame(sequence.frames[0]);sprite.color=new Color(1,.74f,.58f);
                if(hitPause>0)yield return new WaitForSecondsRealtime(hitPause);
                if(version!=motionVersion)yield break;
            }
            float duration=sequence.frames.Length/sequence.fps;
            for(float t=0;t<duration;t+=Time.unscaledDeltaTime)
            {
                if(version!=motionVersion)yield break;
                float p=t/duration;Frame(sequence.frames[Mathf.Min(sequence.frames.Length-1,(int)(t*sequence.fps))]);
                float offset=motion==SpriteMotion.Attack?-85*Mathf.Sin(p*Mathf.PI):motion==SpriteMotion.Hurt?25*weight*Mathf.Sin(p*Mathf.PI):0;
                var recoil=new Vector2(offset,0);
                if(motion==SpriteMotion.Hurt)
                {
                    if(impactStyle=="earth_strike")recoil=new Vector2(32*Mathf.Sin(p*Mathf.PI),Mathf.Sin(p*Mathf.PI)*10+Mathf.Sin(p*30)*(1-p)*3);
                    else if(impactStyle=="fire_bolt")recoil=new Vector2(38*Mathf.Sin(p*Mathf.PI)*(1-p*.3f),0);
                    else if(impactStyle=="lightning_strike")recoil=new Vector2(Mathf.Sin(p*78)*9*(1-p),Mathf.Sin(p*53)*3*(1-p));
                }
                sprite.rectTransform.anchoredPosition=spriteHome+recoil;
                sprite.color=motion==SpriteMotion.Hurt&&t<.10f?new Color(1,.68f,.52f):motion==SpriteMotion.Death?new Color(.7f,.7f,.7f,1-.6f*p):Color.white;
                yield return null;
            }
            if(version!=motionVersion)yield break;
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

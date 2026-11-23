using System.Collections;
using UnityEngine;
using UnityEngine.UI;
namespace Brodun.Presentation
{
    public sealed class PlayerBattleActor : MonoBehaviour
    {
        private Image image;
        private HeroMotion data;
        private Vector2 home;
        private float scale,timer;
        private bool busy;
        private float attackContactTime;
        private int frame;
        private int motionVersion;
        public bool IsAttacking { get; private set; }
        internal float IdleTop { get; private set; }
        public void Bind(Image target,CharacterId character)
        {
            image=target;home=image.rectTransform.anchoredPosition;
            data=Resources.Load<CombatMotionAssets>("BrodunCombatMotion")?.Hero(character);
            var idle=data?.Animation(SpriteMotion.Idle);
            // Respect the BattleView size for every animation frame, keeping native foot pivots.
            scale=idle!=null?Mathf.Max(1,target.rectTransform.sizeDelta.y)/idle.frames[0].rect.height:1;
            IdleTop=idle!=null?BattleUi.SpriteTop(idle.frames,scale):target.rectTransform.sizeDelta.y*(1-target.rectTransform.pivot.y);
            if(idle!=null)Show(idle.frames[0]);
        }
        private void Show(Sprite sprite)
        {
            image.sprite=sprite;image.rectTransform.pivot=new Vector2(sprite.pivot.x/sprite.rect.width,sprite.pivot.y/sprite.rect.height);
            image.rectTransform.sizeDelta=sprite.rect.size*scale;
        }
        public IEnumerator Attack(float contact)
        {
            int version=++motionVersion;
            busy=true;IsAttacking=true;attackContactTime=-1;
            var animation=data?.Animation(SpriteMotion.Attack);
            image.rectTransform.anchoredPosition=home;
            float started=Time.unscaledTime;
            while(version==motionVersion)
            {
                float now=Time.unscaledTime;
                float recovery=now-attackContactTime;
                if(attackContactTime>=0&&recovery>=BattleFeedback.AttackRecovery)break;

                // Hold the wind-up until the actual contact, even if the render loop misses a frame.
                float pose=attackContactTime<0?.49f*Mathf.InverseLerp(0,contact,now-started):
                    Mathf.Lerp(.64f,.99f,Mathf.InverseLerp(.038f,BattleFeedback.AttackRecovery,recovery));
                if(animation!=null)Show(animation.frames[AttackFrame(animation.frames.Length,pose)]);
                yield return null;
            }
            if(version==motionVersion)ResetPose();
        }
        private static int AttackFrame(int count,float phase)=>Mathf.Min(count-1,(int)(phase*count));

        public void ShowAttackContact()
        {
            if(!IsAttacking||attackContactTime>=0)return;
            attackContactTime=Time.unscaledTime;
            var animation=data?.Animation(SpriteMotion.Attack);
            if(animation!=null)Show(animation.frames[AttackFrame(animation.frames.Length,.64f)]);
        }

        public IEnumerator CastSupport(bool brace)
        {
            int version=++motionVersion;
            busy=true;
            for(float t=0;t<.36f;t+=Time.unscaledDeltaTime)
            {
                if(version!=motionVersion)yield break;
                float wave=Mathf.Sin(t/.36f*Mathf.PI);
                image.rectTransform.anchoredPosition=home+new Vector2(brace?-14*wave:0,brace?0:7*wave);
                image.rectTransform.localRotation=Quaternion.Euler(0,0,(brace?3:-2)*wave);
                yield return null;
            }
            if(version==motionVersion)ResetPose();
        }
        public IEnumerator Hit()
        {
            int version=++motionVersion;
            busy=true;var animation=data?.Animation(SpriteMotion.Hurt);
            for(float t=0;t<BattleFeedback.AttackRecovery;t+=Time.unscaledDeltaTime)
            {
                if(version!=motionVersion)yield break;
                float phase=t/BattleFeedback.AttackRecovery;
                if(animation!=null)Show(animation.frames[Mathf.Min(animation.frames.Length-1,(int)(phase*animation.frames.Length))]);
                image.color=t<.06f?new Color(1,.65f,.55f):Color.white;
                image.rectTransform.anchoredPosition=home+Vector2.left*(BattleFeedback.Recoil(phase));
                yield return null;
            }
            if(version==motionVersion)ResetPose();
        }
        private void ResetPose()
        {
            image.color=Color.white;image.rectTransform.anchoredPosition=home;image.rectTransform.localRotation=Quaternion.identity;
            busy=false;IsAttacking=false;frame=0;timer=0;
            var idle=data?.Animation(SpriteMotion.Idle);if(idle!=null)Show(idle.frames[0]);
        }
        private void Update()
        {
            var idle=data?.Animation(SpriteMotion.Idle);if(busy||idle==null)return;
            timer+=Time.unscaledDeltaTime;
            if(timer>=1/idle.fps){timer%=1/idle.fps;frame=(frame+1)%idle.frames.Length;Show(idle.frames[frame]);}
        }
    }
}

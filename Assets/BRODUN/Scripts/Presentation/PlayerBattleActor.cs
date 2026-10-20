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
        private int frame;
        private float impactHold;
        public void HoldImpact(float seconds){if(IsAttacking)impactHold=Mathf.Max(impactHold,seconds);}
        public bool IsAttacking { get; private set; }
        public void Bind(Image target,CharacterId character)
        {
            image=target;home=image.rectTransform.anchoredPosition;
            data=Resources.Load<CombatMotionAssets>("BrodunCombatMotion")?.Hero(character);
            var idle=data?.Animation(SpriteMotion.Idle);
            // Respect the BattleView size for every animation frame, keeping native foot pivots.
            scale=idle!=null?Mathf.Max(1,target.rectTransform.sizeDelta.y)/idle.frames[0].rect.height:1;
            if(idle!=null)Show(idle.frames[0]);
        }
        private void Show(Sprite sprite)
        {
            image.sprite=sprite;image.rectTransform.pivot=new Vector2(sprite.pivot.x/sprite.rect.width,sprite.pivot.y/sprite.rect.height);
            image.rectTransform.sizeDelta=sprite.rect.size*scale;
        }
        public IEnumerator Attack(bool melee)
        {
            busy=true;IsAttacking=true;var animation=data?.Animation(SpriteMotion.Attack);
            float duration=Mathf.Max(.56f,animation!=null?animation.frames.Length/animation.fps:.4f);
            for(float t=0;t<duration;)
            {
                if(impactHold>0){impactHold-=Time.unscaledDeltaTime;yield return null;continue;}
                float p=t/duration;
                if(animation!=null)Show(animation.frames[Mathf.Min(animation.frames.Length-1,(int)(p*animation.frames.Length))]);
                // Anticipation, forward strike, recovery; never moves gameplay positions.
                float reach=melee?115:32;
                float travel=p<.18f?-18*p/.18f:p<.55f?Mathf.Lerp(-18,reach,(p-.18f)/.37f):p<.72f?reach:Mathf.Lerp(reach,0,(p-.72f)/.28f);
                image.rectTransform.anchoredPosition=home+new Vector2(travel,Mathf.Sin(p*Mathf.PI)*8);
                t+=Time.unscaledDeltaTime;yield return null;
            }
            ResetPose();
        }
        public IEnumerator CastSupport(bool brace)
        {
            busy=true;
            for(float t=0;t<.36f;t+=Time.unscaledDeltaTime)
            {
                float wave=Mathf.Sin(t/.36f*Mathf.PI);
                image.rectTransform.anchoredPosition=home+new Vector2(brace?-14*wave:0,brace?0:7*wave);
                image.rectTransform.localRotation=Quaternion.Euler(0,0,(brace?3:-2)*wave);
                yield return null;
            }
            image.rectTransform.localRotation=Quaternion.identity;ResetPose();
        }
        public IEnumerator Hit()
        {
            busy=true;var animation=data?.Animation(SpriteMotion.Hurt);
            for(float t=0;t<.28f;t+=Time.unscaledDeltaTime)
            {
                if(animation!=null)Show(animation.frames[Mathf.Min(animation.frames.Length-1,(int)(t*animation.fps))]);
                image.color=t<.08f?new Color(1,.65f,.55f):Color.white;
                image.rectTransform.anchoredPosition=home+new Vector2(-28*Mathf.Sin(t/.28f*Mathf.PI),0);yield return null;
            }
            ResetPose();
        }
        private void ResetPose()
        {
            image.color=Color.white;image.rectTransform.anchoredPosition=home;
            busy=false;IsAttacking=false;impactHold=0;frame=0;timer=0;
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

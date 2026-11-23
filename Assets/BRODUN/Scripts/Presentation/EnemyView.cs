using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Brodun.Presentation
{
    public sealed class EnemyView : MonoBehaviour,IPointerClickHandler,IPointerEnterHandler,IPointerExitHandler
    {
        public Image sprite,hpBar;
        public Text title,hp,status,intentLabel,tooltip;
        public RectTransform damageAnchor;
        private EnemyVisual data;
        private ImportedMonsterPose imported;
        private float importedHeight;
        private EnemyInstance model;
        private float timer,shownHP;
        private int idleFrame;
        private bool oneShot,dead;
        public SpriteMotion Motion { get; private set; }
        private float pixelsScale;
        private float idleTop;
        private Image intentFrame;
        private BattleHudGlyph intentIcon;
        private Text blockLabel,markLabel;
        private int motionVersion;
        private bool holdHealth;
        private bool holdActionHud;
        private int shownWeak,shownPoison,shownStrength,shownBrand,shownFreeze,shownBurn;
        private ElementType shownMark;
        private float actionPulse;
        private Vector2 spriteHome;
        private RectTransform vitals,details;
        private IntentHover spriteDetails;
        private Outline targetOutline;
        private Action<EnemyInstance> chooseTarget;
        private bool targeting,pointed;
        public Vector2 Home { get; private set; }
        internal EnemyInstance Model => model;
        public void HoldHealth(bool hold){holdHealth=hold;}
        public void HoldActionHud(bool hold)
        {
            if(hold&&!holdActionHud)
            {
                shownWeak=model.Status.Weak;shownPoison=model.Status.Poison;
                shownStrength=model.Status.Strength;shownBrand=model.Status.Brand;
                shownFreeze=model.Status.RapidFreeze;shownBurn=model.Status.VenomBurn;shownMark=model.ElementMark;
            }
            holdActionHud=hold;
        }
        public void RevealActionHud(bool emphasize=true)
        {holdActionHud=false;if(emphasize)actionPulse=.28f;RefreshActionHud();}
        public void Bind(EnemyInstance enemy,EnemyVisual visual,Transform hudParent=null,float displayScale=1,Vector2? home=null,Action<EnemyInstance> targetClick=null,GameObject motionPrefab=null)
        {
            model=enemy;data=visual;shownHP=enemy.CurrentHP;
            Home=home??new Vector2(BattleUi.EnemyHomeX,BattleUi.FloorY);
            chooseTarget=targetClick;
            float displayFactor=enemy.Definition.Type==EnemyType.Boss?.90f:.92f;
            if(motionPrefab!=null)imported=new ImportedMonsterPose(motionPrefab,transform);
            var idleFrames=imported==null?data.Animation(SpriteMotion.Idle).frames:new[]{imported.Sprite};
            float height=imported==null?data.displayHeight:enemy.Definition.Type==EnemyType.Boss?292:
                enemy.Definition.Type==EnemyType.Elite?222:184;
            importedHeight=height*1.48f*displayFactor*displayScale;
            if(imported!=null)importedHeight=Mathf.Min(importedHeight,410*displayScale*imported.Sprite.rect.height/imported.Sprite.rect.width);
            pixelsScale=importedHeight/idleFrames[0].rect.height;
            idleTop=BattleUi.SpriteTop(idleFrames,pixelsScale);
            spriteHome=sprite.rectTransform.anchoredPosition;
            var hud=hudParent!=null
                ?BattleUi.UnitReadout(hudParent,"Enemy vitals",Home.x,Home.y)
                :BattleUi.Rect(transform,"Enemy vitals",new Vector2(.25f,-.14f),new Vector2(.75f,-.02f));
            vitals=hud;
            BattleUi.LayoutUnitReadout(hud,title,hp,hpBar);
            blockLabel=BattleUi.BlockReadout(hud,"Enemy block",title.font);
            markLabel=BattleUi.Text(hud,"Enemy element mark","",title.font,17,BattleUi.Teal,new Vector2(.03f,-.44f),new Vector2(.97f,.05f));
            markLabel.resizeTextForBestFit=true;markLabel.resizeTextMinSize=12;markLabel.resizeTextMaxSize=17;
            BattleUi.Readable(markLabel);
            intentFrame=intentLabel.GetComponentInParent<Image>();
            var intentHover=intentFrame.GetComponent<IntentHover>();
            intentFrame.sprite=null;intentFrame.overrideSprite=null;
            if(hudParent!=null)
            {
                BattleUi.Place(intentFrame.rectTransform,hudParent,Home,Home);
                intentFrame.rectTransform.sizeDelta=new Vector2(105,43);
                BattleUi.PositionAbove(intentFrame.rectTransform,idleTop);
            }
            BattleUi.Place(intentLabel.rectTransform,intentFrame.transform,new Vector2(.32f,.02f),new Vector2(.96f,.98f));
            intentLabel.fontSize=25;
            intentLabel.resizeTextForBestFit=true;
            intentLabel.resizeTextMinSize=14;
            intentLabel.resizeTextMaxSize=25;
            intentIcon=BattleUi.Glyph(intentFrame.transform,"Intent icon",BattleHudGlyph.Symbol.Sword,BattleUi.Gold,new Vector2(.08f,.20f),new Vector2(.32f,.80f));
            BattleUi.Readable(intentLabel);

            var tip=BattleUi.Surface(hudParent!=null?hudParent:transform,"Enemy details",
                hudParent!=null?new Vector2(Home.x-.33f,Home.y+.02f):new Vector2(.03f,.08f),
                hudParent!=null?new Vector2(Home.x-.06f,Home.y+.135f):new Vector2(.95f,.40f),
                BattleChrome.Form.Hud,BattleUi.Steel);
            details=tip;
            BattleUi.Place(tooltip.rectTransform,tip,new Vector2(.05f,.29f),new Vector2(.95f,.94f));
            tooltip.alignment=TextAnchor.MiddleLeft;
            tooltip.fontSize=20;
            tooltip.resizeTextForBestFit=true;
            tooltip.resizeTextMinSize=14;
            tooltip.resizeTextMaxSize=20;
            BattleUi.Place(status.rectTransform,tip,new Vector2(.05f,.06f),new Vector2(.95f,.29f));
            status.alignment=TextAnchor.MiddleLeft;
            status.color=BattleUi.Steel;
            status.fontSize=16;
            tip.gameObject.SetActive(false);
            tooltip.gameObject.SetActive(true);
            intentHover.tooltip=tip.gameObject;
            sprite.raycastTarget=true;
            spriteDetails=sprite.gameObject.AddComponent<IntentHover>();
            spriteDetails.tooltip=tip.gameObject;
            targetOutline=sprite.gameObject.AddComponent<Outline>();
            targetOutline.effectDistance=new Vector2(2,-2);
            targetOutline.enabled=false;
            title.text=enemy.Definition.Name;Refresh(null,0);
            dead=enemy.CurrentHP<=0;oneShot=false;Motion=dead?SpriteMotion.Death:SpriteMotion.Idle;
            if(imported==null)
            {
                var frames=data.Animation(Motion).frames;Frame(frames[dead?frames.Length-1:0]);
            }
            else
            {
                Frame(imported.Sprite);
                imported.Apply(dead?imported.Death:imported.Idle,dead?1:0,sprite,spriteHome,importedHeight,Color.white);
            }
            if(dead)
            {
                if(imported==null)sprite.color=new Color(.7f,.7f,.7f,.4f);
                RetireReadouts();
            }
        }
        public void SetTargeting(bool enabled)
        {
            targeting=enabled&&model.IsAlive;
            // Leave the explicit intent badge inspectable while keeping the target silhouettes clear.
            spriteDetails.enabled=!targeting&&model.IsAlive;
            UpdateTargetOutline();
        }
        public void OnPointerEnter(PointerEventData eventData){pointed=true;UpdateTargetOutline();}
        public void OnPointerExit(PointerEventData eventData){pointed=false;UpdateTargetOutline();}
        public void OnPointerClick(PointerEventData eventData)
        {
            if(targeting&&model.IsAlive&&eventData.button==PointerEventData.InputButton.Left)
                chooseTarget?.Invoke(model);
        }
        private void UpdateTargetOutline()
        {
            if(targetOutline==null)return;
            targetOutline.enabled=targeting;
            targetOutline.effectColor=new Color(.96f,.76f,.39f,pointed?.95f:.54f);
        }
        private void RetireReadouts()
        {
            SetTargeting(false);
            sprite.raycastTarget=false;
            vitals.gameObject.SetActive(false);
            intentFrame.gameObject.SetActive(false);
            details.gameObject.SetActive(false);
        }
        public void Refresh(IntentData intent,int playerBlock)
        {
            if(!holdHealth)hp.text=model.CurrentHP+" / "+model.MaxHP;
            if(!holdActionHud)RefreshActionHud();
            intentLabel.text=intent==null?"—":intent.Type==IntentType.Attack
                ?(intent.AttackReduction>0?"총 "+intent.TotalDamage:intent.Amount+(intent.Hits>1?" × "+intent.Hits:""))
                :"+"+intent.Amount;
            tooltip.text=BattleUi.IntentDescription(intent,playerBlock);
            intentLabel.color=intent==null?BattleUi.Pale:intent.Type==IntentType.Attack?new Color(1,.65f,.40f):intent.Type==IntentType.Block?BattleUi.Teal:BattleUi.Gold;
            if(intentFrame!=null)
            {
                intentFrame.color=new Color(.025f,.052f,.067f,.92f);
                intentFrame.gameObject.SetActive(intent!=null&&!dead);
            }
            if(intentIcon!=null){intentIcon.enabled=intent!=null;intentIcon.color=intentLabel.color;intentIcon.Kind=intent!=null&&intent.Type==IntentType.Attack?BattleHudGlyph.Symbol.Sword:intent!=null&&intent.Type==IntentType.Block?BattleHudGlyph.Symbol.Shield:BattleHudGlyph.Symbol.Power;}
        }
        private void RefreshActionHud()
        {
            BattleUi.SetBlockReadout(blockLabel,model.Block);
            shownFreeze=model.Status.RapidFreeze;shownBurn=model.Status.VenomBurn;shownMark=model.ElementMark;
            SetStatusText(model.Status.Strength,model.Status.Weak,model.Status.Poison,model.Status.Brand);
        }
        private void SetStatusText(int strength,int weak,int poison,int brand)
        {
            status.text=(strength>0?"힘 +"+strength+"   ":"")+(weak>0?"약화 "+weak+"   ":"")+
                (poison>0?"독 "+poison+"   ":"")+(brand>0?"낙인 "+brand:"");
            string mark=shownMark switch{ElementType.Wind=>"바람",ElementType.Water=>"물",ElementType.Ice=>"얼음",
                ElementType.Poison=>"독 표식",ElementType.Fire=>"불",ElementType.Lightning=>"번개",_=>""};
            markLabel.text=mark+(shownFreeze>0?" · 빙결 −"+shownFreeze:"")+(shownBurn>0?" · 연소 "+shownBurn:"")+
                (poison>0?" · 독 "+poison:"")+(weak>0?" · 약화 "+weak:"")+(brand>0?" · 낙인 "+brand:"");
        }
        public void PresentMark(ElementType mark)
        {shownMark=mark;SetStatusText(shownStrength,shownWeak,shownPoison,shownBrand);}
        public void PresentStatus(CombatEvent entry)
        {
            if(entry.Status==StatusEffectType.Weak)shownWeak=entry.After;
            else if(entry.Status==StatusEffectType.Poison)shownPoison=entry.After;
            else if(entry.Status==StatusEffectType.Strength)shownStrength=entry.After;
            else if(entry.Status==StatusEffectType.Brand)shownBrand=entry.After;
            else if(entry.Status==StatusEffectType.RapidFreeze)shownFreeze=entry.After;
            else if(entry.Status==StatusEffectType.VenomBurn)shownBurn=entry.After;
            SetStatusText(shownStrength,shownWeak,shownPoison,shownBrand);
        }
        public void PresentVitals(int currentHP,int currentBlock)
        {
            // Display immutable hit results without assigning to EnemyInstance.
            holdHealth=true;holdActionHud=true;shownHP=currentHP;
            hp.text=currentHP+" / "+model.MaxHP;BattleUi.SetBar(hpBar,(float)currentHP/model.MaxHP);
            BattleUi.SetBlockReadout(blockLabel,currentBlock);
        }
        public IEnumerator PresentAction(IntentData intent,int hits)
        {
            if(dead)yield break;
            int version=++motionVersion;oneShot=true;
            bool attack=intent.Type==IntentType.Attack;
            Motion=attack?SpriteMotion.Attack:SpriteMotion.Idle;
            var sequence=data?.Animation(Motion);
            float duration=BattleFeedback.EnemyDuration(attack?hits:1);
            float lastContact=BattleFeedback.EnemyContact+Mathf.Max(0,hits-1)*BattleFeedback.EnemyHitInterval;
            sprite.rectTransform.anchoredPosition=spriteHome;
            for(float t=0;t<duration;t+=Time.unscaledDeltaTime)
            {
                if(version!=motionVersion)yield break;
                if(imported!=null)
                {
                    float contact=imported.AttackContact;
                    float progress=!attack?t/duration:t<BattleFeedback.EnemyContact
                        ?contact*Mathf.InverseLerp(0,BattleFeedback.EnemyContact,t)
                        :Mathf.Lerp(contact,1,Mathf.InverseLerp(BattleFeedback.EnemyContact,duration,t));
                    var clip=attack?imported.Attack:intent.Type==IntentType.Block?imported.Block:imported.Idle;
                    imported.Apply(clip,progress,sprite,spriteHome,importedHeight,Color.white);
                }
                else if(attack)
                {
                    float pose=t<BattleFeedback.EnemyContact?.64f*Mathf.InverseLerp(0,BattleFeedback.EnemyContact,t):
                        Mathf.Lerp(.64f,.99f,Mathf.InverseLerp(lastContact,duration,t));
                    Frame(sequence.frames[Mathf.Clamp((int)(pose*sequence.frames.Length),0,sequence.frames.Length-1)]);
                }
                else
                {
                    float charge=Mathf.Sin(Mathf.Clamp01(t/duration)*Mathf.PI);
                    Frame(sequence.frames[Mathf.Min(sequence.frames.Length-1,(int)(t*sequence.fps)%sequence.frames.Length)]);
                    sprite.rectTransform.anchoredPosition=spriteHome+new Vector2(0,intent.Type==IntentType.Buff?charge*9:0);
                    sprite.color=Color.Lerp(Color.white,intent.Type==IntentType.Block?new Color(.60f,.83f,1):new Color(1,.70f,.93f),charge*.35f);
                }
                yield return null;
            }
            if(version!=motionVersion)yield break;
            sprite.rectTransform.anchoredPosition=spriteHome;sprite.color=Color.white;
            ResetIdle();
        }
        private void ResetIdle()
        {
            oneShot=false;Motion=SpriteMotion.Idle;timer=0;idleFrame=0;
            if(imported==null)Frame(data.Animation(Motion).frames[0]);
            else imported.Apply(imported.Idle,0,sprite,spriteHome,importedHeight,Color.white);
        }
        private void Frame(Sprite frame)
        {
            sprite.sprite=frame;var rect=(RectTransform)sprite.transform;
            rect.pivot=new Vector2(frame.pivot.x/frame.rect.width,frame.pivot.y/frame.rect.height);
            rect.sizeDelta=frame.rect.size*pixelsScale;
        }
        public IEnumerator Play(SpriteMotion motion,float weight=1)
        {
            if(dead)yield break;int version=++motionVersion;oneShot=true;Motion=motion;var sequence=data?.Animation(motion);
            if(motion==SpriteMotion.Death)dead=true;
            var clip=imported==null?null:motion==SpriteMotion.Death?imported.Death:motion==SpriteMotion.Hurt?imported.Hit:imported.Attack;
            float duration=motion==SpriteMotion.Hurt?BattleFeedback.AttackRecovery:clip!=null?Mathf.Min(clip.length,.65f):sequence.frames.Length/sequence.fps;
            for(float t=0;t<duration;t+=Time.unscaledDeltaTime)
            {
                if(version!=motionVersion)yield break;
                float p=t/duration;
                if(clip!=null)
                {
                    imported.Apply(clip,p,sprite,spriteHome,importedHeight,Color.white);
                    yield return null;
                    continue;
                }
                Frame(sequence.frames[Mathf.Min(sequence.frames.Length-1,(int)(p*sequence.frames.Length))]);
                float recoil=motion==SpriteMotion.Hurt?BattleFeedback.Recoil(p)*weight:0;
                sprite.rectTransform.anchoredPosition=spriteHome+Vector2.right*recoil;
                sprite.color=motion==SpriteMotion.Hurt&&t<.06f?new Color(1,.68f,.52f):motion==SpriteMotion.Death?new Color(.7f,.7f,.7f,1-.6f*p):Color.white;
                yield return null;
            }
            if(version!=motionVersion)yield break;
            sprite.rectTransform.anchoredPosition=spriteHome;
            if(dead)
            {
                if(clip==null){Frame(sequence.frames[sequence.frames.Length-1]);sprite.color=new Color(.7f,.7f,.7f,.4f);}
                else imported.Apply(clip,1,sprite,spriteHome,importedHeight,Color.white);
                RetireReadouts();
            }
            else sprite.color=Color.white;
            oneShot=false;
            if(!dead)ResetIdle();
        }
        private void Update()
        {
            if(model==null)return;
            actionPulse=Mathf.Max(0,actionPulse-Time.unscaledDeltaTime);
            if(blockLabel!=null)blockLabel.transform.localScale=Vector3.one*(1+actionPulse*.7f);
            if(status!=null)status.transform.localScale=Vector3.one*(1+actionPulse*.3f);
            if(!holdHealth){shownHP=Mathf.MoveTowards(shownHP,model.CurrentHP,Time.unscaledDeltaTime*model.MaxHP*3);hp.text=model.CurrentHP+" / "+model.MaxHP;}
            BattleUi.SetBar(hpBar,shownHP/model.MaxHP);
            if(oneShot||dead)return;
            timer+=Time.unscaledDeltaTime;
            if(imported!=null)
            {
                timer%=imported.Idle.length;
                imported.Apply(imported.Idle,timer/imported.Idle.length,sprite,spriteHome,importedHeight,Color.white);
                return;
            }
            var idle=data.Animation(SpriteMotion.Idle);
            if(timer>=1f/idle.fps){timer%=1f/idle.fps;idleFrame=(idleFrame+1)%idle.frames.Length;Frame(idle.frames[idleFrame]);}
        }
        private void LateUpdate()
        {
            if(intentFrame!=null&&intentFrame.gameObject.activeSelf&&intentFrame.rectTransform.anchorMin==intentFrame.rectTransform.anchorMax)
                BattleUi.PositionAbove(intentFrame.rectTransform,idleTop);
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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace Brodun.Presentation
{
    // Small, pixel-snapped uGUI meshes: no packages, gameplay RNG, colliders or raycasts.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed partial class BattleVfx : MaskableGraphic
    {
        public enum Style { Slash, Ground, Gust, Bolt, Shard, Flame, Wave, Venom, Burst, Ward, StoneWall, FrostShield, EarthAura, Heal, Draw, Power, Charge, PoisonCloud, Fortress, Retaliate, BlastShield }
        public enum ImpactLook { Strike, Magic, Guard, Reaction, Stone, Gust, Flame, Lightning, Frost, Holy, Arcane }
        public Style CurrentStyle { get; private set; }
        private float progress;
        private bool cardRelease,cardExhaust,cardEntering;
        private Color ink;
        private Vector2 source,target;
        private CardDefinition definition;
        private bool combo;
        private float impactClock=-1,duration;
        private RectTransform casterRect,victimRect;
        private Vector2 castFoot,victimFoot,emitter,impactPoint;
        private bool anchorsCaptured;
        private static uint signatureSequence;
        private uint signatureSeed;
        private static readonly Dictionary<string,Sprite[]> warriorClips=new Dictionary<string,Sprite[]>();
        private Sprite[] paintedFrames;
        private Image paintedEffect;
        private readonly List<Image> paintedTrails=new List<Image>();
        private RectTransform paintedViewport;
        private int paintedPeak;
        private float paintedContact,paintedHold;
        private bool enhanced;
        private IntentData enemyIntent;
        private EnemyAttackLook enemyLook;
        private float enemyClock;
        private int enemyHitCount;
        private ImpactLook? localImpact;
        private bool heavyImpact;
        private Color? impactTint;
        private const float DrawLandingY=HandPresentation.HandTop-.03f;
        public static bool HasWarriorClip(string id)=>id=="earth_strike"||id=="stone_guard"||id=="wind_cut"||id=="wind_read";
        public static bool IsSignature(string id)=>id=="earth_strike"||id=="fire_bolt"||id=="lightning_strike";
        private float SignatureContact=>definition.Id=="earth_strike"?.34f:definition.Id=="fire_bolt"?.36f:.40f;
        public void BindActors(RectTransform caster,RectTransform victim){casterRect=caster;victimRect=victim;anchorsCaptured=false;}
        public const float MultiHitInterval=.13f;
        public static float HitPause(CardDefinition card)=>card.Id=="earth_strike"?.065f:card.Id=="fire_bolt"?.040f:card.Id=="lightning_strike"?.022f:card.Type!=CardType.Attack?0:card.Effects.Any(e=>e.Type==EffectType.Damage&&e.Hits>1)?.018f:card.BaseDamage>=12?.070f:card.BaseDamage<=4?.025f:.045f;
        private const float Contact=.35f;
        public BattleVfx(){useLegacyMeshGeneration=false;}
        public static Style For(CardDefinition card)
        {
            switch(card.Id)
            {
                case "earth_strike":case "earth_crush":return Style.Ground;
                case "stone_guard":return Style.StoneWall;
                case "stone_fortress":return Style.Fortress;
                case "stone_retaliation":return Style.Retaliate;
                case "wind_cut":return Style.Slash;
                case "wind_judgment":return Style.Gust;
                case "wind_read":return Style.Draw;
                case "fire_bolt":case "fire_inferno":return Style.Flame;
                case "fire_strength":return Style.Power;
                case "ice_guard":return Style.FrostShield;
                case "ice_shard":case "ice_link":return Style.Shard;
                case "lightning_strike":case "lightning_chain":return Style.Bolt;
                case "lightning_charge":return Style.Charge;
                case "water_wave":case "water_stream":return Style.Wave;
                case "water_rest":return Style.Heal;
                case "earth_blessing":return Style.EarthAura;
                case "poison_touch":case "poison_sting":return Style.Venom;
                case "poison_mist":return Style.PoisonCloud;
                case "explosion_blast":case "explosion_link":return Style.Burst;
                case "explosion_barrier":return Style.BlastShield;
            }
            if(card.Type!=CardType.Attack)return Style.Ward;
            if(card.Id=="wind_cut"||card.Id=="stone_retaliation")return Style.Slash;
            switch(card.Element)
            {
                case ElementType.Earth:case ElementType.Stone:return Style.Ground;
                case ElementType.Wind:return Style.Gust;
                case ElementType.Lightning:return Style.Bolt;
                case ElementType.Ice:return Style.Shard;
                case ElementType.Fire:return Style.Flame;
                case ElementType.Water:return Style.Wave;
                case ElementType.Poison:return Style.Venom;
                case ElementType.Explosion:return Style.Burst;
                default:return Style.Slash;
            }
        }
        public IEnumerator Present(CardDefinition card,Action impact,bool conditional=false,Action<int> extraHit=null,int hitCount=1,
            Action delivered=null,Action released=null,Action<float> contactScheduled=null)
        {
            definition=card;combo=conditional;
            enhanced=BattleFeedback.Enhanced&&HasWarriorClip(card.Id);
            CurrentStyle=For(card);ink=Tint(card.Element);raycastTarget=false;
            PreparePaintedClip(card.Id);
            duration=card.Type==CardType.Attack?1.02f:.86f;bool struck=false;int nextHit=1;impactClock=-1;
            bool signature=IsSignature(card.Id);
            if(card.RequiresEnemyTarget)signatureSeed=unchecked(++signatureSequence*2654435761u);
            if(signature)duration=card.Id=="earth_strike"?1.12f:card.Id=="fire_bolt"?1.06f:.86f;
            attackContacts=card.RequiresEnemyTarget&&!signature&&paintedFrames==null?new float[Mathf.Max(1,hitCount)]:null;
            if(attackContacts!=null)Array.Fill(attackContacts,-1f);
            float contact=signature?SignatureContact:Contact;
            bool deliveryShown=false,releaseShown=false;
            if(paintedFrames!=null)
            {
                // Seconds and keyframes are card-local; unpainted cards keep their existing clocks.
                switch(card.Id)
                {
                    case "earth_strike":duration=1.30f;paintedContact=.50f;paintedPeak=8;paintedHold=.12f;break;
                    case "stone_guard":duration=1.25f;paintedContact=.40f;paintedPeak=8;paintedHold=.22f;break;
                    case "wind_cut":duration=1.16f;paintedContact=.42f;paintedPeak=9;paintedHold=.12f;break;
                    default:duration=1.18f;paintedContact=.36f;paintedPeak=7;paintedHold=.15f;break;
                }
                contact=paintedContact;
            }
            contactScheduled?.Invoke(duration*contact);
            for(float t=0;t<duration;t+=Time.unscaledDeltaTime)
            {
                progress=t/duration;
                if(paintedFrames!=null)UpdatePaintedClip(contact);
                if(!releaseShown&&t>=BattleFeedback.WarriorRelease(card.Id)){releaseShown=true;released?.Invoke();}
                if(!struck&&progress>=contact)
                {
                    struck=true;progress=contact;impactClock=Time.unscaledTime;
                    if(attackContacts!=null)attackContacts[0]=impactClock;
                    impact?.Invoke();SetVerticesDirty();
                    if(paintedFrames!=null)UpdatePaintedClip(contact);
                    // Freeze this mesh at contact, not the global clock or combat simulation.
                    if(HitPause(card)>0)yield return new WaitForSecondsRealtime(HitPause(card));
                }
                while(struck&&nextHit<hitCount&&Time.unscaledTime-impactClock>=nextHit*MultiHitInterval)
                {
                    if(attackContacts!=null)attackContacts[nextHit]=Time.unscaledTime;
                    extraHit?.Invoke(nextHit++);
                }
                if(!deliveryShown&&card.Id=="wind_read"&&progress>=.90f){deliveryShown=true;delivered?.Invoke();}
                SetVerticesDirty();yield return null;
            }
            if(!struck)impact?.Invoke();
            while(nextHit<hitCount)extraHit?.Invoke(nextHit++);
            Destroy(gameObject);
        }
        public IEnumerator PresentEnemy(IntentData intent,EnemyAttackLook look,int hits,Action<int> contact)
        {
            enemyIntent=intent;enemyLook=look;enemyHitCount=hits;raycastTarget=false;
            signatureSeed=unchecked(++signatureSequence*2654435761u);
            var parent=GetComponentInParent<Canvas>();var canvas=gameObject.AddComponent<Canvas>();
            canvas.overrideSorting=true;canvas.sortingLayerID=parent!=null?parent.sortingLayerID:0;canvas.sortingOrder=5;
            int count=intent.Type==IntentType.Attack?hits:1,next=0;
            duration=BattleFeedback.EnemyDuration(count);
            for(float t=0;t<duration;t+=Time.unscaledDeltaTime)
            {
                enemyClock=t;progress=t/duration;
                while(next<count&&t>=BattleFeedback.EnemyContact+next*BattleFeedback.EnemyHitInterval){contact?.Invoke(next);next++;}
                SetVerticesDirty();yield return null;
            }
            while(next<count){contact?.Invoke(next);next++;}
            Destroy(gameObject);
        }
        public IEnumerator PresentImpact(ImpactLook look,bool heavy,Color? tint=null)
        {
            localImpact=look;heavyImpact=heavy;impactTint=tint;raycastTarget=false;
            string clip=look==ImpactLook.Gust?"wind_cut":look==ImpactLook.Stone?"earth_strike":null;
            var frames=clip!=null?LoadWarriorFrames(clip):null;
            Image paint=null;
            if(frames!=null)
            {
                paint=BattleUi.Image(transform,"Contact "+clip,null,Color.white,Vector2.one*.5f,Vector2.one*.5f);
                paint.preserveAspect=true;
                paint.rectTransform.sizeDelta=Vector2.one*(heavy?245:195);
                if(look==ImpactLook.Stone)paint.rectTransform.anchoredPosition=Vector2.down*28;
            }
            for(float t=0;t<BattleFeedback.ContactLifetime;t+=Time.unscaledDeltaTime)
            {
                progress=t/BattleFeedback.ContactLifetime;
                if(paint!=null)
                {
                    int frame=Mathf.Min(15,8+Mathf.FloorToInt(progress*8));
                    paint.sprite=frames[frame];paint.color=Alpha(Color.white,1-Ease(Mathf.InverseLerp(.65f,1,progress)));
                }
                SetVerticesDirty();
                yield return null;
            }
            Destroy(gameObject);
        }

        private void DrawLocalImpact(VertexHelper mesh,ImpactLook look)
        {
            float fade=1-Ease(progress);
            float scale=heavyImpact?1.22f:1;
            var center=rectTransform.rect.center;
            if(look==ImpactLook.Guard)
            {
                var steel=Alpha(new Color(.37f,.82f,1),fade);
                Arc(mesh,center,(28+17*progress)*scale,-1.18f,1.18f,6,steel,1.20f);
                Arc(mesh,center,(32+17*progress)*scale,-1.02f,1.02f,2,Alpha(Color.white,fade),1.20f);
                for(int i=-1;i<=1;i++)
                    Diamond(mesh,center+new Vector2((34+23*progress)*scale,i*(16+22*progress)),4,5,steel);
                return;
            }

            var tint=impactTint??(look==ImpactLook.Reaction?new Color(.72f,.53f,1):
                look==ImpactLook.Magic?new Color(.33f,.86f,1):new Color(1,.63f,.31f));
            var bright=Alpha(new Color(1,.96f,.84f),fade);
            if(look==ImpactLook.Strike)
            {
                // A blade-shaped sweep crosses the contact and sheds a short steel afterimage.
                float sweep=1-Mathf.Pow(1-progress,3);
                var hub=center+new Vector2(-31,35)*scale;
                float lead=Mathf.Lerp(-1.9f,.58f,sweep);
                Arc(mesh,hub,68*scale,lead-1.08f*fade,lead,11*fade*scale,new Color(.65f,.80f,.91f,fade),.74f);
                Arc(mesh,hub,73*scale,lead-.81f*fade,lead,3*fade*scale,bright,.74f);
            }
            if(look==ImpactLook.Arcane)
            {
                // A pointed crystal collapses at contact; two splinters continue along its axis.
                var axis=new Vector2(.94f,.34f);
                var side=new Vector2(-axis.y,axis.x);
                var head=center+axis*(12+26*progress)*scale;
                var tail=center-axis*(62*fade)*scale;
                var flank=center+side*12*fade*scale;
                var amethyst=Alpha(new Color(.68f,.48f,1),fade);
                Quad(mesh,tail,flank,head,center-side*12*fade*scale,amethyst);
                Line(mesh,tail,head,4*fade,bright);
                for(int i=-1;i<=1;i+=2)
                {
                    var shard=head+(axis*30+side*i*36)*progress*scale;
                    Line(mesh,shard-axis*12*fade,shard,4*fade,amethyst);
                }
            }
            if(look==ImpactLook.Lightning)
            {
                var top=center+new Vector2(-21,84)*scale;
                var elbow=center+new Vector2(12,41)*scale;
                var fork=center+new Vector2(-5,38)*scale;
                Line(mesh,top,elbow,8*fade,Alpha(tint,fade));Line(mesh,elbow,fork,8*fade,Alpha(tint,fade));
                Line(mesh,fork,center+Vector2.down*20,7*fade,bright);
                Line(mesh,elbow,center+new Vector2(39,19)*scale,3*fade,bright);
            }
            if(look==ImpactLook.Frost||look==ImpactLook.Flame)
            {
                bool fire=look==ImpactLook.Flame;
                for(int i=0;i<7;i++)
                {
                    float angle=i*2.4f+.6f;
                    var axis=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                    var tip=center+axis*(16+progress*66)*scale+Vector2.up*(fire?48*progress*progress:0);
                    var wing=new Vector2(-axis.y,axis.x)*(fire?8:5)*fade*scale;
                    var tail=tip-axis*(fire?18:32)*fade*scale;
                    var mid=Vector2.Lerp(tail,tip,.6f);
                    Quad(mesh,tail,mid+wing,tip,mid-wing,Alpha(tint,fade));
                    Line(mesh,mid,tip,3*fade,bright);
                }
            }
            if(look==ImpactLook.Holy)
            {
                var light=Alpha(new Color(1,.91f,.60f),fade);
                Diamond(mesh,center,6*scale,(34+36*progress)*scale,light);
                Diamond(mesh,center,(23+20*progress)*scale,4*scale,light);
                Arc(mesh,center,(20+22*progress)*scale,0,Mathf.PI*2,3*fade,light,.48f);
            }
            if(look==ImpactLook.Strike)
            {
                var axis=new Vector2(.68f,.73f);
                float sweep=1-Mathf.Pow(1-progress,4);
                var from=center+axis*Mathf.Lerp(-59,34,sweep)*scale;
                var to=center+axis*Mathf.Lerp(14,83,sweep)*scale;
                Line(mesh,from,to,17*fade*scale,Alpha(BattleUi.Ink,fade));
                Line(mesh,from,to,10*fade*scale,Alpha(tint,fade));
                Line(mesh,from,to,4*fade*scale,bright);
            }
            else if(look==ImpactLook.Reaction)
            {
                float radius=(18+29*progress)*scale;
                Arc(mesh,center,radius,.18f,2.65f,4,Alpha(tint,fade),.75f);
                Arc(mesh,center,radius,3.32f,5.75f,4,Alpha(tint,fade),.75f);
            }
            if(look==ImpactLook.Magic)
            {
                float burst=1-Mathf.Pow(1-progress,3);
                for(int i=0;i<3;i++)
                {
                    float start=i*Mathf.PI*2/3+burst*.35f;
                    Arc(mesh,center,(12+42*burst)*scale,start,start+1.35f,5*fade,Alpha(tint,fade),.85f);
                }
            }
            float flash=1-Mathf.SmoothStep(0,1,Mathf.Min(1,progress/.36f));
            Diamond(mesh,center,(8+12*flash)*scale,(10+22*flash)*scale,Alpha(BattleUi.Ink,fade));
            Diamond(mesh,center,(6+10*flash)*scale,(8+18*flash)*scale,bright);
            for(int i=0;i<6;i++)
            {
                float angle=i*2.4f+.33f;
                var axis=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                var tip=center+axis*(17+40*progress)*scale;
                Line(mesh,tip-axis*(7+8*fade)*scale,tip,3+2*fade,Alpha(tint,fade));
            }
        }
        private static Color Tint(ElementType e)
        {
            switch(e)
            {
                case ElementType.Wind:return new Color(.55f,1,.84f);
                case ElementType.Water:case ElementType.Ice:return new Color(.40f,.86f,1);
                case ElementType.Poison:return new Color(.66f,.95f,.30f);
                case ElementType.Fire:case ElementType.Explosion:return new Color(1,.43f,.13f);
                case ElementType.Lightning:return new Color(.71f,.72f,1);
                default:return new Color(1,.76f,.42f);
            }
        }
        internal static BattleVfx CreateCardRelease(RectTransform face,string name)
        {
            // A sibling of the face keeps sparks visible as the card's CanvasGroup fades.
            var rect=BattleUi.Rect(face.parent,name,face.anchorMin,face.anchorMax);
            rect.pivot=face.pivot;
            rect.sizeDelta=face.sizeDelta;
            rect.anchoredPosition=face.anchoredPosition;
            return rect.gameObject.AddComponent<BattleVfx>();
        }

        internal void UpdateCardRelease(float phase,Color tint,bool exhaust,bool entering=false)
        {
            cardRelease=true;
            cardExhaust=exhaust;
            cardEntering=entering;
            progress=phase;
            ink=tint;
            raycastTarget=false;
            SetVerticesDirty();
        }

        private void DrawCardRelease(VertexHelper mesh)
        {
            var face=rectTransform.rect;
            float light=1-Ease(Mathf.InverseLerp(.12f,1,progress));
            var key=Alpha(ink,light);
            var bottomLeft=new Vector2(face.xMin,face.yMin);
            var bottomRight=new Vector2(face.xMax,face.yMin);
            var topLeft=new Vector2(face.xMin,face.yMax);
            var topRight=new Vector2(face.xMax,face.yMax);
            Line(mesh,bottomLeft,topLeft,4*light,key);
            Line(mesh,topLeft,topRight,4*light,key);
            Line(mesh,topRight,bottomRight,4*light,key);
            Line(mesh,bottomRight,bottomLeft,4*light,key);
            float sweep=Mathf.Lerp(face.yMin+12,face.yMax-8,Ease(progress));
            var start=new Vector2(face.xMin+8,sweep-8);
            var end=new Vector2(face.xMax-8,sweep+8);
            Line(mesh,start,end,7+14*light,Alpha(ink,.42f*light));
            Line(mesh,start,end,2+3*light,Alpha(Color.Lerp(ink,Color.white,.58f),light));
            float drift=(cardEntering?1-Ease(progress):Ease(progress))*(cardExhaust?76:46);
            for(int i=0;i<14;i++)
            {
                float outward=i%2==0?1:-1;
                float offset=(i*37%19)/19f;
                var point=face.center+new Vector2(outward*(face.width*(.32f+offset*.18f)+drift*(.5f+offset)),
                    Mathf.Lerp(face.yMin+12,face.yMax-12,(i+.5f)/14)+(cardEntering?1:-1)*drift*offset);
                float size=(3+5*offset)*light;
                Line(mesh,point-new Vector2(outward*size*2,size),point,Mathf.Max(1,size*.45f),Alpha(ink,.7f*light));
                Diamond(mesh,point,size,size*(1+offset),key);
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if(cardRelease){DrawCardRelease(vh);return;}
            if(localImpact.HasValue){DrawLocalImpact(vh,localImpact.Value);return;}
            if(enemyIntent!=null){DrawEnemyFeedback(vh);return;}
            if(definition==null)return;
            if(paintedFrames!=null){if(enhanced)DrawWarriorFeedback(vh);return;}
            if(IsSignature(definition.Id))
            {
                CaptureSignatureAnchors();
                if(definition.Id=="earth_strike")DrawEarthSignature(vh);
                else if(definition.Id=="fire_bolt")DrawFireSignature(vh);
                else DrawLightningSignature(vh);
                return;
            }
            CaptureSignatureAnchors();
            if(definition.RequiresEnemyTarget)
            {
                DrawTargetedCard(vh);
                return;
            }
            source=emitter;
            target=impactPoint;
            var c=Alpha(ink,1-Ease(Mathf.InverseLerp(.74f,1,progress)));
            var core=Color.Lerp(c,Color.white,.65f);
            core.a=c.a;
            DrawSupport(vh,progress,c,core);
        }
        private void DrawSupport(VertexHelper vh,float p,Color c,Color core)
        {
            float fade=c.a;
            if(definition.Type!=CardType.Attack&&definition.Effects.Any(e=>e.Type==EffectType.Block))
            {
                float bloom=Ease(Mathf.InverseLerp(.07f,Contact,p));
                var center=source+Vector2.right*48;
                Diamond(vh,center,74*bloom,120*bloom,Alpha(c,.10f));
                Arc(vh,center,90*bloom,-1.30f,1.30f,11,c,1.15f);
                Arc(vh,center,105*bloom,-1.30f,1.30f,3,Alpha(core,.65f),1.15f);
                if(p>=Contact)Debris(vh,center,(p-Contact)/(1-Contact),8,90,Alpha(c,.65f),false);
            }
            if(CurrentStyle==Style.StoneWall||CurrentStyle==Style.Fortress||CurrentStyle==Style.Retaliate)
            {
                int count=CurrentStyle==Style.Fortress?5:3;float rise=Mathf.Sin(Mathf.Min(1,p*2)*Mathf.PI*.5f);
                for(int i=0;i<count;i++)
                {var pos=source+new Vector2(95+(i-count*.5f)*43,-100+rise*65);Diamond(vh,pos,33,80+(i%2)*20,new Color(.34f,.30f,.25f,fade));Line(vh,pos+new Vector2(-20,50),pos+new Vector2(0,76),8,c);}
            }
            if(CurrentStyle==Style.FrostShield)
            {Arc(vh,source,125,.2f,6.1f,14,c,.95f);for(int i=0;i<6;i++){float a=i*Mathf.PI/3;Diamond(vh,source+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*125,16,35,core);}}
            if(CurrentStyle==Style.EarthAura||CurrentStyle==Style.Heal||CurrentStyle==Style.Ward||CurrentStyle==Style.BlastShield)
            {Arc(vh,source,85+45*p,0,Mathf.PI*2,9,c,1);Arc(vh,source+Vector2.down*100,95+55*p,0,Mathf.PI*2,8,c,.25f);}
            if(p>=Contact&&definition.Effects.Any(e=>e.Type==EffectType.Heal))
            {
                var green=new Color(.4f,1,.65f,fade);
                for(int i=0;i<5;i++){var pos=source+new Vector2((i-2)*35,-80+p*160+(i%2)*35);Line(vh,pos-Vector2.up*12,pos+Vector2.up*12,7,green);Line(vh,pos-Vector2.right*12,pos+Vector2.right*12,7,green);}
                Arc(vh,source+Vector2.down*95,35+Ease((p-Contact)/(1-Contact))*130,0,Mathf.PI*2,8,Alpha(green,1-p),.3f);
            }
            if(definition.Effects.Any(e=>e.Type==EffectType.Draw))
            {
                for(int i=0;i<3;i++){var pos=source+new Vector2(-60+i*35,110+Mathf.Sin(p*Mathf.PI)*50+i*15);Quad(vh,pos+new Vector2(-17,-24),pos+new Vector2(-17,24),pos+new Vector2(17,24),pos+new Vector2(17,-24),c);Line(vh,pos+new Vector2(-8,8),pos+new Vector2(8,8),4,core);}
                Arc(vh,source,95,-2+p*4,1+p*4,8,c,.7f);
                if(p>=Contact)
                {
                    var rect=rectTransform.rect;var destination=rect.min+Vector2.Scale(rect.size,new Vector2(.50f,.21f));
                    for(int i=0;i<3;i++){float flight=Ease(Mathf.InverseLerp(Contact+i*.045f,.95f,p));var pos=Vector2.Lerp(source,destination,flight)+Vector2.up*Mathf.Sin(flight*Mathf.PI)*65;Quad(vh,pos+new Vector2(-12,-18),pos+new Vector2(-12,18),pos+new Vector2(12,18),pos+new Vector2(12,-18),Alpha(core,1-flight));}
                }
            }
            if(CurrentStyle==Style.Power||CurrentStyle==Style.Charge)
            {
                for(int i=0;i<7;i++){var pos=source+new Vector2((i-3)*33,-110+p*230);Line(vh,pos+new Vector2(-12,-20),pos,9,c);Line(vh,pos+new Vector2(12,-20),pos,9,core);}
                Arc(vh,source,65+60*p,0,Mathf.PI*2,12,c,1);
                if(CurrentStyle==Style.Charge)for(int i=0;i<6;i++){float a=i*Mathf.PI/3+p*7;var pos=source+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*105;Diamond(vh,pos,17,17,core);}
                if(CurrentStyle==Style.Charge&&p>=Contact)
                {
                    var rect=rectTransform.rect;var destination=rect.min+Vector2.Scale(rect.size,new Vector2(.10f,.15f));
                    for(int i=0;i<7;i++){float flight=Ease(Mathf.InverseLerp(Contact+i*.025f,.96f,p));var pos=Vector2.Lerp(source,destination,flight)+Vector2.right*Mathf.Sin(flight*Mathf.PI)*55;Diamond(vh,pos,6,12,Alpha(core,1-flight));}
                }
            }
            if(CurrentStyle==Style.PoisonCloud||(p>=Contact&&definition.Effects.Any(e=>e.Type==EffectType.Poison)))
            {
                for(int i=0;i<11;i++){var pos=target+new Vector2(Mathf.Sin(i*2.3f+p*3)*100,Mathf.Cos(i*1.7f)*70+p*30);Diamond(vh,pos,25+i%3*9,20+i%2*10,new Color(.4f,.75f,.15f,fade*.65f));}
            }
            if(definition.Effects.Any(e=>e.Type==EffectType.Weak)&&p>=Contact)
            {for(int i=0;i<3;i++){var pos=target+new Vector2((i-1)*35,115-p*40);Line(vh,pos+new Vector2(-10,15),pos,5,c);Line(vh,pos+new Vector2(10,15),pos,5,c);}}
            if(combo&&p>=Contact){Arc(vh,target,70+p*90,0,Mathf.PI*2,16,core,1);}
            if(CurrentStyle==Style.PoisonCloud)
                for(int i=0;i<4;i++)Arc(vh,target+new Vector2((i-2)*25,-15+p*50),30+p*30,i+p*4,i+p*4+4,7,Alpha(c,.30f),.65f);
            if(CurrentStyle==Style.Draw||CurrentStyle==Style.Charge||CurrentStyle==Style.Heal||CurrentStyle==Style.Power||CurrentStyle==Style.EarthAura)
            {
                for(int i=0;i<8;i++){float a=i*2.4f+p*5,r=100*(1-p)+20;var pos=source+new Vector2(Mathf.Cos(a)*r,Mathf.Sin(a)*r+p*55);Diamond(vh,pos,3,6,Alpha(core,.7f));}
                Arc(vh,source+Vector2.down*105,35+Ease(p)*120,0,Mathf.PI*2,5,Alpha(c,(1-p)*.7f),.22f);
            }
        }
        private static float Ease(float t)=>t*t*(3-2*t);
        private static void ImpactCrown(VertexHelper vh,Vector2 center,float age,Color c,Color core,bool ground)
        {
            // A dark cutout makes bright pixels readable even against the sunlit stone background.
            float snap=1-Ease(Mathf.Clamp01(age/.34f));
            if(snap>0)
            {
                for(int i=0;i<8;i++)
                {
                    float angle=i*Mathf.PI/4+.22f;
                    var dir=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle)*(ground?.4f:1));
                    float start=10+age*90,length=(i%2==0?100:65)*snap;
                    Line(vh,center+dir*start,center+dir*(start+length),15*snap,new Color(.015f,.02f,.03f,snap*.85f));
                    Line(vh,center+dir*start,center+dir*(start+length),8*snap,Alpha(c,snap));
                    Line(vh,center+dir*start,center+dir*(start+length*.8f),3,Alpha(core,snap));
                }
                Diamond(vh,center,23*snap,ground?12*snap:32*snap,Alpha(core,snap));
            }
            float ring=Ease(Mathf.Clamp01(age*3));
            Arc(vh,center,32+ring*135,0,Mathf.PI*2,9*(1-age),Alpha(c,(1-age)*.7f),ground?.24f:.68f);
            // Delayed second ring and sparks carry the hit into a readable afterglow.
            if(age>.10f)Arc(vh,center,25+Ease(Mathf.Clamp01((age-.10f)*2))*105,0,Mathf.PI*2,3,Alpha(core,(1-age)*.45f),ground?.24f:.68f);
            Debris(vh,center,age,14,190,Alpha(c,.75f),ground);
        }
        private static Color Alpha(Color c,float opacity){c.a*=Mathf.Clamp01(opacity);return c;}
        private static void Ribbon(VertexHelper vh,Vector2 from,Vector2 to,float phase,float amplitude,float width,Color c)
        {
            const int segments=14;var last=from;
            for(int i=1;i<=segments;i++)
            {float t=i/(float)segments;var next=Vector2.Lerp(from,to,t)+Vector2.up*Mathf.Sin(t*6.28f-phase*14)*Mathf.Sin(t*Mathf.PI)*amplitude;Line(vh,last,next,width*t,c);last=next;}
        }
        private static void Debris(VertexHelper vh,Vector2 origin,float age,int count,float speed,Color c,bool gravity)
        {
            // Index-derived directions: never consume the Run's random stream.
            for(int i=0;i<count;i++)
            {
                float angle=i*2.39996f,velocity=speed*(.5f+(i%5)*.12f);
                var delta=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*velocity*age;
                if(gravity)delta.y=Mathf.Abs(Mathf.Sin(angle))*velocity*age-age*age*180;
                var pos=origin+delta;float size=(3+i%4)*(1-age*.65f);
                var tint=Alpha(c,(1-age)*(i%3==0?.4f:1));
                Line(vh,pos-delta.normalized*(gravity?4:18)*(1-age),pos,Mathf.Max(2,size),Alpha(tint,.4f));
                Diamond(vh,pos,size,gravity?size*.7f:size,tint);
            }
        }
        private static Sprite[] LoadWarriorFrames(string id)
        {
            if(!warriorClips.TryGetValue(id,out var frames))
            {
                frames=Resources.LoadAll<Sprite>("BrodunWarriorVfx/"+id).OrderBy(s=>s.name,StringComparer.Ordinal).ToArray();
                if(frames.Length!=16)
                {
                    Debug.LogWarning("BRODUN: expected 16 painted VFX frames for "+id+", got "+frames.Length+". Using existing renderer.");
                    return null;
                }
                warriorClips.Add(id,frames);
            }
            return frames;
        }
        private void PreparePaintedClip(string id)
        {
            if(!HasWarriorClip(id))return;
            paintedFrames=LoadWarriorFrames(id);
            if(paintedFrames==null)return;
            // Above the hand scrim, but clipped below HP/Intent and above card text/Energy.
            var parentCanvas=GetComponentInParent<Canvas>();
            var canvas=gameObject.AddComponent<Canvas>();canvas.overrideSorting=true;
            canvas.sortingLayerID=parentCanvas!=null?parentCanvas.sortingLayerID:0;canvas.sortingOrder=(parentCanvas!=null?parentCanvas.sortingOrder:0)+5;
            paintedViewport=BattleUi.Rect(transform,"Painted battle viewport",new Vector2(.02f,id=="wind_read"?DrawLandingY-.03f:HandPresentation.HoverTop),new Vector2(.98f,.81f));
            paintedViewport.gameObject.AddComponent<RectMask2D>();
            paintedEffect=BattleUi.Image(paintedViewport,"Painted "+id,null,Color.white,new Vector2(.5f,.5f),new Vector2(.5f,.5f));
            paintedEffect.preserveAspect=true;
            // Fixed canvas registration, not Sprite pivot guessing or per-frame SetNativeSize.
            paintedEffect.rectTransform.pivot=id=="earth_strike"||id=="stone_guard"?new Vector2(.5f,.10f):new Vector2(.5f,.5f);
            if(id=="earth_strike"||id=="wind_cut")
                for(int i=0;i<3;i++)
                {
                    var trail=BattleUi.Image(paintedViewport,"Painted wake "+i,null,Color.white,new Vector2(.5f,.5f),new Vector2(.5f,.5f));
                    trail.preserveAspect=true;trail.rectTransform.pivot=paintedEffect.rectTransform.pivot;
                    trail.transform.SetAsFirstSibling();paintedTrails.Add(trail);
                }
        }
        private int PaintedFrame(float phase,float contact)
        {
            float holdEnd=contact+paintedHold/duration;
            if(enhanced&&definition.Id=="earth_strike"&&phase<contact)
                return Mathf.Clamp(4+Mathf.FloorToInt(Mathf.InverseLerp(contact-.07f,contact,phase)*4),4,7);
            if(enhanced&&definition.Id=="wind_cut"&&phase<contact)
                return Mathf.Clamp(Mathf.FloorToInt(Mathf.InverseLerp(BattleFeedback.WarriorRelease("wind_cut")/duration,contact,phase)*8),0,8);
            if(definition.Id=="earth_strike"&&phase<contact)
                return phase<.25f?Mathf.Clamp(Mathf.FloorToInt(phase/.25f*4),0,3):4+Mathf.Clamp(Mathf.FloorToInt((phase-.25f)/(contact-.25f)*4),0,3);
            if(phase<contact)return Mathf.Clamp(Mathf.FloorToInt(phase/contact*paintedPeak),0,paintedPeak-1);
            if(phase<holdEnd)
                return definition.Id=="wind_cut"&&phase>contact+(holdEnd-contact)*.5f?10:paintedPeak;
            int first=definition.Id=="wind_cut"?11:paintedPeak+1;
            return Mathf.Clamp(first+Mathf.FloorToInt((phase-holdEnd)/(1-holdEnd)*(16-first)),first,15);
        }
        private void PlacePainted(Image image,int frame,Vector2 point,Vector2 size,float opacity)
        {
            image.sprite=paintedFrames[Mathf.Clamp(frame,0,15)];image.color=new Color(1,1,1,Mathf.Clamp01(opacity));
            // Convert stage-local coordinates to this fixed-center child; no screen-resolution constants.
            image.rectTransform.position=rectTransform.TransformPoint(point);
            image.rectTransform.sizeDelta=size;
        }
        private void UpdatePaintedClip(float contact)
        {
            CaptureSignatureAnchors();float p=progress;
            int frame=PaintedFrame(p,contact);float fade=1-Ease(Mathf.InverseLerp(.88f,1,p));
            if(definition.Id=="earth_strike")
            {
                if(enhanced)
                {
                    if(p*duration<=BattleFeedback.WarriorRelease(definition.Id))castFoot.x=FootPoint(casterRect,castFoot).x;
                    // Eruption is rooted at the target. The mesh carries the moving fissure;
                    // translating a complete rock mound across the floor looked like a sticker.
                    float grow=Ease(Mathf.InverseLerp(contact-.065f,contact+.045f,p));
                    PlacePainted(paintedEffect,frame,victimFoot,Vector2.one*Mathf.Lerp(290,590,grow),fade*grow);
                    for(int i=0;i<paintedTrails.Count;i++)paintedTrails[i].color=Color.clear;
                    return;
                }
                // Charge follows the advancing actor; the ground baseline never bobs.
                if(p<.25f)castFoot.x=FootPoint(casterRect,castFoot).x;
                float travel=Ease(Mathf.InverseLerp(.25f,contact,p));
                var start=castFoot+Vector2.right*45;var point=Vector2.Lerp(start,victimFoot,travel);
                float size=Mathf.Lerp(430,640,Ease(Mathf.InverseLerp(.15f,contact,p)));
                PlacePainted(paintedEffect,frame,point,Vector2.one*size,fade);
                for(int i=0;i<paintedTrails.Count;i++)
                {
                    float t=(i+1)/4f;float seen=Mathf.Clamp01((travel-t)*8);
                    PlacePainted(paintedTrails[i],Mathf.Min(3,frame),Vector2.Lerp(start,victimFoot,t),new Vector2(330,330),seen*fade*.70f);
                }
            }
            else if(definition.Id=="stone_guard")
            {
                // This is a shield beside the body, not a projectile or enemy impact.
                var foot=FootPoint(casterRect,castFoot);
                PlacePainted(paintedEffect,frame,foot+Vector2.right*120,Vector2.one*(enhanced?530:600),fade);
            }
            else if(definition.Id=="wind_cut")
            {
                // Readable crescent launch followed by acceleration; frame 9 is at the target.
                float launch=enhanced?BattleFeedback.WarriorRelease("wind_cut")/duration:.09f;
                if(enhanced&&p<=launch)emitter=ActorPoint(casterRect,new Vector2(.88f,.56f),emitter);
                float travel=Mathf.Clamp01(Mathf.InverseLerp(launch,contact,p));travel=travel*travel;
                if(enhanced&&p<launch){PlacePainted(paintedEffect,frame,emitter,Vector2.one*180,p/launch*.55f);return;}
                PlacePainted(paintedEffect,frame,Vector2.Lerp(emitter,impactPoint,travel),Vector2.one*560,fade);
                for(int i=0;i<paintedTrails.Count;i++)
                {
                    float lag=Mathf.Max(0,p-(i+1)*(enhanced?.025f:.045f)),t=Mathf.InverseLerp(launch,contact,lag);t*=t;
                    float wakeFade=1-Ease(Mathf.InverseLerp(contact,contact+.20f,p));
                    PlacePainted(paintedTrails[i],Mathf.Min(7,PaintedFrame(lag,contact)),Vector2.Lerp(emitter,impactPoint,t),Vector2.one*510,fade*wakeFade*(.24f-i*.065f)*(enhanced&&lag<launch?0:1));
                }
            }
            else
            {
                var r=rectTransform.rect;var hand=r.min+Vector2.Scale(r.size,new Vector2(.50f,DrawLandingY));
                float deliver=Ease(Mathf.InverseLerp(contact+paintedHold/duration,.90f,p));
                var point=Vector2.Lerp(castFoot+Vector2.up*195,hand,deliver)+Vector2.up*Mathf.Sin(deliver*Mathf.PI)*45;
                float size=Mathf.Lerp(430,145,deliver);
                PlacePainted(paintedEffect,frame,point,Vector2.one*size,fade);
            }
        }
        private Vector2 ActorPoint(RectTransform actor,Vector2 normalized,Vector2 fallback)
        {
            if(actor==null)return fallback;
            var local=actor.rect.min+Vector2.Scale(actor.rect.size,normalized);
            return rectTransform.InverseTransformPoint(actor.TransformPoint(local));
        }
        private Vector2 FootPoint(RectTransform actor,Vector2 fallback)
        {return ActorPoint(actor,new Vector2(actor!=null?actor.pivot.x:.5f,0),fallback);}
        private void CaptureSignatureAnchors()
        {
            var r=rectTransform.rect;
            var left=r.min+Vector2.Scale(r.size,new Vector2(.25f,BattleUi.FloorY));
            var right=r.min+Vector2.Scale(r.size,new Vector2(.77f,BattleUi.FloorY));
            if(!anchorsCaptured)
            {
                castFoot=FootPoint(casterRect,left);
                emitter=ActorPoint(casterRect,new Vector2(.76f,.50f),left+Vector2.up*210);
                victimFoot=FootPoint(victimRect,right);
                impactPoint=ActorPoint(victimRect,new Vector2(.43f,.48f),right+Vector2.up*200);
                anchorsCaptured=true;
            }
            // Follow the hand during charge only, then lock the launch point for a stable trajectory.
            if(progress<.14f)emitter=ActorPoint(casterRect,new Vector2(.76f,.50f),emitter);
        }
        private void DrawWarriorFeedback(VertexHelper vh)
        {
            CaptureSignatureAnchors();float p=progress,seconds=p*duration;
            float tail=1-Ease(Mathf.InverseLerp(.75f,1,p));
            var soil=new Color(.62f,.43f,.25f,tail);var glow=new Color(1,.81f,.48f,tail);
            var wind=new Color(.31f,.91f,.79f,tail);var white=new Color(.82f,1,.94f,tail);
            if(definition.Id=="earth_strike")
            {
                float release=BattleFeedback.WarriorRelease(definition.Id);
                float travel=Ease(Mathf.InverseLerp(release,paintedContact*duration,seconds));
                var foot=FootPoint(casterRect,castFoot);
                Dust(vh,foot,Mathf.Clamp01(seconds/.5f),10,100,Mathf.Sin(Mathf.Clamp01(seconds/.6f)*Mathf.PI)*.65f);
                var start=castFoot+Vector2.right*60;
                for(int i=0;i<20;i++)
                {
                    if(travel<i/20f)break;
                    var a=Vector2.Lerp(start,victimFoot,i/20f)+new Vector2(0,(Noise(i+7)-.5f)*24);
                    var b=Vector2.Lerp(start,victimFoot,(i+1)/20f)+new Vector2(0,(Noise(i+8)-.5f)*24);
                    Line(vh,a,b,18,new Color(.06f,.035f,.02f,tail*.9f));Line(vh,a,b,5,soil);
                    float fresh=1-Mathf.Clamp01((travel-i/20f)*3);
                    Line(vh,a,b,2,Alpha(glow,fresh));
                    if(i%3==0)
                    {
                        var branch=b+new Vector2(-20,(i%2==0?1:-1)*(18+Noise(i+60)*19));
                        Line(vh,b,branch,7,new Color(.09f,.06f,.035f,tail));Line(vh,b,branch,2,soil);
                        Dust(vh,b,Mathf.Clamp01((travel-i/20f)*2),3,45,tail*.4f);
                    }
                }
                if(p<paintedContact)return;
                float age=(p-paintedContact)/(1-paintedContact);
                for(int i=0;i<15;i++)
                {
                    float local=Mathf.Max(0,age-Noise(i+100)*.12f);
                    var delta=new Vector2((Noise(i+120)-.5f)*380*local,local*(260+Noise(i+140)*240)-local*local*600);
                    delta.y=Mathf.Max(-8,delta.y);
                    Rock(vh,victimFoot+delta,(6+Noise(i+160)*8)*(1-age),12*(1-age),Alpha(soil,1-age));
                }
                Dust(vh,victimFoot,age,20,230,(1-age)*.55f);
                if(age<.23f)ImpactCrown(vh,victimFoot,age*2,soil,glow,true);
            }
            else if(definition.Id=="stone_guard")
            {
                var center=FootPoint(casterRect,castFoot)+new Vector2(120,110);
                float gather=Ease(Mathf.Clamp01(p/paintedContact));
                for(int i=0;i<11;i++)
                {
                    float angle=i*2.4f,remain=1-gather;
                    var position=center+new Vector2(Mathf.Cos(angle)*(60+remain*100),Mathf.Sin(angle)*70-remain*70);
                    Rock(vh,position,(4+i%3*2)*(1-p),8,Alpha(soil,Mathf.Sin(gather*Mathf.PI)));
                }
                Dust(vh,center+Vector2.down*110,p,12,170,tail*.40f);
                if(p>=paintedContact)
                {
                    float lockAge=(p-paintedContact)*duration;
                    float shine=Mathf.Max(0,1-lockAge/.28f);
                    Line(vh,center+new Vector2(-52,-66),center+new Vector2(0,60),4,Alpha(glow,shine));
                    Line(vh,center+new Vector2(0,60),center+new Vector2(54,-66),4,Alpha(glow,shine));
                }
            }
            else if(definition.Id=="wind_cut")
            {
                float launch=BattleFeedback.WarriorRelease(definition.Id);
                if(seconds<launch)
                {
                    var weapon=ActorPoint(casterRect,new Vector2(.88f,.56f),emitter);
                    float charge=seconds/launch;
                    for(int i=0;i<5;i++)Line(vh,weapon+new Vector2(-70-i*9,(i-2)*10)*(1-charge),weapon,2,Alpha(wind,charge*.6f));
                }
                if(p>=paintedContact)
                {
                    float age=(p-paintedContact)/(1-paintedContact),cut=1-Ease(Mathf.Clamp01(age/.45f));
                    for(int i=0;i<2;i++)
                    {
                        var axis=new Vector2(.85f,i==0?.65f:-.65f);
                        Line(vh,impactPoint-axis*(70+age*90),impactPoint+axis*(100+age*110),10*cut,Alpha(wind,cut));
                        Line(vh,impactPoint-axis*50,impactPoint+axis*80,3,Alpha(white,cut));
                    }
                    Debris(vh,impactPoint,age,17,210,wind,false);
                }
            }
            else // Support only: orbit -> card-shaped light -> actual hand delivery callback.
            {
                var center=castFoot+Vector2.up*170;
                float flight=Ease(Mathf.InverseLerp(.50f,.90f,p));
                var r=rectTransform.rect;var destination=r.min+Vector2.Scale(r.size,new Vector2(.5f,DrawLandingY));
                for(int i=0;i<12;i++)
                {
                    float angle=i*2.4f+p*9;
                    var swirl=center+new Vector2(Mathf.Cos(angle)*78,Mathf.Sin(angle)*48);
                    var position=Vector2.Lerp(swirl,destination,flight)+Vector2.up*Mathf.Sin(flight*Mathf.PI)*42;
                    Diamond(vh,position,3,6,Alpha(white,tail*(1-flight*.55f)));
                }
                if(flight>0&&p<.92f)
                {
                    var card=Vector2.Lerp(center,destination,flight)+Vector2.up*Mathf.Sin(flight*Mathf.PI)*42;
                    var a=card+new Vector2(-15,-23);var b=card+new Vector2(-11,24);var c=card+new Vector2(18,20);var d=card+new Vector2(14,-27);
                    Quad(vh,a,b,c,d,new Color(.06f,.22f,.23f,tail*.85f));
                    Line(vh,a,b,3,white);Line(vh,b,c,3,wind);Line(vh,c,d,3,white);Line(vh,d,a,3,wind);
                }
                if(p>=.90f)Arc(vh,destination,18+(p-.9f)*400,0,Mathf.PI*2,3,Alpha(wind,tail),.3f);
            }
        }
        private void DrawEnemyFeedback(VertexHelper vh)
        {
            CaptureSignatureAnchors();
            var c=enemyLook==EnemyAttackLook.Fire?new Color(1,.43f,.13f):enemyLook==EnemyAttackLook.Venom?new Color(.60f,.83f,.27f):enemyLook==EnemyAttackLook.Spark?new Color(.76f,.83f,1):enemyLook==EnemyAttackLook.Magic?new Color(.54f,.72f,1):new Color(.88f,.73f,.55f);
            float t=enemyClock;
            if(enemyIntent.Type!=IntentType.Attack)
            {
                var center=ActorPoint(casterRect,new Vector2(.5f,.45f),emitter);
                float gather=Ease(Mathf.InverseLerp(0,BattleFeedback.EnemyContact,t));
                float fade=1-Ease(Mathf.InverseLerp(BattleFeedback.EnemyContact+.08f,duration,t));
                if(enemyIntent.Type==IntentType.Block)
                {
                    var shield=new Color(.51f,.76f,.91f,fade);var top=center+Vector2.up*85;
                    var l=center+new Vector2(-67,45);var r=center+new Vector2(67,45);var bottom=center+Vector2.down*80;
                    Quad(vh,top,l,bottom,r,Alpha(shield,.11f*gather));
                    Line(vh,top,l,6,Alpha(shield,gather));Line(vh,l,bottom,6,Alpha(shield,gather));
                    Line(vh,bottom,r,6,Alpha(shield,gather));Line(vh,r,top,6,Alpha(shield,gather));
                    if(t>=BattleFeedback.EnemyContact)Debris(vh,center,(t-BattleFeedback.EnemyContact)/.4f,9,115,shield,false);
                }
                else if(enemyIntent.Type==IntentType.Buff)
                {
                    var aura=new Color(.91f,.50f,.94f,fade);
                    for(int i=0;i<11;i++)
                    {
                        float angle=i*2.4f+t*4;
                        var position=center+new Vector2(Mathf.Cos(angle)*(110-70*gather),Mathf.Sin(angle)*55+Mathf.Max(0,t-BattleFeedback.EnemyContact)*110);
                        Diamond(vh,position,4,9,aura);
                    }
                    if(t>=BattleFeedback.EnemyContact)
                    {
                        var up=center+Vector2.up*(t-BattleFeedback.EnemyContact)*100;
                        Line(vh,up+new Vector2(-22,-7),up+Vector2.up*18,6,aura);Line(vh,up+new Vector2(22,-7),up+Vector2.up*18,6,aura);
                    }
                }
                return;
            }
            // One contact per immutable result.Hits entry, never per animation frame.
            if(enemyLook==EnemyAttackLook.Claw||enemyLook==EnemyAttackLook.Heavy)return;
            int hits=enemyHitCount;
            for(int i=0;i<hits;i++)
            {
                float release=BattleFeedback.EnemyRelease+i*BattleFeedback.EnemyHitInterval;
                float hit=BattleFeedback.EnemyContact+i*BattleFeedback.EnemyHitInterval;
                if(t<release||t>=hit)continue;
                var aim=impactPoint+Vector2.up*(i%2==0?12:-12);
                float travel=Ease(Mathf.InverseLerp(release,hit,t));
                var start=emitter;var point=Vector2.Lerp(start,aim,travel);
                if(enemyLook==EnemyAttackLook.Spark)ElectricPath(vh,start,point,40+i,5,c);
                else
                {
                    float arc=enemyLook==EnemyAttackLook.Venom?42:20;
                    point+=Vector2.up*Mathf.Sin(travel*Mathf.PI)*arc;
                    Ribbon(vh,Vector2.Lerp(start,point,.65f),point,t,10,8,Alpha(c,.45f));
                    Diamond(vh,point,enemyLook==EnemyAttackLook.Fire?18:12,enemyLook==EnemyAttackLook.Venom?16:12,c);
                    Diamond(vh,point,6,7,Color.white);
                }
            }
        }
        private float Noise(int index)
        {
            uint n=unchecked(signatureSeed+(uint)index*747796405u);n=(n^(n>>16))*2246822519u;
            return (n&65535)/65535f;
        }
        private float SignatureAge=>impactClock<0?-1:Mathf.Clamp01((Time.unscaledTime-impactClock)/(duration*(1-SignatureContact)));
        private void Dust(VertexHelper vh,Vector2 center,float phase,int count,float spread,float opacity)
        {
            for(int i=0;i<count;i++)
            {
                float age=Mathf.Clamp01(phase-Noise(i)*.12f),fade=(1-age)*opacity;
                var p=center+new Vector2((Noise(i+30)-.5f)*spread*(.35f+age),8+Mathf.Sin(age*Mathf.PI)*(9+Noise(i+40)*15));
                Diamond(vh,p,12+Noise(i+10)*18+age*12,6+Noise(i+20)*8,new Color(.40f,.33f,.25f,fade));
            }
        }
        private static void Rock(VertexHelper vh,Vector2 p,float size,float lift,Color tint)
        {
            var a=p+new Vector2(-size,0);var b=p+new Vector2(-size*.7f,lift);
            var top=p+new Vector2(size*.25f,lift+size*.3f);var side=p+new Vector2(size*.9f,lift*.55f);
            Quad(vh,a,b,top,p+new Vector2(size*.1f,-size*.25f),tint);
            Quad(vh,top,side,p+new Vector2(size*.8f,-size*.15f),p+new Vector2(size*.1f,-size*.25f),new Color(tint.r*.55f,tint.g*.55f,tint.b*.55f,tint.a));
            Line(vh,b,top,4,new Color(.77f,.64f,.40f,tint.a));
        }
        private void DrawEarthSignature(VertexHelper vh)
        {
            float p=progress,age=SignatureAge,front=Ease(Mathf.InverseLerp(.055f,SignatureContact,p));
            var start=castFoot+Vector2.right*42;var end=victimFoot;
            Dust(vh,castFoot,p*2,9,110,Mathf.Clamp01(1-p*2)*.35f);
            // Each broken flagstone is revealed by the moving front, not a fixed ring at the enemy.
            const int segments=16;
            for(int i=0;i<segments;i++)
            {
                float u=i/(float)segments;if(u>=front)break;
                var a=Vector2.Lerp(start,end,u)+Vector2.up*(Noise(i+80)-.5f)*22;
                var b=Vector2.Lerp(start,end,Mathf.Min(front,(i+1f)/segments))+Vector2.up*(Noise(i+81)-.5f)*22;
                float fade=age<0?1:1-Ease(age);
                Line(vh,a,b,24,new Color(.055f,.045f,.035f,fade));
                Line(vh,a+Vector2.up*5,b+Vector2.up*5,6,new Color(.69f,.49f,.26f,fade));
                if(i%2==0)
                {
                    Line(vh,b,b+new Vector2(-15,Noise(i)> .5f?25:-22),5,new Color(.17f,.13f,.09f,fade));
                    float local=Mathf.Clamp01((front-u)*5);
                    Rock(vh,a+new Vector2(10,Mathf.Sin(local*Mathf.PI)*20),10,9,new Color(.38f,.34f,.27f,fade));
                }
            }
            var head=Vector2.Lerp(start,end,front);
            if(age<0){Dust(vh,head,front*.5f,7,75,.26f);return;}
            float settle=1-Ease(age);
            // Broad ground-hugging pressure front, interrupted by angular rays and stone silhouettes.
            for(int i=0;i<7;i++)
            {
                float direction=(i-3)/3f;var dir=new Vector2(direction,(i%2==0?1:-1)*.22f);
                Line(vh,end+dir*(18+age*35),end+dir*(55+Ease(age)*150),9*settle,new Color(.65f,.52f,.32f,settle*.65f));
                float vx=(i-3)*(28+age*28),launch=120+Noise(i+110)*110;
                float height=Mathf.Max(0,launch*age*3.3f-age*age*launch*3.1f);
                Rock(vh,end+new Vector2(vx,height),15+Noise(i+120)*11,22+Noise(i+130)*28,new Color(.40f,.36f,.29f,settle));
            }
            if(age<.13f){float punch=1-age/.13f;Line(vh,end+Vector2.left*70,end+Vector2.right*70,12*punch,new Color(.93f,.79f,.52f,punch));}
            Dust(vh,end,age,16,270,settle*.32f);
            for(int i=0;i<10;i++)
            {float a=age;var chip=end+new Vector2((Noise(i+150)-.5f)*280*a,Mathf.Max(-12,(90+Noise(i+160)*90)*a-190*a*a));Rock(vh,chip,4,6,new Color(.54f,.45f,.32f,settle));}
        }
        private Vector2 FirePath(float t)
        {return Vector2.Lerp(emitter,impactPoint,t)+Vector2.up*Mathf.Sin(t*Mathf.PI)*35;}
        private void DrawFireSignature(VertexHelper vh)
        {
            float p=progress,age=SignatureAge;
            var red=new Color(.72f,.12f,.055f);var orange=new Color(1,.37f,.06f);var yellow=new Color(1,.85f,.30f);var white=new Color(1,1,.80f);
            if(age<0)
            {
                float flight=Ease(Mathf.InverseLerp(.14f,SignatureContact,p));
                float charge=Ease(Mathf.Clamp01(p/.14f));var head=FirePath(flight);
                for(int i=13;i>=0;i--)
                {
                    float trail=Mathf.Max(0,flight-i*.027f);var tail=FirePath(trail)+Vector2.up*Mathf.Sin(p*34+i*2)*i*.7f;
                    float size=(34-i*1.8f)*charge;
                    Diamond(vh,tail,size*1.4f,size,new Color(red.r,red.g,red.b,1-i*.055f));
                    if(i<9)Diamond(vh,tail,size,size*.72f,orange);
                    if(i<4)Diamond(vh,tail,size*.68f,size*.45f,yellow);
                }
                Diamond(vh,head+Vector2.right*8,17*charge,11*charge,white);
                for(int i=0;i<8;i++){var mote=FirePath(Mathf.Max(0,flight-Noise(i)*.25f))+new Vector2(-10,Mathf.Sin(p*25+i)*25);Diamond(vh,mote,3,6,Alpha(yellow,.8f));}
                return;
            }
            float fade=1-Ease(age);
            // Tearing flame tongues, not a colored circular shockwave.
            for(int i=0;i<9;i++)
            {
                float a=i*2.39996f;var dir=new Vector2(Mathf.Cos(a),Mathf.Sin(a)*.7f);
                float distance=Ease(Mathf.Clamp01(age*2.4f))*(65+Noise(i+190)*65);
                var tip=impactPoint+dir*distance+Vector2.up*age*45;
                var basePoint=Vector2.Lerp(impactPoint,tip,.45f);
                Line(vh,basePoint,tip,28*fade,Alpha(red,fade));
                Line(vh,basePoint,tip,17*fade,Alpha(orange,fade));
                Diamond(vh,basePoint,12*fade,20*fade,Alpha(yellow,fade));
            }
            if(age<.11f)Diamond(vh,impactPoint,48*(1-age/.11f),60*(1-age/.11f),Alpha(white,1-age/.11f));
            for(int i=0;i<24;i++)
            {
                float angle=Noise(i+220)*Mathf.PI*2,speed=65+Noise(i+250)*140;
                var pos=impactPoint+new Vector2(Mathf.Cos(angle)*speed*age,Mathf.Sin(angle)*speed*age+45*age);
                Line(vh,pos-new Vector2(4,7)*fade,pos,4,Alpha(i%3==0?yellow:orange,fade));
            }
            for(int i=0;i<5;i++)Diamond(vh,impactPoint+new Vector2((i-2)*25,age*95),14+age*15,12+age*12,new Color(.13f,.10f,.12f,fade*.20f));
        }
        private void ElectricPath(VertexHelper vh,Vector2 from,Vector2 to,int branch,float width,Color tint)
        {
            var last=from;int segments=branch==0?11:5;
            for(int i=1;i<=segments;i++)
            {
                var next=Vector2.Lerp(from,to,i/(float)segments);
                if(i<segments)next+=new Vector2((Noise(i+branch*31)-.5f)*45,(Noise(i+branch*41)-.5f)*13);
                Line(vh,last,next,width*2.6f,Alpha(tint,.15f));Line(vh,last,next,width,tint);
                Line(vh,last,next,Mathf.Max(2,width*.30f),new Color(1,1,.82f,tint.a));last=next;
            }
        }
        private void DrawLightningSignature(VertexHelper vh)
        {
            float p=progress,age=SignatureAge;
            var blue=new Color(.20f,.60f,1);var pale=new Color(.78f,.94f,1);var gold=new Color(1,.90f,.42f);
            float top=Mathf.Min(rectTransform.rect.yMax-240,impactPoint.y+290);var origin=new Vector2(impactPoint.x+10,top);
            if(age<0)
            {
                // Only target-local ionization: there is no projectile coming from the caster.
                float charge=Ease(Mathf.InverseLerp(.12f,SignatureContact,p));
                for(int i=0;i<4;i++)
                {
                    var offset=new Vector2((i-1.5f)*32,(i%2==0?25:-25));
                    ElectricPath(vh,impactPoint+offset,impactPoint+offset+new Vector2(14,-20),i+7,2,Alpha(blue,charge*.7f));
                }
                if(p>SignatureContact-.045f)
                {float fall=Ease(Mathf.InverseLerp(SignatureContact-.045f,SignatureContact,p));ElectricPath(vh,origin,Vector2.Lerp(origin,impactPoint,fall),0,4,Alpha(pale,fall));}
                return;
            }
            float flash=Mathf.Clamp01(1-age/.10f),after=1-Ease(Mathf.Clamp01(age/.55f));
            if(after>0)
            {
                float pulse=age<.10f?1:.25f+.25f*Mathf.Abs(Mathf.Sin(age*60));
                ElectricPath(vh,origin,impactPoint,0,10,Alpha(pale,after*pulse));
                for(int i=0;i<4;i++)
                {var fork=Vector2.Lerp(origin,impactPoint,.22f+i*.15f);ElectricPath(vh,fork,fork+new Vector2((i%2==0?-1:1)*(45+Noise(i+290)*40),-55),i+1,4,Alpha(blue,after*pulse));}
            }
            if(flash>0)
            {
                Diamond(vh,impactPoint,36*flash,55*flash,new Color(.92f,1,1,flash));
                Line(vh,impactPoint-Vector2.right*65,impactPoint+Vector2.right*65,5,Alpha(gold,flash));
            }
            for(int i=0;i<9;i++)
            {
                float delay=Noise(i+310)*.10f,local=age-delay;if(local<0||local>.45f)continue;
                float a=i*2.39996f;var dir=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
                var start=impactPoint+dir*(25+local*45);var end=start+dir*(30+Noise(i+330)*40);
                ElectricPath(vh,start,end,i+12,3,Alpha(i%3==0?gold:blue,1-local/.45f));
            }
        }
        private static Vector2 Snap(Vector2 p)=>new Vector2(Mathf.Round(p.x/3)*3,Mathf.Round(p.y/3)*3);
        private static void Quad(VertexHelper v,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color color)
        {
            int n=v.currentVertCount;v.AddVert(Snap(a),color,Vector2.zero);v.AddVert(Snap(b),color,Vector2.zero);v.AddVert(Snap(c),color,Vector2.zero);v.AddVert(Snap(d),color,Vector2.zero);
            v.AddTriangle(n,n+1,n+2);v.AddTriangle(n,n+2,n+3);
        }
        private static void Line(VertexHelper v,Vector2 a,Vector2 b,float width,Color c)
        {var normal=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;Quad(v,a-normal,a+normal,b+normal,b-normal,c);}
        private static void Diamond(VertexHelper v,Vector2 p,float x,float y,Color c)
        {Quad(v,p+Vector2.left*x,p+Vector2.up*y,p+Vector2.right*x,p+Vector2.down*y,c);}
        private static void Arc(VertexHelper v,Vector2 p,float radius,float start,float end,float width,Color c,float squash)
        {
            const int segments=22;
            for(int i=0;i<segments;i++){float a=Mathf.Lerp(start,end,i/(float)segments),b=Mathf.Lerp(start,end,(i+1f)/segments);Line(v,p+new Vector2(Mathf.Cos(a),Mathf.Sin(a)*squash)*radius,p+new Vector2(Mathf.Cos(b),Mathf.Sin(b)*squash)*radius,width,c);}
        }
    }
}

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
    public sealed class BattleVfx : MaskableGraphic
    {
        public enum Style { Slash, Ground, Gust, Bolt, Shard, Flame, Wave, Venom, Burst, Ward, StoneWall, FrostShield, EarthAura, Heal, Draw, Power, Charge, PoisonCloud, Fortress, Retaliate, BlastShield }
        public Style CurrentStyle { get; private set; }
        private float progress;
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
        public IEnumerator Present(CardDefinition card,Action impact,bool conditional=false,Action<int> extraHit=null,int hitCount=1,Action delivered=null)
        {
            definition=card;combo=conditional;
            CurrentStyle=For(card);ink=Tint(card.Element);raycastTarget=false;
            PreparePaintedClip(card.Id);
            duration=card.Type==CardType.Attack?1.02f:.86f;bool struck=false;int nextHit=1;impactClock=-1;
            bool signature=IsSignature(card.Id);
            if(signature){signatureSeed=unchecked(++signatureSequence*2654435761u);duration=card.Id=="earth_strike"?1.12f:card.Id=="fire_bolt"?1.06f:.86f;}
            float contact=signature?SignatureContact:Contact;
            bool deliveryShown=false;
            if(paintedFrames!=null)
            {
                // Seconds and keyframes are card-local; unpainted cards keep their existing clocks.
                switch(card.Id)
                {
                    case "earth_strike":duration=1.30f;paintedContact=.44f;paintedPeak=8;paintedHold=.12f;break;
                    case "stone_guard":duration=1.25f;paintedContact=.40f;paintedPeak=8;paintedHold=.22f;break;
                    case "wind_cut":duration=1.16f;paintedContact=.42f;paintedPeak=9;paintedHold=.12f;break;
                    default:duration=1.18f;paintedContact=.36f;paintedPeak=7;paintedHold=.15f;break;
                }
                contact=paintedContact;
            }
            for(float t=0;t<duration;t+=Time.unscaledDeltaTime)
            {
                progress=t/duration;
                if(paintedFrames!=null)UpdatePaintedClip(contact);
                if(!struck&&progress>=contact)
                {
                    struck=true;progress=contact;impactClock=Time.unscaledTime;impact?.Invoke();SetVerticesDirty();
                    if(paintedFrames!=null)UpdatePaintedClip(contact);
                    // Freeze this mesh at contact, not the global clock or combat simulation.
                    if(HitPause(card)>0)yield return new WaitForSecondsRealtime(HitPause(card));
                }
                while(struck&&nextHit<hitCount&&Time.unscaledTime-impactClock>=nextHit*MultiHitInterval){extraHit?.Invoke(nextHit);nextHit++;}
                if(!deliveryShown&&card.Id=="wind_read"&&progress>=.90f){deliveryShown=true;delivered?.Invoke();}
                SetVerticesDirty();yield return null;
            }
            if(!struck)impact?.Invoke();Destroy(gameObject);
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
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(definition==null)return;
            if(paintedFrames!=null)return; // Real PNG animation replaces, rather than covers, the old mesh for these four cards.
            if(IsSignature(definition.Id))
            {
                CaptureSignatureAnchors();
                if(definition.Id=="earth_strike")DrawEarthSignature(vh);
                else if(definition.Id=="fire_bolt")DrawFireSignature(vh);
                else DrawLightningSignature(vh);
                return; // The other 24 cards retain the existing renderer and timing.
            }
            // Resolve stretched Canvas coordinates at rebuild time, not before layout settles.
            var rect=rectTransform.rect;
            // Layout anchors only: preserve every effect shape, asset and timing.
            CaptureSignatureAnchors();source=emitter;
            target=CurrentStyle==Style.Ground?victimFoot:impactPoint;
            float p=progress,travel=Ease(Mathf.InverseLerp(.07f,Contact,p)),fade=1-Ease(Mathf.InverseLerp(.74f,1,p));
            var c=ink;c.a=fade;var core=Color.Lerp(c,Color.white,.65f);core.a=fade;
            var point=Vector2.Lerp(source,target,travel);
            bool support=definition.Type!=CardType.Attack;
            DrawSupport(vh,p,c,core);
            if(support)return;
            // Anticipation is local to the caster; impacts only bloom at the existing contact time.
            if(p<Contact)
            {
                float charge=Mathf.Sin(Mathf.Clamp01(p/Contact)*Mathf.PI);
                Arc(vh,source,22+35*charge,-p*9,3.8f-p*9,3,Alpha(c,charge*.7f),.75f);
            }
            if(CurrentStyle==Style.Ward)
            {Arc(vh,source+Vector2.left*75,65+65*p,.25f,5.9f,6,c,.8f);return;}
            if(CurrentStyle==Style.Ground)
            {
                var floor=castFoot;
                // A traveling fissure, then an erupting rock crown and a wide ground shockwave.
                bool crush=definition.Id=="earth_crush";
                for(int i=0;i<12*travel;i++)
                {
                    var a=Vector2.Lerp(floor,target,i/12f)+new Vector2(0,i%2==0?-12:12);
                    var b=Vector2.Lerp(floor,target,(i+1)/12f)+new Vector2(0,i%2==0?12:-12);
                    Line(vh,a,b,17,new Color(.04f,.025f,.02f,fade));Line(vh,a,b,6,c);
                    if(i%3==0)Line(vh,b,b+new Vector2(-18,i%2==0?28:-28),4,Alpha(c,.6f));
                }
                if(crush&&p<Contact)Arc(vh,target,110-65*travel,0,Mathf.PI*2,5,Alpha(c,travel),.25f);
                if(p>=Contact)
                {
                    float hit=(p-Contact)/(1-Contact),power=crush?1.3f:1;
                    Arc(vh,target,(55+210*hit)*power,0,Mathf.PI*2,13,c,.30f);
                    Arc(vh,target,(35+155*hit)*power,0,Mathf.PI*2,5,core,.24f);
                    for(int i=0;i<9;i++){float a=i*Mathf.PI/4;var d=new Vector2(Mathf.Cos(a),Mathf.Sin(a)*.32f);Line(vh,target+d*20,target+d*(100+hit*155)*power,8,c);}
                    for(int i=0;i<7;i++)
                    {
                        // Ballistic lift and fall instead of a stationary crown.
                        var v=target+new Vector2((i-3)*(34+hit*38),hit*(crush?620:420)*(1-hit)-hit*40);
                        Diamond(vh,v,18*power,(crush?66:32)*power,new Color(.27f,.20f,.13f,fade));
                        Line(vh,v+new Vector2(-9,14),v+new Vector2(0,32),7,c);
                    }
                    Debris(vh,target,hit,crush?20:12,crush?220:130,c,true);
                    Arc(vh,target+Vector2.up*12,40+hit*210,0,Mathf.PI,9,Alpha(c,(1-hit)*.25f),.3f);
                }
            }
            else if(CurrentStyle==Style.Retaliate)
            {
                // A slab-driven counter, not a gray wind slash. Only the trace decides the bonus bloom.
                var slab=Vector2.Lerp(source+Vector2.right*70,target,travel);
                if(p<Contact){Diamond(vh,slab,32,52,new Color(.28f,.25f,.20f,fade));Line(vh,slab+new Vector2(-20,25),slab+new Vector2(0,45),8,c);}
                else {float age=(p-Contact)/(1-Contact);Debris(vh,target,age,combo?24:12,combo?210:125,c,true);if(combo)for(int i=0;i<5;i++)Line(vh,target+new Vector2((i-2)*28,-60),target+new Vector2((i-2)*44,70-age*100),8,Alpha(core,1-age));}
            }
            else if(CurrentStyle==Style.Slash)
            {
                if(p>.10f)
                {
                    for(int i=3;i>=0;i--)
                    {
                        float phase=p-i*.035f,sweep=Ease(Mathf.InverseLerp(.10f,Contact,phase));
                        var center=Vector2.Lerp(source,target,sweep)+Vector2.left*i*12;
                        float end=-1.7f+4.2f*sweep;
                        Arc(vh,center,125,-1.7f,end,i==0?20:10,Alpha(c,i==0?1:.12f*(4-i)),.72f);
                        if(i==0)Arc(vh,center,119,-1.7f,end,5,core,.72f);
                    }
                    if(p>=Contact)Debris(vh,target,(p-Contact)/(1-Contact),10,180,c,false);
                }
            }
            else if(CurrentStyle==Style.Gust||CurrentStyle==Style.Wave)
            {
                bool water=CurrentStyle==Style.Wave;
                for(int i=3;i>=0;i--)
                {
                    float lag=Ease(Mathf.InverseLerp(.10f,Contact,p-i*.025f));
                    var wake=Vector2.Lerp(source,target,lag);
                    Ribbon(vh,source,wake,p+i*.16f,water?19:34,i==0?11:5,Alpha(c,1-i*.23f));
                    Arc(vh,wake,40+i*12,-1.3f,1.3f,8-i,Alpha(c,1-i*.22f),water?.55f:1);
                }
                if(!water)for(int i=0;i<3;i++)Arc(vh,point,45+i*22,p*17+i,p*17+i+4,4,Alpha(core,.6f),.6f);
                if(p>=Contact)
                {
                    float age=(p-Contact)/(1-Contact);
                    if(water)for(int i=0;i<15;i++){float a=i*2.4f;var splash=target+new Vector2(Mathf.Cos(a)*age*170,Mathf.Abs(Mathf.Sin(a))*age*200-age*age*180);Diamond(vh,splash,4,8*(1-age),Alpha(core,1-age));}
                    else {Debris(vh,target,age,14,180,c,false);for(int i=0;i<3;i++)Arc(vh,target+Vector2.up*age*35,55+i*25+age*55,p*18+i,p*18+i+4,6,Alpha(c,1-age),.55f);}
                }
            }
            else if(CurrentStyle==Style.Bolt)
            {
                int strikes=definition.Id=="lightning_chain"?2:1;
                if(p<Contact)
                {Arc(vh,target,30+18*travel,0,Mathf.PI*2,3,Alpha(c,travel),.35f);}
                for(int j=0;j<strikes;j++)
                {
                    float age=impactClock<0?-1:(Time.unscaledTime-impactClock-j*MultiHitInterval)/duration;
                    if(age<0||age>.24f)continue;
                    float intensity=(1-age/.34f)*(.65f+.35f*Mathf.Abs(Mathf.Cos(age*60)));
                    var aim=target+Vector2.right*(strikes==1?0:j==0?-22:30);var top=aim+Vector2.up*300;
                    var last=top;
                    for(int i=1;i<=8;i++)
                    {
                        var next=Vector2.Lerp(top,aim,i/8f)+Vector2.right*(i==8?0:Mathf.Sin(i*9+j*3)*33);
                        Line(vh,last,next,22,Alpha(c,intensity*.35f));Line(vh,last,next,10,Alpha(c,intensity));Line(vh,last,next,4,Alpha(core,intensity));
                        if(i==3||i==5)Line(vh,next,next+new Vector2((i%2==0?-1:1)*65,-42),3,Alpha(c,intensity*.6f));
                        last=next;
                    }
                    Debris(vh,aim,age/.34f,10,110,Alpha(c,intensity),false);
                    if(age<.065f)ImpactCrown(vh,aim,age*4,c,core,false);
                }
            }
            else if(CurrentStyle==Style.Shard)
            {
                for(int i=0;i<3;i++)
                {var crystal=point+new Vector2(-i*27,(i-1)*26*(1-travel));Line(vh,crystal-Vector2.right*65,crystal,6,Alpha(c,.35f));Diamond(vh,crystal,32,12,c);Diamond(vh,crystal+Vector2.up*3,22,5,core);}
                if(p>=Contact){float h=(p-Contact)/(1-Contact);Debris(vh,target,h,18,150,core,false);for(int i=0;i<6;i++){float a=i*Mathf.PI/3;var frost=target+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*(35+h*100);Line(vh,frost-Vector2.up*8,frost+Vector2.up*8,3,Alpha(c,1-h));Line(vh,frost-Vector2.right*8,frost+Vector2.right*8,3,Alpha(core,1-h));}if(combo){Arc(vh,target,110*(1-h),0,Mathf.PI*2,7,Alpha(new Color(.25f,.65f,1,fade),1-h),.6f);for(int i=0;i<5;i++)Diamond(vh,target+new Vector2((i-2)*30,h*70),12,25,Alpha(core,1-h));}}
            }
            else if(CurrentStyle==Style.Flame)
            {
                // Arcing fireball with a tapered, cooling wake; inferno rains fire downward.
                bool inferno=definition.Id=="fire_inferno";
                var origin=inferno?target+new Vector2(-120,270):source;
                var fire=Vector2.Lerp(origin,target,travel)+Vector2.up*Mathf.Sin(travel*Mathf.PI)*(inferno?20:65);
                if(p<Contact)
                {
                    for(int i=7;i>=0;i--){var tail=fire+new Vector2(-i*13,Mathf.Sin(i*1.9f+p*24)*9);Diamond(vh,tail,26-i*2,20-i,Alpha(c,1-i*.10f));}
                    Diamond(vh,fire,22,19,core);
                }
                else
                {
                    float h=(p-Contact)/(1-Contact);
                    for(int i=0;i<7;i++){var flame=target+new Vector2((i-3)*22,-20+h*(70+i%3*38));Diamond(vh,flame,(18+i%2*9)*(1-h),35*(1-h),Alpha(c,1-h));}
                    Debris(vh,target,h,inferno?26:15,inferno?210:135,c,false);
                }
            }
            else if(CurrentStyle==Style.Burst||CurrentStyle==Style.BlastShield)
            {
                if(p<Contact){Ribbon(vh,source,point,p,10,12,c);Diamond(vh,point,12,12,core);}
                else
                {
                    float h=(p-Contact)/(1-Contact),blast=Ease(Mathf.Clamp01(h*2.7f));
                    if(combo&&h>.1f)for(int i=0;i<3;i++)ImpactCrown(vh,target+new Vector2((i-1)*65,Mathf.Abs(i-1)*32),Mathf.Clamp01((h-.1f)*1.5f),c,core,false);
                    Arc(vh,target,28+blast*170,0,Mathf.PI*2,18*(1-h),c,.72f);
                    Arc(vh,target,12+blast*120,0,Mathf.PI*2,5,Alpha(core,1-h),.75f);
                    if(h<.18f)Diamond(vh,target,85*(1-h/.18f),70*(1-h/.18f),Alpha(core,.8f));
                    Debris(vh,target,h,24,245,c,false);
                    for(int i=0;i<5;i++)Diamond(vh,target+new Vector2((i-2)*32,h*110),22+25*h,18+20*h,new Color(.14f,.09f,.10f,(1-h)*.35f));
                }
            }
            else
            {
                // Venom: a weighted droplet rather than a fireball recolor.
                var drop=point+Vector2.up*Mathf.Sin(travel*Mathf.PI)*90;
                if(p<Contact){for(int i=0;i<5;i++)Diamond(vh,drop-new Vector2(i*14,i*3),18-i*2,23-i*3,Alpha(c,1-i*.15f));}
                else {float h=(p-Contact)/(1-Contact);Debris(vh,target,h,combo?24:12,combo?180:120,c,true);for(int i=0;i<5;i++)Arc(vh,target+new Vector2((i-2)*20,-h*70),10+h*22,0,Mathf.PI*2,4,Alpha(new Color(.64f,.30f,.80f,fade),1-h),.8f);if(combo)for(int i=0;i<5;i++)Line(vh,target+new Vector2((i-2)*24,40),target+new Vector2((i-2)*24,-30-h*100),5,Alpha(c,1-h));}
            }
            if(p>=Contact)
            {
                float hit=(p-Contact)/(1-Contact);
                if(CurrentStyle==Style.Ground||CurrentStyle==Style.Retaliate||CurrentStyle==Style.Burst||CurrentStyle==Style.BlastShield)ImpactCrown(vh,target,hit,c,core,CurrentStyle==Style.Ground);
                else if(CurrentStyle!=Style.Bolt&&hit<.2f)
                    for(int i=0;i<4;i++){float a=i*Mathf.PI/2+.4f;var dir=new Vector2(Mathf.Cos(a),Mathf.Sin(a));Line(vh,target+dir*10,target+dir*(55*(1-hit/.2f)),6,Alpha(core,1-hit/.2f));}
            }
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
        private void PreparePaintedClip(string id)
        {
            if(!HasWarriorClip(id))return;
            if(!warriorClips.TryGetValue(id,out var frames))
            {
                frames=Resources.LoadAll<Sprite>("BrodunWarriorVfx/"+id).OrderBy(s=>s.name,StringComparer.Ordinal).ToArray();
                if(frames.Length!=16)
                {
                    Debug.LogWarning("BRODUN: expected 16 painted VFX frames for "+id+", got "+frames.Length+". Using existing renderer.");
                    return;
                }
                warriorClips.Add(id,frames);
            }
            paintedFrames=frames;
            // Above the hand scrim, but clipped below HP/Intent and above card text/Energy.
            var parentCanvas=GetComponentInParent<Canvas>();
            var canvas=gameObject.AddComponent<Canvas>();canvas.overrideSorting=true;
            canvas.sortingLayerID=parentCanvas!=null?parentCanvas.sortingLayerID:0;canvas.sortingOrder=(parentCanvas!=null?parentCanvas.sortingOrder:0)+5;
            paintedViewport=BattleUi.Rect(transform,"Painted battle viewport",new Vector2(.02f,id=="wind_read"?.295f:HandPresentation.HoverTop),new Vector2(.98f,.81f));
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
                float travel=Ease(Mathf.InverseLerp(.08f,contact,p));
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
                PlacePainted(paintedEffect,frame,foot+Vector2.right*120,Vector2.one*600,fade);
            }
            else if(definition.Id=="wind_cut")
            {
                // Readable crescent launch followed by acceleration; frame 9 is at the target.
                float travel=Mathf.Clamp01(Mathf.InverseLerp(.09f,contact,p));travel=travel*travel;
                PlacePainted(paintedEffect,frame,Vector2.Lerp(emitter,impactPoint,travel),Vector2.one*560,fade);
                for(int i=0;i<paintedTrails.Count;i++)
                {
                    float lag=Mathf.Max(0,p-(i+1)*.045f),t=Mathf.InverseLerp(.09f,contact,lag);t*=t;
                    float wakeFade=1-Ease(Mathf.InverseLerp(contact,contact+.20f,p));
                    PlacePainted(paintedTrails[i],Mathf.Min(7,PaintedFrame(lag,contact)),Vector2.Lerp(emitter,impactPoint,t),Vector2.one*510,fade*wakeFade*(.24f-i*.065f));
                }
            }
            else
            {
                var r=rectTransform.rect;var hand=r.min+Vector2.Scale(r.size,new Vector2(.50f,.325f));
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
            var left=r.min+Vector2.Scale(r.size,new Vector2(.25f,.44f));
            var right=r.min+Vector2.Scale(r.size,new Vector2(.77f,.44f));
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

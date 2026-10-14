using System;
using System.Collections;
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
        private const float Contact=.40f;
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
        public IEnumerator Present(CardDefinition card,Action impact,bool conditional=false)
        {
            definition=card;combo=conditional;
            CurrentStyle=For(card);ink=Tint(card.Element);raycastTarget=false;
            float duration=card.Type==CardType.Attack?.92f:.78f;bool struck=false;
            for(float t=0;t<duration;t+=Time.unscaledDeltaTime)
            {
                progress=t/duration;
                if(!struck&&progress>=Contact){struck=true;impact?.Invoke();}
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
            // Resolve stretched Canvas coordinates at rebuild time, not before layout settles.
            var rect=rectTransform.rect;
            source=rect.min+Vector2.Scale(rect.size,new Vector2(.25f,.55f));
            target=rect.min+Vector2.Scale(rect.size,new Vector2(.77f,CurrentStyle==Style.Ground?.39f:.58f));
            float p=progress,travel=Mathf.Clamp01(p/Contact),fade=Mathf.Clamp01((1-p)*4);
            var c=ink;c.a=fade;var core=Color.Lerp(c,Color.white,.65f);core.a=fade;
            var point=Vector2.Lerp(source,target,travel);
            bool support=definition.Type!=CardType.Attack;
            DrawSupport(vh,p,c,core);
            if(support)return;
            if(CurrentStyle==Style.Ward)
            {Arc(vh,source+Vector2.left*75,65+65*p,.25f,5.9f,6,c,.8f);return;}
            if(CurrentStyle==Style.Ground)
            {
                var floor=rect.min+Vector2.Scale(rect.size,new Vector2(.25f,.39f));
                // A traveling fissure, then an erupting rock crown and a wide ground shockwave.
                for(int i=0;i<12*travel;i++)
                {
                    var a=Vector2.Lerp(floor,target,i/12f)+new Vector2(0,i%2==0?-12:12);
                    var b=Vector2.Lerp(floor,target,(i+1)/12f)+new Vector2(0,i%2==0?12:-12);
                    Line(vh,a,b,17,new Color(.04f,.025f,.02f,fade));Line(vh,a,b,6,c);
                }
                if(p>=Contact)
                {
                    float hit=(p-Contact)/(1-Contact),power=definition.Cost>=2?1.3f:1;
                    Arc(vh,target,(55+210*hit)*power,0,Mathf.PI*2,13,c,.30f);
                    Arc(vh,target,(35+155*hit)*power,0,Mathf.PI*2,5,core,.24f);
                    for(int i=0;i<9;i++){float a=i*Mathf.PI/4;var d=new Vector2(Mathf.Cos(a),Mathf.Sin(a)*.32f);Line(vh,target+d*20,target+d*(100+hit*155)*power,8,c);}
                    for(int i=0;i<7;i++)
                    {
                        var v=target+new Vector2((i-3)*34,Mathf.Sin(Mathf.Min(1,hit*1.6f)*Mathf.PI)*(70+i%3*24));
                        Diamond(vh,v,18*power,40*power,new Color(.27f,.20f,.13f,fade));
                        Line(vh,v+new Vector2(-9,14),v+new Vector2(0,32),7,c);
                    }
                }
            }
            else if(CurrentStyle==Style.Slash||CurrentStyle==Style.Retaliate)
            {
                if(p>.12f){float sweep=Mathf.Clamp01((p-.12f)/.28f);Arc(vh,target,145,-1.6f,-1.6f+4*sweep,27,c,.85f);Arc(vh,target+new Vector2(10,4),130,-1.6f,-1.6f+4*sweep,9,core,.85f);}
            }
            else if(CurrentStyle==Style.Gust||CurrentStyle==Style.Wave)
            {
                for(int i=0;i<3;i++)Arc(vh,point-new Vector2(i*32,0),40+i*19,-1.25f,1.25f,8-i,c,CurrentStyle==Style.Wave?.55f:1);
                for(int i=0;i<4;i++)Line(vh,point+new Vector2(-90-i*14,(i-1.5f)*25),point+new Vector2(-30,(i-1.5f)*25),3,c);
            }
            else if(CurrentStyle==Style.Bolt)
            {
                int strikes=definition.Id=="lightning_chain"?2:1;
                for(int j=0;j<strikes;j++)
                {var aim=target+Vector2.right*(j==0?-22:30);var last=aim+Vector2.up*300;for(int i=1;i<=7;i++){var next=Vector2.Lerp(aim+Vector2.up*300,aim,i/7f)+Vector2.right*(i==7?0:(i%2==0?-28:28));Line(vh,last,next,19,c);Line(vh,last,next,6,core);last=next;}}
            }
            else if(CurrentStyle==Style.Shard)
            {for(int i=0;i<3;i++)Diamond(vh,point-new Vector2(i*45,(i-1)*30),40,18,core);if(combo)Arc(vh,target,120,0,Mathf.PI*2,12,c,1);}
            else
            {
                float power=definition.Cost>=2?1.65f:1;
                for(int i=0;i<8;i++){float r=(32-i*2)*power;Diamond(vh,point-new Vector2(i*24,Mathf.Sin(i+p*15)*15),r,r*.8f,c);}
            }
            if(p>=Contact)
            {
                float hit=(p-Contact)/(1-Contact),power=CurrentStyle==Style.Burst||CurrentStyle==Style.BlastShield?1.8f:1;
                Arc(vh,target,(22+120*hit)*power,0,Mathf.PI*2,10*fade,core,.8f);
                for(int i=0;i<9;i++){float a=i*2.4f;var d=new Vector2(Mathf.Cos(a),Mathf.Sin(a));Line(vh,target+d*(20+hit*55),target+d*(38+hit*95),5*fade,c);}
                if(hit<.25f){Line(vh,target-Vector2.up*42,target+Vector2.up*42,9,core);Line(vh,target-Vector2.right*42,target+Vector2.right*42,7,core);}
            }
        }
        private void DrawSupport(VertexHelper vh,float p,Color c,Color core)
        {
            float fade=c.a;
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
            if(definition.Effects.Any(e=>e.Type==EffectType.Heal))
            {
                var green=new Color(.4f,1,.65f,fade);
                for(int i=0;i<5;i++){var pos=source+new Vector2((i-2)*35,-80+p*160+(i%2)*35);Line(vh,pos-Vector2.up*12,pos+Vector2.up*12,7,green);Line(vh,pos-Vector2.right*12,pos+Vector2.right*12,7,green);}
            }
            if(definition.Effects.Any(e=>e.Type==EffectType.Draw))
            {
                for(int i=0;i<3;i++){var pos=source+new Vector2(-60+i*35,110+Mathf.Sin(p*Mathf.PI)*50+i*15);Quad(vh,pos+new Vector2(-17,-24),pos+new Vector2(-17,24),pos+new Vector2(17,24),pos+new Vector2(17,-24),c);Line(vh,pos+new Vector2(-8,8),pos+new Vector2(8,8),4,core);}
                Arc(vh,source,95,-2+p*4,1+p*4,8,c,.7f);
            }
            if(CurrentStyle==Style.Power||CurrentStyle==Style.Charge)
            {
                for(int i=0;i<7;i++){var pos=source+new Vector2((i-3)*33,-110+p*230);Line(vh,pos+new Vector2(-12,-20),pos,9,c);Line(vh,pos+new Vector2(12,-20),pos,9,core);}
                Arc(vh,source,65+60*p,0,Mathf.PI*2,12,c,1);
                if(CurrentStyle==Style.Charge)for(int i=0;i<6;i++){float a=i*Mathf.PI/3+p*7;var pos=source+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*105;Diamond(vh,pos,17,17,core);}
            }
            if(CurrentStyle==Style.PoisonCloud||definition.Effects.Any(e=>e.Type==EffectType.Poison))
            {
                for(int i=0;i<11;i++){var pos=target+new Vector2(Mathf.Sin(i*2.3f+p*3)*100,Mathf.Cos(i*1.7f)*70+p*30);Diamond(vh,pos,25+i%3*9,20+i%2*10,new Color(.4f,.75f,.15f,fade*.65f));}
            }
            if(definition.Effects.Any(e=>e.Type==EffectType.Weak)&&p>=Contact)
            {for(int i=0;i<3;i++){var pos=target+new Vector2((i-1)*35,115-p*40);Line(vh,pos+new Vector2(-10,15),pos,5,c);Line(vh,pos+new Vector2(10,15),pos,5,c);}}
            if(combo&&p>=Contact){Arc(vh,target,70+p*90,0,Mathf.PI*2,16,core,1);}
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

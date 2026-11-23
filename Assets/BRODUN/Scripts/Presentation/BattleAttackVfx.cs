using UnityEngine;
using UnityEngine.UI;

namespace Brodun.Presentation
{
    public sealed partial class BattleVfx
    {
        private float[] attackContacts;

        private float AttackAge(int hit,float lifetime)
        {
            if(attackContacts==null||hit>=attackContacts.Length||attackContacts[hit]<0)return -1;
            return Mathf.Clamp01((Time.unscaledTime-attackContacts[hit])/lifetime);
        }

        private void DrawTargetedCard(VertexHelper mesh)
        {
            float flight=Ease(Mathf.InverseLerp(.12f,Contact,progress));
            float age=AttackAge(0,.54f);
            float bonus=AttackAge(1,.32f);
            switch(CurrentStyle)
            {
                case Style.Ground: DrawBedrock(mesh,flight,age);break;
                case Style.Retaliate: DrawCounter(mesh,flight,age,bonus);break;
                case Style.Gust:case Style.Slash: DrawGale(mesh,flight,age);break;
                case Style.Wave: DrawWater(mesh,flight,age);break;
                case Style.Shard: DrawFrost(mesh,flight,age,bonus);break;
                case Style.Venom:case Style.PoisonCloud: DrawToxin(mesh,flight,age,bonus);break;
                case Style.Flame: DrawFireRain(mesh,flight,age);break;
                case Style.Bolt: DrawChain(mesh,flight,age,bonus);break;
                case Style.Burst:case Style.BlastShield: DrawDetonation(mesh,flight,age,bonus);break;
            }
        }

        private void DrawBedrock(VertexHelper mesh,float flight,float age)
        {
            var seam=new Color(.16f,.10f,.075f);
            var clay=new Color(.56f,.39f,.24f);
            var edge=new Color(.98f,.77f,.48f);
            float fade=age<0?1:1-Ease(age);
            var start=castFoot+Vector2.right*70;
            for(int branch=-1;branch<=1;branch++)
            {
                var last=start;
                for(int i=1;i<=11;i++)
                {
                    float t=i/11f;
                    if(t>flight)break;
                    var point=Vector2.Lerp(start,victimFoot,t);
                    point.y+=Mathf.Sin(t*Mathf.PI)*branch*38+(Noise(i+branch*31)-.5f)*25;
                    Line(mesh,last,point,branch==0?18:10,Alpha(seam,fade));
                    Line(mesh,last+Vector2.up*4,point+Vector2.up*4,4,Alpha(edge,fade*.66f));
                    last=point;
                }
            }
            if(age<0)
            {
                Dust(mesh,Vector2.Lerp(start,victimFoot,flight),flight*.5f,7,105,.45f*flight);
                return;
            }
            for(int i=0;i<9;i++)
            {
                float burst=Mathf.Clamp01((age-Noise(i+55)*.10f)*1.25f);
                float y=Mathf.Sin(burst*Mathf.PI)*(100+Noise(i+72)*148);
                var point=victimFoot+new Vector2((i-4)*(19+burst*23),y);
                Rock(mesh,point,(20+Noise(i+25)*17)*fade,52*fade,Alpha(clay,fade));
            }
            ImpactCrown(mesh,victimFoot,age,clay,edge,true);
            Dust(mesh,victimFoot,age,14,280,fade*.65f);
        }

        private void DrawCounter(VertexHelper mesh,float flight,float age,float bonus)
        {
            var stone=new Color(.50f,.45f,.36f);
            var edge=new Color(.97f,.86f,.65f);
            var brace=emitter+new Vector2(44,-30);
            if(age<0&&progress>.035f)
            {
                float guard=Ease(Mathf.InverseLerp(.035f,.14f,progress));
                for(int i=0;i<3;i++)
                {
                    var basePoint=brace+new Vector2(i*21-32,-59+i*18);
                    Rock(mesh,basePoint,24*guard,81*guard,Alpha(stone,(1-flight)*guard));
                }
                var tip=Vector2.Lerp(brace,impactPoint,flight);
                EdgeStroke(mesh,Vector2.Lerp(brace,tip,.49f)-Vector2.up*18,tip,34*flight,stone,edge);
            }
            if(age>=0)Debris(mesh,impactPoint,age,14,182,stone,true);
            if(bonus>=0)
            {
                float cut=1-Ease(Mathf.Clamp01(bonus*2.6f));
                EdgeStroke(mesh,impactPoint+new Vector2(-107,-63),impactPoint+new Vector2(84,78),30*cut,stone,edge);
                Debris(mesh,impactPoint,bonus,18,215,edge,true);
            }
        }

        private void DrawGale(VertexHelper mesh,float flight,float age)
        {
            var teal=new Color(.22f,.87f,.72f);
            var tip=new Color(.88f,1,.91f);
            float strength=age<0?Ease(Mathf.InverseLerp(.045f,.18f,progress)):1-Ease(age);
            var hub=age<0?Vector2.Lerp(emitter,impactPoint,flight):impactPoint+Vector2.up*age*52;
            for(int band=0;band<5;band++)
            {
                float turn=progress*23+band*1.33f;
                float radius=(52+band*13)*strength;
                var center=hub+new Vector2(-band*8,(band-2)*32)*strength;
                Arc(mesh,center,radius,turn,turn+3.85f,12,Alpha(teal,strength*.68f),.43f);
                Arc(mesh,center,radius,turn+.33f,turn+2.65f,4,Alpha(tip,strength*.87f),.43f);
            }
            if(age>=0)
            {
                float cut=1-Ease(Mathf.Clamp01(age*2.5f));
                EdgeStroke(mesh,impactPoint+new Vector2(-122,18),impactPoint+new Vector2(95,-16),24*cut,teal,tip);
                Debris(mesh,impactPoint,age,18,216,teal,false);
            }
        }

        private void DrawWater(VertexHelper mesh,float flight,float age)
        {
            bool stream=definition.Id=="water_stream";
            var blue=new Color(.18f,.66f,.96f);
            var foam=new Color(.79f,.98f,1);
            if(age<0)
            {
                if(flight<=0)return;
                var head=Vector2.Lerp(emitter,impactPoint,flight);
                if(stream)
                {
                    Ribbon(mesh,emitter,head,progress,39,19,blue);
                    Ribbon(mesh,emitter,head,progress+.22f,36,6,foam);
                }
                else
                {
                    var basePoint=head+Vector2.down*30;
                    var crest=basePoint+new Vector2(23,55)*flight;
                    EdgeStroke(mesh,basePoint-Vector2.right*105*flight,crest,38*flight,blue,foam);
                    Arc(mesh,crest-Vector2.right*16,48*flight,-.8f,2.6f,9,blue,1.1f);
                    Arc(mesh,crest-Vector2.right*16,41*flight,-.45f,1.9f,5,foam,1.1f);
                }
                return;
            }
            for(int i=0;i<14;i++)
            {
                float rise=(84+Noise(i+81)*136)*age-age*age*185;
                var drop=impactPoint+new Vector2((Noise(i+104)-.5f)*285*age,rise);
                Diamond(mesh,drop,4+Noise(i)*4,10*(1-age),Alpha(i%3==0?foam:blue,1-age));
            }
            var landing=stream?new Vector2(emitter.x+80,Mathf.Max(rectTransform.rect.yMin+rectTransform.rect.height*HandPresentation.HoverTop,castFoot.y-32)):emitter;
            float back=Ease(Mathf.InverseLerp(.08f,.94f,age));
            for(int i=0;i<5;i++)
            {
                var drop=Vector2.Lerp(impactPoint,landing,Mathf.Clamp01(back-i*.08f));
                drop.y+=Mathf.Sin(back*Mathf.PI)*57;
                Diamond(mesh,drop,stream?5:8,stream?9:4,Alpha(foam,1-age));
            }
        }

        private void DrawFrost(VertexHelper mesh,float flight,float age,float bonus)
        {
            bool link=definition.Id=="ice_link";
            var ice=new Color(.28f,.70f,.99f);
            var pale=new Color(.88f,.98f,1);
            if(age<0)
            {
                for(int i=0;i<3;i++)
                {
                    var point=Vector2.Lerp(emitter,impactPoint,flight)+new Vector2(-i*26,(i-1)*37*(1-flight));
                    EdgeStroke(mesh,point+new Vector2(-69,-11)*flight,point+Vector2.right*14*flight,(i==1?26:17)*flight,ice,pale);
                }
                return;
            }
            Debris(mesh,impactPoint,age,14,165,pale,false);
            float growth=Ease(Mathf.Clamp01(age*5));
            for(int i=0;i<5;i++)
            {
                var foot=impactPoint+new Vector2((i-2)*21,-34);
                var peak=foot+new Vector2((i-2)*13,53+Noise(i+65)*42)*growth;
                EdgeStroke(mesh,foot,peak,25*growth*(1-age),Alpha(ice,1-age),Alpha(pale,1-age));
            }
            if(link&&bonus>=0)
            {
                float sharp=1-Ease(Mathf.Clamp01(bonus*2.3f));
                for(int i=0;i<7;i++)
                {
                    var dir=Vector2FromAngle(i*2.4f);
                    EdgeStroke(mesh,impactPoint+dir*18,impactPoint+dir*(70+bonus*100),21*sharp,ice,pale);
                }
                Debris(mesh,impactPoint,bonus,20,210,ice,false);
            }
        }

        private void DrawToxin(VertexHelper mesh,float flight,float age,float bonus)
        {
            bool mist=CurrentStyle==Style.PoisonCloud;
            bool sting=definition.Id=="poison_sting";
            var acid=new Color(.68f,.94f,.25f);
            var stain=new Color(.44f,.22f,.51f);
            if(age<0)
            {
                if(mist)
                {
                    DrawMiasma(mesh,Vector2.Lerp(castFoot+Vector2.right*80,victimFoot,flight),flight,.57f*flight,stain,acid);
                    return;
                }
                var tip=Vector2.Lerp(emitter,impactPoint,flight);
                int blades=sting?1:3;
                for(int i=0;i<blades;i++)
                {
                    var offset=Vector2.up*(i-1)*25*flight;
                    EdgeStroke(mesh,Vector2.Lerp(emitter,tip,.66f)+offset,tip+offset,(sting?21:15)*flight,stain,acid);
                }
                return;
            }
            DrawMiasma(mesh,mist?victimFoot:impactPoint+Vector2.down*27,age,(1-age)*(mist?.83f:.52f),stain,acid);
            if(!mist)Debris(mesh,impactPoint,age,12,160,acid,true);
            if(bonus>=0)
            {
                float cut=1-Ease(Mathf.Clamp01(bonus*2.8f));
                EdgeStroke(mesh,impactPoint+new Vector2(62,57),impactPoint+new Vector2(-87,-49),28*cut,stain,acid);
                Debris(mesh,impactPoint,bonus,17,190,acid,true);
            }
        }

        private void DrawMiasma(VertexHelper mesh,Vector2 center,float phase,float opacity,Color stain,Color acid)
        {
            for(int i=0;i<13;i++)
            {
                float turn=i*2.4f+phase*4;
                var p=center+new Vector2(Mathf.Cos(turn)*(44+phase*79),18+Noise(i+35)*47+Mathf.Sin(turn)*25);
                Diamond(mesh,p,19+Noise(i)*21,13+Noise(i+63)*19,Alpha(stain,opacity*.74f));
                Diamond(mesh,p+new Vector2(9,8),5,7,Alpha(acid,opacity));
            }
        }

        private void DrawFireRain(VertexHelper mesh,float flight,float age)
        {
            var ember=new Color(.76f,.16f,.055f);
            var heat=new Color(1,.69f,.19f);
            if(age<0)
            {
                for(int i=0;i<5;i++)
                {
                    float travel=Ease(Mathf.InverseLerp(i*.10f,1,flight));
                    if(travel<=0)continue;
                    var start=impactPoint+new Vector2(-146+i*61,205+Noise(i+30)*76);
                    var end=Vector2.Lerp(start,impactPoint,travel);
                    EdgeStroke(mesh,Vector2.Lerp(start,end,.58f),end,20+Noise(i)*12,ember,heat);
                    Debris(mesh,end,travel*.5f,5,90,heat,false);
                }
                return;
            }
            BlastFracture(mesh,impactPoint,age,ember,heat,132);
            for(int i=0;i<7;i++)
            {
                var foot=victimFoot+new Vector2((i-3)*21,0);
                var apex=foot+new Vector2((i-3)*14,Mathf.Sin(age*Mathf.PI)*(85+Noise(i+90)*91));
                EdgeStroke(mesh,foot,apex,38*(1-age),Alpha(ember,1-age),Alpha(heat,1-age));
            }
        }

        private void DrawChain(VertexHelper mesh,float flight,float age,float second)
        {
            var violet=new Color(.50f,.48f,1);
            var white=new Color(.83f,.96f,1);
            var firstPoint=impactPoint+new Vector2(-23,29);
            var secondPoint=impactPoint+new Vector2(29,-22);
            if(age<0&&flight>0)
                ElectricPath(mesh,emitter,Vector2.Lerp(emitter,firstPoint,flight),0,4,Alpha(violet,.72f));
            if(age>=0&&age<.40f)
            {
                ElectricPath(mesh,emitter,firstPoint,0,9,Alpha(violet,1-age/.40f));
                Debris(mesh,firstPoint,age,11,136,white,false);
            }
            if(second>=0&&second<.73f)
            {
                ElectricPath(mesh,firstPoint+new Vector2(87,74),secondPoint,3,11,Alpha(white,1-second/.73f));
                ElectricPath(mesh,secondPoint,secondPoint+new Vector2(-88,-42),4,5,Alpha(violet,1-second/.73f));
                Debris(mesh,secondPoint,second,15,155,violet,false);
            }
        }

        private void DrawDetonation(VertexHelper mesh,float flight,float age,float bonus)
        {
            bool shield=CurrentStyle==Style.BlastShield;
            bool ignite=definition.Id=="explosion_link";
            var ember=new Color(.91f,.29f,.09f);
            var heat=new Color(1,.86f,.49f);
            var launch=shield?emitter+Vector2.right*38:emitter;
            if(shield)
            {
                float brace=age<0?Ease(Mathf.InverseLerp(.04f,.16f,progress)):1-Ease(age);
                var top=launch+new Vector2(-21,81)*brace;
                var corner=launch+new Vector2(47,48)*brace;
                var bottom=launch+new Vector2(4,-76)*brace;
                Quad(mesh,top,corner,bottom,launch+Vector2.left*49*brace,Alpha(ember,.27f*brace));
                Line(mesh,top,corner,7,Alpha(heat,brace));
                Line(mesh,corner,bottom,8,Alpha(ember,brace));
            }
            if(age<0)
            {
                var head=Vector2.Lerp(launch,impactPoint,flight);
                if(ignite)
                {
                    Ribbon(mesh,launch,head,progress,12,10,ember);
                    Diamond(mesh,head,13*flight,17*flight,heat);
                }
                else EdgeStroke(mesh,Vector2.Lerp(launch,head,.48f),head,(shield?29:42)*flight,ember,heat);
                return;
            }
            BlastFracture(mesh,impactPoint,age,ember,heat,shield?68:ignite?83:146);
            if(ignite&&bonus>=0)
                BlastFracture(mesh,impactPoint+new Vector2(16,20),bonus,ember,heat,117);
        }

        private void BlastFracture(VertexHelper mesh,Vector2 center,float age,Color edge,Color core,float radius)
        {
            float snap=1-Ease(Mathf.Clamp01(age*3));
            for(int i=0;i<9;i++)
            {
                var axis=Vector2FromAngle(i*2.4f);
                float reach=(.60f+Noise(i+130)*.52f)*radius;
                var start=center+axis*age*reach*.61f;
                EdgeStroke(mesh,start,start+axis*reach*snap,28*snap,edge,core);
            }
            Debris(mesh,center,age,18,radius*1.6f,edge,false);
        }

        private static Vector2 Vector2FromAngle(float angle)=>new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));

        private static void EdgeStroke(VertexHelper mesh,Vector2 start,Vector2 end,float width,Color shade,Color edge)
        {
            if(width<1||(end-start).sqrMagnitude<9)return;
            var direction=end-start;
            var cross=new Vector2(-direction.y,direction.x).normalized*width;
            var shoulder=Vector2.Lerp(start,end,.70f);
            Quad(mesh,start,shoulder+cross,end,shoulder-cross*.47f,shade);
            Quad(mesh,Vector2.Lerp(start,end,.21f),shoulder+cross*.44f,end,shoulder-cross*.08f,edge);
        }
    }
}

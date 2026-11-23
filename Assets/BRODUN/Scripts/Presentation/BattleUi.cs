using System;
using UnityEngine;
using UnityEngine.UI;
namespace Brodun.Presentation
{
    // Presentation primitives only: no combat, deck, reward or random calculations.
    public static class BattleUi
    {
        public static readonly Color Gold=new Color(.94f,.77f,.40f),Pale=new Color(.90f,.91f,.85f),Teal=new Color(.25f,.78f,.68f);
        public static readonly Color Ink=new Color(.025f,.039f,.055f),Bronze=new Color(.56f,.46f,.29f),Steel=new Color(.46f,.72f,.84f),HpRed=new Color(.68f,.17f,.20f);
        public const float FloorY=.43f;
        public const float PlayerHomeX=.25f,EnemyHomeX=.77f;
        // Battle-only surfaces. Existing map/Room Panel and Button helpers are unchanged.
        public static RectTransform Surface(Transform parent,string name,Vector2 min,Vector2 max,BattleChrome.Form form,Color accent)
        {
            var rect=Rect(parent,name,min,max);var chrome=rect.gameObject.AddComponent<BattleChrome>();
            chrome.FormKind=form;chrome.Accent=accent;chrome.raycastTarget=false;return rect;
        }
        public static BattleChrome Decorate(RectTransform parent,BattleChrome.Form form,Color accent)
        {return Surface(parent,"Engraved trim",Vector2.zero,Vector2.one,form,accent).GetComponent<BattleChrome>();}
        public static StyledButton CommandButton(Transform parent,string name,string text,PresentationAssets art,Vector2 min,Vector2 max,Action callback,BattleChrome.Form form=BattleChrome.Form.Command)
        {
            var rect=Surface(parent,name,min,max,form,Teal);
            var chrome=rect.GetComponent<BattleChrome>();chrome.raycastTarget=true;
            var button=rect.gameObject.AddComponent<StyledButton>();button.targetGraphic=chrome;button.Configure(null,null,null,null);
            var label=Text(rect,"Label",text,art.font,25,Pale,new Vector2(.06f,.12f),new Vector2(.94f,.88f));Readable(label);
            button.onClick.AddListener(()=>callback());return button;
        }
        public static RectTransform Rect(Transform parent,string name,Vector2 min,Vector2 max)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);
            rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;return rect;
        }
        public static void Place(RectTransform rect,Transform parent,Vector2 min,Vector2 max)
        {
            rect.SetParent(parent,false);
            rect.anchorMin=min;
            rect.anchorMax=max;
            rect.offsetMin=rect.offsetMax=Vector2.zero;
        }
        public static RectTransform UnitReadout(Transform parent,string name,float centerX,float floorY=FloorY)
        {
            float bottom=floorY<.39f?floorY-.044f:floorY-.052f;
            return Rect(parent,name,new Vector2(centerX-.092f,bottom),new Vector2(centerX+.092f,floorY-.005f));
        }
        internal static void PositionAbove(RectTransform badge,float spriteTop)
        {
            var stage=(RectTransform)badge.parent;
            float halfHeight=badge.sizeDelta.y*.5f;
            float ceiling=stage.rect.height*(.93f-badge.anchorMin.y)-halfHeight;
            badge.anchoredPosition=new Vector2(0,Mathf.Min(spriteTop+halfHeight+12,ceiling));
        }
        internal static float SpriteTop(Sprite[] frames,float scale)
        {
            float top=0;
            foreach(var frame in frames)top=Mathf.Max(top,(frame.rect.height-frame.pivot.y)*scale);
            return top;
        }
        public static void LayoutUnitReadout(RectTransform row,Text name,Text hp,Image fill)
        {
            Surface(row,"Vitals backing",new Vector2(-.05f,0),new Vector2(1.05f,1),BattleChrome.Form.Readout,Color.black)
                .SetAsFirstSibling();
            Place(name.rectTransform,row,new Vector2(.03f,.36f),new Vector2(.48f,1));
            name.alignment=TextAnchor.MiddleLeft;
            name.fontSize=22;
            name.resizeTextForBestFit=true;
            name.resizeTextMinSize=14;
            name.resizeTextMaxSize=22;
            Place(hp.rectTransform,row,new Vector2(.49f,.36f),new Vector2(.79f,1));
            hp.fontSize=25;
            hp.resizeTextForBestFit=true;
            hp.resizeTextMinSize=14;
            hp.resizeTextMaxSize=25;
            hp.alignment=TextAnchor.MiddleRight;
            var track=(RectTransform)fill.transform.parent;
            Place(track,row,new Vector2(.03f,.12f),new Vector2(.97f,.25f));
            track.GetComponent<Image>().color=new Color(.012f,.022f,.029f,.94f);
            fill.color=HpRed;
            Readable(name);
            Readable(hp);
        }
        public static Image Image(Transform parent,string name,Sprite sprite,Color color,Vector2 min,Vector2 max)
        {var rect=Rect(parent,name,min,max);var image=rect.gameObject.AddComponent<Image>();image.sprite=sprite;image.color=color;image.raycastTarget=false;return image;}
        public static Text Text(Transform parent,string name,string text,Font font,int size,Color color,Vector2 min,Vector2 max)
        {
            var rect=Rect(parent,name,min,max);var label=rect.gameObject.AddComponent<Text>();label.font=font;label.text=text;label.fontSize=size;label.color=color;
            label.alignment=TextAnchor.MiddleCenter;label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Truncate;label.raycastTarget=false;return label;
        }
        public static StyledButton Button(Transform parent,string name,string text,PresentationAssets art,Vector2 min,Vector2 max,Action callback)
        {
            var image=Image(parent,name,art.buttonNormal,Color.white,min,max);image.raycastTarget=true;
            var button=image.gameObject.AddComponent<StyledButton>();button.targetGraphic=image;
            button.Configure(art.buttonNormal,art.buttonHover,art.buttonPressed,art.buttonDisabled);
            Text(image.transform,"Label",text,art.font,25,Gold,new Vector2(.04f,.07f),new Vector2(.96f,.93f));
            button.onClick.AddListener(()=>callback());return button;
        }
        public static Image Bar(Transform parent,string name,Vector2 min,Vector2 max,Color tint)
        {
            var bg=Image(parent,name+" track",null,new Color(.07f,.09f,.11f),min,max);
            return Image(bg.transform,name,null,tint,Vector2.zero,Vector2.one);
        }
        public static void Readable(Text label)
        {
            var outline=label.GetComponent<Outline>()??label.gameObject.AddComponent<Outline>();
            outline.effectColor=new Color(.005f,.01f,.02f,.95f);outline.effectDistance=new Vector2(2,-2);
        }
        public static BattleHudGlyph Glyph(Transform parent,string name,BattleHudGlyph.Symbol symbol,Color tint,Vector2 min,Vector2 max)
        {
            var glyph=Rect(parent,name,min,max).gameObject.AddComponent<BattleHudGlyph>();
            glyph.Kind=symbol;glyph.color=tint;glyph.raycastTarget=false;return glyph;
        }
        public static Text BlockReadout(Transform hud,string name,Font font)
        {
            var badge=Rect(hud,name+" badge",new Vector2(.815f,.36f),new Vector2(.97f,1));
            Glyph(badge,name+" shield",BattleHudGlyph.Symbol.Shield,Steel,new Vector2(0,.17f),new Vector2(.43f,.83f));
            var value=Text(badge,name,"0",font,22,Pale,new Vector2(.43f,0),Vector2.one);
            value.horizontalOverflow=HorizontalWrapMode.Overflow;value.verticalOverflow=VerticalWrapMode.Overflow;
            Readable(value);return value;
        }
        public static void SetBlockReadout(Text label,int amount)
        {
            label.text=amount.ToString();
            label.color=new Color(.82f,.95f,1);
            label.transform.parent.gameObject.SetActive(amount>0);
        }
        public static RectTransform Panel(Transform parent,string name,Vector2 min,Vector2 max)
        {
            var edge=Image(parent,name+" rim",null,new Color(.49f,.40f,.24f,.92f),min,max);
            var fill=Image(edge.transform,name,null,new Color(.035f,.055f,.075f,.97f),Vector2.zero,Vector2.one);
            fill.rectTransform.offsetMin=Vector2.one*2;fill.rectTransform.offsetMax=Vector2.one*-2;
            return edge.rectTransform;
        }
        public static void SetBar(Image bar,float fraction)
        {var r=(RectTransform)bar.transform;r.anchorMax=new Vector2(Mathf.Clamp01(fraction),1);r.offsetMin=r.offsetMax=Vector2.zero;}
        public static string Intent(IntentData intent)
        {
            if(intent==null)return "—";
            return intent.Type==IntentType.Attack?"공격  "+intent.Amount+(intent.Hits>1?" × "+intent.Hits:""):
                intent.Type==IntentType.Block?"방어  "+intent.Amount:"힘  +"+intent.Amount;
        }
        public static string IntentDescription(IntentData intent,int block)
        {
            if(intent==null)return "전투가 종료되었습니다.";
            return intent.Type==IntentType.Attack?(intent.AttackReduction>0?"다음 행동: 급속 빙결 적용 후 총 "+intent.TotalDamage+" 피해":
                "다음 행동: "+intent.Amount+" 피해를 "+intent.Hits+"회 가합니다.")+"\n현재 방어도 기준 HP 피해: "+intent.ExpectedHPDamage(block):
                intent.Type==IntentType.Block?"다음 행동: 적 방어도 "+intent.Amount+" 획득":"다음 행동: 적 힘 +"+intent.Amount;
        }
    }
    // Resolution-independent, pixel-snapped bevels/engraving, not replacement game artwork.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleChrome : MaskableGraphic
    {
        public enum Form { Card, Hud, Dock, Command, Medallion, Shadow, Vignette, HandShade, Action, Readout }
        public Form FormKind;
        public Color Accent=BattleUi.Bronze;
        private float emphasis;
        public void Emphasize(float value){if(Mathf.Abs(emphasis-value)<.005f)return;emphasis=value;SetVerticesDirty();}
        public BattleChrome(){useLegacyMeshGeneration=false;}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;if(r.width<=0||r.height<=0)return;
            if(FormKind==Form.Shadow)
            {
                int start=vh.currentVertCount;vh.AddVert(r.center,Tint(new Color(0,.008f,.015f,.45f)),Vector2.zero);
                for(int i=0;i<=24;i++){float a=i*Mathf.PI*2/24;vh.AddVert(r.center+new Vector2(Mathf.Cos(a)*r.width*.5f,Mathf.Sin(a)*r.height*.5f),Color.clear,Vector2.zero);if(i>0)vh.AddTriangle(start,start+i,start+i+1);}return;
            }
            if(FormKind==Form.Vignette)
            {
                Gradient(vh,new Rect(r.xMin,r.yMin,r.width*.22f,r.height),new Color(.005f,.015f,.025f,.66f),Color.clear,true);
                Gradient(vh,new Rect(r.xMax-r.width*.22f,r.yMin,r.width*.22f,r.height),Color.clear,new Color(.005f,.015f,.025f,.66f),true);
                Gradient(vh,new Rect(r.xMin,r.yMax-r.height*.20f,r.width,r.height*.20f),Color.clear,new Color(.005f,.015f,.025f,.70f),false);return;
            }
            if(FormKind==Form.HandShade)
            {
                Gradient(vh,r,new Color(.008f,.016f,.025f,.8f),new Color(.008f,.016f,.025f,0),false);
                return;
            }
            if(FormKind==Form.Readout)
            {
                var ink=new Color(.012f,.025f,.035f,.76f);
                float fade=r.width*.08f;
                Gradient(vh,new Rect(r.xMin,r.yMin,fade,r.height),Color.clear,ink,true);
                Gradient(vh,new Rect(r.xMin+fade,r.yMin,r.width-2*fade,r.height),ink,ink,true);
                Gradient(vh,new Rect(r.xMax-fade,r.yMin,fade,r.height),ink,Color.clear,true);
                return;
            }
            if(FormKind==Form.Action)
            {
                Bevel(vh,r,5,Alpha(Accent,.62f));
                Bevel(vh,Inset(r,1),4,new Color(.025f,.06f,.07f,.92f));
                return;
            }
            float cut=FormKind==Form.Card?12:FormKind==Form.Medallion?Mathf.Min(r.width,r.height)*.20f:8;
            Bevel(vh,r,cut,new Color(.009f,.015f,.022f,.98f));
            Bevel(vh,Inset(r,1),cut,Color.Lerp(new Color(.20f,.24f,.26f),Accent,.48f+emphasis*.25f));
            Bevel(vh,Inset(r,3),cut-2,new Color(.06f,.09f,.11f,.99f));
            Bevel(vh,Inset(r,6),Mathf.Max(2,cut-4),FormKind==Form.Dock?new Color(.025f,.042f,.055f,.98f):new Color(.035f,.053f,.068f,.98f));
            Line(vh,new Vector2(r.xMin+cut+5,r.yMax-5),new Vector2(r.xMax-cut-5,r.yMax-5),1,Alpha(Accent,.70f));
            Line(vh,new Vector2(r.xMin+cut+5,r.yMin+5),new Vector2(r.xMax-cut-5,r.yMin+5),1,new Color(.005f,.01f,.02f,.8f));
            if(FormKind==Form.Dock)
            {
                for(float x=r.xMin+36;x<r.xMax-20;x+=64)Line(vh,new Vector2(x,r.yMin+12),new Vector2(x+14,r.yMax-18),1,new Color(.20f,.29f,.31f,.045f));
                for(int side=-1;side<=1;side+=2){float x=r.center.x+side*r.width*.31f;Line(vh,new Vector2(x,r.yMax-12),new Vector2(x+side*42,r.yMax-12),1,Alpha(Accent,.38f));}
            }
            else
            {
                foreach(float x in new[]{r.xMin+9,r.xMax-9})foreach(float y in new[]{r.yMin+13,r.yMax-13})Diamond(vh,new Vector2(x,y),2,Alpha(Accent,.75f));
                if(FormKind==Form.Card)
                {
                    Line(vh,new Vector2(r.xMin+8,r.yMin+r.height*.35f),new Vector2(r.xMax-8,r.yMin+r.height*.35f),1,Alpha(Accent,.6f));
                    Diamond(vh,new Vector2(r.center.x,r.yMin+9),3,Alpha(Accent,.85f));
                }
            }
        }
        private Color Tint(Color c)=>c*color;
        private static Color Alpha(Color c,float a){c.a=a;return c;}
        private static Rect Inset(Rect r,float n)=>new Rect(r.xMin+n,r.yMin+n,Mathf.Max(0,r.width-2*n),Mathf.Max(0,r.height-2*n));
        private void Bevel(VertexHelper vh,Rect r,float cut,Color ink)
        {
            cut=Mathf.Clamp(cut,0,Mathf.Min(r.width,r.height)*.4f);
            var p=new[]{new Vector2(r.xMin+cut,r.yMin),new Vector2(r.xMax-cut,r.yMin),new Vector2(r.xMax,r.yMin+cut),new Vector2(r.xMax,r.yMax-cut),new Vector2(r.xMax-cut,r.yMax),new Vector2(r.xMin+cut,r.yMax),new Vector2(r.xMin,r.yMax-cut),new Vector2(r.xMin,r.yMin+cut)};
            int s=vh.currentVertCount;vh.AddVert(r.center,Tint(ink),Vector2.zero);
            foreach(var v in p)vh.AddVert(new Vector2(Mathf.Round(v.x),Mathf.Round(v.y)),Tint(ink),Vector2.zero);
            for(int i=0;i<8;i++)vh.AddTriangle(s,s+i+1,s+(i+1)%8+1);
        }
        private void Line(VertexHelper vh,Vector2 a,Vector2 b,float width,Color ink)
        {
            var n=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;int s=vh.currentVertCount;
            foreach(var p in new[]{a-n,a+n,b+n,b-n})vh.AddVert(p,Tint(ink),Vector2.zero);
            vh.AddTriangle(s,s+1,s+2);vh.AddTriangle(s,s+2,s+3);
        }
        private void Diamond(VertexHelper vh,Vector2 center,float size,Color ink)
        {Line(vh,center-Vector2.up*size,center+Vector2.up*size,size*1.4f,ink);}
        private void Gradient(VertexHelper vh,Rect r,Color a,Color b,bool horizontal)
        {
            int s=vh.currentVertCount;
            vh.AddVert(new Vector2(r.xMin,r.yMin),Tint(a),Vector2.zero);vh.AddVert(new Vector2(r.xMin,r.yMax),Tint(horizontal?a:b),Vector2.zero);
            vh.AddVert(new Vector2(r.xMax,r.yMax),Tint(b),Vector2.zero);vh.AddVert(new Vector2(r.xMax,r.yMin),Tint(horizontal?b:a),Vector2.zero);
            vh.AddTriangle(s,s+1,s+2);vh.AddTriangle(s,s+2,s+3);
        }
    }
    // Screen-space aiming guide. Target validation stays with the controller.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleTargetArrow : MaskableGraphic
    {
        private static readonly Color Rim=new Color(.035f,.024f,.028f,.95f);
        private static readonly Color Aiming=new Color(.53f,.73f,.77f,.92f);
        private static readonly Color Locked=new Color(1,.76f,.34f);
        private Vector2 origin,tip;
        private bool overTarget,selfTarget;

        public BattleTargetArrow(){useLegacyMeshGeneration=false;}

        public void SetPath(Vector2 from,Vector2 to,bool validTarget,bool targetsSelf)
        {
            if(origin==from&&tip==to&&overTarget==validTarget&&selfTarget==targetsSelf)return;
            origin=from;
            tip=to;
            overTarget=validTarget;
            selfTarget=targetsSelf;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var delta=tip-origin;
            float distance=delta.magnitude;
            if(distance<16)return;

            // Bow upward while advancing monotonically toward the cursor.
            var axis=delta/distance;
            var lift=Vector2.up-Vector2.Dot(Vector2.up,axis)*axis;
            var bend=origin+delta*.34f+lift*Mathf.Min(120,distance*.42f);
            var heading=(tip-bend).normalized;
            var sideways=new Vector2(-heading.y,heading.x);
            float scale=Mathf.Min(1,distance/104);
            float headLength=(overTarget?44:36)*scale;
            float headWidth=(overTarget?23:19)*scale;
            var headBase=tip-heading*headLength;
            var shaftEnd=headBase+heading*(5*scale);
            var ink=overTarget?(selfTarget?BattleUi.Teal:Locked):Aiming;
            var startInk=Color.Lerp(ink,Rim,.32f);

            AddShaft(mesh,bend,shaftEnd,7*scale,11*scale,Rim,Rim);
            AddHead(mesh,tip,headBase,sideways,headWidth,Rim);
            AddShaft(mesh,bend,shaftEnd,4*scale,8*scale,startInk,ink);
            AddHead(mesh,tip-heading*(5*scale),headBase+heading*(3*scale),sideways,headWidth-5*scale,ink);
        }

        private void AddShaft(VertexHelper mesh,Vector2 bend,Vector2 end,float startWidth,float endWidth,Color startInk,Color endInk)
        {
            const int segments=24;
            int first=mesh.currentVertCount;
            for(int i=0;i<=segments;i++)
            {
                float t=(float)i/segments;
                var a=Vector2.Lerp(origin,bend,t);
                var b=Vector2.Lerp(bend,end,t);
                var tangent=b-a;
                var offset=new Vector2(-tangent.y,tangent.x).normalized*Mathf.Lerp(startWidth,endWidth,t);
                var position=Vector2.Lerp(a,b,t);
                var ink=Color.Lerp(startInk,endInk,t);
                mesh.AddVert(position+offset,ink,Vector2.zero);
                mesh.AddVert(position-offset,ink,Vector2.zero);
                if(i==0)continue;
                int pair=first+2*i;
                mesh.AddTriangle(pair-2,pair,pair-1);
                mesh.AddTriangle(pair-1,pair,pair+1);
            }
        }

        private static void AddHead(VertexHelper mesh,Vector2 point,Vector2 center,Vector2 sideways,float width,Color ink)
        {
            int first=mesh.currentVertCount;
            mesh.AddVert(center+sideways*width,ink,Vector2.zero);
            mesh.AddVert(point,ink,Vector2.zero);
            mesh.AddVert(center-sideways*width,ink,Vector2.zero);
            mesh.AddTriangle(first,first+1,first+2);
        }
    }

    // HUD symbols, not another combat-effects system. No font glyph or external asset dependency.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleHudGlyph : MaskableGraphic
    {
        public enum Symbol { Shield, Sword, Power, Energy, BrokenShield, Heal }
        private Symbol kind;
        private float fracture;
        public Symbol Kind {get=>kind;set{kind=value;SetVerticesDirty();}}
        public float Fracture {set{fracture=Mathf.Clamp01(value);SetVerticesDirty();}}
        public BattleHudGlyph(){useLegacyMeshGeneration=false;}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;float size=Mathf.Min(r.width,r.height);var center=r.center;
            if(kind==Symbol.Shield)
            {
                Poly(vh,center,size,new[]{new Vector2(-.39f,.40f),new Vector2(.39f,.40f),new Vector2(.30f,-.12f),new Vector2(0,-.47f),new Vector2(-.30f,-.12f)},color);
                Poly(vh,center+Vector2.up*size*.02f,size*.69f,new[]{new Vector2(-.39f,.40f),new Vector2(.39f,.40f),new Vector2(.30f,-.12f),new Vector2(0,-.47f),new Vector2(-.30f,-.12f)},new Color(.035f,.09f,.10f,color.a));
            }
            else if(kind==Symbol.BrokenShield)
            {
                // Two offset pieces and a dark lightning-shaped fracture distinguish loss.
                Poly(vh,center+new Vector2(-2-fracture*3,2+fracture*2),size,new[]{new Vector2(-.39f,.40f),new Vector2(.02f,.40f),new Vector2(-.10f,.05f),new Vector2(.01f,-.04f),new Vector2(-.15f,-.35f),new Vector2(-.30f,-.12f)},color);
                Poly(vh,center+new Vector2(3+fracture*3,-2-fracture*2),size,new[]{new Vector2(.17f,.40f),new Vector2(.39f,.40f),new Vector2(.30f,-.12f),new Vector2(0,-.47f),new Vector2(.14f,-.01f),new Vector2(.03f,.08f)},color);
            }
            else if(kind==Symbol.Heal)
            {
                Poly(vh,center,size,new[]{new Vector2(-.13f,.43f),new Vector2(.13f,.43f),new Vector2(.13f,-.43f),new Vector2(-.13f,-.43f)},color);
                Poly(vh,center,size,new[]{new Vector2(-.43f,.13f),new Vector2(.43f,.13f),new Vector2(.43f,-.13f),new Vector2(-.43f,-.13f)},color);
            }
            else if(kind==Symbol.Sword)
            {
                Poly(vh,center,size,new[]{new Vector2(-.15f,-.14f),new Vector2(-.15f,.29f),new Vector2(0,.48f),new Vector2(.15f,.29f),new Vector2(.15f,-.14f)},color);
                Quad(vh,center,size,-.31f,-.25f,.31f,-.10f,color);
                Quad(vh,center,size,-.08f,-.42f,.08f,-.22f,color);
                Quad(vh,center,size,-.13f,-.49f,.13f,-.39f,color);
            }
            else if(kind==Symbol.Energy)
            {
                Poly(vh,center,size,new[]{new Vector2(0,.49f),new Vector2(.37f,0),new Vector2(0,-.49f),new Vector2(-.37f,0)},color);
                Poly(vh,center,size,new[]{new Vector2(0,.34f),new Vector2(.19f,0),new Vector2(0,-.16f)},Color.Lerp(color,Color.white,.75f));
            }
            else Poly(vh,center,size,new[]{new Vector2(-.38f,.04f),new Vector2(0,.46f),new Vector2(.38f,.04f),new Vector2(.14f,.04f),new Vector2(.14f,-.45f),new Vector2(-.14f,-.45f),new Vector2(-.14f,.04f)},color);
        }
        private static void Quad(VertexHelper vh,Vector2 center,float size,float left,float bottom,float right,float top,Color color)
        {
            var offset=new Vector2((left+right)*.5f,(bottom+top)*.5f);
            float halfWidth=(right-left)*.5f,halfHeight=(top-bottom)*.5f;
            Poly(vh,center+offset*size,size,new[]{new Vector2(-halfWidth,-halfHeight),new Vector2(-halfWidth,halfHeight),new Vector2(halfWidth,halfHeight),new Vector2(halfWidth,-halfHeight)},color);
        }
        private static void Poly(VertexHelper vh,Vector2 center,float size,Vector2[] points,Color ink)
        {
            int start=vh.currentVertCount;vh.AddVert(center,ink,Vector2.zero);
            foreach(var p in points){var pos=center+p*size;vh.AddVert(new Vector2(Mathf.Round(pos.x),Mathf.Round(pos.y)),ink,Vector2.zero);}
            for(int i=0;i<points.Length;i++)vh.AddTriangle(start,start+1+i,start+1+(i+1)%points.Length);
        }
    }
}

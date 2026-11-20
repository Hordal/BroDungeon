using System;
using UnityEngine;
using UnityEngine.UI;
namespace Brodun.Presentation
{
    // Presentation primitives only: no combat, deck, reward or random calculations.
    public static class BattleUi
    {
        public static readonly Color Gold=new Color(.94f,.77f,.40f),Pale=new Color(.90f,.91f,.85f),Teal=new Color(.25f,.78f,.68f);
        public static RectTransform Rect(Transform parent,string name,Vector2 min,Vector2 max)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);
            rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;return rect;
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
            return intent.Type==IntentType.Attack?"다음 행동: "+intent.Amount+" 피해를 "+intent.Hits+"회 가합니다.\n현재 방어도 기준 HP 피해: "+intent.ExpectedHPDamage(block):
                intent.Type==IntentType.Block?"다음 행동: 적 방어도 "+intent.Amount+" 획득":"다음 행동: 적 힘 +"+intent.Amount;
        }
    }
    // HUD symbols, not another combat-effects system. No font glyph or external asset dependency.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleHudGlyph : MaskableGraphic
    {
        public enum Symbol { Shield, Sword, Power, Energy }
        private Symbol kind;
        public Symbol Kind {get=>kind;set{kind=value;SetVerticesDirty();}}
        public BattleHudGlyph(){useLegacyMeshGeneration=false;}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;float size=Mathf.Min(r.width,r.height);var center=r.center;
            if(kind==Symbol.Shield)
            {
                Poly(vh,center,size,new[]{new Vector2(-.39f,.40f),new Vector2(.39f,.40f),new Vector2(.30f,-.12f),new Vector2(0,-.47f),new Vector2(-.30f,-.12f)},color);
                Poly(vh,center+Vector2.up*size*.02f,size*.69f,new[]{new Vector2(-.39f,.40f),new Vector2(.39f,.40f),new Vector2(.30f,-.12f),new Vector2(0,-.47f),new Vector2(-.30f,-.12f)},new Color(.035f,.09f,.10f,color.a));
            }
            else if(kind==Symbol.Sword)
            {
                Poly(vh,center,size,new[]{new Vector2(-.29f,-.12f),new Vector2(.22f,.43f),new Vector2(.43f,.46f),new Vector2(.40f,.23f),new Vector2(-.12f,-.29f)},color);
                Poly(vh,center,size,new[]{new Vector2(-.45f,-.19f),new Vector2(-.33f,-.08f),new Vector2(.01f,-.43f),new Vector2(-.10f,-.49f)},color);
                Poly(vh,center,size,new[]{new Vector2(-.39f,-.49f),new Vector2(-.49f,-.39f),new Vector2(-.29f,-.18f),new Vector2(-.18f,-.29f)},color);
            }
            else if(kind==Symbol.Energy)
            {
                Poly(vh,center,size,new[]{new Vector2(0,.49f),new Vector2(.37f,0),new Vector2(0,-.49f),new Vector2(-.37f,0)},color);
                Poly(vh,center,size,new[]{new Vector2(0,.34f),new Vector2(.19f,0),new Vector2(0,-.16f)},Color.Lerp(color,Color.white,.75f));
            }
            else Poly(vh,center,size,new[]{new Vector2(-.38f,.04f),new Vector2(0,.46f),new Vector2(.38f,.04f),new Vector2(.14f,.04f),new Vector2(.14f,-.45f),new Vector2(-.14f,-.45f),new Vector2(-.14f,.04f)},color);
        }
        private static void Poly(VertexHelper vh,Vector2 center,float size,Vector2[] points,Color ink)
        {
            int start=vh.currentVertCount;vh.AddVert(center,ink,Vector2.zero);
            foreach(var p in points){var pos=center+p*size;vh.AddVert(new Vector2(Mathf.Round(pos.x),Mathf.Round(pos.y)),ink,Vector2.zero);}
            for(int i=0;i<points.Length;i++)vh.AddTriangle(start,start+1+i,start+1+(i+1)%points.Length);
        }
    }
}

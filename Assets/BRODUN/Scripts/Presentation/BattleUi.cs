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
}

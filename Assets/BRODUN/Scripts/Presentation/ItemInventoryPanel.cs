using UnityEngine;
using UnityEngine.UI;
namespace Brodun.Presentation {
public static class ItemInventoryPanel {
 public static void Show(RectTransform root,PresentationAssets art,RunState run){
  if(root.Find("Item inventory")!=null)return;
  var shade=BattleUi.Image(root,"Item inventory",null,new Color(0,0,0,.80f),Vector2.zero,Vector2.one);shade.raycastTarget=true;shade.transform.SetAsLastSibling();
  var panel=BattleUi.Panel(shade.transform,"Inventory panel",new Vector2(.12f,.12f),new Vector2(.88f,.90f));panel.SetAsLastSibling();
  BattleUi.Text(panel,"Title","보유 유물 / "+run.Items.Count,art.font,32,BattleUi.Gold,new Vector2(.06f,.86f),new Vector2(.94f,.97f));
  var viewport=BattleUi.Image(panel,"Viewport",null,new Color(.025f,.04f,.05f,1),new Vector2(.05f,.15f),new Vector2(.95f,.84f));viewport.gameObject.AddComponent<RectMask2D>();viewport.raycastTarget=true;
  var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.scrollSensitivity=45;scroll.viewport=viewport.rectTransform;
  var content=BattleUi.Rect(viewport.transform,"Items",new Vector2(0,1),Vector2.one);content.pivot=new Vector2(.5f,1);scroll.content=content;
  var layout=content.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=8;layout.childControlHeight=true;layout.childForceExpandHeight=false;layout.childControlWidth=true;layout.childForceExpandWidth=true;content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
  if(run.Items.Count==0)Add(content,"아직 유물이 없습니다. 엘리트·보스 승리 또는 성소에서 획득할 수 있습니다.",art);
  foreach(var owned in run.Items){var item=Item50Catalog.Find(owned.ItemId);Add(content,item.Name+(item.GrowthCap>0?" · 성장 "+owned.Progress+"/"+item.GrowthCap:"")+"\n"+item.Description,art,item.Id);}
  BattleUi.Button(panel,"Close items","닫기",art,new Vector2(.35f,.03f),new Vector2(.65f,.12f),()=>Object.Destroy(shade.gameObject));
 }
 static void Add(Transform parent,string text,PresentationAssets art,string itemId=null){var row=BattleUi.Rect(parent,"Item row",Vector2.zero,Vector2.one);row.gameObject.AddComponent<LayoutElement>().preferredHeight=120;bool hasArt=itemId!=null;if(hasArt)RelicArt.Add(row,itemId,new Vector2(.01f,.05f),new Vector2(.17f,.95f));var label=BattleUi.Text(row,"Item",text,art.font,22,BattleUi.Pale,new Vector2(hasArt?.19f:0,0),Vector2.one);label.resizeTextForBestFit=true;label.resizeTextMinSize=17;label.resizeTextMaxSize=22;}
}
}

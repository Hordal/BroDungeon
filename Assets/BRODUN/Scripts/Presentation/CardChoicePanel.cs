using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Brodun.Presentation {
public static class CardChoicePanel {
 public static void Show(Transform parent,PresentationAssets art,GameDefinitions data,IEnumerable<CardInstance> candidates,string title,Action<string> select,bool allowCancel=true) {
  var choices=candidates.ToArray();int page=0;RectTransform panel=null;
  Action render=null;render=()=>{
   if(panel!=null){panel.gameObject.SetActive(false);UnityEngine.Object.Destroy(panel.gameObject);}
   var shade=BattleUi.Image(parent,"Card choice",null,new Color(.01f,.02f,.03f,.99f),Vector2.zero,Vector2.one);shade.raycastTarget=true;panel=shade.rectTransform;
   BattleUi.Text(panel,"Title",title,art.font,32,BattleUi.Gold,new Vector2(.1f,.89f),new Vector2(.9f,.97f));
   Action<string> finish=id=>{panel.gameObject.SetActive(false);UnityEngine.Object.Destroy(panel.gameObject);select(id);};
   foreach(var pair in choices.Skip(page*6).Take(6).Select((c,i)=>new{c,i})) {
    var item=pair.c;var def=data.CardVersion(item.CardId,item.UpgradeLevel);float top=.85f-pair.i*.115f;
    BattleUi.Button(panel,"Choose "+item.InstanceId,def.Name+"  ["+(def.XCost?"X":def.Cost.ToString())+"]\n"+def.Description.Split('\n')[0],art,new Vector2(.1f,top-.10f),new Vector2(.9f,top),()=>finish(item.InstanceId));
   }
   if(page>0)BattleUi.Button(panel,"Previous","이전",art,new Vector2(.10f,.035f),new Vector2(.28f,.11f),()=>{page--;render();});
   if((page+1)*6<choices.Length)BattleUi.Button(panel,"Next","다음",art,new Vector2(.72f,.035f),new Vector2(.90f,.11f),()=>{page++;render();});
   if(allowCancel)BattleUi.Button(panel,"Cancel","선택 종료 / 취소",art,new Vector2(.33f,.035f),new Vector2(.67f,.11f),()=>finish(null));
  };render();
 }
}}

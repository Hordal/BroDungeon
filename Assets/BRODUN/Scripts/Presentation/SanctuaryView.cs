using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace Brodun.Presentation {
public sealed partial class RoomView {
 RectTransform tradeList;
 bool IsAltar=>room.Node.Event==EventKind.Altar;
 int tradeTab;
 void BuildSanctuary(){
  string[] tabs=new[]{"아이템 구매","아이템 판매","카드 구매","카드 판매","성소 서비스"};
  for(int i=0;i<tabs.Length;i++){
   int index=i;float x=.10f+i*.80f/tabs.Length;
   BattleUi.Button(root,"TradeTab_"+i,tabs[i],Art,new Vector2(x,.71f),new Vector2(x+.80f/tabs.Length-.008f,.77f),()=>{
    if(flow.Session.IsTransitioning)return;
    ShowTradeTab(index);
   });
  }
  leave=BattleUi.Button(root,"RoomLeave","던전 맵으로",Art,new Vector2(.68f,.055f),new Vector2(.88f,.135f),()=>{if(!flow.Session.IsTransitioning)flow.ReturnFromRoom();});
  BattleUi.Text(root,"Trade rules","카드 판매: 방문당 2장 · 덱 최소 8장 / 아이템 판매: 구매가의 50% · 누적 성장 소멸",Art.font,20,BattleUi.Pale,new Vector2(.10f,.65f),new Vector2(.90f,.70f));
  ShowTradeTab(0);
 }
 void ShowTradeTab(int tab){
  tradeTab=tab;
  if(tradeList!=null){tradeList.gameObject.SetActive(false);Destroy(tradeList.gameObject);tradeList=null;}
  tradeList=BattleUi.Rect(root,"Trade list",new Vector2(.10f,.205f),new Vector2(.90f,.64f));
  var viewport=BattleUi.Image(tradeList,"Viewport",null,new Color(.025f,.04f,.05f,1),Vector2.zero,Vector2.one);
  viewport.gameObject.AddComponent<RectMask2D>();viewport.raycastTarget=true;
  var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.scrollSensitivity=45;scroll.viewport=viewport.rectTransform;
  var content=BattleUi.Rect(viewport.transform,"Rows",new Vector2(0,1),Vector2.one);content.pivot=new Vector2(.5f,1);scroll.content=content;
  var layout=content.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=8;layout.padding=new RectOffset(8,8,8,8);layout.childControlHeight=true;layout.childForceExpandHeight=false;layout.childControlWidth=true;layout.childForceExpandWidth=true;
  content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
  int count=0;
  if(tab==0)for(int i=0;i<room.Visit.ItemOffers.Count;i++){
   int index=i;var offer=room.Visit.ItemOffers[i];var item=Item50Catalog.Find(offer.ItemId);
   TradeRow(content,item.Name+" · "+ItemCategory(item)+" · "+item.Tier+"등급",item.Description,offer.Purchased?"구매 완료":room.Run.Gold<offer.Price?"골드 부족 / "+offer.Price:offer.Price+" GOLD / 구매",room.CanBuyItem(index),()=>Transact(()=>room.BuyItem(index)),"ItemBuy_"+index);count++;
  }
  if(tab==1)foreach(var owned in room.Run.Items.ToArray()){
   var item=Item50Catalog.Find(owned.ItemId);string id=owned.ItemId;
   TradeRow(content,item.Name+" · "+ItemCategory(item)+(item.GrowthCap>0?" · 성장 "+owned.Progress+"/"+item.GrowthCap:""),item.Description,item.SellPrice+" GOLD / 판매",room.CanSellItem(id),()=>Transact(()=>room.SellItem(id)),"ItemSell_"+id);count++;
  }
  if(tab==2)for(int i=0;i<room.Visit.Offers.Count;i++){
   int index=i;var offer=room.Visit.Offers[i];var card=room.Data.Card(offer.CardId);
   TradeRow(content,card.Name+" · 비용 "+(card.XCost?"X":card.Cost.ToString()),card.Description,offer.Purchased?"구매 완료":room.Run.Gold<offer.Price?"골드 부족 / "+offer.Price:offer.Price+" GOLD / 구매",room.CanBuy(index),()=>Transact(()=>room.Buy(index)),"ShopBuy_"+index);count++;
  }
  if(tab==3)foreach(var instance in room.Run.Deck.ToArray()){
   string id=instance.InstanceId;var card=room.Data.Card(instance.CardId,instance.UpgradeLevel);
   TradeRow(content,card.Name,card.Description,room.CardSalePrice(id)+" GOLD / 판매",room.CanSellCard(id),()=>Transact(()=>room.SellCard(id)),"CardSell_"+id);count++;
  }
  if(tab==4){BuildSanctuaryServices(content);count=1;}
  if(count==0)TradeRow(content,"목록이 비어 있습니다","다른 탭을 선택하거나 다음 성소에서 다시 확인하세요.","",false,()=>{});
  Refresh();
 }
 string ItemCategory(ItemDefinition item)=>(item.ThemeId==null?"범용":DungeonThemes.Find(item.ThemeId).Name+" 테마")+" · "+item.Synergy;
 void BuildSanctuaryServices(Transform content){
  TradeRow(content,"카드 강화","선택한 카드 1장을 강화합니다. 방문당 1회.","30 GOLD",room.CanInteract&&!room.Visit.Upgraded&&room.Run.Gold>=30,()=>CardChoicePanel.Show(root,Art,room.Data,room.Run.Deck.Where(c=>c.UpgradeLevel==0),"강화할 카드",id=>{if(id!=null)room.Upgrade(id);ShowTradeTab(4);}));
  TradeRow(content,"카드 제거","판매와 별도로 카드 1장을 영구 제거합니다. 방문당 1회.","40 GOLD",room.CanInteract&&!room.Visit.Removed&&room.Run.Gold>=40&&room.Run.Deck.Count>1,()=>CardChoicePanel.Show(root,Art,room.Data,room.Run.Deck,"제거할 카드",id=>{if(id!=null)room.Remove(id);ShowTradeTab(4);}));
  if(IsAltar){
   TradeRow(content,"성소 회복","기존 성소 선택입니다. 회복과 희생 중 방문당 하나를 선택합니다.",room.Rules.HealPrice+" GOLD",room.CanInteract&&!room.Visit.ChoiceMade&&room.Run.Gold>=room.Rules.HealPrice,()=>Transact(()=>room.Choose(RoomChoice.PaidHeal)),"AltarHeal");
   TradeRow(content,"성소 희생","HP "+room.Rules.SacrificeHP+"를 지불하고 GOLD "+room.Data.Config.EventGold+" 획득.","희생",room.CanInteract&&!room.Visit.ChoiceMade&&room.Run.CurrentHP>room.Rules.SacrificeHP,()=>Transact(()=>room.Choose(RoomChoice.Sacrifice)),"AltarSacrifice");
  }
 }
 void Transact(Func<bool> action){if(flow.Session.IsTransitioning)return;action();ShowTradeTab(tradeTab);}
 void TradeRow(Transform parent,string title,string description,string label,bool enabled,Action action,string actionId="TradeAction"){
  var row=BattleUi.Image(parent,actionId+"Row",null,new Color(.07f,.11f,.14f,.98f),Vector2.zero,Vector2.one);
  row.gameObject.AddComponent<LayoutElement>().preferredHeight=160;
  BattleUi.Text(row.transform,"Name",title,Art.font,25,BattleUi.Gold,new Vector2(.02f,.70f),new Vector2(.75f,.97f));
  var body=BattleUi.Text(row.transform,"Effect",description,Art.font,20,BattleUi.Pale,new Vector2(.02f,.06f),new Vector2(.75f,.69f));body.resizeTextForBestFit=true;body.resizeTextMinSize=15;body.resizeTextMaxSize=20;
  var button=BattleUi.Button(row.transform,actionId,label,Art,new Vector2(.77f,.23f),new Vector2(.98f,.76f),action);button.interactable=enabled;
 }
}
}

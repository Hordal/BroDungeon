using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace Brodun.Presentation {
public sealed partial class RoomView {
 RectTransform tradeList;
 int tradeTab;
 void BuildSanctuary(){
  string[] tabs=IsAltar
   ?new[]{"아이템 구매","아이템 판매","카드 구매","카드 판매","제단 거래"}
   :new[]{"아이템 구매","아이템 판매","카드 구매","카드 판매","정비"};
  for(int i=0;i<tabs.Length;i++){
   int index=i;float x=.10f+i*.80f/tabs.Length;
   BattleUi.Button(root,"TradeTab_"+i,tabs[i],Art,new Vector2(x,.71f),new Vector2(x+.80f/tabs.Length-.008f,.77f),()=>{
    if(flow.Session.IsTransitioning||revealingSwap)return;
    if(room.Visit.PendingAltarCards.Count>0&&index!=4){feedback.text="받을 제단 카드를 먼저 선택하세요.";return;}
    ShowTradeTab(index);
   });
  }
  leave=BattleUi.Button(root,"RoomLeave","던전 맵으로",Art,new Vector2(.68f,.055f),new Vector2(.88f,.135f),()=>{if(!flow.Session.IsTransitioning)flow.ReturnFromRoom();});
  BattleUi.Text(root,"Trade rules","카드 판매: 방문당 2장 · 덱 최소 8장 / 아이템 판매: 구매가의 50% · 누적 성장 소멸",Art.font,20,BattleUi.Pale,new Vector2(.10f,.65f),new Vector2(.90f,.70f));
  ShowTradeTab(IsAltar&&(room.Visit.PendingAltarCards.Count>0||room.Visit.ChoiceMade)?4:0);
 }
 void ShowTradeTab(int tab){
  tradeTab=tab;
  if(tradeList!=null){tradeList.gameObject.SetActive(false);Destroy(tradeList.gameObject);tradeList=null;}
  if(altarPanel!=null){altarPanel.gameObject.SetActive(false);Destroy(altarPanel.gameObject);altarPanel=null;}
  if(tab==4&&IsAltar){BuildAltar();Refresh();return;}
  tradeList=BattleUi.Rect(root,"Trade list",new Vector2(.10f,.205f),new Vector2(.90f,.64f));
  var viewport=BattleUi.Image(tradeList,"Viewport",null,BattleUi.Ink,Vector2.zero,Vector2.one);
  viewport.gameObject.AddComponent<RectMask2D>();viewport.raycastTarget=true;
  var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.scrollSensitivity=45;scroll.viewport=viewport.rectTransform;
  var content=BattleUi.Rect(viewport.transform,"Rows",new Vector2(0,1),Vector2.one);content.pivot=new Vector2(.5f,1);scroll.content=content;
  var layout=content.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=8;layout.padding=new RectOffset(8,8,8,8);layout.childControlHeight=true;layout.childForceExpandHeight=false;layout.childControlWidth=true;layout.childForceExpandWidth=true;
  content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
  int count=0;
  if(tab==0)for(int i=0;i<room.Visit.ItemOffers.Count;i++){
   int index=i;var offer=room.Visit.ItemOffers[i];var item=Item50Catalog.Find(offer.ItemId);
   TradeRow(content,item.Name+" · "+ItemOwner(item)+" · "+item.Tier+"등급",item.Description,offer.Purchased?"구매 완료":room.Run.Gold<offer.Price?"골드 부족 / "+offer.Price:offer.Price+" GOLD / 구매",room.CanBuyItem(index),()=>Transact(()=>room.BuyItem(index)),"ItemBuy_"+index,item.Id);count++;
  }
  if(tab==1)foreach(var owned in room.Run.Items.ToArray()){
   var item=Item50Catalog.Find(owned.ItemId);string id=owned.ItemId;
   TradeRow(content,item.Name+" · "+ItemOwner(item)+(item.GrowthCap>0?" · 성장 "+owned.Progress+"/"+item.GrowthCap:""),item.Description,item.SellPrice+" GOLD / 판매",room.CanSellItem(id),()=>Transact(()=>room.SellItem(id)),"ItemSell_"+id,item.Id);count++;
  }
  if(tab==2)for(int i=0;i<room.Visit.Offers.Count;i++){
   int index=i;var offer=room.Visit.Offers[i];var card=room.Data.Card(offer.CardId);
   TradeRow(content,card.Name+" · 비용 "+(card.XCost?"X":card.Cost.ToString()),card.Description,offer.Purchased?"구매 완료":room.Run.Gold<offer.Price?"골드 부족 / "+offer.Price:offer.Price+" GOLD / 구매",room.CanBuy(index),()=>Transact(()=>room.Buy(index)),"ShopBuy_"+index);count++;
  }
  if(tab==3)foreach(var instance in room.Run.Deck.ToArray()){
   string id=instance.InstanceId;var card=room.Data.CardVersion(instance.CardId,instance.UpgradeLevel);
   TradeRow(content,card.Name,card.Description,room.CardSalePrice(id)+" GOLD / 판매",room.CanSellCard(id),()=>Transact(()=>room.SellCard(id)),"CardSell_"+id);count++;
  }
  if(tab==4){BuildServices(content);count=1;}
  if(count==0)TradeRow(content,"목록이 비어 있습니다","다른 탭을 선택하거나 다음 성소에서 다시 확인하세요.","",false,()=>{});
  Refresh();
 }
 string ItemOwner(ItemDefinition item)=>item.ThemeId==null?"범용":DungeonThemes.Find(item.ThemeId).Name+" 테마";
 void BuildServices(Transform content){
  TradeRow(content,"카드 강화","카드 1장을 강화합니다. 방문당 1회.",RoomRuntime.UpgradePrice+" GOLD",room.CanUpgrade,
   ()=>ChooseServiceCard("강화할 카드",true),"SanctuaryUpgrade");
  TradeRow(content,"카드 제거","카드 1장을 덱에서 제거합니다. 방문당 1회.",RoomRuntime.RemovalPrice+" GOLD",room.CanRemove,
   ()=>ChooseServiceCard("제거할 카드",false),"SanctuaryRemove");
  if(!room.Node.IsBossSanctuary)return;
  TradeRow(content,"회복","HP 최대 "+room.Data.Config.EventHeal+" 회복. 회복과 희생 중 1회 선택.",
   RoomRuntime.PrayerPrice+" GOLD",room.CanRecover,()=>Transact(room.RecoverAtSanctuary),"SanctuaryHeal");
  TradeRow(content,"희생","HP "+RoomRuntime.SacrificeHealth+" 소모 후 GOLD "+room.Data.Config.EventGold+" 획득.",
   "선택",room.CanSacrifice,()=>Transact(room.SacrificeAtSanctuary),"SanctuarySacrifice");
 }
 void ChooseServiceCard(string title,bool upgrade){
  if(!room.CanUseServices)return;
  var cards=room.Run.Deck.Where(c=>!upgrade||c.UpgradeLevel==0&&room.Data.Card(c.CardId).IsReworked);
  CardChoicePanel.Show(root,Art,room.Data,cards,title,id=>{
   if(id!=null)Transact(()=>upgrade?room.Upgrade(id):room.Remove(id));
  });
 }
 void Transact(Func<bool> action){if(flow.Session.IsTransitioning||revealingSwap)return;action();ShowTradeTab(tradeTab);}
 void TradeRow(Transform parent,string title,string description,string label,bool enabled,Action action,string actionId="TradeAction",string itemId=null){
  var row=BattleUi.Image(parent,actionId+"Row",null,new Color(.07f,.11f,.14f,.98f),Vector2.zero,Vector2.one);
  row.gameObject.AddComponent<LayoutElement>().preferredHeight=160;
  bool hasArt=itemId!=null;if(hasArt)RelicArt.Add(row.transform,itemId,new Vector2(.01f,.08f),new Vector2(.16f,.92f));
  float textLeft=hasArt?.18f:.02f;
  BattleUi.Text(row.transform,"Name",title,Art.font,25,BattleUi.Gold,new Vector2(textLeft,.70f),new Vector2(.75f,.97f));
  var body=BattleUi.Text(row.transform,"Effect",description,Art.font,20,BattleUi.Pale,new Vector2(textLeft,.06f),new Vector2(.75f,.69f));body.resizeTextForBestFit=true;body.resizeTextMinSize=15;body.resizeTextMaxSize=20;
  var button=BattleUi.Button(row.transform,actionId,label,Art,new Vector2(.77f,.23f),new Vector2(.98f,.76f),action);button.interactable=enabled;
 }
}
}

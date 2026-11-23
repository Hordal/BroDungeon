using System;
using System.Collections.Generic;
using System.Linq;
namespace Brodun {
public sealed partial class BattleRuntime {
 readonly HashSet<string> itemUsed=new HashSet<string>();
 int itemAttacks,itemCards,itemSkills,itemCardOpeningBlock,itemDrawBefore,itemDirectHits,itemRetention,itemExpensive;
 bool itemHealed,itemOwnedPlayed;
 bool IH(string id)=>Run.HasItem(id);
 bool IF(string id)=>IH(id)&&itemUsed.Add(id);
 bool ItemDebuff()=>target70!=null&&target70.IsAlive&&(target70.Status.Poison>0||target70.Status.Weak>0||target70.Status.Brand>0);
 void ItemDraw(int n){
  if(PendingDiscard)pendingItemDraw+=n;
  else DrawCards(trace,n,CombatCause.Item);
 }
 void ItemHeal(int n){if(trace!=null)Heal70(n);else Run.Heal(n);}
 void InitializeItems(){
  if(!UsesReworkedCards)return;
  if(IH("I01"))Strength++;if(IH("I02"))Block+=6;if(IH("I03"))Deck.Draw(1);
  if(IH("I04"))Energy=Math.Min(Data.Config.EnergyCap,Energy+1);
  if(IH("I17"))Strength+=Run.ItemProgress("I17");if(IH("I22"))Block+=Run.ItemProgress("I22");
  if(IH("I33"))foreach(var enemy in Enemies){enemy.Status.ApplyWeak(1);if(Run.SelectedCharacter==CharacterId.Priest)enemy.Status.Brand=1;}
  if(IH("I34")){Block+=3;if(Run.SelectedCharacter==CharacterId.Priest)Grace=Math.Min(10,Grace+2);}
  if(IH("I35"))Strength+=Run.ItemProgress("I35")/2;
  if(IH("I36")&&Run.Deck.Count<=12)Deck.Draw(1);
  if(IH("I38")&&Run.Deck.Count>=18)Block+=10;
  if(IH("I39")&&Run.CurrentHP*2<=Run.MaxHP)Block+=8;
  if(IH("I41"))Block+=4;
  if(IH("I49")&&Run.Deck.Count(c=>!Data.Card(c.CardId).Owner.HasValue)>=4){Block+=3;Energy=Math.Min(Data.Config.EnergyCap,Energy+1);}
 }
 void BeginItemTurn(){itemUsed.Clear();itemSkills=0;itemHealed=false;itemOwnedPlayed=false;}
 int ItemAttackBonus(int hitIndex){int n=IH("I25")&&++itemDirectHits%3==0?4:0;if(hitIndex!=0)return n;if(IF("I07"))n+=3;if(itemCardOpeningBlock>=12&&IF("I21"))n+=5;if(ItemDebuff()&&IF("I48"))n+=4;return n;}
 void BeforeItemCard(CardDefinition card){
  itemCardOpeningBlock=Block;itemDrawBefore=Deck.TotalDrawn;
  if(card.Type==CardType.Attack){
   if(IF("I43"))TemporaryStrength+=2;
   if(ItemDebuff()&&IF("I29"))ItemHeal(2);
   if(ItemDebuff()&&IF("I30")){
    int before=target70.Block;
    target70.Block=Math.Max(0,before-3);
    trace.Add(CombatEventKind.Block,CombatCause.Item,CombatTrace.PlayerId,target70.TargetId,before,target70.Block,effectId:"I30");
   }
  }
 }
 void AfterItemCard(CardDefinition card,int previousBlock,int previousStrength){
  if(Run.Items.Count==0)return;
  bool gainedBlock=Block>previousBlock;int spentBlock=card.Id=="W09"?previousBlock/2:card.Id=="W16"?previousBlock:0;
  if(gainedBlock){if(IF("I08"))Block+=3;if(target70.IsAlive&&IF("I24"))ApplyEnemyStatus(trace,target70,StatusEffectType.Weak,1,CombatCause.Item);if(IF("I45"))itemRetention+=2;}
  if(card.Type==CardType.Skill&&IF("I09"))Block+=4;
  if(card.Type==CardType.Attack&&IH("I10")&&++itemAttacks%3==0)Strength++;
  if(++itemCards%3==0){if(IH("I11"))Block+=4;if(IH("I42")&&target70.IsAlive)StatusHit(3);}
  if(card.Exhausts){if(IF("I12"))ItemHeal(3);if(IF("I44")){Block+=2;if(target70.IsAlive)StatusHit(4);}}
  if(card.Type==CardType.Power&&IH("I13"))Block+=5;
  if(paid>=2){if(IF("I14"))AP(1);if(IF("I26"))scheduledEnergy++;if(card.Type==CardType.Attack&&IH("I28"))Block+=paid;if(IH("I47")&&++itemExpensive%3==0){scheduledEnergy++;ItemDraw(1);}}
  if(card.Type==CardType.Skill&&++itemSkills==3&&IH("I15"))ItemDraw(1);
  if(card.Element==ElementType.Poison&&target70.IsAlive){int poison=(IH("I16")?1:0)+(IF("I40")?2:0);if(poison>0)ApplyEnemyStatus(trace,target70,StatusEffectType.Poison,poison,CombatCause.Item);}
  if(Strength>previousStrength){if(IF("I18"))Block+=3;if(IF("I23"))Strength++;}
  if(spentBlock>0&&IF("I19"))Block+=Math.Min(6,spentBlock/4);
  if(Deck.TotalDrawn>itemDrawBefore&&IF("I27"))ItemDraw(1);
  if(card.Type==CardType.Guard&&IF("I41"))ItemDraw(1);
  if(!card.Owner.HasValue&&IF("I50")){Block+=2;if(itemOwnedPlayed)ItemDraw(1);}
  if(card.Owner.HasValue)itemOwnedPlayed=true;
 }
 void ItemOnActualHeal(){itemHealed=true;if(IF("I31")){Block+=2;if(Run.SelectedCharacter==CharacterId.Priest)Grace=Math.Min(10,Grace+1);}if(IF("I32"))Block+=4;if(IF("I46"))Block+=3;}
 void EndItems(){if(IH("I46")&&!itemHealed)Block+=2;}
 void ItemVictory(){
  if(!UsesReworkedCards||!Run.itemVictoryRooms.Add(Run.CurrentArea+":"+RoomId))return;
  Run.LastItemReward=null;
  if(IH("I05"))Run.Heal(3);if(IH("I06"))Run.AddGold(Math.Min(8,int.MaxValue-Run.Gold));
  if(IH("I37"))Run.AddGold(Math.Min(Math.Min(6,Run.Gold/20),int.MaxValue-Run.Gold));
  Run.GrowItem("I17");Run.GrowItem("I22",2);Run.GrowItem("I35");
  var room=DungeonRuntime.Node(Run,RoomId);
  if(room.Type==RoomType.Boss||room.Encounter!=EnemyType.Elite)return;
  var available=Item50Catalog.Available(Run.ThemeId).Where(item=>!Run.HasItem(item.Id)).ToArray();
  if(available.Length==0)return;
  var rng=new SeededRandom(EncounterPool.NodeSeed(Run.AreaSeed,RoomId,0x50D09u));
  var reward=available[(int)(rng.Next()*available.Length)];
  if(Run.AddItem(reward.Id))Run.LastItemReward=reward.Id;
 }
}
}

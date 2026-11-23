using System;
using System.Collections.Generic;
using System.Linq;
namespace Brodun {
public sealed partial class BattleRuntime {
 readonly HashSet<string> itemUsed=new HashSet<string>();
 int itemAttacks,itemCards,itemSkills,itemCardOpeningBlock;
 bool IH(string id)=>Run.HasItem(id);
 bool IF(string id)=>IH(id)&&itemUsed.Add(id);
 void ItemDraw(int amount){if(trace!=null)DrawCards(trace,amount,CombatCause.Item);else Deck.Draw(amount);}
 void ItemHeal(int amount){if(trace!=null)Heal70(amount);else Run.Heal(amount);}
 void InitializeItems(){
  if(!UsesReworkedCards)return;
  if(IH("I01"))Strength++;
  if(IH("I02"))Block+=6;
  if(IH("I03"))Deck.Draw(1);
  if(IH("I04"))Energy=Math.Min(Data.Config.EnergyCap,Energy+1);
  if(IH("I17"))Strength+=Run.ItemProgress("I17");
  if(IH("I22"))Block+=Run.ItemProgress("I22");
  if(IH("I33"))foreach(var enemy in Enemies)enemy.Status.Brand=2;
  if(IH("I34"))Grace=Math.Min(10,Grace+2);
  if(IH("I35"))Strength+=Run.ItemProgress("I35")/2;
  if(IH("I36")&&Run.Deck.Count<=12)Deck.Draw(1);
  if(IH("I38")&&Run.Deck.Count>=18)Block+=10;
  if(IH("I39")&&Run.CurrentHP*2<=Run.MaxHP)Block+=8;
 }
 void BeginItemTurn(){itemUsed.Clear();itemSkills=0;}
 int ItemAttackBonus(){int bonus=0;if(IF("I07"))bonus+=3;if(itemCardOpeningBlock>=12&&IF("I21"))bonus+=5;return bonus;}
 void BeforeItemCard(CardDefinition card,bool mageElement){
  itemCardOpeningBlock=Block;
  if(mageElement&&card.Element==ElementType.Fire&&IF("I23"))Strength++;
  if(mageElement&&card.Element==ElementType.Ice&&IF("I24")){
   var enemy=target70??Enemies.FirstOrDefault(e=>e.IsAlive);
   if(enemy!=null)ApplyEnemyStatus(trace,enemy,StatusEffectType.Weak,1,CombatCause.Item);
  }
 }
 void AfterItemCard(CardDefinition card,int previousBlock,int previousStrength,int consumedBrand){
  if(Run.Items.Count==0)return;
  bool gainedBlock=Block>previousBlock;
  if(gainedBlock&&IF("I08"))Block+=3;
  if(card.Type==CardType.Skill&&IF("I09"))Block+=4;
  if(card.Type==CardType.Attack&&IH("I10")&&++itemAttacks%3==0)Strength++;
  if(IH("I11")&&++itemCards%3==0)Block+=4;
  if(card.Exhausts&&IF("I12"))ItemHeal(3);
  if(card.Type==CardType.Power&&IH("I13"))Block+=5;
  if(paid>=2&&IF("I14"))AP(1);
  if(card.Type==CardType.Skill&&++itemSkills==3&&IH("I15"))ItemDraw(1);
  if(card.Element==ElementType.Poison&&target70!=null&&target70.IsAlive){
   int poison=(IH("I16")?1:0)+(IF("I40")?2:0);
   if(poison>0)ApplyEnemyStatus(trace,target70,StatusEffectType.Poison,poison,CombatCause.Item);
  }
  if(Strength>previousStrength&&IF("I18"))Block+=3;
  int spentBlock=card.Id=="W09"?previousBlock/2:card.Id=="W16"?previousBlock:0;
  if(spentBlock>0&&IF("I19"))Block+=Math.Min(6,spentBlock/4);
  bool mage=Run.SelectedCharacter==CharacterId.Mage&&card.Owner==CharacterId.Mage;
  if(mage&&card.Element==ElementType.Lightning&&IH("I25")&&target70!=null&&target70.IsAlive)HitEnemy(trace,target70,2,CombatCause.Item,effectId:"I25");
  if(mage&&card.Element==ElementType.Water&&IF("I26"))scheduledEnergy++;
  if(mage&&card.Element==ElementType.Wind&&IF("I27"))ItemDraw(1);
  if(consumedBrand>0&&IF("I29"))ItemHeal(2);
 }
 void ItemOnActualHeal(){
  if(IF("I31"))Grace=Math.Min(10,Grace+1);
  if(IF("I32"))Block+=4;
 }
 void ItemVictory(){
  if(!UsesReworkedCards||!Run.itemVictoryRooms.Add(Run.CurrentArea+":"+RoomId))return;
  Run.LastItemReward=null;
  if(IH("I05"))Run.Heal(3);
  if(IH("I06"))Run.AddGold(Math.Min(8,int.MaxValue-Run.Gold));
  if(IH("I37"))Run.AddGold(Math.Min(Math.Min(6,Run.Gold/20),int.MaxValue-Run.Gold));
  Run.GrowItem("I17");Run.GrowItem("I22",2);Run.GrowItem("I35");
  var room=DungeonRuntime.Node(Run,RoomId);
  if(room.Type==RoomType.Boss||room.Encounter==EnemyType.Elite){
   var rng=new SeededRandom(EncounterPool.NodeSeed(Run.AreaSeed,RoomId,0x40D09u));
   var pool=Item40Catalog.All.Where(i=>(!i.Owner.HasValue||i.Owner==Run.SelectedCharacter)&&!Run.HasItem(i.Id)).ToArray();
   if(pool.Length>0){var item=pool[Math.Min(pool.Length-1,(int)(rng.Next()*pool.Length))];Run.AddItem(item.Id);Run.LastItemReward=item.Id;}
  }
 }
}
}

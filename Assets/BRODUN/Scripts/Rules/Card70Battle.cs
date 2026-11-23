using System;
using System.Collections.Generic;
using System.Linq;
namespace Brodun {
public sealed class CardPlayOptions {
 public string SelectedInstance; public string[] RecallInstances=Array.Empty<string>(); public bool ExtraEnergy;
}
public sealed partial class BattleRuntime {
 public int Resonance {get;internal set;} public int Grace {get;internal set;} public int Brand {get=> (target70??Enemy).Status.Brand;internal set=>(target70??Enemy).Status.Brand=value;}
 public int TemporaryStrength {get;internal set;}
 public int IceRetention {get;private set;}
 public int SpentResonance {get;private set;}
 int pendingDraw,pendingItemDraw;
 public bool PendingDiscard=>pendingDraw>0;
 public IReadOnlyDictionary<string,int> Powers => powers;
 public int InstalledPowerCount => activePowers.Count;
 // The dictionary counts copies; each installation owns its upgrade and trigger state.
 readonly Dictionary<string,int> powers=new Dictionary<string,int>();
 sealed class InstalledPower {
  public string Id; public int Upgrade,Training,GuardTraining;
  public readonly HashSet<string> Used=new HashSet<string>();
 }
 readonly List<InstalledPower> activePowers=new List<InstalledPower>();
 readonly List<int> regens=new List<int>();
 readonly HashSet<string> once=new HashSet<string>();
 int scheduledEnergy,scheduledBlock,retainBlock,cardsUsed,amplify;
 bool healed;
 CardDefinition resolving; CombatTrace trace; bool conditional; EnemyInstance target70; int paid; CardPlayOptions selection;
 bool Has(string id)=>powers.ContainsKey(id);
 InstalledPower[] Trigger(string id,string channel="main")=>activePowers.Where(p=>p.Id==id&&p.Used.Add(channel)).ToArray();
 void Install(CardDefinition d) {
  activePowers.Add(new InstalledPower{Id=d.Id,Upgrade=d.UpgradeLevel});
  powers[d.Id]=powers.TryGetValue(d.Id,out int count)?count+1:1;
 }
 void GuardTraining() {foreach(var p in Trigger("N07")){Block+=(p.Upgrade>0?3:2)+p.GuardTraining;p.GuardTraining++;}}
 public CardDefinition Definition(CardInstance c)=>Data.CardVersion(c.CardId,c.UpgradeLevel);
 public int EffectiveCost(CardInstance c) {var d=Definition(c);return d.XCost?Energy:d.Cost;}
 private bool CanPlay70(string id) {
  var c=Deck.Hand.FirstOrDefault(x=>x.InstanceId==id);if(c==null||Result!=BattleResult.Active||PendingDiscard)return false;
  var d=Definition(c);if(!d.IsReworked)return Energy>=d.Cost;
  if(Energy<(d.XCost?1:d.Cost))return false;
  int hp=d.Id=="W08"?3:d.Id=="P07"?(d.UpgradeLevel>0?3:4):d.Id=="P11"?(d.UpgradeLevel>0?3:5):0;
  if(hp>0&&Run.CurrentHP<=hp)return false;
  int res=d.Id=="M10"?(d.UpgradeLevel>0?1:2):d.Id=="M12"?(d.UpgradeLevel>0?3:4):d.Id=="M16"?(d.UpgradeLevel>0?1:2):0;
  if(Resonance<res)return false;
  if((d.Id=="N05"||d.Id=="N10")&&Deck.Hand.Count<2)return false;
  return true;
 }
 bool Play70(CardInstance instance,CardDefinition d,CardPlayOptions options,EnemyInstance target) {
  if(!CanPlay70(instance.InstanceId))return false;
  options=options??new CardPlayOptions();
  if(d.Id=="N05"||d.Id=="N10") {
   if(!Deck.Hand.Any(c=>c.InstanceId==options.SelectedInstance&&c.InstanceId!=instance.InstanceId))return false;
  }
  if(d.Id=="N16" && (options.RecallInstances==null||options.RecallInstances.Length>2||options.RecallInstances.Distinct().Count()!=options.RecallInstances.Length||options.RecallInstances.Any(id=>!Deck.DiscardPile.Any(c=>c.InstanceId==id))))return false;
  if(options.ExtraEnergy&&(d.Id!="M11"||Energy<d.Cost+2))return false;
  resolving=d;selection=options;target70=target;conditional=false;trace=NewTrace(d.Id);
  paid=d.XCost?Energy:d.Cost+(options.ExtraEnergy?2:0);
  ChangeEnergy(trace,Energy-paid,CombatCause.CardCost);
  int preBlock=Block, preStrength=Strength;
  // Used card leaves the hand before draws but is not available for shuffle/recall until resolution ends.
  Deck.BeginUse(instance.InstanceId);
  // Non-poison elements are Mage-only. Shared poison works for every class.
  bool mageElement=Run.SelectedCharacter==CharacterId.Mage&&d.Owner==CharacterId.Mage;
  BeforeItemCard(d);
  if(mageElement&&d.Element==ElementType.Fire)Strength++;
  if(mageElement&&d.Element==ElementType.Ice){Block+=3;IceRetention+=3;}
  if(Run.SelectedCharacter==CharacterId.Mage&&IsElemental(d.Element)) {
   int before=Resonance;Res(1);foreach(var p in Trigger("M08"))Res(p.Upgrade>0?2:1);
   if(Resonance>before)foreach(var p in Trigger("M15")){AP(1);DrawCards(trace,1);}
  }
  if(d.Type==CardType.Attack)Strength+=Trigger("W14").Length;
  int brandBefore=Brand;bool allBrand=d.Id=="P15";int consumedBrand=0;
  bool up=d.UpgradeLevel>0;Func<int,int,int> v=(a,b)=>up?b:a;
  int n;
  switch(d.Id) {
   case "W01":case "M01":case "P01": Hit70(v(6,8));break;
   case "W02":case "M02":case "P02": Block+=v(5,8);break;
   case "W03":Hit70(v(5,7));Strength++;break;
   case "W04":Hit70(v(5,8)+Block*v(25,28)/100);break;
   case "W05":Block+=v(8,11);retainBlock=Math.Max(retainBlock,v(4,6));break;
   case "W06":QueueDiscardDraw(v(2,3));if(Strength+TemporaryStrength>=3)Refund(1);break;
   case "W07":Block+=v(5,8);Strength++;break;
   case "W08":PayHealth(3);Strength+=v(2,3);break;
   case "W09":n=Block/2;Block-=n;Hit70(v(8,12)+n*2);break;
   case "W10":Hit70(v(4,6));Block+=v(4,6)+Strength+TemporaryStrength;break;
   case "W11":Block=(Block+v(5,8))*2;break;
   case "W12":Block+=v(6,9)+(Strength+TemporaryStrength)*2;break;
   case "W15":if(Strength>0)Strength*=2;else Strength++;break;
   case "W16":n=Block;Block=0;Hit70(8+n*2);break;
   case "W18":n=Math.Max(0,Strength);Strength-=n;Hit70(12+n*v(8,10),1,true,false);break;
   case "M03":Hit70(v(6,9));break;
   case "M04":Hit70(4);Block+=v(2,5);break;
   case "M05":Hit70(v(3,4),Resonance>=6?3:2);break;
   case "M06":Res(2);Block+=v(4,7);break;
   case "M07":Block+=v(5,8)+Resonance;break;
   case "M09":n=Resonance;SpendRes(n);Hit70(10+n*4);break;
   case "M10":SpendRes(v(2,1));scheduledEnergy++;break;
   case "M11":Hit70(v(14,17),selection.ExtraEnergy?2:1,true);break;
   case "M12":SpendRes(v(4,3));AP(2);break;
   case "M13":amplify=v(4,6);break;
   case "M16":SpendRes(v(2,1));DrawCards(trace,1);break;
   case "M17":Hit70(v(9,11)*paid);break;
   case "M18":Block+=v(6,9)+SpentResonance;break;
   case "P03":Hit70(v(4,7));break;
   case "P04":Hit70(v(5,8)+Brand*2);break;
   case "P05":Heal70(v(4,6));Block+=4;break;
   case "P06":Block+=v(5,8)+Grace;break;
   case "P07":PayHealth(v(4,3));Grace=Math.Min(10,Grace+3);Mark(1);break;
   case "P10":Mark(v(2,3));ApplyEnemyStatus(trace,target70,StatusEffectType.Weak,1,CombatCause.Card);break;
   case "P11":PayHealth(v(5,3));AP(2);break;
   case "P12":Heal70(v(3,5)+Grace/2);break;
   case "P13":regens.Add(3);break;
   case "P15":n=Brand;SetBrand(0);consumedBrand=n;Hit70(8+n*6);break;
   case "P16":n=Grace;Grace=0;Hit70(10+n*4);Heal70(n);break;
   case "P17":if(Brand==0)Mark(2);else SetBrand(Math.Min(8,Brand*2));break;
   case "P18":DrawCards(trace,v(2,3));if(healed)Refund(1);break;
   case "N01":QueueDiscardDraw(2);if(up)Block+=3;break;
   case "N02":Block+=v(6,9);retainBlock=Math.Max(retainBlock,v(6,9));break;
   case "N03":Hit70(v(5,8)+cardsUsed*2);break;
   case "N04":Heal70(v(3,5)+Deck.ExhaustPile.Count);break;
   case "N05":scheduledEnergy++;if(up)scheduledBlock+=4;Deck.Retain(selection.SelectedInstance);break;
   case "N06":if(up)Strength++;break;
   case "N08":Hit70(4);ApplyEnemyStatus(trace,target70,StatusEffectType.Poison,v(0,1)+powers.Count,CombatCause.Card);break;
   case "N09":Block+=v(5,8)+powers.Count*3;break;
   case "N10":Deck.Exhaust(selection.SelectedInstance);DrawCards(trace,2);if(up)Block+=3;break;
   case "N11":DrawCards(trace,3);break;
   case "N12":AP(2);if(!up)scheduledEnergy--;break;
   case "N15":once.Add("double-ready");break;
   case "N16":foreach(string id in selection.RecallInstances)Deck.Recall(id);break;
   default:if(d.Type!=CardType.Power)throw new InvalidOperationException("Unimplemented card: "+d.Id);break;
  }
  if(mageElement&&d.Element==ElementType.Lightning&&target70.CurrentHP>0)StatusHit(3);
  if(mageElement&&d.Element==ElementType.Water)scheduledEnergy++;
  if(mageElement&&d.Element==ElementType.Wind)DrawCards(trace,1);
  if(!d.Owner.HasValue&&d.Element==ElementType.Poison)ApplyEnemyStatus(trace,target70,StatusEffectType.Poison,2,CombatCause.Card);
  if(d.Type==CardType.Attack&&Run.SelectedCharacter==CharacterId.Priest&&!allBrand&&brandBefore>0&&target70.IsAlive){SetBrand(Brand-1);consumedBrand=1;StatusHit(4);}
  if(d.Id=="P03")Mark(1);
  if(consumedBrand>0) {
   foreach(var p in Trigger("P09")){Mark(1);Block+=p.Upgrade>0?5:3;}
   foreach(var p in Trigger("P14","brand"))Heal70(2);
  }
  bool cardBlock=(mageElement&&d.Element==ElementType.Ice)||d.Type==CardType.Guard||d.Id=="W10"||d.Id=="M04"||(up&&(d.Id=="N01"||d.Id=="N10"));
  if(cardBlock&&Block>preBlock)GuardTraining();
  if(d.Type==CardType.Attack) {
   foreach(var p in activePowers.Where(p=>p.Id=="N06"))if(++p.Training>=3){p.Training-=3;Strength++;}
   if(paid>=2)foreach(var p in Trigger("M14"))Res(p.Upgrade>0?3:2);
  }
  if(paid>=2)foreach(var p in Trigger("N14"))AP(1);
  // Existing powers may react to this card, but a new installation cannot trigger itself.
  if(d.Type==CardType.Power)Install(d);
  AfterItemCard(d,preBlock,preStrength);
  if(Block!=preBlock)RecordBlock(preBlock);
  if(Strength!=preStrength)trace.Add(CombatEventKind.Status,CombatCause.Card,CombatTrace.PlayerId,CombatTrace.PlayerId,preStrength,Strength,status:StatusEffectType.Strength);
  cardsUsed++;
  Deck.FinishUse(instance,d.Exhausts);
  trace.Add(d.Exhausts?CombatEventKind.CardExhausted:CombatEventKind.CardDiscarded,CombatCause.Card,CombatTrace.PlayerId,CombatTrace.PlayerId,effectId:instance.InstanceId);
  trace.Complete(Enemies);
  LastCardResult=new CardResult(d,trace,instance.InstanceId,paid,d.RequiresEnemyTarget?target70.TargetId:null,conditional);
  if(Enemies.All(enemy=>!enemy.IsAlive)){pendingDraw=0;pendingItemDraw=0;Win();}
  return true;
 }
 void QueueDiscardDraw(int count)
 {
  if(Deck.Hand.Count==0){DrawCards(trace,count);return;}
  pendingDraw=count;
 }
 public bool ResolveDiscard(string instanceId)
 {
  if(!PendingDiscard||!Deck.Discard(instanceId))return false;
  trace.Add(CombatEventKind.CardDiscarded,CombatCause.Card,CombatTrace.PlayerId,CombatTrace.PlayerId,effectId:instanceId);
  int drawCount=pendingDraw;
  pendingDraw=0;
  var drawn=DrawCards(trace,drawCount);
  if(pendingItemDraw>0){ItemDraw(pendingItemDraw);pendingItemDraw=0;}
  if(drawn.Count>0&&IF("I27"))ItemDraw(1);
  trace.Complete(Enemies);
  return true;
 }
 void Res(int n)=>Resonance=Math.Min(12,Resonance+n);
 void SpendRes(int n){Resonance-=n;SpentResonance+=n;}
 void Mark(int n)=>SetBrand(Math.Min(8,Brand+n));
 void SetBrand(int value) {
  if(!target70.IsAlive||value==Brand)return;
  trace.Add(CombatEventKind.Status,CombatCause.Brand,CombatTrace.PlayerId,target70.TargetId,Brand,value,status:StatusEffectType.Brand);
  Brand=value;
 }
 void AP(int n)=>ChangeEnergy(trace,Math.Clamp(Energy+n,0,Data.Config.EnergyCap),CombatCause.Card);
 void PayHealth(int amount) {
  int before=Run.CurrentHP;
  Run.CurrentHP-=amount;
  trace.Add(CombatEventKind.Heal,CombatCause.CardCost,CombatTrace.PlayerId,CombatTrace.PlayerId,before,Run.CurrentHP);
 }
 void RecordBlock(int before,CombatCause? cause=null) {
  var entry=trace.Add(CombatEventKind.Block,cause??(Block>before?CombatCause.BlockGain:CombatCause.BlockSpent),CombatTrace.PlayerId,CombatTrace.PlayerId,before,Block);
  blockHistory.Add(entry);
 }
 void Refund(int n)=>AP(Math.Min(paid,n));
 void Heal70(int amount) {
  int before=Run.CurrentHP;Run.Heal(Math.Max(0,amount));if(Run.CurrentHP==before)return;
  trace.Add(CombatEventKind.Heal,CombatCause.Card,CombatTrace.PlayerId,CombatTrace.PlayerId,before,Run.CurrentHP);
  if(!healed&&Run.SelectedCharacter==CharacterId.Priest){Grace=Math.Min(10,Grace+1);}healed=true;
  foreach(var p in Trigger("P08")){Grace=Math.Min(10,Grace+1);DrawCards(trace,1);}
  Block+=Trigger("P14","heal").Length*3;
  ItemOnActualHeal();
 }
 void Hit70(int amount,int hits=1,bool pierce=false,bool strength=true) {
  int bonus=0;if(amplify>0&&paid>=2){bonus=amplify*paid;amplify=0;}
  bool twice=once.Remove("double-ready");conditional|=bonus>0||twice||amount>=12;
  for(int i=0;i<hits&&target70.IsAlive;i++) {
   int raw=Math.Max(0,amount+(strength?Strength+TemporaryStrength:0)+(i==0?bonus:0));
   if(twice)raw*=2;
   raw+=ItemAttackBonus(i);
   HitEnemy(trace,target70,raw,CombatCause.DirectAttack,ignoreBlock:pierce);
  }
 }
 void StatusHit(int amount)=>HitEnemy(trace,target70,amount,CombatCause.Card);
 static bool IsElemental(ElementType e)=>e==ElementType.Fire||e==ElementType.Ice||e==ElementType.Lightning||e==ElementType.Water||e==ElementType.Wind||e==ElementType.Poison;
 void End70(CombatTrace turnTrace){
  trace=turnTrace;
  int before=Block;
  EndItems();
  if(powers.TryGetValue("W17",out int count))Block+=count*Math.Max(0,3+Strength+TemporaryStrength);
  if(Block!=before)RecordBlock(before);
  TemporaryStrength=0;amplify=0;once.Remove("double-ready");
 }
 void Start70(){
  Turn++;once.Clear();BeginItemTurn();foreach(var p in activePowers)p.Used.Clear();
  ChangeEnergy(trace,Math.Clamp(MaxEnergy+scheduledEnergy,0,Data.Config.EnergyCap),CombatCause.TurnStart);
  scheduledEnergy=0;
  int before=Block;
  Block=Has("W13")?Block:Math.Min(Block,Math.Max(Math.Max(retainBlock,IceRetention),Math.Max(itemRetention,IH("I20")?6:0)));retainBlock=0;Block+=scheduledBlock;
  if(scheduledBlock>0)GuardTraining();scheduledBlock=0;
  SpentResonance=0;cardsUsed=0;healed=false;
  for(int i=0;i<regens.Count;i++){regens[i]--;Heal70(3);}regens.RemoveAll(turns=>turns==0);
  if(Block!=before)RecordBlock(before,CombatCause.TurnStart);
  DrawCards(trace,Data.Config.HandSize+(powers.TryGetValue("N13",out int draws)?draws:0));
 }
}
}

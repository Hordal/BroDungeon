using System;
using System.Collections.Generic;
using System.Linq;
namespace Brodun {
public sealed class CardPlayOptions {
 public string SelectedInstance; public string[] RecallInstances=Array.Empty<string>(); public bool ExtraEnergy;
}
public sealed partial class BattleRuntime {
 public int Resonance {get;internal set;} public int Grace {get;internal set;} public int Brand {get;internal set;}
 public int TemporaryStrength {get;internal set;}
 public int IceRetention {get;private set;}
 public int SpentResonance {get;private set;}
 public bool PendingDiscard {get;private set;}
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
 CardDefinition resolving; CardResult trace; int paid; CardPlayOptions selection;
 bool Has(string id)=>powers.ContainsKey(id);
 InstalledPower[] Trigger(string id,string channel="main")=>activePowers.Where(p=>p.Id==id&&p.Used.Add(channel)).ToArray();
 void Install(CardDefinition d) {
  activePowers.Add(new InstalledPower{Id=d.Id,Upgrade=d.UpgradeLevel});
  powers[d.Id]=powers.TryGetValue(d.Id,out int count)?count+1:1;
 }
 void GuardTraining() {foreach(var p in Trigger("N07")){Block+=(p.Upgrade>0?3:2)+p.GuardTraining;p.GuardTraining++;}}
 public CardDefinition Definition(CardInstance c)=>Data.Card(c.CardId,c.UpgradeLevel);
 public int EffectiveCost(CardInstance c) {var d=Definition(c);return d.XCost?Energy:d.Cost;}
 public bool CanPlayCard(string id) {
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
 bool Play70(CardInstance instance,CardDefinition d,CardPlayOptions options) {
  if(!CanPlayCard(instance.InstanceId))return false;
  options=options??new CardPlayOptions();
  if(d.Id=="N05"||d.Id=="N10") {
   if(!Deck.Hand.Any(c=>c.InstanceId==options.SelectedInstance&&c.InstanceId!=instance.InstanceId))return false;
  }
  if(d.Id=="N16" && (options.RecallInstances==null||options.RecallInstances.Length>2||options.RecallInstances.Distinct().Count()!=options.RecallInstances.Length||options.RecallInstances.Any(id=>!Deck.DiscardPile.Any(c=>c.InstanceId==id))))return false;
  if(options.ExtraEnergy&&(d.Id!="M11"||Energy<d.Cost+2))return false;
  resolving=d;selection=options;trace=new CardResult(d.Id);paid=d.XCost?Energy:d.Cost+(options.ExtraEnergy?2:0);Energy-=paid;
  int preBlock=Block;
  // Used card leaves the hand before draws but is not available for shuffle/recall until resolution ends.
  Deck.BeginUse(instance.InstanceId);
  // Element bonuses apply on every successful use, irrespective of order or card type.
  if(d.Element==ElementType.Fire)Strength++;
  if(d.Element==ElementType.Ice){Block+=3;IceRetention+=3;}
  if(Run.SelectedCharacter==CharacterId.Mage&&IsElemental(d.Element)) {
   int before=Resonance;Res(1);foreach(var p in Trigger("M08"))Res(p.Upgrade>0?2:1);
   if(Resonance>before)foreach(var p in Trigger("M15")){AP(1);Deck.Draw(1);}
  }
  if(d.Type==CardType.Attack)Strength+=Trigger("W14").Length;
  int brandBefore=Brand;bool allBrand=d.Id=="P15";int consumedBrand=0;
  bool up=d.UpgradeLevel>0;Func<int,int,int> v=(a,b)=>up?b:a;
  int n;
  switch(d.Id) {
   case "W01":case "M01":case "P01": Hit70(v(6,8));break;
   case "W02":case "M02":case "P02": Block+=v(5,8);break;
   case "W03":Hit70(v(5,7));Strength++;break;
   case "W04":Hit70(v(5,8)+Block/4);break;
   case "W05":Block+=v(8,11);retainBlock=Math.Max(retainBlock,v(4,6));break;
   case "W06":Deck.Draw(v(2,3));PendingDiscard=Deck.Hand.Count>0;if(Strength+TemporaryStrength>=3)Refund(1);break;
   case "W07":Block+=v(5,8);Strength++;break;
   case "W08":Run.CurrentHP-=3;Strength+=v(1,2);break;
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
   case "M16":SpendRes(v(2,1));Deck.Draw(1);break;
   case "M17":Hit70(v(9,11)*paid);break;
   case "M18":Block+=v(6,9)+SpentResonance;break;
   case "P03":Hit70(v(4,7));break;
   case "P04":Hit70(v(5,8)+Brand*2);break;
   case "P05":Heal70(v(4,6));Block+=4;break;
   case "P06":Block+=v(5,8)+Grace;break;
   case "P07":Run.CurrentHP-=v(4,3);Grace=Math.Min(10,Grace+3);Mark(1);break;
   case "P10":Mark(v(2,3));Enemy.Status.Weak++;break;
   case "P11":Run.CurrentHP-=v(5,3);AP(2);break;
   case "P12":Heal70(v(3,5)+Grace/2);break;
   case "P13":regens.Add(3);break;
   case "P15":n=Brand;Brand=0;consumedBrand=n;Hit70(8+n*6);break;
   case "P16":n=Grace;Grace=0;Hit70(10+n*4);Heal70(n);break;
   case "P17":Brand=Brand==0?2:Math.Min(8,Brand*2);break;
   case "P18":Deck.Draw(v(2,3));if(healed)Refund(1);break;
   case "N01":Deck.Draw(1);if(up)Block+=3;break;
   case "N02":Block+=v(3,6);retainBlock=Math.Max(retainBlock,v(6,9));break;
   case "N03":Hit70(v(5,8)+cardsUsed*2);break;
   case "N04":Heal70(v(3,5)+Deck.ExhaustPile.Count);break;
   case "N05":scheduledEnergy++;if(up)scheduledBlock+=4;Deck.Retain(selection.SelectedInstance);break;
   case "N06":if(up)Strength++;break;
   case "N08":Hit70(4);Enemy.Status.AddPoison(v(0,1)+powers.Count);break;
   case "N09":Block+=v(2,5)+powers.Count*3;break;
   case "N10":Deck.Exhaust(selection.SelectedInstance);Deck.Draw(2);if(up)Block+=3;break;
   case "N11":Deck.Draw(2);break;
   case "N12":AP(2);if(!up)scheduledEnergy--;break;
   case "N15":once.Add("double-ready");break;
   case "N16":foreach(string id in selection.RecallInstances)Deck.Recall(id);break;
   default:if(d.Type!=CardType.Power)throw new InvalidOperationException("Unimplemented card: "+d.Id);break;
  }
  if(d.Element==ElementType.Lightning&&Enemy.CurrentHP>0)StatusHit(3);
  if(d.Element==ElementType.Water)scheduledEnergy++;
  if(d.Element==ElementType.Wind)Deck.Draw(1);
  if(d.Element==ElementType.Poison)Enemy.Status.AddPoison(2);
  if(d.Id=="N01"||d.Id=="W06")PendingDiscard=Deck.Hand.Count>0;
  if(d.Type==CardType.Attack&&Run.SelectedCharacter==CharacterId.Priest&&!allBrand&&brandBefore>0){Brand--;consumedBrand=1;StatusHit(4);}
  if(d.Id=="P03")Mark(1);
  if(consumedBrand>0) {
   foreach(var p in Trigger("P09")){Mark(1);Block+=p.Upgrade>0?5:3;}
   foreach(var p in Trigger("P14","brand"))Heal70(2);
  }
  bool cardBlock=d.Element==ElementType.Ice||d.Type==CardType.Guard||d.Id=="W10"||d.Id=="M04"||(up&&(d.Id=="N01"||d.Id=="N10"));
  if(cardBlock&&Block>preBlock)GuardTraining();
  if(d.Type==CardType.Attack) {
   foreach(var p in activePowers.Where(p=>p.Id=="N06"))if(++p.Training>=3){p.Training-=3;Strength++;}
   if(paid>=2)foreach(var p in Trigger("M14"))Res(p.Upgrade>0?3:2);
  }
  if(paid>=2)foreach(var p in Trigger("N14"))AP(1);
  // Existing powers may react to this card, but a new installation cannot trigger itself.
  if(d.Type==CardType.Power)Install(d);
  cardsUsed++;Deck.FinishUse(instance,d.Exhausts);LastCardResult=trace;
  if(Enemy.CurrentHP<=0){PendingDiscard=false;Win();}
  return true;
 }
 public bool ResolveDiscard(string instanceId) {if(!PendingDiscard||!Deck.Discard(instanceId))return false;PendingDiscard=false;return true;}
 void Res(int n)=>Resonance=Math.Min(12,Resonance+n);
 void SpendRes(int n){Resonance-=n;SpentResonance+=n;}
 void Mark(int n)=>Brand=Math.Min(8,Brand+n);
 void AP(int n)=>Energy=Math.Max(0,Math.Min(6,Energy+n));
 void Refund(int n)=>AP(Math.Min(paid,n));
 void Heal70(int amount) {
  int before=Run.CurrentHP;Run.Heal(Math.Max(0,amount));if(Run.CurrentHP==before)return;
  if(!healed&&Run.SelectedCharacter==CharacterId.Priest){Grace=Math.Min(10,Grace+1);}healed=true;
  foreach(var p in Trigger("P08")){Grace=Math.Min(10,Grace+1);Deck.Draw(1);}
  Block+=Trigger("P14","heal").Length*3;
 }
 void Hit70(int amount,int hits=1,bool pierce=false,bool strength=true) {
  int bonus=0;if(amplify>0&&paid>=2){bonus=amplify*paid;amplify=0;}
  bool twice=once.Remove("double-ready");trace.ConditionalTriggered|=bonus>0||twice||amount>=12;
  for(int i=0;i<hits&&Enemy.CurrentHP>0;i++) {
   int raw=Math.Max(0,amount+(strength?Strength+TemporaryStrength:0)+(i==0?bonus:0));if(twice)raw*=2;
   int hp=Enemy.CurrentHP,b=Enemy.Block,original=b;
   if(pierce)b=0;var hit=DamageCalculator.Hit(raw,ref hp,ref b);Enemy.CurrentHP=hp;if(!pierce)Enemy.Block=b;else Enemy.Block=original;
   trace.Damage+=hit.HPDamage;trace.Blocked+=hit.Blocked;trace.RecordHit(hit);
  }
  Enemy.Element=resolving.Element;
 }
 void StatusHit(int amount){int hp=Enemy.CurrentHP,b=Enemy.Block;var hit=DamageCalculator.Hit(amount,ref hp,ref b);Enemy.CurrentHP=hp;Enemy.Block=b;trace.Damage+=hit.HPDamage;trace.Blocked+=hit.Blocked;trace.RecordHit(hit);}
 static bool IsElemental(ElementType e)=>e==ElementType.Fire||e==ElementType.Ice||e==ElementType.Lightning||e==ElementType.Water||e==ElementType.Wind||e==ElementType.Poison;
 void End70(){if(powers.TryGetValue("W17",out int count))Block+=count*Math.Max(0,3+Strength+TemporaryStrength);TemporaryStrength=0;amplify=0;once.Remove("double-ready");}
 void Start70(){
  Turn++;once.Clear();foreach(var p in activePowers)p.Used.Clear();Energy=Math.Max(0,Math.Min(6,MaxEnergy+scheduledEnergy));scheduledEnergy=0;
  Block=Has("W13")?Block:Math.Min(Block,Math.Max(retainBlock,IceRetention));retainBlock=0;Block+=scheduledBlock;
  if(scheduledBlock>0)GuardTraining();scheduledBlock=0;
  SpentResonance=0;cardsUsed=0;healed=false;
  for(int i=0;i<regens.Count;i++){regens[i]--;Heal70(3);}regens.RemoveAll(turns=>turns==0);
  Deck.Draw(Data.Config.HandSize+(powers.TryGetValue("N13",out int draws)?draws:0));
 }
}
}

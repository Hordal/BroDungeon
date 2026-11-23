using System;
using System.Collections.Generic;
using System.Linq;
namespace Brodun {
public static class Card70Rewards {
 public static bool CanAcquire(GameDefinitions data,RunState run,string id) {
  var c=data.Card(id);return !c.IsReworked || c.Rarity!=CardRarity.Basic&&(!c.Owner.HasValue||c.Owner==run.SelectedCharacter)&&run.Deck.Count(x=>x.CardId==id)<c.CopyLimit;
 }
 public static IEnumerable<CardDefinition> Eligible(GameDefinitions data,RunState run)=>data.Cards.Where(c=>CanAcquire(data,run,c.Id));
 public static PendingReward Create(GameDefinitions data,RunState run) {
  var node=DungeonRuntime.Node(run,run.ActiveRoom);var rng=new SeededRandom(EncounterPool.NodeSeed(run.Seed,node.Id,0x71CAu));
  var available=Eligible(data,run).ToList();var chosen=new List<string>();
  bool boss=node.Type==RoomType.Boss,elite=node.Encounter==EnemyType.Elite;
  for(int slot=0;slot<3;slot++) {
   var pool=available.Where(c=>slot==0?c.Owner.HasValue:slot==1?!c.Owner.HasValue:true).ToList();
   if(pool.Count==0)pool=available.ToList();if(pool.Count==0)break;
   double roll=rng.Next();CardRarity rank=boss?CardRarity.High:elite?(roll<.6?CardRarity.Mid:CardRarity.High):(roll<.45?CardRarity.Low:roll<.85?CardRarity.Mid:CardRarity.High);
   bool early=slot==0&&!boss&&!elite&&run.CompletedNodes.Count(id=>DungeonRuntime.Node(run,id).Type==RoomType.Battle)<3;
   if(early&&rank==CardRarity.High)rank=CardRarity.Mid;
   var ranked=pool.Where(c=>c.Rarity==rank).ToList();
   if(ranked.Count==0)ranked=pool.Where(c=>!boss&&(!early||c.Rarity!=CardRarity.High)).ToList();
   if(ranked.Count==0)continue;
   var pick=ranked[(int)(rng.Next()*ranked.Count)];chosen.Add(pick.Id);available.Remove(pick);
  }
  return new PendingReward(run.RunId+":"+run.ActiveRoom,run.ActiveRoom,data.Reward.Gold,chosen);
 }
}}

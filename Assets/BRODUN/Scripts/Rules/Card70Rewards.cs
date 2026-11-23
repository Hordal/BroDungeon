using System;
using System.Collections.Generic;
using System.Linq;
namespace Brodun {
public static class Card70Rewards {
 public static bool CanAcquire(GameDefinitions data,RunState run,string id) {
  // Ownership does not remove a card from future rewards or shop stock.
  var c=data.Card(id);return !c.IsReworked || c.Rarity!=CardRarity.Starter&&(!c.Owner.HasValue||c.Owner==run.SelectedCharacter);
 }
 public static IEnumerable<CardDefinition> Eligible(GameDefinitions data,RunState run)=>data.Cards.Where(c=>CanAcquire(data,run,c.Id));
 public static IReadOnlyList<string> Candidates(GameDefinitions data,RunState run) {
  var node=DungeonRuntime.Node(run,run.ActiveRoom);var rng=new SeededRandom(EncounterPool.NodeSeed(run.AreaSeed,node.Id,0x71CAu));
  var available=Eligible(data,run).ToList();var chosen=new List<string>();
  bool boss=node.Type==RoomType.Boss,elite=node.Encounter==EnemyType.Elite;
  for(int slot=0;slot<3;slot++) {
   var pool=available.Where(c=>slot==0?c.Owner.HasValue:slot==1?!c.Owner.HasValue:true).ToList();
   if(pool.Count==0)pool=available.ToList();if(pool.Count==0)break;
   double roll=rng.Next();CardRarity rank=boss?CardRarity.High:elite?(roll<.6?CardRarity.Middle:CardRarity.High):(roll<.45?CardRarity.Low:roll<.85?CardRarity.Middle:CardRarity.High);
   bool early=slot==0&&!boss&&!elite&&run.CompletedNodes.Count(id=>DungeonRuntime.Node(run,id).Type==RoomType.Battle)<3;
   if(early&&rank==CardRarity.High)rank=CardRarity.Middle;
   var ranked=pool.Where(c=>c.Rarity==rank).ToList();
   if(ranked.Count==0)ranked=pool.Where(c=>!boss&&(!early||c.Rarity!=CardRarity.High)).ToList();
   if(ranked.Count==0)continue;
   var pick=ranked[(int)(rng.Next()*ranked.Count)];chosen.Add(pick.Id);available.Remove(pick);
  }
  return chosen;
 }
}}

using System;
using System.Linq;
using NUnit.Framework;
namespace Brodun.Tests {
public sealed class Item50Tests:RulesTest {
 GameDefinitions Modern()=>Card70Catalog.Apply(DungeonThemes.WithThemeEnemies(Data));
 RunState NewRun(CharacterId hero=CharacterId.Warrior,string theme="ruins",params string[] cards){var r=new RunState(Modern(),hero,17,firstTheme:theme);if(cards.Length>0){r.deck.Clear();foreach(string id in cards)r.AddCard(id);}return r;}
 void Give(RunState r,params string[] ids){foreach(string id in ids)Assert.IsTrue(r.AddItem(id),id);}
 BattleRuntime Start(RunState r,string node="r1",bool fullEnergy=true){r.ActiveRoom=node;var b=new BattleRuntime(Modern(),r,DungeonRuntime.Node(r,node));b.Deck.Draw(10);if(fullEnergy)b.Energy=6;b.Enemy.CurrentHP=10000;b.Enemy.Block=0;b.Enemy.CurrentIntent=new IntentData(IntentType.Attack,0);return b;}
 void Play(BattleRuntime b,string id){var card=b.Deck.Hand.First(c=>c.CardId==id);Assert.IsTrue(b.PlayCard(card.InstanceId),id);}
 RoomRuntime Store(RunState r,string id="store",EventKind kind=EventKind.Altar){r.ActiveRoom=id;return new RoomRuntime(Modern(),r,new RoomNode(id,RoomType.Event,2,50,kind==EventKind.Shop?new[]{"boss"}:Array.Empty<string>(),kind));}
 [Test] public void CampaignRoutesReserveTheMerchantForTheBossAndKeepElitesAndAltars()
 {
  for(uint seed=0;seed<32;seed++)
  {
   var run=new RunState(Modern(),CharacterId.Warrior,seed,dungeon:LayeredMapGenerator.Generate(seed,MapGenerationConfig.Campaign),firstTheme:"ruins");
   var dungeon=run.Map;
   Assert.AreEqual(1,dungeon.Count(node=>node.IsBossSanctuary));
   Assert.IsFalse(dungeon.Any(node=>!node.IsBossSanctuary&&(node.Event==EventKind.Shop||node.Event==EventKind.RestSite)));
   Assert.IsTrue(dungeon.Any(node=>node.Event==EventKind.Altar));
   Assert.IsTrue(dungeon.Any(node=>node.Encounter==EnemyType.Elite));
   Assert.IsFalse(dungeon.Any(node=>node.Type==RoomType.Puzzle));
  }
 }
 [Test] public void CampaignHealthGoldAndEnemyScalingChangeIndependently()
 {
  var data=Modern();
  Assert.AreEqual(80,data.Character(CharacterId.Warrior).MaxHP);
  Assert.AreEqual(60,data.Character(CharacterId.Mage).MaxHP);
  Assert.AreEqual(70,data.Character(CharacterId.Priest).MaxHP);
  Assert.AreEqual(50,data.Reward.Policy.Normal.BaseGold);
  Assert.AreEqual(80,data.Reward.Policy.Elite.BaseGold);
  Assert.AreEqual(120,data.Reward.Policy.Boss.BaseGold);
  var room=new RoomNode("test",RoomType.Battle,1,50,Array.Empty<string>(),EventKind.None);
  var definition=data.Enemies.First(enemy=>enemy.Pattern[0].Type==IntentType.Attack);
  var first=new EnemyInstance(definition,room,data.Config);
  var second=new EnemyInstance(definition,room,data.Config,runArea:2);
  var third=new EnemyInstance(definition,room,data.Config,runArea:3);
  Assert.AreEqual((first.MaxHP*130+99)/100,second.MaxHP);
  Assert.AreEqual((first.MaxHP*165+99)/100,third.MaxHP);
  var baseIntent=IntentResolver.Decide(first);
  Assert.AreEqual((baseIntent.Amount*120+99)/100,IntentResolver.Decide(second).Amount);
  Assert.AreEqual((baseIntent.Amount*145+99)/100,IntentResolver.Decide(third).Amount);
 }
 [Test] public void FiftyItemsAreAllEquippableByEveryClass(){Assert.AreEqual(50,Item50Catalog.All.Count);Assert.AreEqual(42,Item50Catalog.All.Count(i=>i.ThemeId==null));foreach(CharacterId hero in Enum.GetValues(typeof(CharacterId))){var r=NewRun(hero);foreach(var i in Item50Catalog.All){Assert.Greater(i.BuyPrice,i.SellPrice);Assert.AreEqual(i.BuyPrice/2,i.SellPrice);Assert.IsTrue(r.AddItem(i.Id));Assert.IsFalse(r.AddItem(i.Id));}Assert.AreEqual(50,r.Items.Count);}}
 [Test] public void EachThemeHasExactlyTwoItemsAndNoOtherThemesLeakIntoStock(){
  foreach(var theme in DungeonThemes.All){
   var themedIds=Item50Catalog.All.Where(item=>item.ThemeId==theme.Id).Select(item=>item.Id).ToArray();
   Assert.AreEqual(2,themedIds.Length);
   Assert.AreEqual(44,Item50Catalog.Available(theme.Id).Count());
   var observed=new System.Collections.Generic.HashSet<string>();
   for(uint seed=1;seed<=32;seed++){
    var run=new RunState(Modern(),CharacterId.Warrior,seed,firstTheme:theme.Id);
    var offers=Store(run).Visit.ItemOffers.Select(offer=>Item50Catalog.Find(offer.ItemId)).ToArray();
    Assert.AreEqual(4,offers.Length);
    Assert.AreEqual(3,offers.Count(item=>item.ThemeId==null));
    observed.Add(offers.Single(item=>item.ThemeId==theme.Id).Id);
   }
   CollectionAssert.AreEquivalent(themedIds,observed);
   var withOneOwned=NewRun(CharacterId.Warrior,theme.Id);
   Give(withOneOwned,themedIds[0]);
   var remaining=Store(withOneOwned).Visit.ItemOffers.Select(offer=>Item50Catalog.Find(offer.ItemId))
    .Single(item=>item.ThemeId!=null);
   Assert.AreEqual(themedIds[1],remaining.Id);
  }
 }
 [Test] public void StockFallsBackToFourGeneralItemsAfterBothThemeItemsAreOwned(){var r=NewRun();foreach(var i in Item50Catalog.All.Where(i=>i.ThemeId=="ruins"))Give(r,i.Id);var s=Store(r);Assert.AreEqual(4,s.Visit.ItemOffers.Count);Assert.IsTrue(s.Visit.ItemOffers.All(o=>Item50Catalog.Find(o.ItemId).ThemeId==null));}
 [Test] public void PurchasesAndSalesAreAtomicAndPersistAcrossReentry(){var r=NewRun();var s=Store(r);int price=s.Visit.ItemOffers[0].Price;string id=s.Visit.ItemOffers[0].ItemId;r.AddGold(price-1);Assert.IsFalse(s.BuyItem(0));Assert.AreEqual(0,r.Items.Count);r.AddGold(1);Assert.IsTrue(s.BuyItem(0));Assert.AreEqual(0,r.Gold);Assert.IsFalse(s.BuyItem(0));Assert.IsTrue(s.SellItem(id));Assert.AreEqual(Item50Catalog.Find(id).SellPrice,r.Gold);var again=Store(r);Assert.AreSame(s.Visit,again.Visit);Assert.IsFalse(again.BuyItem(0));Assert.IsFalse(again.SellItem(id));}
 [Test] public void CardSalesKeepEightCardsAndTwoSalesPerVisit(){var r=NewRun();var s=Store(r);string id=r.Deck[0].InstanceId;Assert.IsTrue(s.SellCard(id));Assert.IsFalse(s.SellCard(id));Assert.IsTrue(s.SellCard(r.Deck[0].InstanceId));Assert.AreEqual(8,r.Deck.Count);Assert.IsFalse(s.SellCard(r.Deck[0].InstanceId));Assert.AreEqual(2,Store(r).Visit.CardsSold);}
 [Test] public void ServicesAndLegacyAltarChoiceRemainAvailableAlongsideShopping(){var r=NewRun();r.AddGold(500);var s=Store(r,kind:EventKind.Shop);r.CurrentHP-=10;Assert.IsTrue(s.BuyItem(0));Assert.IsTrue(s.Buy(0));Assert.IsTrue(s.Upgrade(r.Deck[0].InstanceId));Assert.IsTrue(s.Remove(r.Deck[1].InstanceId));Assert.IsTrue(s.RecoverAtSanctuary());Assert.IsFalse(s.SacrificeAtSanctuary());Assert.IsTrue(s.CanBuyItem(1));}
 [Test] public void StaleRoomsAndGoldOverflowCannotSellOrSpend(){var r=NewRun();Give(r,"I01");r.AddGold(int.MaxValue);var s=Store(r);Assert.IsFalse(s.SellItem("I01"));Assert.IsFalse(s.SellCard(r.Deck[0].InstanceId));Assert.IsTrue(r.HasItem("I01"));r.ActiveRoom="other";Assert.IsFalse(s.Buy(0));Assert.IsFalse(s.BuyItem(0));}
 [Test] public void OldWarriorRelicsWorkOnMageAndPriestAttacks(){foreach(var hero in new[]{CharacterId.Mage,CharacterId.Priest}){string prefix=hero==CharacterId.Mage?"M":"P";var r=NewRun(hero,"ruins",prefix+"01",prefix+"01",prefix+"01");Give(r,"I17","I18","I19","I20","I21","I22");r.GrowItem("I17",2);r.GrowItem("I22",4);var b=Start(r);Assert.AreEqual(2,b.Strength);Assert.AreEqual(4,b.Block);b.Block=20;Play(b,prefix+"01");Assert.AreEqual(13,b.LastCardResult.Damage);b.EndTurn();Assert.AreEqual(6,b.Block);}}
 [Test] public void RevisedStrengthAndBlockRelicsTriggerWithoutMageElements(){var r=NewRun(CharacterId.Warrior,"ruins","W03","W02");Give(r,"I23","I24");var b=Start(r);Play(b,"W03");Assert.AreEqual(2,b.Strength);Play(b,"W02");Assert.AreEqual(1,b.Enemy.Status.Weak);}
 [Test] public void HitAccumulatorWorksOnSingleHitCardsAndDoesNotNeedLightning(){var r=NewRun(CharacterId.Priest,"ruins","P01","P01","P01");Give(r,"I25");var b=Start(r);Play(b,"P01");Assert.AreEqual(6,b.LastCardResult.Damage);Play(b,"P01");Play(b,"P01");Assert.AreEqual(10,b.LastCardResult.Damage);}
 [Test] public void ExpensiveCardRelicsWorkOnWarriorCards(){var r=NewRun(CharacterId.Warrior,"ruins","W09");Give(r,"I26","I28");var b=Start(r);Play(b,"W09");Assert.AreEqual(2,b.Block);b.EndTurn();Assert.AreEqual(4,b.Energy);}
 [Test] public void DrawRelicTriggersOnGeneralCardDrawAndNeverRecurses(){var r=NewRun(CharacterId.Warrior,"ruins","W01","W01","N11");Give(r,"I27");var b=Start(r);Play(b,"W01");Play(b,"W01");int before=b.Deck.TotalDrawn;Play(b,"N11");Assert.AreEqual(before+2,b.Deck.TotalDrawn);Assert.AreEqual(2,b.Deck.Hand.Count);}
 [Test] public void DebuffRelicsWorkWithSharedPoisonOnAnyClass(){foreach(CharacterId hero in Enum.GetValues(typeof(CharacterId))){var r=NewRun(hero,"ruins","N08","N08");Give(r,"I29","I30");var b=Start(r);r.CurrentHP-=10;Play(b,"N08");int hp=r.CurrentHP;b.Enemy.Block=10;Play(b,"N08");Assert.AreEqual(hp+2,r.CurrentHP);Assert.AreEqual(3,b.Enemy.Block);}}
 [Test] public void DefenseReductionRelicFollowsSelectedEnemyInMultiEnemyBattle()
 {
  var run=NewRun(CharacterId.Warrior,"ruins","W01");
  Give(run,"I30");
  run.ActiveRoom="r1";
  var data=Modern();
  var battle=new BattleRuntime(data,run,DungeonRuntime.Node(run,"r1"),additionalEnemies:new[]{data.Enemy("moss_slime")});
  var main=battle.Enemies[0];
  var target=battle.Enemies[1];
  main.Block=10;target.Block=10;target.Status.Weak=1;
  var card=battle.Deck.Hand.Single(c=>c.CardId=="W01");
  Assert.IsTrue(battle.PlayCard(card.InstanceId,target.TargetId));
  Assert.AreEqual(10,main.Block);
  Assert.AreEqual(1,target.Block);
  Assert.AreEqual(target.TargetId,battle.LastCardResult.Events.Single(e=>e.EffectId=="I30").TargetId);
 }
 [Test] public void HealingAndOpeningRelicsHaveGeneralEffectsAndPriestExtras(){foreach(CharacterId hero in Enum.GetValues(typeof(CharacterId))){var r=NewRun(hero,"ruins","N04");Give(r,"I31","I32","I33","I34");r.CurrentHP-=10;var b=Start(r);Assert.AreEqual(3,b.Block);Assert.AreEqual(1,b.Enemy.Status.Weak);Assert.AreEqual(hero==CharacterId.Priest?1:0,b.Brand);Play(b,"N04");Assert.AreEqual(9,b.Block);Assert.AreEqual(hero==CharacterId.Priest?4:0,b.Grace);}}
 [Test] public void RuinsItemsGiveOpeningBlockGuardDrawAndThirdCardDamage(){var r=NewRun(CharacterId.Warrior,"ruins","W01","W02","W01");Give(r,"I41","I42");var b=Start(r);Assert.AreEqual(4,b.Block);Play(b,"W01");int drawn=b.Deck.TotalDrawn;Play(b,"W02");Assert.AreEqual(drawn+1,b.Deck.TotalDrawn);Play(b,"W01");Assert.AreEqual(9,b.LastCardResult.Damage);}
 [Test] public void FoundryItemsUseTemporaryStrengthAndExhaustOnEveryClass(){var r=NewRun(CharacterId.Priest,"fire","P01","N12");Give(r,"I43","I44");var b=Start(r);Play(b,"P01");Assert.AreEqual(8,b.LastCardResult.Damage);Assert.AreEqual(2,b.TemporaryStrength);Play(b,"N12");Assert.AreEqual(4,b.LastCardResult.Damage);Assert.AreEqual(2,b.Block);b.EndTurn();Assert.AreEqual(0,b.TemporaryStrength);}
 [Test] public void FrostItemRetentionAccumulatesAndNeverRestoresSpentBlock(){var r=NewRun(CharacterId.Warrior,"frost","W02");Give(r,"I45");var b=Start(r);Play(b,"W02");b.EndTurn();Assert.AreEqual(2,b.Block);Play(b,"W02");b.Enemy.CurrentIntent=new IntentData(IntentType.Attack,0);b.EndTurn();Assert.AreEqual(4,b.Block);b.Block=0;b.Enemy.CurrentIntent=new IntentData(IntentType.Attack,0);b.EndTurn();Assert.AreEqual(0,b.Block);}
 [Test] public void FrostChaliceRequiresActualHealingForItsHealingBranch(){var r=NewRun(CharacterId.Mage,"frost","N04");Give(r,"I46");var b=Start(r);Play(b,"N04");Assert.AreEqual(0,b.Block);b.Enemy.CurrentIntent=new IntentData(IntentType.Attack,3);b.EndTurn();Assert.AreEqual(r.MaxHP-1,r.CurrentHP);}
 [Test] public void VoidItemsRewardExpensiveCardsAndDebuffedTargets(){var r=NewRun(CharacterId.Warrior,"void","W09","W09","W09");Give(r,"I47","I48");var b=Start(r);b.Enemy.Status.Weak=1;Play(b,"W09");Assert.AreEqual(12,b.LastCardResult.Damage);Play(b,"W09");Play(b,"W09");Assert.AreEqual(1,b.Deck.Hand.Count);b.EndTurn();Assert.AreEqual(4,b.Energy);}
 [Test] public void NewGeneralItemsRewardSharedDecksAndMixedSequences(){var r=NewRun(CharacterId.Warrior,"ruins","W02","N03","N12","N04","N08");Give(r,"I49","I50");var b=Start(r,fullEnergy:false);Assert.AreEqual(4,b.Energy);Assert.AreEqual(3,b.Block);Play(b,"W02");int before=b.Deck.TotalDrawn;Play(b,"N03");Assert.AreEqual(10,b.Block);Assert.AreEqual(before+1,b.Deck.TotalDrawn);Play(b,"N12");Assert.AreEqual(10,b.Block);}
 [Test] public void ThemeEffectsRemainActiveAfterAcquisitionRegardlessOfCurrentTheme(){var r=NewRun(CharacterId.Warrior,"ruins","W01");Give(r,"I43");var b=Start(r);Play(b,"W01");Assert.AreEqual(8,b.LastCardResult.Damage);}
 [Test] public void GrowthIsUniversalCappedAndLostOnSale(){var r=NewRun(CharacterId.Mage);Give(r,"I17","I22","I35");foreach(var item in r.Items){r.GrowItem(item.ItemId,100);Assert.AreEqual(Item50Catalog.Find(item.ItemId).GrowthCap,item.Progress);}var s=Store(r);Assert.IsTrue(s.SellItem("I17"));Give(r,"I17");Assert.AreEqual(0,r.ItemProgress("I17"));Assert.AreEqual(0,NewRun().Items.Count);}
 [TestCase(0)]
 [TestCase(1)]
 [TestCase(2)]
 public void BossVictoryDoesNotGrantRelics(int ownedCount){
  foreach(var theme in DungeonThemes.All){
   var run=NewRun(CharacterId.Warrior,theme.Id,"W01");
   var owned=Item50Catalog.All.Where(item=>item.ThemeId==theme.Id).Take(ownedCount).Select(item=>item.Id).ToArray();
   Give(run,owned);
   var battle=Start(run,"boss");battle.Enemy.CurrentHP=1;Play(battle,"W01");
   Assert.AreEqual(BattleResult.Won,battle.Result);
   Assert.IsNull(run.LastItemReward);
   CollectionAssert.AreEqual(owned,run.Items.Select(item=>item.ItemId));
  }
 }
 [Test] public void EveryItemWorksOnEveryHeroWithoutAClassRestriction(){foreach(var item in Item50Catalog.All)foreach(CharacterId hero in Enum.GetValues(typeof(CharacterId))){string prefix=hero==CharacterId.Warrior?"W":hero==CharacterId.Mage?"M":"P";var r=NewRun(hero,"ruins",prefix+"01",prefix+"02","N08","N12","N06");Give(r,item.Id);var b=Start(r);r.CurrentHP=r.MaxHP/2;foreach(string id in new[]{prefix+"01",prefix+"02","N08","N12","N06"}){b.Energy=6;Play(b,id);Assert.That(b.Energy,Is.InRange(0,6),item.Id);Assert.GreaterOrEqual(b.Block,0,item.Id);}Assert.IsTrue(b.EndTurn());}}
 [Test] public void AllCardUpgradesResolveWithAllFiftyRelics(){var data=Modern();foreach(var d in data.GameplayCards)foreach(int up in new[]{0,1}){var hero=d.Owner??CharacterId.Warrior;string prefix=hero==CharacterId.Warrior?"W":hero==CharacterId.Mage?"M":"P";string other=d.Id==prefix+"01"?prefix+"02":prefix+"01";var r=NewRun(hero,"ruins",d.Id,other);r.Deck.First(c=>c.CardId==d.Id).UpgradeLevel=up;foreach(var item in Item50Catalog.All)Give(r,item.Id);var b=Start(r);r.CurrentHP=r.MaxHP/2;b.Resonance=12;b.Grace=10;b.Brand=8;b.Strength=3;b.Block=20;var c=b.Deck.Hand.First(x=>x.CardId==d.Id);Assert.IsTrue(b.PlayCard(c.InstanceId,options:new CardPlayOptions{SelectedInstance=b.Deck.Hand.First(x=>x.CardId==other).InstanceId}),d.Id);if(b.PendingDiscard)Assert.IsTrue(b.ResolveDiscard(b.Deck.Hand[0].InstanceId));Assert.That(b.Energy,Is.InRange(0,6));Assert.GreaterOrEqual(b.Block,0);}}
}
}

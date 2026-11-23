using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Brodun.Data;
using Brodun.Editor;
using NUnit.Framework;
using UnityEditor;
namespace Brodun.Tests
{
    public sealed class MageCardTests:RulesTest
    {
        private BattleRuntime Setup(int count=1,string extra=null)
        {
            var run=Run(CharacterId.Mage);if(extra!=null)run.AddCard(extra);
            var odd=new EnemyDefinition("odd-mage-probe","odd",EnemyType.Normal,1,101,0,"moss_slime",new[]{new IntentData(IntentType.Attack,3)});
            var b=new BattleRuntime(Data,run,DungeonRuntime.Enter(run,"r1"),additionalEnemies:Enumerable.Repeat(odd,count-1));
            foreach(var enemy in b.Enemies){enemy.CurrentHP=1000;enemy.CurrentIntent=new IntentData(IntentType.Attack,3);}
            return b;
        }
        private CardResult Use(BattleRuntime b,string id,EnemyInstance target=null)=>
            CardEffectResolver.Resolve(b,Data.Card(id),Data.Card(id).RequiresTargetSelection?target??b.Enemy:null);
        private static IEnumerable Specs()
        {
            foreach(var line in File.ReadLines("BRODUN_CARD_SPEC_V1_1.md"))
            {
                if(!Regex.IsMatch(line,@"^\| `M\d\d` \|"))continue;
                var row=line.Trim('|').Split('|').Select(c=>c.Trim()).ToArray();var id=row[0].Trim((char)96);
                if(!File.Exists("Assets/BRODUN/Data/Generated/Cards/"+id+".asset"))continue;
                var grade=row[2].Split('/').Select(c=>c.Trim()).ToArray();
                yield return new TestCaseData(id,row[1],int.Parse(grade[1]),grade[0],row[4]);
            }
        }
        [TestCaseSource(nameof(Specs))]
        public void MageCatalogMatchesThePublishedSpecification(string id,string name,int ap,string grade,string text)
        {
            var c=Data.Card(id);Assert.AreEqual(name,c.Name);Assert.AreEqual(ap,c.Cost);Assert.AreEqual(text,c.Description);
            Assert.AreEqual(grade=="하급"?CardRarity.Low:grade=="중급"?CardRarity.Middle:CardRarity.High,c.Rarity);
            Assert.AreEqual(CharacterId.Mage,c.Owner);Assert.IsTrue(c.CatalogOnly&&c.RewardEligible);Assert.IsFalse(c.StarterOnly);
            Assert.AreEqual(ElementType.None,c.Element);Assert.AreEqual(ElementApplication.None,c.ElementApplication);
            Assert.AreEqual(id=="M07",c.Exhausts);Assert.IsFalse(Data.Cards.Contains(c));
            Assert.Throws<InvalidOperationException>(()=>Run(CharacterId.Warrior).AddCard(id));
            Assert.Throws<InvalidOperationException>(()=>Run(CharacterId.Priest).AddCard(id));
        }
        [TestCase("M01",9)][TestCase("M04",27)][TestCase("M07",39)]
        public void SniperDirectDamageAndRealCostsUseTheExistingCombatPath(string id,int damage)
        {
            var b=Setup(extra:id);var card=b.Run.Deck.Last();b.Deck.Draw(30);b.Strength=2;
            Assert.IsTrue(b.PlayCard(card.InstanceId));Assert.AreEqual(damage+2,b.LastCardResult.Damage);
            Assert.AreEqual(5-Data.Card(id).Cost,b.Energy);Assert.AreEqual(id=="M07",b.Deck.ExhaustPile.Contains(card));
            Assert.IsFalse(b.LastCardResult.Events.Any(e=>e.Kind==CombatEventKind.ElementMark||e.Reaction.HasValue));
        }
        [TestCase(0,0,8,0)][TestCase(1,0,7,0)][TestCase(5,2,5,2)][TestCase(6,3,5,3)][TestCase(25,12,0,17)]
        public void SniperPartialPenetrationIgnoresFloorHalfWithoutRemovingTheIgnoredArmor(int block,int ignored,int hpDamage,int left)
        {
            var b=Setup();b.Enemy.Block=block;var r=Use(b,"M02");Assert.AreEqual(hpDamage,r.Damage);Assert.AreEqual(left,b.Enemy.Block);
            Assert.AreEqual(ignored,r.HitEvents.Single().Hit.IgnoredBlock);Assert.AreEqual(block-left,r.Blocked);
        }
        [TestCase(0)][TestCase(1)][TestCase(50)]
        public void SniperFullPenetrationPreservesAllEnemyArmor(int block)
        {var b=Setup();b.Enemy.Block=block;var r=Use(b,"M05");Assert.AreEqual(13,r.Damage);Assert.AreEqual(block,b.Enemy.Block);Assert.AreEqual(0,r.Blocked);}
        [TestCase(49,true)][TestCase(50,true)][TestCase(51,false)]
        public void SniperOddMaximumHPUsesPreCardHalfHealthForDrawAndBonus(int hp,bool qualifies)
        {
            var b=Setup(2);var target=b.Enemies[1];Assert.AreEqual(101,target.MaxHP);target.CurrentHP=hp;
            var r=Use(b,"M03",target);Assert.AreEqual(qualifies?1:0,r.Events.Count(e=>e.Kind==CombatEventKind.Draw));
            target.CurrentHP=hp;r=Use(b,"M08",target);Assert.AreEqual(qualifies?32:20,r.Damage);
        }
        [TestCase(-1,true)][TestCase(0,true)][TestCase(1,false)]
        public void SniperEvenMaximumHalfBoundaryIsInclusive(int delta,bool qualifies)
        {
            var b=Setup();Assert.AreEqual(0,b.Enemy.MaxHP%2);b.Enemy.CurrentHP=b.Enemy.MaxHP/2+delta;
            Assert.AreEqual(qualifies?1:0,Use(b,"M03").Events.Count(e=>e.Kind==CombatEventKind.Draw));
        }
        [TestCase(14,4)][TestCase(15,3)]
        public void SniperExecutionRefundsOnlyItsOwnDirectKill(int hp,int ap)
        {
            var b=Setup(extra:"M06");b.Enemy.CurrentHP=hp;b.Deck.Draw(30);var card=b.Run.Deck.Last();
            Assert.IsTrue(b.PlayCard(card.InstanceId));Assert.AreEqual(ap,b.Energy);
            Assert.AreEqual(hp==14?1:0,b.LastCardResult.Events.Count(e=>e.Cause==CombatCause.KillRefund));
        }
        [Test] public void SniperExecutionCapsEnergyAndCannotReuseAnOldKill()
        {
            var b=Setup(2);b.Enemy.CurrentHP=1;b.Energy=8;Use(b,"M06");Assert.AreEqual(8,b.Energy);
            b.Energy=5;Use(b,"M06",b.Enemies[1]);Assert.AreEqual(5,b.Energy);
        }
        private BattleRuntime WithDeck(params string[] cards)
        {
            var heroes=Data.Characters.Select(h=>h.Id==CharacterId.Mage?new CharacterDefinition(h.Id,h.Name,h.Description,h.MaxHP,h.Energy,cards):h);
            var data=new GameDefinitions(Data.AllCards,heroes,Data.Enemies,Data.Config,Data.Reward,Data.Elements);
            var run=new RunState(data,CharacterId.Mage,17);var b=new BattleRuntime(data,run,DungeonRuntime.Enter(run,"r1"));
            b.Enemy.CurrentHP=1000;return b;
        }
        private CardInstance Play(BattleRuntime b,string id)
        {var c=DrawCard(b,id);Assert.IsTrue(b.PlayCard(c.InstanceId),id);return c;}
        [Test] public void ResonanceAmplificationIsOneReservationPerCardAndOncePerDirectTarget()
        {
            var b=Setup(3);Use(b,"M09");Use(b,"M09");var r=Use(b,"E14");
            var boosts=r.HitEvents.Where(e=>e.Cause==CombatCause.Enhancement).ToArray();
            Assert.AreEqual(3,boosts.Length);Assert.IsTrue(boosts.All(e=>e.Hit.Amount==2&&e.EffectId=="M09"));Assert.AreEqual(39,r.Damage);
            Assert.IsFalse(Use(b,"E13").HitEvents.Any(e=>e.Cause==CombatCause.Enhancement));
            Use(b,"M09");Use(b,"M01");r=Use(b,"E18",b.Enemies[2]);Assert.AreEqual(2,r.HitEvents.Count(e=>e.Cause==CombatCause.Enhancement));
            CollectionAssert.AreEqual(new[]{"enemy-2","enemy-0"},r.HitEvents.Where(e=>e.Cause==CombatCause.Enhancement).Select(e=>e.TargetId));
        }
        [Test] public void ResonanceIndependentFireChargeAndAmplificationBonusesKeepTheirOwnSources()
        {
            var b=Setup(3);Use(b,"E15");Use(b,"M09");Use(b,"E19");
            Assert.IsFalse(b.ActiveCardEffects.Contains("M09"));Use(b,"M09");var r=Use(b,"E14");
            Assert.AreEqual(3,r.HitEvents.Count(e=>e.EffectId=="M09"));Assert.AreEqual(1,r.HitEvents.Count(e=>e.EffectId=="E19"));
            Assert.AreEqual(3,r.HitEvents.Count(e=>e.Cause==CombatCause.DirectAttack&&e.Hit.CardBonus==2));
            Assert.AreEqual(3,r.Events.Count(e=>e.Kind==CombatEventKind.ElementMark));
            Use(b,"M09");b.EndTurn();Assert.IsFalse(Use(b,"E13").HitEvents.Any(e=>e.Cause==CombatCause.Enhancement));
        }
        [Test] public void ResonanceBarrierCountsAnyLivingMarkedEnemyOnlyOnce()
        {
            var b=Setup(3);Use(b,"M10");Assert.AreEqual(5,b.Block);
            foreach(var e in b.Enemies)e.ElementMark=ElementType.Water;Use(b,"M10");Assert.AreEqual(12,b.Block);
            foreach(var e in b.Enemies)e.ElementMark=ElementType.None;b.Enemies[1].CurrentHP=0;b.Enemies[1].ElementMark=ElementType.Fire;
            Use(b,"M10");Assert.AreEqual(17,b.Block);
        }
        [TestCase("E13",7)][TestCase("B-M01",4)]
        public void ResonanceAnalysisReadsTheActualDrawnCard(string drawn,int damage)
        {
            var b=WithDeck(Enumerable.Repeat(drawn,6).ToArray());var expected=b.Deck.DrawPile.First().InstanceId;
            var r=Use(b,"M11");Assert.AreEqual(damage,r.Damage);
            CollectionAssert.AreEqual(new[]{expected},r.Events.Single(e=>e.Kind==CombatEventKind.Draw).CardInstances);
        }
        [Test] public void ResonanceAnalysisDoesNotHitAnotherEnemyWhenItsOriginalTargetDiesOrTheDrawFails()
        {
            var b=WithDeck();Assert.AreEqual(4,Use(b,"M11").Damage);
            b=Setup(2);b.Enemy.CurrentHP=4;var r=Use(b,"M11");Assert.AreEqual(1,r.HitEvents.Count);Assert.AreEqual(1000,b.Enemies[1].CurrentHP);
        }
        [Test] public void ResonanceDiscountUsesOfficialSharedClassificationAndPreservesInvalidOrCancelledActions()
        {
            var b=WithDeck("M12","E01","E09","M01","B-M02");Play(b,"M12");Assert.AreEqual(0,b.GetCardCost(Data.Card("E01")));
            Assert.AreEqual(1,b.GetCardCost(Data.Card("E09")));Assert.AreEqual(1,b.GetCardCost(Data.Card("M01")));
            Use(b,"M12");Assert.AreEqual(0,b.GetCardCost(Data.Card("E01")));
            var wind=DrawCard(b,"E01");Assert.IsFalse(b.PlayCard(wind.InstanceId,"missing"));Assert.AreEqual(0,b.GetCardCost(Data.Card("E01")));
            Play(b,"M01");Assert.AreEqual(0,b.GetCardCost(Data.Card("E01")));int ap=b.Energy;Play(b,"E01");
            Assert.AreEqual(ap,b.Energy);Assert.AreEqual(1,b.LastCardResult.BaseCost);Assert.AreEqual(0,b.LastCardResult.PaidCost);
            Assert.AreEqual(2,b.GetCardCost(Data.Card("E09")));Use(b,"M12");b.EndTurn();Assert.AreEqual(1,b.GetCardCost(Data.Card("E01")));
        }
        [Test] public void ResonanceCatalystCountsOneCardUseNotAreaHitsAndHonorsAnEarlierUse()
        {
            var b=Setup(3);Use(b,"E13");Use(b,"M13");Use(b,"M13");var r=Use(b,"E14");
            Assert.IsFalse(r.Events.Any(e=>e.Cause==CombatCause.Resonance));b.EndTurn();r=Use(b,"E14");
            Assert.AreEqual(1,r.Events.Count(e=>e.Kind==CombatEventKind.Draw&&e.EffectId=="M13"));
            Assert.IsFalse(Use(b,"E13").Events.Any(e=>e.Kind==CombatEventKind.Draw&&e.EffectId=="M13"));Assert.AreEqual(2,b.SharedCardsUsedThisTurn);
        }
        [Test] public void ResonanceRecoveryUsesExactInstancesAndSharesOneLimitAcrossAllCopies()
        {
            var b=WithDeck("E13","E17","M14","M14","lightning_charge","B-M01");b.Deck.Draw(30);
            var fire=Play(b,"E13");var lightning=Play(b,"E17");var cycle=DrawCard(b,"M14");
            Assert.AreEqual(2,b.RecoveryChoices(Data.Card("M14")).Count);int ap=b.Energy;
            Assert.IsFalse(b.PlayCard(cycle.InstanceId));Assert.AreEqual(ap,b.Energy);
            Assert.IsTrue(b.PlayCard(cycle.InstanceId,recoveryInstanceId:fire.InstanceId));Assert.Contains(fire,b.Deck.Hand.ToList());
            var recovery=b.LastCardResult.Events.Single(e=>e.Kind==CombatEventKind.CardRecovered);
            CollectionAssert.AreEqual(new[]{fire.InstanceId},recovery.CardInstances);
            Play(b,"lightning_charge");Play(b,"M14");Assert.Contains(lightning,b.Deck.DiscardPile.ToList());
            Assert.IsFalse(b.LastCardResult.Events.Any(e=>e.Kind==CombatEventKind.CardRecovered));
            var zones=b.Deck.Hand.Concat(b.Deck.DrawPile).Concat(b.Deck.DiscardPile).Concat(b.Deck.ExhaustPile);
            Assert.AreEqual(b.Run.Deck.Count,zones.Select(c=>c.InstanceId).Distinct().Count());
            b.EndTurn();Assert.AreEqual(0,b.RecoveryChoices(Data.Card("M14")).Count);
        }
        [Test] public void ResonanceRecoveryExcludesDiscountedHighCostCardsAndExhaustedHealingCards()
        {
            var b=WithDeck("M12","E09","E06","M14","B-M01");Play(b,"M12");Play(b,"E09");Assert.AreEqual(1,b.LastCardResult.PaidCost);
            Play(b,"E06");Assert.AreEqual(0,b.RecoveryChoices(Data.Card("M14")).Count);
        }
        [Test] public void ResonanceRecoveryDistinguishesDuplicateCardsAndRejectsStaleChoicesBeforePaying()
        {
            var b=WithDeck("E13","E13","M14","B-M02");var one=Play(b,"E13");var two=Play(b,"E13");var cycle=DrawCard(b,"M14");
            Assert.AreEqual(2,b.RecoveryChoices(Data.Card("M14")).Count);b.Deck.Recover(one.InstanceId);int ap=b.Energy;
            Assert.IsFalse(b.PlayCard(cycle.InstanceId,recoveryInstanceId:one.InstanceId));Assert.AreEqual(ap,b.Energy);
            Assert.IsTrue(b.PlayCard(cycle.InstanceId,recoveryInstanceId:two.InstanceId));
            Assert.IsTrue(b.Deck.Hand.Contains(one)&&b.Deck.Hand.Contains(two));Assert.AreNotEqual(one.InstanceId,two.InstanceId);
        }
        [Test] public void ResonanceSurgeRequiresAnActualReactionFromThisTurn()
        {
            var b=Setup();Use(b,"E13");Assert.AreEqual(20,Use(b,"M15").Damage);Use(b,"E17");Assert.AreEqual(30,Use(b,"M15").Damage);
            b.EndTurn();Assert.AreEqual(20,Use(b,"M15").Damage);
            b=Setup();b.Enemy.ElementMark=ElementType.Fire;b.Enemy.ReactionTurn=b.Turn;b.Enemy.ReactionsThisTurn=2;Use(b,"E17");Assert.AreEqual(20,Use(b,"M15").Damage);
        }
        [Test] public void ResonanceCommandUsesOnlyTheFirstActualReactionAndNeverChainsItself()
        {
            var b=Setup(3);Use(b,"M16");Use(b,"M16");Use(b,"E04");var r=Use(b,"E18",b.Enemies[1]);
            var extra=r.HitEvents.Single(e=>e.Cause==CombatCause.Resonance);Assert.AreEqual("enemy-1",extra.TargetId);Assert.AreEqual(6,extra.Hit.Amount);
            Assert.AreEqual(CombatEventKind.Reaction,r.Events[extra.ParentEventIndex.Value].Kind);Assert.AreEqual(2,r.Events.Count(e=>e.Reaction.HasValue));
        }
        [Test] public void ResonanceCommandInstalledAfterAReactionWaitsForTheNextTurn()
        {
            var b=Setup();Use(b,"E13");Use(b,"E17");Use(b,"M16");Assert.IsFalse(Use(b,"E13").HitEvents.Any(e=>e.Cause==CombatCause.Resonance));
            b.EndTurn();Assert.AreEqual(1,Use(b,"E17").HitEvents.Count(e=>e.Cause==CombatCause.Resonance));
        }
        [Test] public void ResonanceFirstReactionAgainstADeadTargetConsumesTheOpportunityWithoutRetargeting()
        {
            var b=Setup(2);Use(b,"M16");foreach(var e in b.Enemies)e.ElementMark=ElementType.Lightning;b.Enemy.CurrentHP=1;
            var r=Use(b,"E14");Assert.AreEqual(2,r.Events.Count(e=>e.Reaction.HasValue));Assert.IsFalse(r.HitEvents.Any(e=>e.Cause==CombatCause.Resonance));
        }
        [Test] public void AnnihilationCatalogCompletesNinetySevenNewCardsAndKeepsLegacyRewardsSeparate()
        {
            Assert.AreEqual(124,Data.AllCards.Select(c=>c.Id).Distinct().Count());Assert.AreEqual(27,Data.Cards.Count);
            Assert.AreEqual(97,Data.AllCards.Count(c=>c.StarterOnly||c.CatalogOnly));Assert.AreEqual(91,Data.AllCards.Count(c=>c.CatalogOnly&&c.RewardEligible));
            Assert.AreEqual(6,Data.AllCards.Count(c=>c.StarterOnly));
            var mage=Data.AllCards.Where(c=>c.Owner==CharacterId.Mage&&!c.StarterOnly).ToArray();Assert.AreEqual(24,mage.Length);
            Assert.AreEqual(9,mage.Count(c=>c.Rarity==CardRarity.Low));Assert.AreEqual(9,mage.Count(c=>c.Rarity==CardRarity.Middle));Assert.AreEqual(6,mage.Count(c=>c.Rarity==CardRarity.High));
            foreach(CharacterId hero in Enum.GetValues(typeof(CharacterId)))
            {
                Assert.AreEqual(43,Data.AllCards.Count(c=>c.CatalogOnly&&!c.StarterOnly&&(c.Owner==hero||c.ElementApplication==ElementApplication.SharedAttack)));
                var run=Run(hero);Assert.AreEqual(10,run.Deck.Count);Assert.IsTrue(run.Deck.All(c=>Data.Card(c.CardId).IsFormalCard));
            }
            var catalog=AssetDatabase.LoadAssetAtPath<GameCatalogData>("Assets/BRODUN/Data/Generated/GameCatalog.asset");
            foreach (var card in mage)
                Assert.IsNotNull(catalog.CardArtwork(card.Id), card.Id);
            foreach(var build in new[]{CardBuild.Sniper,CardBuild.Resonance,CardBuild.Annihilation})
            {
                var preview=MagePreview.Definitions(Data,build);CollectionAssert.AreEqual(Data.AllCards,preview.AllCards);
                Assert.AreEqual(10,new RunState(preview,CharacterId.Mage,17).Deck.Count);
                CollectionAssert.AreEqual(Data.Character(CharacterId.Priest).StartingDeck,preview.Character(CharacterId.Priest).StartingDeck);
            }
            Assert.AreEqual(10,MagePreview.Deck(CardBuild.Resonance,true).Length);
        }
        [TestCase(1,5)][TestCase(2,8)][TestCase(4,8)]
        public void AnnihilationBarrierUsesThePreCardLivingCountOnce(int enemies,int block)
        {var b=Setup(enemies);Use(b,"M18");Assert.AreEqual(block,b.Block);}
        [Test] public void AnnihilationBarrageDrawIsNotCancelledWhenTheFirstEnemyDies()
        {
            var b=Setup(2);b.Enemy.CurrentHP=4;var r=Use(b,"M19");Assert.AreEqual(1,r.Events.Count(e=>e.Kind==CombatEventKind.Draw));
            b=Setup();Assert.IsFalse(Use(b,"M19").Events.Any(e=>e.Kind==CombatEventKind.Draw));
        }
        [TestCase(1,2)][TestCase(2,4)][TestCase(3,6)][TestCase(5,6)]
        public void AnnihilationShockwaveCountsDistinctHitsIncludingFullyBlockedHits(int enemies,int block)
        {
            var b=Setup(enemies);foreach(var e in b.Enemies)e.Block=20;
            var r=Use(b,"M20");Assert.AreEqual(block,b.Block);Assert.AreEqual(0,r.Damage);Assert.AreEqual(enemies,r.HitEvents.Count);
        }
        [TestCase(1,19)][TestCase(2,11)]
        public void AnnihilationCondensationUsesThePreCardOnlyEnemyCondition(int enemies,int damage)
        {var b=Setup(enemies);Assert.AreEqual(damage,Use(b,"M21").Damage);}
        [Test] public void AnnihilationDebrisResolvesAllBaseHitsBeforeOneNonRecursiveFollowUp()
        {
            var b=Setup(4);b.Enemies[0].CurrentHP=8;b.Enemies[1].CurrentHP=7;b.Enemies[2].CurrentHP=11;
            var r=Use(b,"M22");var direct=r.HitEvents.Where(e=>e.Cause==CombatCause.DirectAttack).ToArray();var extra=r.HitEvents.Where(e=>e.Cause==CombatCause.Conditional).ToArray();
            Assert.AreEqual(4,direct.Length);Assert.AreEqual(2,extra.Length);Assert.Less(direct.Last().Index,extra.First().Index);
            Assert.AreEqual(3,r.HitEvents.Count(e=>e.Hit.Killed));Assert.AreEqual(988,b.Enemies[3].CurrentHP);
            b=Setup(2);Assert.IsFalse(Use(b,"M22").HitEvents.Any(e=>e.Cause==CombatCause.Conditional));
        }
        [TestCase(1,13)][TestCase(2,17)][TestCase(3,21)][TestCase(5,21)]
        public void AnnihilationCollapseSnapshotsTheCountAndAddsStrengthOnlyOnce(int enemies,int damage)
        {
            var b=Setup(enemies);b.Strength=2;b.Enemy.CurrentHP=1;var r=Use(b,"M24");
            foreach(var e in b.Enemies.Skip(1))Assert.AreEqual(1000-damage-2,e.CurrentHP);
            Assert.AreEqual(1,r.HitEvents.Count(e=>e.Hit.Killed));
        }
        [TestCase("M17",8)][TestCase("M23",18)]
        public void AnnihilationAreaAttacksUseEveryRealInstanceAndNoElements(string id,int damage)
        {
            var b=Setup(3);var r=Use(b,id);Assert.AreEqual(3,r.EnemyResults.Count);Assert.AreEqual(damage*3,r.Damage);
            Assert.IsFalse(r.Events.Any(e=>e.Kind==CombatEventKind.ElementMark||e.Reaction.HasValue));
        }
        [Test] public void AnnihilationAllMageCardsPayTheirRealCostsAndLeaveOneCardInstanceAcrossThePiles()
        {
            foreach(var c in Data.AllCards.Where(c=>c.Owner==CharacterId.Mage&&!c.StarterOnly))
            {
                var b=Setup(extra:c.Id);b.Deck.Draw(30);var card=b.Run.Deck.Last();Assert.IsTrue(b.PlayCard(card.InstanceId),c.Id);
                Assert.AreEqual(5-c.Cost,b.Energy,c.Id);Assert.AreEqual(c.Exhausts,b.Deck.ExhaustPile.Contains(card));
                var zones=b.Deck.Hand.Concat(b.Deck.DrawPile).Concat(b.Deck.DiscardPile).Concat(b.Deck.ExhaustPile);
                Assert.AreEqual(1,zones.Count(x=>x.InstanceId==card.InstanceId),c.Id);
            }
        }
        [Test] public void AnnihilationTemporaryAndBattleEffectsResetAtTheirOwnBoundaries()
        {
            var b=Setup(extra:"M01");Use(b,"M09");Use(b,"M12");Use(b,"M13");Use(b,"M16");
            b.EndTurn();Assert.IsFalse(b.ActiveCardEffects.Contains("M09"));Assert.AreEqual(1,b.GetCardCost(Data.Card("E13")));
            Assert.IsTrue(b.ActiveCardEffects.Contains("M13")&&b.ActiveCardEffects.Contains("M16"));
            b.Enemy.CurrentHP=1;b.Deck.Draw(30);var card=b.Deck.Hand.First(c=>c.CardId=="M01");Assert.IsTrue(b.PlayCard(card.InstanceId));
            Assert.IsEmpty(b.ActiveCardEffects);var next=new BattleRuntime(Data,b.Run,b.Run.Map.Single(n=>n.Id=="r1"));
            Assert.IsEmpty(next.ActiveCardEffects);Assert.IsFalse(next.ReactedThisTurn);Assert.AreEqual(0,next.SharedCardsUsedThisTurn);
        }
        [Test] public void SniperExhaustedCardReturnsInTheNextBattleDeck()
        {
            var b=Setup(extra:"M07");var card=b.Run.Deck.Last();b.Deck.Draw(30);Assert.IsTrue(b.PlayCard(card.InstanceId));Assert.Contains(card,b.Deck.ExhaustPile.ToList());
            var next=new BattleRuntime(Data,b.Run,b.Run.Map.Single(n=>n.Id=="r1"));next.Deck.Draw(30);
            Assert.Contains(card,next.Deck.Hand.ToList());Assert.AreEqual(0,next.Deck.ExhaustPile.Count);
        }
    }
}

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
    public sealed class ElementCardTests:RulesTest
    {
        private BattleRuntime Setup(int enemies=1,CharacterId hero=CharacterId.Warrior,string extra=null)
        {
            var run=Run(hero);if(extra!=null)run.AddCard(extra);
            var battle=new BattleRuntime(Data,run,DungeonRuntime.Enter(run,"r1"),additionalEnemies:Enumerable.Repeat(Data.Enemy("moss_slime"),enemies-1));
            foreach(var e in battle.Enemies){e.CurrentHP=1000;e.CurrentIntent=new IntentData(IntentType.Attack,3);}
            return battle;
        }
        private CardResult Use(BattleRuntime b,string id,EnemyInstance target=null,StatusEffectType? cleanse=null)=>
            CardEffectResolver.Resolve(b,Data.Card(id),Data.Card(id).RequiresTargetSelection?target??b.Enemy:null,cleanse);
        private static IEnumerable Specs()
        {
            foreach(var line in File.ReadLines("BRODUN_CARD_SPEC_V1_1.md"))
            {
                if(!Regex.IsMatch(line,@"^\| `E\d\d` \|"))continue;
                var row=line.Trim('|').Split('|').Select(c=>c.Trim()).ToArray();var grade=row[2].Split('/').Select(c=>c.Trim()).ToArray();
                yield return new TestCaseData(row[0].Trim((char)96),row[1],int.Parse(grade[1]),grade[0],row[4]);
            }
        }
        [TestCaseSource(nameof(Specs))]
        public void CatalogMatchesAllNineteenPublishedSpecifications(string id,string name,int ap,string grade,string description)
        {
            var c=Data.Card(id);Assert.AreEqual(name,c.Name);Assert.AreEqual(ap,c.Cost);Assert.AreEqual(description,c.Description);
            Assert.AreEqual(grade=="하급"?CardRarity.Low:grade=="중급"?CardRarity.Middle:CardRarity.High,c.Rarity);
            Assert.IsNull(c.Owner);Assert.IsTrue(c.CatalogOnly&&c.RewardEligible);Assert.IsFalse(c.StarterOnly);
            Assert.Greater(c.BaseDamage,0);Assert.AreEqual(ElementApplication.SharedAttack,c.ElementApplication);
            Assert.AreEqual(id=="E06",c.Exhausts);Assert.IsFalse(Data.Cards.Contains(c));
            foreach(CharacterId hero in Enum.GetValues(typeof(CharacterId)))
            {
                var b=Setup(hero:hero,extra:id);var instance=b.Run.Deck.Last();b.Deck.Draw(30);
                Assert.IsTrue(b.PlayCard(instance.InstanceId,c.RequiresTargetSelection?b.Enemy.TargetId:null));
                Assert.AreEqual(5-ap,b.Energy);Assert.IsTrue(b.LastCardResult.Events.Any(e=>e.Kind==CombatEventKind.ElementMark));
                Assert.AreEqual(11,b.Run.Deck.Count); Assert.AreEqual(2,b.Run.Deck.Count(x=>x.CardId.StartsWith("E")&&x.InstanceId!=instance.InstanceId));
                Assert.AreEqual(5,Data.Config.HandSize);
            }
        }
        [Test] public void CatalogArtOwnershipAndPreviewRunsRemainSeparated()
        {
            var cards=Data.AllCards.Where(c=>c.Id.StartsWith("E")).ToArray();Assert.AreEqual(19,cards.Length);
            Assert.AreEqual(124,Data.AllCards.Count);Assert.AreEqual(97,Data.AllCards.Count(c=>c.StarterOnly||c.CatalogOnly));
            Assert.AreEqual(27,Data.Cards.Count);Assert.AreEqual(8,cards.Count(c=>c.Rarity==CardRarity.Low));
            Assert.AreEqual(8,cards.Count(c=>c.Rarity==CardRarity.Middle));Assert.AreEqual(3,cards.Count(c=>c.Rarity==CardRarity.High));
            CollectionAssert.AreEqual(new[]{3,3,3,3,4,3},new[]{ElementType.Wind,ElementType.Water,ElementType.Ice,ElementType.Poison,ElementType.Fire,ElementType.Lightning}.Select(e=>cards.Count(c=>c.Element==e)));
            var catalog=AssetDatabase.LoadAssetAtPath<GameCatalogData>("Assets/BRODUN/Data/Generated/GameCatalog.asset");
            foreach (var card in cards)
                Assert.IsNotNull(catalog.CardArtwork(card.Id), card.Id);
            foreach(ElementPreviewMode mode in Enum.GetValues(typeof(ElementPreviewMode)))
            {
                var preview=ElementPreview.Definitions(Data,mode);CollectionAssert.AreEqual(Data.AllCards,preview.AllCards);
                foreach(CharacterId hero in Enum.GetValues(typeof(CharacterId)))
                {Assert.AreEqual(10,new RunState(preview,hero,17).Deck.Count);Assert.AreEqual(10,Run(hero).Deck.Count);Assert.IsTrue(Run(hero).Deck.All(c=>Data.Card(c.CardId).IsFormalCard));}
            }
        }
        [TestCase("E05","E08",ElementReaction.RapidFreeze)][TestCase("E08","E05",ElementReaction.RapidFreeze)]
        [TestCase("E08","E01",ElementReaction.FrostGust)][TestCase("E01","E08",ElementReaction.FrostGust)]
        [TestCase("E01","E10",ElementReaction.PoisonSpread)][TestCase("E10","E01",ElementReaction.PoisonSpread)]
        [TestCase("E10","E13",ElementReaction.VenomBurn)][TestCase("E13","E10",ElementReaction.VenomBurn)]
        [TestCase("E13","E17",ElementReaction.Overload)][TestCase("E17","E13",ElementReaction.Overload)]
        [TestCase("E17","E05",ElementReaction.Shock)][TestCase("E05","E17",ElementReaction.Shock)]
        public void ActualCardsTriggerAllSixReactionsInBothDirections(string first,string second,ElementReaction reaction)
        {
            var b=Setup();Use(b,first);var r=Use(b,second);
            Assert.AreEqual(reaction,r.Events.Single(e=>e.Kind==CombatEventKind.Reaction).Reaction);
            Assert.AreEqual(Data.Card(second).Element,b.Enemy.ElementMark);
            Assert.AreEqual(1,r.Events.Count(e=>e.Kind==CombatEventKind.ElementMark));
        }
        [Test] public void SameAndUnpairedElementsRefreshOnlyTheMark()
        {
            var b=Setup();Use(b,"E13");Assert.IsFalse(Use(b,"E13").Events.Any(e=>e.Reaction.HasValue));
            Assert.IsFalse(Use(b,"E08").Events.Any(e=>e.Reaction.HasValue));Assert.AreEqual(ElementType.Ice,b.Enemy.ElementMark);
        }
        [Test] public void PerEnemyReactionCapStillUpdatesMarksAndResetsOnTheNextTurn()
        {
            var b=Setup(2);Use(b,"E05");Use(b,"E17");Use(b,"E05");
            Assert.IsFalse(Use(b,"E17").Events.Any(e=>e.Reaction.HasValue));Assert.AreEqual(ElementType.Lightning,b.Enemy.ElementMark);
            Use(b,"E05",b.Enemies[1]);Assert.IsTrue(Use(b,"E17",b.Enemies[1]).Events.Any(e=>e.Reaction.HasValue));
            b.EndTurn();Assert.IsTrue(Use(b,"E05").Events.Any(e=>e.Reaction.HasValue));
        }
        [TestCase(ElementType.None,0)][TestCase(ElementType.Wind,0)][TestCase(ElementType.Fire,1)]
        public void GustDrawReadsTheOldMark(ElementType mark,int draws)
        {var b=Setup();b.Enemy.ElementMark=mark;Assert.AreEqual(draws,Use(b,"E01").Events.Count(e=>e.Kind==CombatEventKind.Draw));}
        [TestCase(ElementType.None,0)][TestCase(ElementType.Wind,5)]
        public void VacuumBlockReadsTheOldMark(ElementType mark,int block)
        {var b=Setup();b.Enemy.ElementMark=mark;Use(b,"E03");Assert.AreEqual(block,b.Block);}
        [Test] public void AreaAndIceReactionBlockAreAwardedOnceAfterTheReaction()
        {
            var b=Setup(3);foreach(var e in b.Enemies)e.ElementMark=ElementType.Ice;
            var r=Use(b,"E02");Assert.AreEqual(4,b.Block);Assert.AreEqual(3,r.Events.Count(e=>e.Kind==CombatEventKind.Reaction));
            Assert.AreEqual(1,r.Events.Count(e=>e.Kind==CombatEventKind.Block));Assert.AreEqual(CombatEventKind.Block,r.Events.Last().Kind);
            b=Setup();Use(b,"E09");Assert.AreEqual(0,b.Block);
            b.Enemy.ElementMark=ElementType.Water;r=Use(b,"E09");Assert.AreEqual(4,b.Block);Assert.AreEqual(15,r.Damage);
        }
        [Test] public void StreamAndIceMantleAttackAndGrantOnlyTheirPrintedBlock()
        {
            var b=Setup();Assert.AreEqual(6,Use(b,"E05").Damage);Assert.AreEqual(3,b.Block);
            b.Enemy.ElementMark=ElementType.None;Assert.AreEqual(7,Use(b,"E07").Damage);Assert.AreEqual(8,b.Block);
            Assert.AreEqual(ElementType.Ice,b.Enemy.ElementMark);
        }
        [Test] public void CleanseSelectsOneWholePlayerStatusWithoutChangingEnemies()
        {
            var b=Setup(extra:"E04");b.PlayerStatus.AddPoison(4);b.PlayerStatus.ApplyWeak(3);b.Enemy.Status.AddPoison(2);
            var card=b.Run.Deck.Last();b.Deck.Draw(30);int energy=b.Energy;
            Assert.IsFalse(b.PlayCard(card.InstanceId));Assert.AreEqual(energy,b.Energy);Assert.Contains(card,b.Deck.Hand.ToList());
            Assert.IsTrue(b.PlayCard(card.InstanceId,cleanseStatus:StatusEffectType.Poison));
            Assert.AreEqual(0,b.PlayerStatus.Poison);Assert.AreEqual(3,b.PlayerStatus.Weak);Assert.AreEqual(2,b.Enemy.Status.Poison);
            var cleanse=b.LastCardResult.Events.Single(e=>e.Cause==CombatCause.Cleanse);Assert.AreEqual(4,cleanse.Before);Assert.AreEqual(0,cleanse.After);
            Assert.AreEqual(CombatTrace.PlayerId,cleanse.TargetId);
            b=Setup();b.PlayerStatus.ApplyWeak(3);Use(b,"E04");Assert.AreEqual(0,b.PlayerStatus.Weak);
            b=Setup();Assert.IsFalse(Use(b,"E04").Events.Any(e=>e.Cause==CombatCause.Cleanse));
        }
        [TestCase(60,0,0)][TestCase(59,1,2)][TestCase(50,3,2)]
        public void ElementalHealingUsesActualHPForPriestBrandsAndOath(int hp,int heal,int triggers)
        {
            var b=Setup(2,CharacterId.Priest);Use(b,"P06");Use(b,"P23");b.Run.CurrentHP=hp;
            var r=Use(b,"E06");Assert.AreEqual(heal,r.Events.Single(e=>e.Kind==CombatEventKind.Heal).ActualHealing);
            Assert.AreEqual(triggers,r.Events.Count(e=>e.Kind==CombatEventKind.BrandTriggered));Assert.AreEqual(heal>0?12:8,b.Block);
            Assert.IsFalse(r.HitEvents.Any(e=>e.Cause==CombatCause.Brand&&r.Events[e.ParentEventIndex.Value].Kind!=CombatEventKind.BrandTriggered));
            foreach(var e in r.Events.Where(e=>e.Kind==CombatEventKind.BrandTriggered))
                Assert.AreEqual(CombatEventKind.Heal,r.Events[e.ParentEventIndex.Value].Kind);
        }
        [Test] public void ElementalHitsAndReactionDamageCannotTriggerPriestBrands()
        {
            var b=Setup(hero:CharacterId.Priest);Use(b,"P19");Use(b,"E13");var r=Use(b,"E17");
            Assert.AreEqual(1,b.Enemy.Status.Brand);Assert.IsFalse(r.Events.Any(e=>e.Kind==CombatEventKind.BrandTriggered));
            Assert.AreEqual(14,r.Damage);
        }
        [Test] public void PoisonStatusAndMarkStaySeparateAndTicksBypassBlockWithoutReacting()
        {
            var b=Setup();Use(b,"E10");Assert.AreEqual(2,b.Enemy.Status.Poison);Assert.AreEqual(ElementType.Poison,b.Enemy.ElementMark);
            b.Enemy.Block=100;int hp=b.Enemy.CurrentHP;b.EndTurn();
            Assert.AreEqual(hp-2,b.Enemy.CurrentHP);Assert.AreEqual(1,b.Enemy.Status.Poison);Assert.AreEqual(ElementType.Poison,b.Enemy.ElementMark);
            Assert.IsFalse(b.LastTurnResult.Events.Any(e=>e.Kind==CombatEventKind.Reaction||e.Kind==CombatEventKind.ElementMark));
            Assert.AreEqual(0,b.LastTurnResult.Events.Single(e=>e.Cause==CombatCause.PoisonTick&&e.Hit!=null).Hit.Result.Blocked);
        }
        [Test] public void PoisonMistUsesPreCardEnemyCountAndAppliesPoisonBeforeBurnReaction()
        {
            var b=Setup();b.Enemy.ElementMark=ElementType.Fire;var r=Use(b,"E12");
            Assert.AreEqual(4,b.Enemy.Status.Poison);Assert.AreEqual(2,b.Enemy.Status.VenomBurn);
            Assert.Less(r.Events.First(e=>e.Status==StatusEffectType.Poison).Index,r.Events.Single(e=>e.Reaction.HasValue).Index);
            b=Setup(2);b.Enemy.CurrentHP=4;Use(b,"E12");Assert.AreEqual(2,b.Enemies[1].Status.Poison);
        }
        [Test] public void VenomBurnRequiresExistingPoisonAndEnhancesOnlyTheNextTwoTicks()
        {
            var b=Setup();b.Enemy.ElementMark=ElementType.Poison;Use(b,"E13");Assert.AreEqual(0,b.Enemy.Status.VenomBurn);
            b=Setup();Use(b,"E10");Use(b,"E13");b.Enemy.ElementMark=ElementType.Poison;Use(b,"E13");
            Assert.AreEqual(2,b.Enemy.Status.VenomBurn);int hp=b.Enemy.CurrentHP;b.EndTurn();Assert.AreEqual(hp-4,b.Enemy.CurrentHP);
            hp=b.Enemy.CurrentHP;b.EndTurn();Assert.AreEqual(hp-3,b.Enemy.CurrentHP);Assert.AreEqual(0,b.Enemy.Status.Poison);Assert.AreEqual(0,b.Enemy.Status.VenomBurn);
        }
        [Test] public void FreezeAndWeaknessReduceMultihitAttackInTheApprovedOrder()
        {
            var b=Setup();Use(b,"E05");Use(b,"E08");b.Enemy.Status.ApplyWeak(2);b.Enemy.CurrentIntent=new IntentData(IntentType.Attack,8,3);
            Assert.AreEqual(11,IntentResolver.Preview(b.Enemy,Data.Config).TotalDamage);b.EndTurn();
            CollectionAssert.AreEqual(new[]{1,5,5},b.LastTurnResult.Events.Where(e=>e.Cause==CombatCause.EnemyAction&&e.Hit!=null).Select(e=>e.Hit.Amount));
            CollectionAssert.AreEqual(new[]{0,3,5},b.LastEnemyAction.Hits.Select(h=>h.HPDamage));
            Assert.AreEqual(0,b.Enemy.Status.RapidFreeze);Assert.AreEqual(1,b.Enemy.Status.Weak);
        }
        [Test] public void ShockAndPoisonSpreadShareTheirPerCardSpreadBudget()
        {
            var b=Setup(4);Use(b,"E04");var r=Use(b,"E18",b.Enemies[2]);
            Assert.AreEqual(2,r.Events.Count(e=>e.Kind==CombatEventKind.Reaction));
            Assert.AreEqual(2,r.HitEvents.Count(e=>e.Cause==CombatCause.Spread));
            CollectionAssert.AreEqual(new[]{"enemy-0","enemy-1"},r.HitEvents.Where(e=>e.Cause==CombatCause.Spread).Select(e=>e.TargetId));
            Assert.AreEqual(ElementType.Lightning,b.Enemies[2].ElementMark);Assert.AreEqual(ElementType.Lightning,b.Enemies[0].ElementMark);
            Assert.AreEqual(ElementType.Water,b.Enemies[1].ElementMark);
            b=Setup(3);foreach(var e in b.Enemies)e.ElementMark=ElementType.Poison;r=Use(b,"E02");
            Assert.AreEqual(1,r.Events.Count(e=>e.Cause==CombatCause.Spread&&e.Status==StatusEffectType.Poison));
            Assert.AreEqual(2,b.Enemies[1].Status.Poison);
        }
        [Test] public void LethalElementalHitStillRecordsReactionAndSpreadsToSurvivors()
        {
            var b=Setup(3);b.Enemy.CurrentHP=1;b.Enemy.ElementMark=ElementType.Water;var r=Use(b,"E17");
            Assert.AreEqual(1,r.Events.Count(e=>e.Reaction.HasValue));Assert.AreEqual(2,r.HitEvents.Count(e=>e.Cause==CombatCause.Spread));
            Assert.AreEqual(1,r.HitEvents.Count(e=>e.TargetId==b.Enemy.TargetId));
            Assert.AreEqual(1,r.HitEvents.Count(e=>e.Hit.Killed));Assert.AreEqual(ElementType.Lightning,b.Enemy.ElementMark);
        }
        [Test] public void FirePowerStartsAfterRegistrationAppliesOncePerTargetAndDoesNotStack()
        {
            var b=Setup(3);Assert.AreEqual(5,Use(b,"E15").Damage);
            var r=Use(b,"E14");CollectionAssert.AreEqual(new[]{13,13,13},r.HitEvents.Select(e=>e.Hit.Amount));
            Assert.IsTrue(r.HitEvents.All(e=>e.Hit.BaseDamage==11&&e.Hit.CardBonus==2));
            Assert.AreEqual(7,Use(b,"E13").Damage);Use(b,"E15");Assert.AreEqual(7,Use(b,"E13").Damage);
            b.EndTurn();Assert.AreEqual(9,Use(b,"E13").Damage);Assert.AreEqual(7,Use(b,"E13").Damage);
        }
        [Test] public void ChargeAffectsOneTargetAndNeverReappliesStrengthOrElements()
        {
            var b=Setup(3);b.Strength=2;Use(b,"E19");var r=Use(b,"E14");
            var boost=r.HitEvents.Single(e=>e.Cause==CombatCause.Enhancement);Assert.AreEqual(3,boost.Hit.Amount);Assert.AreEqual("enemy-0",boost.TargetId);
            Assert.AreEqual(3,r.Events.Count(e=>e.Kind==CombatEventKind.ElementMark));
            Assert.AreEqual(2,r.HitEvents.First(e=>e.Cause==CombatCause.DirectAttack).Hit.StrengthBonus);
            Use(b,"E19");r=Use(b,"E18",b.Enemies[2]);Assert.AreEqual("enemy-2",r.HitEvents.Single(e=>e.Cause==CombatCause.Enhancement).TargetId);
            Assert.AreEqual(2,r.Events.Count(e=>e.Kind==CombatEventKind.ElementMark));
        }
        [Test] public void ChargeExpiresAtTurnEndAndInvalidInputCannotConsumeIt()
        {
            var b=Setup(2,extra:"E13");Use(b,"E19");var added=b.Run.Deck.Last();b.Deck.Draw(30);
            Assert.IsFalse(b.PlayCard(added.InstanceId,"missing"));Assert.IsTrue(b.PlayCard(added.InstanceId,b.Enemy.TargetId));
            Assert.AreEqual(1,b.LastCardResult.HitEvents.Count(e=>e.Cause==CombatCause.Enhancement));
            Use(b,"E19");b.EndTurn();Assert.IsFalse(Use(b,"E13").HitEvents.Any(e=>e.Cause==CombatCause.Enhancement));
        }
        [Test] public void RepeatedChargeConsumesOldReservationThenQueuesOneNewReservation()
        {
            var b=Setup();Use(b,"E19");Assert.AreEqual(7,Use(b,"E19").Damage);Assert.AreEqual(16,Use(b,"E13").Damage);
            Assert.AreEqual(7,Use(b,"E13").Damage);
        }
        [Test] public void IgnitionSplashUsesFirstSurvivingOtherOrPrimaryFallbackWithoutReactingAgain()
        {
            var b=Setup(3);b.Enemy.ElementMark=ElementType.Lightning;b.Enemies[1].ElementMark=ElementType.Water;b.Strength=2;
            var r=Use(b,"E16");var bonus=r.HitEvents.Single(e=>e.Cause==CombatCause.Conditional);
            Assert.AreEqual("enemy-1",bonus.TargetId);Assert.AreEqual(4,bonus.Hit.Amount);Assert.AreEqual(ElementType.Water,b.Enemies[1].ElementMark);
            Assert.AreEqual(1,r.Events.Count(e=>e.Reaction.HasValue));
            b=Setup();b.Enemy.ElementMark=ElementType.Lightning;r=Use(b,"E16");Assert.AreEqual(18,r.Damage);
            b=Setup();Assert.AreEqual(10,Use(b,"E16").Damage);
        }
        [Test] public void SoloChainBonusIsNonElementalAndDoesNotApplyStrengthAgain()
        {
            var b=Setup();b.Strength=2;var r=Use(b,"E18");Assert.AreEqual(16,r.Damage);
            Assert.AreEqual(3,r.HitEvents.Single(e=>e.Cause==CombatCause.Conditional).Hit.Amount);
            Assert.AreEqual(1,r.Events.Count(e=>e.Kind==CombatEventKind.ElementMark));
        }
        [Test] public void FireChargeStrengthAndReactionBonusesRemainDistinctOnTheSameAreaAttack()
        {
            var b=Setup(3);Use(b,"E15");Use(b,"E19");b.Strength=2;var r=Use(b,"E14");
            var direct=r.HitEvents.Where(e=>e.Cause==CombatCause.DirectAttack).ToArray();
            Assert.AreEqual(3,direct.Length);Assert.IsTrue(direct.All(e=>e.Hit.BaseDamage==11&&e.Hit.StrengthBonus==2&&e.Hit.CardBonus==2&&e.Hit.Amount==15));
            Assert.AreEqual(3,r.HitEvents.Single(e=>e.Cause==CombatCause.Enhancement).Hit.Amount);
            Assert.AreEqual(6,r.HitEvents.Single(e=>e.Cause==CombatCause.Reaction).Hit.ReactionBonus);
            Assert.AreEqual(54,r.Damage);
        }
        [Test] public void CompletelyBlockedElementalHitsStillApplyMarksAndReactions()
        {
            var b=Setup();b.Enemy.Block=100;Use(b,"E13");var r=Use(b,"E17");
            Assert.AreEqual(0,r.Damage);Assert.AreEqual(14,r.Blocked);Assert.AreEqual(1000,b.Enemy.CurrentHP);
            Assert.AreEqual(ElementType.Lightning,b.Enemy.ElementMark);Assert.AreEqual(ElementReaction.Overload,r.Events.Single(e=>e.Reaction.HasValue).Reaction);
        }
        [Test] public void ChargeIsConsumedByTheFirstHitEvenIfThatTargetDiesWithoutReceivingAnotherHit()
        {
            var b=Setup(2);Use(b,"E19");b.Enemy.CurrentHP=7;var r=Use(b,"E14");
            Assert.IsFalse(r.HitEvents.Any(e=>e.Cause==CombatCause.Enhancement));
            Assert.AreEqual(1,r.HitEvents.Count(e=>e.TargetId==b.Enemy.TargetId));
            Assert.AreEqual(989,b.Enemies[1].CurrentHP);Assert.IsFalse(Use(b,"E13",b.Enemies[1]).HitEvents.Any(e=>e.Cause==CombatCause.Enhancement));
        }
        [TestCase(1)][TestCase(12)]
        public void IgnitionStillSplashesAfterDirectOrReactionDamageKillsItsPrimary(int hp)
        {
            var b=Setup(3);b.Enemy.CurrentHP=hp;b.Enemy.ElementMark=ElementType.Lightning;b.Enemies[1].CurrentHP=4;
            var r=Use(b,"E16");Assert.AreEqual(2,r.HitEvents.Count(e=>e.Hit.Killed));
            var splash=r.HitEvents.Single(e=>e.Cause==CombatCause.Conditional);Assert.AreEqual("enemy-1",splash.TargetId);
            Assert.AreEqual(4,splash.Hit.Amount);Assert.AreEqual(1000,b.Enemies[2].CurrentHP);
        }
        [Test] public void ReactionDependentBlockDoesNotTriggerWhenThePerEnemyCapPreventsEveryReaction()
        {
            var b=Setup(3);foreach(var e in b.Enemies){e.ElementMark=ElementType.Ice;e.ReactionTurn=b.Turn;e.ReactionsThisTurn=2;}
            var r=Use(b,"E02");Assert.AreEqual(0,b.Block);Assert.IsFalse(r.Events.Any(e=>e.Reaction.HasValue));
            Assert.IsTrue(b.Enemies.All(e=>e.ElementMark==ElementType.Wind));
        }
        [Test] public void NewBattleDoesNotCarryEnhancementsOrCleanseStatuses()
        {
            var b=Setup();Use(b,"E15");Use(b,"E19");b.PlayerStatus.AddPoison(4);b.Enemy.CurrentHP=1;b.Deck.Draw(30);
            Assert.IsTrue(b.PlayCard(b.Deck.Hand.First(c=>Data.Card(c.CardId).BaseDamage>0).InstanceId));
            var next=new BattleRuntime(Data,b.Run,b.Run.Map.Single(n=>n.Id=="r1"));
            Assert.AreEqual(7,Use(next,"E13").Damage);Assert.AreEqual(0,next.PlayerStatus.Poison);
        }
    }
}

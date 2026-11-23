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
    public sealed class PriestCardTests : RulesTest
    {
        private BattleRuntime Setup(int enemies=1)
        {
            var run=Run(CharacterId.Priest);
            var battle=new BattleRuntime(Data,run,DungeonRuntime.Enter(run,"r1"),
                additionalEnemies:Enumerable.Repeat(Data.Enemy("moss_slime"),enemies-1));
            foreach(var enemy in battle.Enemies){enemy.CurrentHP=1000;enemy.CurrentIntent=new IntentData(IntentType.Attack,3,3);}
            return battle;
        }
        private CardResult Use(BattleRuntime battle,string id) => CardEffectResolver.Resolve(battle,Data.Card(id),Data.Card(id).RequiresTargetSelection?battle.Enemy:null);
        private static IEnumerable Specifications()
        {
            foreach(var line in File.ReadLines("BRODUN_CARD_SPEC_V1_1.md"))
            {
                if(!Regex.IsMatch(line,@"^\| .P\d\d. \|"))continue;
                var row=line.Trim('|').Split('|').Select(c=>c.Trim()).ToArray();
                var grade=row[2].Split('/').Select(c=>c.Trim()).ToArray();
                yield return new TestCaseData(row[0].Trim((char)96),row[1],int.Parse(grade[1]),grade[0],row[4]);
            }
        }
        [TestCaseSource(nameof(Specifications))]
        public void CatalogMatchesPublishedPriestSpecification(string id,string name,int cost,string grade,string description)
        {
            var card=Data.Card(id);
            Assert.AreEqual(name,card.Name);Assert.AreEqual(cost,card.Cost);Assert.AreEqual(description,card.Description);
            Assert.AreEqual(grade=="하급"?CardRarity.Low:grade=="중급"?CardRarity.Middle:CardRarity.High,card.Rarity);
            Assert.AreEqual(CharacterId.Priest,card.Owner);Assert.AreEqual(ElementType.None,card.Element);
            Assert.AreEqual(ElementApplication.None,card.ElementApplication);
            Assert.IsTrue(card.CatalogOnly&&card.RewardEligible);Assert.IsFalse(card.StarterOnly);
            Assert.IsFalse(Data.Cards.Contains(card));
            Assert.AreEqual(id=="P02"||id=="P07",card.Exhausts);
        }
        [Test] public void CatalogOwnershipRewardsAndPreviewDecksStaySeparate()
        {
            Assert.AreEqual(124,Data.AllCards.Select(c=>c.Id).Distinct().Count());
            var priests=Data.AllCards.Where(c=>c.Owner==CharacterId.Priest&&!c.StarterOnly).ToArray();
            Assert.AreEqual(24,priests.Length);
            Assert.AreEqual(9,priests.Count(c=>c.Rarity==CardRarity.Low));
            Assert.AreEqual(9,priests.Count(c=>c.Rarity==CardRarity.Middle));
            Assert.AreEqual(6,priests.Count(c=>c.Rarity==CardRarity.High));
            foreach(CharacterId hero in Enum.GetValues(typeof(CharacterId)))
            {
                var run=Run(hero);Assert.AreEqual(10,run.Deck.Count);
                Assert.IsTrue(run.Deck.All(c=>Data.Card(c.CardId).IsFormalCard));
                if(hero!=CharacterId.Priest)
                    foreach(var card in priests)Assert.Throws<InvalidOperationException>(()=>run.AddCard(card.Id));
            }
            foreach(var build in new[]{CardBuild.Blessing,CardBuild.Weakness,CardBuild.Brand})
            {
                var preview=PriestPreview.Definitions(Data,build);
                CollectionAssert.AreEqual(Data.AllCards,preview.AllCards);
                Assert.AreSame(Data.Config,preview.Config);
                Assert.AreEqual(8,preview.Character(CharacterId.Priest).StartingDeck.Count(id=>preview.Card(id).Build==build));
                Assert.AreEqual(10,preview.Character(CharacterId.Priest).StartingDeck.Count);
                CollectionAssert.AreEqual(Data.Character(CharacterId.Warrior).StartingDeck,preview.Character(CharacterId.Warrior).StartingDeck);
            }
            Assert.AreEqual(27,Data.Cards.Count);Assert.AreEqual(5,Data.Config.HandSize);
            Assert.AreEqual(5,Data.BaseEnergyFor(CharacterId.Priest));Assert.AreEqual(8,Data.Config.EnergyCap);
        }
        [Test] public void CardsUseTheirRealCostsAndExhaustOnlyWhenSpecified()
        {
            foreach(var card in Data.AllCards.Where(c=>c.Owner==CharacterId.Priest&&!c.StarterOnly))
            {
                var run=Run(CharacterId.Priest);var added=run.AddCard(card.Id);
                var b=new BattleRuntime(Data,run,DungeonRuntime.Enter(run,"r1"));b.Enemy.CurrentHP=1000;
                b.Deck.Draw(30);Assert.IsTrue(b.PlayCard(added.InstanceId),card.Id);
                Assert.AreEqual(5-card.Cost,b.Energy);
                Assert.AreEqual(1,b.LastCardResult.Events.Count(e=>e.Kind==(card.Exhausts?CombatEventKind.CardExhausted:CombatEventKind.CardDiscarded)&&e.EffectId==added.InstanceId),card.Id);
                // A draw effect may legitimately shuffle its just-discarded card back into hand.
                var zones=b.Deck.Hand.Concat(b.Deck.DrawPile).Concat(b.Deck.DiscardPile).Concat(b.Deck.ExhaustPile);
                Assert.AreEqual(1,zones.Count(c=>c.InstanceId==added.InstanceId),card.Id);
                Assert.AreEqual(card.Exhausts,b.Deck.ExhaustPile.Contains(added),card.Id);
                Assert.IsFalse(b.LastCardResult.Events.Any(e=>e.Kind==CombatEventKind.ElementMark||e.Kind==CombatEventKind.Reaction));
            }
        }
        [TestCase(60,0)][TestCase(59,1)][TestCase(50,5)]
        public void HealingRecordsRequestedAndActualAmounts(int before,int healed)
        {
            var b=Setup();b.Run.CurrentHP=before;
            var r=Use(b,"P02");var heal=r.Events.Single(e=>e.Kind==CombatEventKind.Heal);
            Assert.AreEqual(5,heal.RequestedAmount);Assert.AreEqual(healed,heal.ActualHealing);
            Assert.AreEqual(before+healed,b.Run.CurrentHP);Assert.AreEqual(before,heal.Before);
        }
        [Test] public void BlessingAndSanctuaryUseThePrintedOrder()
        {
            var b=Setup();Use(b,"P01");Assert.AreEqual(8,b.Block);
            var power=Use(b,"P04");Assert.AreEqual(1,b.Strength);Assert.AreEqual(15,b.Block);
            Assert.Less(power.Events.Single(e=>e.Kind==CombatEventKind.Status).Index,power.Events.Single(e=>e.Kind==CombatEventKind.Block).Index);
            b.Run.CurrentHP=57;var sanctuary=Use(b,"P07");
            Assert.AreEqual(29,b.Block);Assert.AreEqual(60,b.Run.CurrentHP);
            Assert.AreEqual(3,sanctuary.Events.Single(e=>e.Kind==CombatEventKind.Heal).ActualHealing);
            var grace=Use(b,"P03");Assert.AreEqual(7,grace.Damage);
        }
        [TestCase(0,9)][TestCase(1,16)][TestCase(2,17)]
        public void BlessedStrikeAddsStrengthOnlyToTheDirectHit(int strength,int damage)
        {
            var b=Setup();b.Strength=strength;Assert.AreEqual(damage,Use(b,"P05").Damage);
        }
        [Test] public void OathRequiresActualHealingAndTriggersOncePerPlayerTurn()
        {
            var b=Setup();Use(b,"P06");Use(b,"P06");
            Use(b,"P02");Assert.AreEqual(0,b.Block);Assert.IsFalse(b.HealedThisTurn);
            b.Run.CurrentHP=50;Use(b,"P02");Assert.AreEqual(4,b.Block);
            Use(b,"P02");Assert.AreEqual(4,b.Block);
            b.EndTurn();Assert.IsFalse(b.HealedThisTurn);
            Use(b,"P02");Assert.AreEqual(4,b.Block);
        }
        [Test] public void OathInstalledAfterAnEarlierHealWaitsForNextTurn()
        {
            var b=Setup();b.Run.CurrentHP=50;Use(b,"P02");Use(b,"P06");Use(b,"P02");
            Assert.AreEqual(0,b.Block);
            b.EndTurn();Use(b,"P02");Assert.AreEqual(4,b.Block);
        }
        [Test] public void EternalBlessingStartsNextTurnAfterResetAndDoesNotStack()
        {
            var b=Setup();Use(b,"P08");Use(b,"P08");Assert.AreEqual(0,b.Block);
            b.Block=20;b.RetainBlock(10);b.EndTurn();Assert.AreEqual(14,b.Block);
            var entries=b.LastTurnResult.Events.Where(e=>e.Kind==CombatEventKind.Block).ToArray();
            Assert.Less(entries.Single(e=>e.Cause==CombatCause.BlockRetained).Index,entries.Single(e=>e.Cause==CombatCause.TurnStart).Index);
            b.EndTurn();Assert.AreEqual(4,b.Block);
        }
        [TestCase(0,0)][TestCase(1,6)][TestCase(3,6)]
        public void BindingWordUsesWeaknessBeforeApplication(int weak,int block)
        {
            var b=Setup();b.Enemy.Status.ApplyWeak(weak);Use(b,"P11");
            Assert.AreEqual(block,b.Block);Assert.AreEqual(Math.Min(3,weak+1),b.Enemy.Status.Weak);
        }
        [Test] public void PunishmentPrayerAndAreaSilenceKeepEnemyStatusesSeparate()
        {
            var b=Setup(3);Use(b,"P09");Assert.AreEqual(994,b.Enemy.CurrentHP);Assert.AreEqual(1,b.Enemy.Status.Weak);
            int hand=b.Deck.Hand.Count;Use(b,"P10");Assert.AreEqual(hand+1,b.Deck.Hand.Count);
            Assert.AreEqual(2,b.Enemy.Status.Weak);
            var silence=Use(b,"P13");Assert.AreEqual(5,b.Block);
            CollectionAssert.AreEqual(new[]{3,1,1},b.Enemies.Select(e=>e.Status.Weak));
            Assert.AreEqual(3,silence.EnemyResults.Count);Assert.IsEmpty(silence.HitEvents);
            b.EndTurn();
            Assert.IsTrue(b.LastEnemyActions.All(a=>a.Hits.All(hit=>hit.HPDamage==0)));
            CollectionAssert.AreEqual(new[]{2,0,0},b.Enemies.Select(e=>e.Status.Weak));
        }
        [TestCase(0,10)][TestCase(1,17)]
        public void PenitentStrikeUsesPriorWeakness(int weak,int damage)
        {
            var b=Setup();b.Enemy.Status.ApplyWeak(weak);Assert.AreEqual(damage,Use(b,"P12").Damage);
        }
        [TestCase(0,9)][TestCase(1,15)][TestCase(2,21)][TestCase(3,27)]
        public void SinWeightCapsItsBonusAndNeverRepeatsStrength(int weak,int damage)
        {
            var b=Setup();b.Enemy.Status.ApplyWeak(weak);b.Strength=2;
            Assert.AreEqual(damage,Use(b,"P14").Damage);
        }
        [Test] public void FinalJudgmentChecksEachEnemiesOwnPreCardWeakness()
        {
            var b=Setup(3);b.Enemies[1].Status.ApplyWeak(1);b.Enemies[2].Status.ApplyWeak(3);b.Strength=1;
            var r=Use(b,"P15");
            CollectionAssert.AreEqual(new[]{13,21,21},b.Enemies.Select(e=>1000-e.CurrentHP));
            Assert.AreEqual(3,r.EnemyResults.Count);
        }
        [TestCase(0,16,2)][TestCase(1,22,3)]
        public void PainSentenceDoesNotUseTheWeaknessItJustApplied(int weak,int damage,int remaining)
        {
            var b=Setup();b.Enemy.Status.ApplyWeak(weak);Assert.AreEqual(damage,Use(b,"P16").Damage);
            Assert.AreEqual(remaining,b.Enemy.Status.Weak);
        }
        [TestCase(0,4)][TestCase(1,9)]
        public void SinBrandAppliesAfterItsAttackAndPreservesTheNewBrand(int old,int damage)
        {
            var b=Setup();b.Enemy.Status.Brand=old;var r=Use(b,"P17");
            Assert.AreEqual(damage,r.Damage);Assert.AreEqual(1,b.Enemy.Status.Brand);
            Assert.AreEqual(old,r.Events.Count(e=>e.Kind==CombatEventKind.BrandTriggered));
            Assert.AreEqual(StatusEffectType.Brand,r.Events.Last().Status);
        }
        [TestCase(0,0)][TestCase(1,1)]
        public void AtonementDrawUsesBrandBeforeConsumption(int brand,int draws)
        {
            var b=Setup();b.Enemy.Status.Brand=brand;int hand=b.Deck.Hand.Count;
            Use(b,"P18");Assert.AreEqual(hand+draws,b.Deck.Hand.Count);Assert.AreEqual(0,b.Enemy.Status.Brand);
        }
        [Test] public void WhisperAndBellCapBrandsAtOneWithoutDetonatingThem()
        {
            var b=Setup(3);int hand=b.Deck.Hand.Count;Use(b,"P19");Use(b,"P19");
            Assert.AreEqual(1,b.Enemy.Status.Brand);Assert.AreEqual(hand+2,b.Deck.Hand.Count);
            var r=Use(b,"P23");Assert.AreEqual(8,b.Block);
            Assert.IsTrue(b.Enemies.All(e=>e.Status.Brand==1));Assert.IsEmpty(r.HitEvents);
            Assert.AreEqual(3,r.EnemyResults.Count);
        }
        [TestCase(60,0)][TestCase(59,1)][TestCase(50,4)]
        public void MercyHealsAllExistingBrandsOnlyWhenHPActuallyRises(int hp,int healed)
        {
            var b=Setup(3);b.Run.CurrentHP=hp;foreach(var enemy in b.Enemies)enemy.Status.Brand=1;
            var r=Use(b,"P20");Assert.AreEqual(hp+healed,b.Run.CurrentHP);Assert.AreEqual(9,b.Block);
            var heal=r.Events.Single(e=>e.Kind==CombatEventKind.Heal);
            Assert.AreEqual(4,heal.RequestedAmount);Assert.AreEqual(healed,heal.ActualHealing);
            Assert.AreEqual(healed>0?3:0,r.Events.Count(e=>e.Kind==CombatEventKind.BrandTriggered));
            Assert.IsTrue(b.Enemies.All(e=>e.Status.Brand==(healed>0?0:1)));
            foreach(var trigger in r.Events.Where(e=>e.Kind==CombatEventKind.BrandTriggered))
                Assert.AreEqual(heal.Index,trigger.ParentEventIndex);
        }
        [TestCase(60,1)][TestCase(50,2)]
        public void HealingJudgmentDoesNotDetonateTheSameBrandTwice(int hp,int triggers)
        {
            var b=Setup(2);b.Run.CurrentHP=hp;foreach(var enemy in b.Enemies)enemy.Status.Brand=1;
            var r=Use(b,"P21");
            Assert.AreEqual(triggers,r.Events.Count(e=>e.Kind==CombatEventKind.BrandTriggered));
            Assert.AreEqual(985,b.Enemy.CurrentHP);
            Assert.AreEqual(hp==60?1000:995,b.Enemies[1].CurrentHP);
            Assert.AreEqual(hp==60?60:52,b.Run.CurrentHP);
        }
        [TestCase(1000,true)][TestCase(3,false)]
        public void RitualHealsDetonatesThenRebrandsOnlyTheSurvivingSelectedEnemy(int hp,bool survives)
        {
            var b=Setup();b.Run.CurrentHP=50;b.Enemy.CurrentHP=hp;b.Enemy.Status.Brand=1;
            var r=Use(b,"P22");
            Assert.AreEqual(53,b.Run.CurrentHP);Assert.AreEqual(survives?1:0,b.Enemy.Status.Brand);
            var trigger=r.Events.Single(e=>e.Kind==CombatEventKind.BrandTriggered);
            Assert.AreEqual(CombatEventKind.Heal,r.Events[trigger.ParentEventIndex.Value].Kind);
            if(survives)Assert.Greater(r.Events.Last(e=>e.Status==StatusEffectType.Brand).Index,trigger.Index);
            Assert.AreEqual(survives?0:1,r.HitEvents.Count(e=>e.Hit.Killed));
        }
        [TestCase(1,2)][TestCase(3,6)][TestCase(4,6)]
        public void RippleCountsActualAttackBrandTriggersAndCapsBlock(int enemies,int block)
        {
            var b=Setup(enemies);foreach(var enemy in b.Enemies)enemy.Status.Brand=1;
            var r=Use(b,"P24");Assert.AreEqual(block,b.Block);
            Assert.AreEqual(enemies,r.Events.Count(e=>e.Kind==CombatEventKind.BrandTriggered));
            Assert.IsTrue(b.Enemies.All(e=>e.CurrentHP==988&&e.Status.Brand==0));
        }
        [Test] public void DirectLethalDamageClearsBrandWithoutTriggerOrRippleBlock()
        {
            var b=Setup(3);
            b.Enemies[0].CurrentHP=7;b.Enemies[1].CurrentHP=9;
            foreach(var enemy in b.Enemies)enemy.Status.Brand=1;
            var r=Use(b,"P24");
            Assert.AreEqual(2,r.Events.Count(e=>e.Kind==CombatEventKind.BrandTriggered));
            Assert.AreEqual(4,b.Block);Assert.AreEqual(2,r.HitEvents.Count(e=>e.Hit.Killed));
            Assert.AreEqual(1,r.HitEvents.Count(e=>e.TargetId==b.Enemies[0].TargetId));
            Assert.IsTrue(b.Enemies.All(e=>e.Status.Brand==0));
        }
        [Test] public void BasicPriestAttackTriggersBrandsWhileWarriorAndElementalAttacksDoNot()
        {
            var b=Setup();b.Enemy.Status.Brand=1;Assert.AreEqual(11,Use(b,"B-P01").Damage);
            b.Enemy.Status.Brand=1;Assert.AreEqual(7,Use(b,"W17").Damage);Assert.AreEqual(1,b.Enemy.Status.Brand);
            var elemental=new CardDefinition("element-probe","probe","",1,ElementType.Water,
                new[]{new CardEffect(EffectType.Damage,2),new CardEffect(EffectType.Heal,1)},elementApplication:ElementApplication.SharedAttack);
            var zero=CardEffectResolver.Resolve(b,elemental,b.Enemy);
            Assert.IsFalse(zero.Events.Any(e=>e.Kind==CombatEventKind.BrandTriggered));Assert.AreEqual(1,b.Enemy.Status.Brand);
            b.Run.CurrentHP=59;var actual=CardEffectResolver.Resolve(b,elemental,b.Enemy);
            var brand=actual.Events.Single(e=>e.Kind==CombatEventKind.BrandTriggered);
            Assert.AreEqual(CombatEventKind.Heal,actual.Events[brand.ParentEventIndex.Value].Kind);
        }
        [Test] public void BrandDamageUsesBlockAndDoesNotReceiveStrength()
        {
            var b=Setup();b.Enemy.Status.Brand=1;b.Enemy.Block=10;
            var r=Use(b,"P18");Assert.AreEqual(10,r.Blocked);Assert.AreEqual(2,r.Damage);
            Assert.AreEqual(5,r.HitEvents.Single(e=>e.Cause==CombatCause.Brand).Hit.Amount);
            b.Enemy.Status.Brand=1;b.Strength=2;Assert.AreEqual(14,Use(b,"P18").Damage);
        }
        [Test] public void PoisonDoesNotConsumeOrTriggerBrand()
        {
            var b=Setup();b.Enemy.Status.Brand=1;b.Enemy.Status.AddPoison(3);b.EndTurn();
            Assert.AreEqual(1,b.Enemy.Status.Brand);
            Assert.IsFalse(b.LastTurnResult.Events.Any(e=>e.Kind==CombatEventKind.BrandTriggered));
        }
        [Test] public void BrandTraceRetainsOrderSnapshotsAndOneKill()
        {
            var b=Setup();b.Enemy.CurrentHP=10;b.Enemy.Status.Brand=1;
            var r=Use(b,"P18");var trigger=r.Events.Single(e=>e.Kind==CombatEventKind.BrandTriggered);
            var spent=r.Events.Single(e=>e.Status==StatusEffectType.Brand&&e.Cause==CombatCause.Brand);
            var hit=r.HitEvents.Single(e=>e.Cause==CombatCause.Brand);
            Assert.Less(r.HitEvents[0].Index,trigger.Index);Assert.Less(trigger.Index,spent.Index);Assert.Less(spent.Index,hit.Index);
            Assert.AreEqual(trigger.Index,hit.ParentEventIndex);
            Assert.AreEqual(1,r.EnemyResults.Single().Initial.Brand);Assert.AreEqual(0,r.EnemyResults.Single().Final.Brand);
            Assert.AreEqual(1,r.HitEvents.Count(e=>e.Hit.Killed));
        }
        [Test] public void BlessingPassivesResetBetweenBattles()
        {
            var b=Setup();Use(b,"P06");Use(b,"P08");b.Enemy.CurrentHP=1;b.Deck.Draw(20);
            var attack=b.Deck.Hand.First(c=>Data.Card(c.CardId).BaseDamage>0);
            Assert.IsTrue(b.PlayCard(attack.InstanceId));Assert.AreEqual(BattleResult.Won,b.Result);
            var next=new BattleRuntime(Data,b.Run,b.Run.Map.Single(n=>n.Id=="r1"));
            next.Run.CurrentHP=50;Use(next,"P02");Assert.AreEqual(0,next.Block);
            next.EndTurn();Assert.AreEqual(0,next.Block);
        }
    }
}
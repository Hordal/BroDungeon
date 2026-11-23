using System.Linq;
using NUnit.Framework;

namespace Brodun.Tests
{
    public sealed class Phase2CombatTests : RulesTest
    {
        private static CardDefinition Attack(string id,ElementType element,CardTargetScope scope=CardTargetScope.Single,int hits=1,int damage=1)
            => new CardDefinition(id,id,"",1,element,new[]{new CardEffect(EffectType.Damage,damage,hits)},
                scope,ElementApplication.SharedAttack);

        private BattleRuntime Setup(int enemyCount=1,params CardDefinition[] cards)
        {
            var data=new GameDefinitions(Data.AllCards.Concat(cards),Data.Characters,Data.Enemies,Data.Config,Data.Reward,Data.Elements);
            var run=new RunState(data,CharacterId.Warrior,17,"phase2");
            foreach(var card in cards)run.AddCard(card.Id);
            return new BattleRuntime(data,run,DungeonRuntime.Enter(run,"r1"),
                additionalEnemies:Enumerable.Repeat(data.Enemy("moss_slime"),enemyCount-1));
        }

        [Test]
        public void BaseEnergyCapAndTurnDrawUseConfiguredDeck()
        {
            var battle=Setup();
            Assert.AreEqual(5,battle.Energy);
            Assert.AreEqual(5,battle.Deck.Hand.Count);
            var gain=new CardDefinition("gain","gain","",0,ElementType.None,new[]{new CardEffect(EffectType.Energy,12)});
            var result=CardEffectResolver.Resolve(battle,gain);
            Assert.AreEqual(8,battle.Energy);
            var energy=result.Events.Single();
            Assert.AreEqual(5,energy.Before); Assert.AreEqual(8,energy.After);
            Assert.IsTrue(battle.EndTurn());
            Assert.AreEqual(5,battle.Energy);
            Assert.AreEqual(5,battle.Deck.Hand.Count);
        }

        [Test]
        public void EffectiveCostIsPaidOnceAndInvalidTargetKeepsDiscount()
        {
            var card=Attack("discount",ElementType.Fire);
            var battle=Setup(2,card);
            battle.Deck.Draw(1);
            var id=DrawCard(battle,card.Id).InstanceId;
            battle.DiscountNextSharedAttack(3);
            Assert.AreEqual(0,battle.GetCardCost(card));
            Assert.IsFalse(battle.PlayCard(id,"missing"));
            Assert.AreEqual(5,battle.Energy);
            Assert.AreEqual(0,battle.GetCardCost(card));
            Assert.IsTrue(battle.PlayCard(id,battle.Enemies[1].TargetId));
            var result=battle.LastCardResult;
            Assert.AreEqual(1,result.BaseCost); Assert.AreEqual(0,result.PaidCost);
            Assert.AreEqual(card.Id,result.CardId); Assert.AreEqual(id,result.CardInstanceId);
            Assert.AreEqual(battle.Enemies[1].TargetId,result.SelectedTargetId);
            Assert.AreEqual(CombatCause.CardCost,result.Events[0].Cause);
            Assert.AreEqual(5,battle.Energy);
            Assert.AreEqual(1,battle.GetCardCost(card));
            Assert.IsFalse(battle.PlayCard(id,battle.Enemies[1].TargetId));
        }

        [Test]
        public void SingleHitRecordsRealTargetBlockAndActualHealing()
        {
            var battle=Setup(2);
            var enemy=battle.Enemies[1];
            enemy.Block=2;
            battle.Run.CurrentHP=59;
            var result=CardEffectResolver.Resolve(battle,Data.Card("water_wave"),enemy);
            var entry=result.HitEvents.Single();
            Assert.AreEqual(enemy.TargetId,entry.TargetId);
            Assert.AreEqual(4,entry.Hit.Amount); Assert.AreEqual(2,entry.Hit.Result.Blocked);
            Assert.AreEqual(2,entry.Hit.Result.HPDamage);
            Assert.AreEqual(CombatCue.Hit,entry.Cue);
            var heal=result.Events.Single(e=>e.Kind==CombatEventKind.Heal);
            Assert.AreEqual(59,heal.Before); Assert.AreEqual(60,heal.After);
            Assert.AreEqual(enemy.CurrentHP,result.EnemyResults.Single().Final.HP);
            enemy.CurrentHP=1;
            Assert.AreEqual(entry.Hit.HPAfter,result.EnemyResults.Single().Final.HP);
        }

        [Test]
        public void AllEnemyHitsAndStatusesAreRecordedInBattleOrder()
        {
            var card=new CardDefinition("area","area","",1,ElementType.None,new[]
            {
                new CardEffect(EffectType.Damage,4), new CardEffect(EffectType.Weak,1)
            },CardTargetScope.AllEnemies,ElementApplication.None);
            var battle=Setup(3,card);
            battle.Deck.Draw(1);
            battle.Enemies[1].Block=4;
            var id=DrawCard(battle,card.Id).InstanceId;
            Assert.IsFalse(battle.PlayCard(id,battle.Enemies[1].TargetId));
            Assert.IsTrue(battle.PlayCard(id));
            var result=battle.LastCardResult;
            Assert.IsNull(result.SelectedTargetId);
            Assert.AreEqual(1,result.PaidCost); Assert.AreEqual(4,battle.Energy);
            CollectionAssert.AreEqual(battle.Enemies.Select(e=>e.TargetId),result.HitEvents.Select(e=>e.TargetId));
            CollectionAssert.AreEqual(new[]{4,0,4},result.Hits.Select(h=>h.HPDamage));
            Assert.AreEqual(3,result.EnemyResults.Count);
            Assert.IsTrue(result.EnemyResults.All(e=>e.Final.Weak==1));
            Assert.IsTrue(result.EnemyResults.All(e=>e.Final.Mark==ElementType.None));
            CollectionAssert.AreEqual(Enumerable.Range(0,result.Events.Count),result.Events.Select(e=>e.Index));
        }

        [Test]
        public void ChainEffectsCanAssignDifferentDamageToEachTarget()
        {
            var battle=Setup(3);
            var card=new CardDefinition("chain","chain","",2,ElementType.Lightning,new[]
            {
                new CardEffect(EffectType.Damage,11,targets:EffectTargetScope.Primary),
                new CardEffect(EffectType.Damage,6,targets:EffectTargetScope.Secondary)
            },CardTargetScope.Chain,ElementApplication.SharedAttack);
            var result=CardEffectResolver.Resolve(battle,card,battle.Enemies[2]);
            CollectionAssert.AreEqual(new[]{"enemy-2","enemy-0"},result.HitEvents.Select(e=>e.TargetId));
            CollectionAssert.AreEqual(new[]{11,6},result.Hits.Select(e=>e.HPDamage));
            Assert.AreEqual(ElementType.None,battle.Enemies[1].ElementMark);
        }

        [Test]
        public void PoisonTickIgnoresBlockAndDoesNotApplyOrRemoveMark()
        {
            var battle=Setup();
            var enemy=battle.Enemy;
            enemy.Block=9;
            enemy.Status.AddPoison(3);
            enemy.ElementMark=ElementType.Water;
            Assert.IsTrue(battle.EndTurn());
            var poison=battle.LastTurnResult.Events.First(e=>e.Hit!=null);
            Assert.AreEqual(CombatCause.PoisonTick,poison.Cause);
            Assert.AreEqual(3,poison.Hit.Result.HPDamage);
            Assert.AreEqual(9,poison.Hit.BlockAfter);
            Assert.AreEqual(2,enemy.Status.Poison);
            Assert.AreEqual(ElementType.Water,enemy.ElementMark);
        }

        [Test]
        public void StatusRemovalProducesAnExplicitBeforeAndAfterEvent()
        {
            var battle=Setup();
            battle.Enemy.Status.ApplyWeak(2);
            var clear=new CardDefinition("clear","clear","",0,ElementType.None,new[]{new CardEffect(EffectType.ClearWeak)});
            var result=CardEffectResolver.Resolve(battle,clear);
            var change=result.Events.Single();
            Assert.AreEqual(StatusEffectType.Weak,change.Status);
            Assert.AreEqual(2,change.Before); Assert.AreEqual(0,change.After);
            Assert.AreEqual(0,result.EnemyResults.Single().Final.Weak);
        }

        [Test]
        public void WeakStacksOnlyExtendDurationAndFreezeUsesRemainingReductionInHitOrder()
        {
            var battle=Setup();
            var enemy=battle.Enemy;
            enemy.Status.ApplyWeak(2); enemy.Status.ApplyWeak(2);
            Assert.AreEqual(3,enemy.Status.Weak);
            enemy.CurrentIntent=new IntentData(IntentType.Attack,5,3);
            enemy.ElementMark=ElementType.Water;
            CardEffectResolver.Resolve(battle,Attack("freeze",ElementType.Ice));
            var preview=IntentResolver.Preview(enemy,battle.Data.Config);
            CollectionAssert.AreEqual(new[]{0,0,2},Enumerable.Range(0,3).Select(preview.HitAmount));
            Assert.AreEqual(2,preview.ExpectedHPDamage(0));
            battle.EndTurn();
            CollectionAssert.AreEqual(new[]{0,0,2},battle.LastEnemyAction.Hits.Select(h=>h.HPDamage));
            Assert.AreEqual(2,enemy.Status.Weak);
            Assert.AreEqual(0,enemy.Status.RapidFreeze);
        }

        [Test]
        public void CountersFireAfterLastHitOncePerEffectAndRespectTurnLimit()
        {
            var battle=Setup(2);
            battle.Block=20;
            foreach(var enemy in battle.Enemies)enemy.CurrentIntent=new IntentData(IntentType.Attack,3,2);
            Assert.IsTrue(battle.RegisterCounter("once",3,perEnemyTurn:1));
            Assert.IsTrue(battle.RegisterCounter("both",2,perAttacker:1));
            battle.EndTurn();
            var events=battle.LastTurnResult.Events;
            var counters=events.Where(e=>e.Cause==CombatCause.Counter).ToArray();
            CollectionAssert.AreEqual(new[]{"once","both","both"},counters.Select(e=>e.EffectId));
            CollectionAssert.AreEqual(new[]{"enemy-0","enemy-0","enemy-1"},counters.Select(e=>e.TargetId));
            foreach(var entry in counters)
            {
                Assert.AreEqual(2,events.Count(e=>e.Cause==CombatCause.EnemyAction&&e.Hit!=null&&e.ActorId==entry.TargetId));
                Assert.IsTrue(events.Where(e=>e.Hit!=null&&e.ActorId==entry.TargetId).All(e=>e.Index<entry.Index));
            }
            Assert.AreEqual(4,battle.BlockHistory.Count(e=>e.Cause==CombatCause.BlockAbsorbed));
        }

        [Test]
        public void NoAbsorptionMeansNoCounterAndCounterVictoryStillCreatesReward()
        {
            var battle=Setup();
            var enemy=battle.Enemy;
            Assert.IsTrue(battle.RegisterCounter("persistent",3,expiresAfterEnemyTurn:false));
            battle.EndTurn();
            Assert.IsFalse(battle.LastTurnResult.Events.Any(e=>e.Cause==CombatCause.Counter));
            enemy.CurrentHP=1;
            enemy.CurrentIntent=new IntentData(IntentType.Attack,2,2);
            battle.Block=1;
            battle.EndTurn();
            Assert.AreEqual(BattleResult.Won,battle.Result);
            Assert.IsNotNull(battle.Reward);
            Assert.AreEqual(CombatCause.Counter,battle.LastTurnResult.Events[battle.LastTurnResult.EnemyResults.Single().KilledByEvent.Value].Cause);
        }

        [Test]
        public void BlockSpendRestoreAbsorbAndRetentionAreDistinct()
        {
            var battle=Setup();
            var card=new CardDefinition("block_test","block","",0,ElementType.None,new[]
            {
                new CardEffect(EffectType.Block,10),new CardEffect(EffectType.SpendBlock,3),
                new CardEffect(EffectType.RestoreBlock,2),new CardEffect(EffectType.RetainBlock,4)
            });
            CardEffectResolver.Resolve(battle,card);
            battle.Enemy.CurrentIntent=new IntentData(IntentType.Attack,2);
            battle.EndTurn();
            CollectionAssert.AreEqual(new[]{CombatCause.BlockGain,CombatCause.BlockSpent,CombatCause.BlockRestored,
                CombatCause.BlockAbsorbed,CombatCause.BlockRetained},battle.BlockHistory.Select(e=>e.Cause));
            Assert.AreEqual(4,battle.Block);
        }

        [TestCase(ElementType.Water,ElementType.Ice,ElementReaction.RapidFreeze)]
        [TestCase(ElementType.Ice,ElementType.Wind,ElementReaction.FrostGust)]
        [TestCase(ElementType.Wind,ElementType.Poison,ElementReaction.PoisonSpread)]
        [TestCase(ElementType.Poison,ElementType.Fire,ElementReaction.VenomBurn)]
        [TestCase(ElementType.Fire,ElementType.Lightning,ElementReaction.Overload)]
        [TestCase(ElementType.Lightning,ElementType.Water,ElementReaction.Shock)]
        public void ReactionsAreSymmetric(ElementType a,ElementType b,ElementReaction reaction)
        {
            foreach(bool reverse in new[]{false,true})
            {
                var battle=Setup();
                battle.Enemy.ElementMark=reverse?b:a;
                var result=CardEffectResolver.Resolve(battle,Attack("pair",reverse?a:b));
                Assert.AreEqual(reaction,result.Events.Single(e=>e.Kind==CombatEventKind.Reaction).Reaction);
                Assert.AreEqual(reverse?a:b,battle.Enemy.ElementMark);
            }
        }

        [Test]
        public void FirstAndSameMarkDoNotReactAndMultiHitUsesOnlyOneReaction()
        {
            var battle=Setup();
            var fire=Attack("fire",ElementType.Fire);
            var lightning=Attack("bolt",ElementType.Lightning,hits:2);
            for(int i=0;i<2;i++)
                Assert.IsFalse(CardEffectResolver.Resolve(battle,fire).Events.Any(e=>e.Reaction.HasValue));
            var result=CardEffectResolver.Resolve(battle,lightning);
            Assert.AreEqual(1,result.Events.Count(e=>e.Reaction.HasValue));
            Assert.AreEqual(1,result.Events.Count(e=>e.Kind==CombatEventKind.ElementMark));
            Assert.AreEqual(6,result.HitEvents.Single(e=>e.Cause==CombatCause.Reaction).Hit.Amount);
            Assert.AreEqual(2,result.HitEvents.Count(e=>e.Cause==CombatCause.DirectAttack));
        }

        [Test]
        public void ReactionLimitStillUpdatesMarkAndResetsNextPlayerTurn()
        {
            var battle=Setup();
            var water=Attack("water",ElementType.Water);
            var bolt=Attack("bolt",ElementType.Lightning);
            battle.Enemy.ElementMark=ElementType.Water;
            Assert.IsTrue(CardEffectResolver.Resolve(battle,bolt).Events.Any(e=>e.Reaction.HasValue));
            Assert.IsTrue(CardEffectResolver.Resolve(battle,water).Events.Any(e=>e.Reaction.HasValue));
            Assert.IsFalse(CardEffectResolver.Resolve(battle,bolt).Events.Any(e=>e.Reaction.HasValue));
            Assert.AreEqual(ElementType.Lightning,battle.Enemy.ElementMark);
            battle.EndTurn();
            Assert.IsTrue(CardEffectResolver.Resolve(battle,water).Events.Any(e=>e.Reaction.HasValue));
        }

        [Test]
        public void ShockSpreadsOnceInLivingBattleOrderAndDoesNotMarkRecipients()
        {
            var battle=Setup(4);
            foreach(var enemy in battle.Enemies)enemy.ElementMark=ElementType.Water;
            var result=CardEffectResolver.Resolve(battle,Attack("wave",ElementType.Lightning,CardTargetScope.AllEnemies));
            Assert.AreEqual(4,result.Events.Count(e=>e.Reaction==ElementReaction.Shock));
            CollectionAssert.AreEqual(new[]{"enemy-1","enemy-2"},result.HitEvents.Where(e=>e.Cause==CombatCause.Spread).Select(e=>e.TargetId));
            Assert.AreEqual(4,result.Events.Count(e=>e.Kind==CombatEventKind.ElementMark));
            var separate=Setup(3);
            separate.Enemy.ElementMark=ElementType.Water;
            CardEffectResolver.Resolve(separate,Attack("bolt",ElementType.Lightning),separate.Enemy);
            Assert.IsTrue(separate.Enemies.Skip(1).All(e=>e.ElementMark==ElementType.None));
        }

        [Test]
        public void PoisonSpreadOnlyAffectsOneNeighborPerCardAndDoesNotApplyMark()
        {
            var battle=Setup(3);
            foreach(var enemy in battle.Enemies)enemy.ElementMark=ElementType.Poison;
            var result=CardEffectResolver.Resolve(battle,Attack("wind",ElementType.Wind,CardTargetScope.AllEnemies));
            var spread=result.Events.Single(e=>e.Cause==CombatCause.Spread);
            Assert.AreEqual("enemy-1",spread.TargetId);
            Assert.AreEqual(StatusEffectType.Poison,spread.Status);
            Assert.AreEqual(2,battle.Enemies[1].Status.Poison);
            Assert.AreEqual(3,result.Events.Count(e=>e.Reaction==ElementReaction.PoisonSpread));
            Assert.AreEqual(3,result.Events.Count(e=>e.Kind==CombatEventKind.ElementMark));
        }

        [Test]
        public void LethalHitStillTriggersReactionButNeverHitsTheDeadTargetAgain()
        {
            var battle=Setup(2);
            var target=battle.Enemy;
            target.CurrentHP=1; target.ElementMark=ElementType.Water;
            var result=CardEffectResolver.Resolve(battle,Attack("bolt",ElementType.Lightning,hits:3),target);
            Assert.AreEqual(1,result.HitEvents.Count(e=>e.TargetId==target.TargetId));
            Assert.AreEqual(1,result.Events.Count(e=>e.Reaction==ElementReaction.Shock));
            Assert.IsTrue(result.HitEvents.Any(e=>e.Cause==CombatCause.Spread&&e.TargetId=="enemy-1"));
            Assert.AreEqual(result.HitEvents[0].Index,result.EnemyResults[0].KilledByEvent);
        }

        [Test]
        public void FullyBlockedHitStillAppliesMarkAndLegacyCardsKeepSeparateConditions()
        {
            var battle=Setup();
            battle.Enemy.Block=8;
            var result=CardEffectResolver.Resolve(battle,Attack("water",ElementType.Water));
            Assert.AreEqual(0,result.Damage);
            Assert.AreEqual(ElementType.Water,battle.Enemy.ElementMark);
            battle.Enemy.Element=ElementType.Water;
            var legacy=CardEffectResolver.Resolve(battle,Data.Card("ice_link"));
            Assert.IsTrue(legacy.ConditionalTriggered);
            Assert.IsFalse(legacy.Events.Any(e=>e.Reaction.HasValue));
            Assert.AreEqual(ElementType.Water,battle.Enemy.ElementMark);
        }

        [Test]
        public void VenomBurnRefreshesTwoTicksWithoutStackingAndUsesDirectPoisonFirst()
        {
            var battle=Setup();
            var enemy=battle.Enemy;
            enemy.ElementMark=ElementType.Fire;
            var toxin=new CardDefinition("toxin","toxin","",1,ElementType.Poison,new[]
            {
                new CardEffect(EffectType.Poison,3),new CardEffect(EffectType.Damage,1)
            },elementApplication:ElementApplication.SharedAttack);
            var result=CardEffectResolver.Resolve(battle,toxin);
            Assert.AreEqual(CombatEventKind.Damage,result.Events[0].Kind);
            Assert.AreEqual(StatusEffectType.Poison,result.Events[1].Status);
            Assert.AreEqual(2,enemy.Status.VenomBurn);
            battle.EndTurn();
            var tick=battle.LastTurnResult.Events.Single(e=>e.Cause==CombatCause.PoisonTick&&e.Hit!=null).Hit;
            Assert.AreEqual(5,tick.HPBefore-tick.HPAfter); Assert.AreEqual(2,tick.ReactionBonus);
            Assert.AreEqual(1,enemy.Status.VenomBurn);
            CardEffectResolver.Resolve(battle,Attack("fire",ElementType.Fire));
            Assert.AreEqual(2,enemy.Status.VenomBurn);
            battle.EndTurn();
            Assert.AreEqual(1,enemy.Status.VenomBurn);
            battle.EndTurn();
            Assert.AreEqual(0,enemy.Status.VenomBurn);
            Assert.AreEqual(0,enemy.Status.Poison);
        }

        [Test]
        public void FreezeSurvivesAUtilityActionAndDoesNotStack()
        {
            var battle=Setup();
            var enemy=battle.Enemy;
            enemy.ElementMark=ElementType.Water;
            CardEffectResolver.Resolve(battle,Attack("ice",ElementType.Ice));
            CardEffectResolver.Resolve(battle,Attack("water",ElementType.Water));
            Assert.AreEqual(4,enemy.Status.RapidFreeze);
            enemy.CurrentIntent=new IntentData(IntentType.Block,2);
            battle.EndTurn();
            Assert.AreEqual(4,enemy.Status.RapidFreeze);
            enemy.CurrentIntent=new IntentData(IntentType.Attack,3,2);
            battle.EndTurn();
            Assert.AreEqual(2,battle.LastEnemyAction.HPDamage);
            Assert.AreEqual(0,enemy.Status.RapidFreeze);
        }
    }
}

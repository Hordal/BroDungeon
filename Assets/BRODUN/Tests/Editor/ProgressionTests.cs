using System.Linq;
using NUnit.Framework;
namespace Brodun.Tests
{
    public sealed class ProgressionTests : RulesTest
    {
        [Test] public void FixedGraphHasTenRoomsWithOnlyConnectedChoices()
        {
            var r=Run();Assert.AreEqual(11,r.Map.Count);
            Assert.AreEqual(5,r.Map.Count(n=>n.Type==RoomType.Battle));Assert.AreEqual(2,r.Map.Count(n=>n.Type==RoomType.Event));
            Assert.AreEqual(2,r.Map.Count(n=>n.Type==RoomType.Puzzle));Assert.AreEqual(1,r.Map.Count(n=>n.Type==RoomType.Boss));
            Assert.IsNull(DungeonRuntime.Enter(r,"boss"));CollectionAssert.AreEqual(new[]{"r1"},DungeonRuntime.Available(r));
            Assert.NotNull(DungeonRuntime.Enter(r,"r1"));Assert.AreEqual(0,DungeonRuntime.Available(r).Count);
            Assert.IsNull(DungeonRuntime.Enter(r,"r2"));DungeonRuntime.Complete(r);
            CollectionAssert.AreEqual(new[]{"r2","r3","r4"},DungeonRuntime.Available(r));
            Assert.IsNull(DungeonRuntime.Enter(r,"r1"));Assert.IsFalse(DungeonRuntime.Complete(r));
        }
        [TestCase(RewardType.Gold)]
        [TestCase(RewardType.Card)]
        [TestCase(RewardType.Skip)]
        public void RewardAppliedOnceAndRoomCompletesOnlyAfterChoice(RewardType kind)
        {
            var r=Run();DungeonRuntime.Enter(r,"r1");var reward=RewardResolver.Create(Data,r);
            var duplicate=RewardResolver.Create(Data,r);string chosen=reward.Candidates[0];
            Assert.AreEqual(0,r.CompletedNodes.Count);Assert.AreEqual(0,r.Gold);Assert.AreEqual(4,r.Deck.Count);
            Assert.IsTrue(RewardResolver.Resolve(Data,r,reward,kind,kind==RewardType.Card?chosen:null));
            Assert.IsFalse(RewardResolver.Resolve(Data,r,reward,kind,chosen));
            Assert.IsFalse(RewardResolver.Resolve(Data,r,duplicate,kind,chosen));
            Assert.AreEqual(kind==RewardType.Gold?50:0,r.Gold);Assert.AreEqual(kind==RewardType.Card?5:4,r.Deck.Count);
            Assert.IsNull(r.ActiveRoom);CollectionAssert.Contains(r.CompletedNodes,"r1");Assert.AreEqual(1,r.ResolvedRewards.Count);
            Assert.AreEqual(3,DungeonRuntime.Available(r).Count);
        }
        [Test] public void InvalidRewardChoiceIsAtomicAndOtherRoomCannotClaimIt()
        {
            var r=Run();DungeonRuntime.Enter(r,"r1");var reward=RewardResolver.Create(Data,r);
            Assert.IsFalse(RewardResolver.Resolve(Data,r,reward,RewardType.Card,"missing"));
            Assert.IsFalse(RewardResolver.Resolve(Data,r,reward,(RewardType)999));
            Assert.IsFalse(reward.Resolved);Assert.AreEqual(0,r.ResolvedRewards.Count);
            DungeonRuntime.Complete(r);DungeonRuntime.Enter(r,"r2");
            Assert.IsFalse(RewardResolver.Resolve(Data,r,reward,RewardType.Gold));Assert.AreEqual(0,r.Gold);
        }
        [Test] public void CandidatesAreDistinctExcludeOnlyOwnStartersAndAllowAlreadyOwnedCards()
        {
            var r=Run();DungeonRuntime.Enter(r,"r1");var a=RewardResolver.Create(Data,r);
            Assert.AreEqual(3,a.Candidates.Distinct().Count());
            Assert.IsFalse(a.Candidates.Intersect(Data.Character(r.SelectedCharacter).StartingDeck).Any());
            r.AddCard(a.Candidates[0]);var b=RewardResolver.Create(Data,r);
            CollectionAssert.AreEqual(a.Candidates,b.Candidates);
        }
        [Test] public void AcquiredCardAppearsInLaterBattleAndProgressRetainsRunState()
        {
            var r=Run();DungeonRuntime.Enter(r,"r1");var reward=RewardResolver.Create(Data,r);string id=reward.Candidates[0];
            r.CurrentHP=41;r.AddGold(12);
            RewardResolver.Resolve(Data,r,reward,RewardType.Card,id);
            var node=DungeonRuntime.Enter(r,DungeonRuntime.Available(r)[0]);Assert.NotNull(node);
            // Seed 17 r3 is battle; use a valid connected battle node for this scenario.
            if(node.Type!=RoomType.Battle){DungeonRuntime.Complete(r);node=DungeonRuntime.Enter(r,"r6");}
            Assert.AreEqual(RoomType.Battle,node.Type);
            var b=new BattleRuntime(Data,r,node);b.Deck.Draw(20);
            var acquired=b.Deck.Hand.Single(c=>c.CardId==id);Assert.AreEqual("card-4",acquired.InstanceId);
            Assert.IsTrue(b.PlayCard(acquired.InstanceId));
            Assert.AreEqual(41,r.CurrentHP);Assert.AreEqual(12,r.Gold);Assert.AreEqual(5,r.Deck.Count);
            Assert.AreEqual(CharacterId.Warrior,r.SelectedCharacter);Assert.AreEqual(60,r.MaxHP);
        }
        [Test] public void BattleVictoryWaitsForRewardAndBlocksMoreCards()
        {
            var b=Battle();b.Enemy.CurrentHP=1;
            Assert.IsTrue(b.PlayCard(b.Deck.Hand.First(c=>c.CardId=="earth_strike").InstanceId));
            Assert.AreEqual(BattleResult.Won,b.Result);Assert.NotNull(b.Reward);
            Assert.AreEqual(0,b.Run.Gold);Assert.AreEqual("r1",b.Run.ActiveRoom);
            Assert.IsFalse(b.PlayCard(b.Deck.Hand[0].InstanceId));Assert.IsFalse(b.EndTurn());
            Assert.IsTrue(RewardResolver.Resolve(Data,b.Run,b.Reward,RewardType.Gold));Assert.AreEqual(50,b.Run.Gold);
            CollectionAssert.Contains(b.Run.CompletedNodes,"r1");
        }
        [Test] public void BossCompletionHappensAfterReward()
        {
            var r=Run();
            foreach(string id in new[]{"r1","r3","r6","r9"}){Assert.NotNull(DungeonRuntime.Enter(r,id));DungeonRuntime.Complete(r);}
            var b=new BattleRuntime(Data,r,DungeonRuntime.Enter(r,"boss"));b.Enemy.CurrentHP=1;
            b.PlayCard(b.Deck.Hand.First(c=>c.CardId=="earth_strike").InstanceId);
            Assert.AreEqual(RunStatus.InProgress,r.Status);
            RewardResolver.Resolve(Data,r,b.Reward,RewardType.Skip);
            Assert.AreEqual(RunStatus.Complete,r.Status);Assert.AreEqual(0,DungeonRuntime.Available(r).Count);
        }
    }
}

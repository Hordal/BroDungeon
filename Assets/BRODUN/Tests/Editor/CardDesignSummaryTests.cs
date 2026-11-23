using System.Linq;
using Brodun.Presentation;
using NUnit.Framework;
namespace Brodun.Tests
{
    public sealed class CardDesignSummaryTests:RulesTest
    {
        [TestCase("B-W01",1)][TestCase("B-W02",1)][TestCase("W02",1)][TestCase("W05",2)][TestCase("W12",2)][TestCase("W08",3)]
        public void SixCardsKeepPublishedCostAndFullDescription(string id,int cost)
        {var card=Data.Card(id);var before=card.Description;Assert.AreEqual(cost,card.Cost);Assert.IsNotEmpty(CardEffectSummary.For(card));Assert.AreEqual(before,card.Description);}
        [Test] public void BasicsAndStanceReadTheirRealNumbers()
        {
            Assert.AreEqual("6",CardEffectSummary.For(Data.Card("B-W01"))[0].Badges[0].Value);
            Assert.AreEqual("5",CardEffectSummary.For(Data.Card("B-W02"))[0].Badges[0].Value);
            var badges=CardEffectSummary.For(Data.Card("W02"))[0].Badges;
            CollectionAssert.AreEqual(new[]{CardEffectSymbol.Block,CardEffectSymbol.Draw},badges.Select(b=>b.Symbol));
            CollectionAssert.AreEqual(new[]{"4","1"},badges.Select(b=>b.Value));
        }
        [Test] public void RepairShowsTwoExclusiveBranchesNeverAnUnconditionalTwentyThreeBlock()
        {
            var rows=CardEffectSummary.For(Data.Card("W05"));Assert.AreEqual(2,rows.Count);Assert.IsTrue(rows.All(r=>r.Conditional));
            Assert.AreEqual("=0",rows[0].BlindCondition);Assert.AreEqual(">0",rows[1].BlindCondition);
            CollectionAssert.AreEqual(new[]{"14"},rows[0].Badges.Select(b=>b.Value));CollectionAssert.AreEqual(new[]{"9","1"},rows[1].Badges.Select(b=>b.Value));
        }
        [Test] public void ThornArmorCounterIsConditionalAndSeparateFromGuaranteedBlock()
        {
            var card=Data.Card("W12");var rows=CardEffectSummary.For(card);
            Assert.IsFalse(rows[0].Conditional);Assert.AreEqual("7",rows[0].Badges[0].Value);
            Assert.IsTrue(rows[1].Conditional);Assert.AreEqual(CardEffectSymbol.Counter,rows[1].Badges[0].Symbol);Assert.AreEqual("4",rows[1].Badges[0].Value);
            StringAssert.Contains("적당 1회",rows[1].Badges[0].Caption);
        }
        [Test] public void GuardianStrikeShowsScalingAndCapRatherThanFlatTwentyFiveDamage()
        {
            var rows=CardEffectSummary.For(Data.Card("W08"));Assert.AreEqual("10",rows[0].Badges[0].Value);
            Assert.AreEqual(CardEffectSymbol.BlockScale,rows[1].Badges[0].Symbol);Assert.AreEqual("+75%",rows[1].Badges[0].Value);
            Assert.AreEqual("≤+15",rows[1].Badges[0].Limit);StringAssert.Contains("소모 없음",rows[1].Condition);
        }
        [Test] public void RemainingNinetyOneCardsAreNotRedesigned()
        {
            Assert.AreEqual(6,Data.GameplayCards.Count(c=>CardEffectSummary.Supports(c.Id)));
            foreach(var c in Data.GameplayCards.Where(c=>!CardEffectSummary.Supports(c.Id)))Assert.IsEmpty(CardEffectSummary.For(c));
            Assert.AreEqual(91,Data.GameplayCards.Count(c=>!CardEffectSummary.Supports(c.Id)));
        }
        [Test] public void PreviewDeckAndSeedLeaveProductionDeckAndCardReferencesIntact()
        {
            var source=Data.Character(CharacterId.Warrior).StartingDeck.ToArray();var preview=CardDesignComparison.Definitions(Data);
            Assert.AreEqual(10,source.Length);CollectionAssert.AreEqual(source,Data.Character(CharacterId.Warrior).StartingDeck);
            CollectionAssert.AreEqual(CardEffectSummary.PreviewIds,preview.Character(CharacterId.Warrior).StartingDeck);
            foreach(var c in Data.AllCards)Assert.AreSame(c,preview.Card(c.Id));
            uint seed=CardDesignComparison.ComparisonSeed(preview);Assert.AreEqual(seed,CardDesignComparison.ComparisonSeed(preview));
            var dungeon=LayeredMapGenerator.Generate(seed);var run=new RunState(preview,CharacterId.Warrior,seed,dungeon:dungeon);
            var hand=DeckRuntime.Begin(preview,run,dungeon.Rooms.First(r=>r.Layer==1&&r.Type==RoomType.Battle)).Hand;
            CollectionAssert.AreEquivalent(new[]{"B-W01","W02","W05","W12","W08"},hand.Select(c=>c.CardId));
        }
    }
}

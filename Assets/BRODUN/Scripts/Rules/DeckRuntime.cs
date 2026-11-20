using System.Collections.Generic;
using System.Linq;
namespace Brodun
{
    public sealed class DeckRuntime
    {
        private List<CardInstance> draw;
        private readonly List<CardInstance> hand = new List<CardInstance>(), discard = new List<CardInstance>(), exhaust = new List<CardInstance>();
        private readonly IRandomSource rng;
        private readonly GameDefinitions data;
        public IReadOnlyList<CardInstance> DrawPile => draw.AsReadOnly();
        public IReadOnlyList<CardInstance> Hand => hand.AsReadOnly();
        public IReadOnlyList<CardInstance> DiscardPile => discard.AsReadOnly();
        public IReadOnlyList<CardInstance> ExhaustPile => exhaust.AsReadOnly();
        public DeckRuntime(GameDefinitions definitions, IEnumerable<CardInstance> permanentDeck, IRandomSource random, int initialDraw)
        { data=definitions; rng=random; draw=Shuffling.Shuffle(permanentDeck, rng); Draw(initialDraw); }
        public static DeckRuntime Begin(GameDefinitions data, RunState run, RoomNode room) =>
            new DeckRuntime(data, run.Deck, new SeededRandom(unchecked(run.Seed + (uint)run.CompletedNodes.Count*211u + (uint)room.Column)), data.Config.HandSize);
        public void Draw(int count)
        {
            for (int i=0;i<count;i++)
            {
                if (draw.Count==0 && discard.Count>0) { draw=Shuffling.Shuffle(discard,rng); discard.Clear(); }
                if (draw.Count==0) break;
                hand.Add(draw[0]); draw.RemoveAt(0);
            }
        }
        public CardDefinition Use(string instanceId)
        {
            var card=hand.FirstOrDefault(c=>c.InstanceId==instanceId);
            if (card==null) return null;
            var def=data.Card(card.CardId);
            hand.Remove(card); (def.Exhausts?exhaust:discard).Add(card); return def;
        }
        public void EndTurn() { discard.AddRange(hand); hand.Clear(); }
        public void NextTurn() { Draw(data.Config.HandSize); }
    }
}

using System.Collections.Generic;
using System.Linq;
namespace Brodun
{
    public sealed class DeckRuntime
    {
        public int TotalDrawn {get;private set;}
        private List<CardInstance> draw;
        private readonly List<CardInstance> hand = new List<CardInstance>(), discard = new List<CardInstance>(), exhaust = new List<CardInstance>();
        private readonly IRandomSource rng;
        private readonly GameDefinitions data;
        public IReadOnlyList<CardInstance> DrawPile => draw.AsReadOnly();
        public IReadOnlyList<CardInstance> Hand => hand.AsReadOnly();
        public IReadOnlyList<CardInstance> DiscardPile => discard.AsReadOnly();
        public IReadOnlyList<CardInstance> ExhaustPile => exhaust.AsReadOnly();
        public DeckRuntime(GameDefinitions definitions, IEnumerable<CardInstance> permanentDeck, IRandomSource random, int initialDraw)
        { data=definitions; rng=random; draw=Shuffling.Shuffle(permanentDeck.Select(c=>new CardInstance(c.InstanceId,c.CardId,c.UpgradeLevel)), rng); Draw(initialDraw); }
        public static DeckRuntime Begin(GameDefinitions data, RunState run, RoomNode room) =>
            new DeckRuntime(data, run.Deck, new SeededRandom(unchecked(run.Seed + (uint)run.CompletedNodes.Count*211u + (uint)room.Column)), data.Config.HandSize);
        public void Draw(int count)
        {
            for (int i=0;i<count;i++)
            {
                if (data.Cards.Any(c=>c.IsReworked) && hand.Count>=10) break;
                if (draw.Count==0 && discard.Count>0) { draw=Shuffling.Shuffle(discard,rng); discard.Clear(); }
                if (draw.Count==0) break;
                hand.Add(draw[0]);TotalDrawn++; draw.RemoveAt(0);
            }
        }
        internal void BeginUse(string id) { hand.RemoveAll(c=>c.InstanceId==id); }
        internal void FinishUse(CardInstance card,bool exhausted) { (exhausted?exhaust:discard).Add(card); }
        public CardDefinition Use(string instanceId)
        {
            var card=hand.FirstOrDefault(c=>c.InstanceId==instanceId);
            if (card==null) return null;
            var def=data.Card(card.CardId,card.UpgradeLevel);
            hand.Remove(card); (def.Exhausts?exhaust:discard).Add(card); return def;
        }
        private readonly HashSet<string> retained=new HashSet<string>();
        public void Retain(string id) {retained.Add(id);}
        public bool Discard(string id) {var c=hand.FirstOrDefault(x=>x.InstanceId==id);if(c==null)return false;hand.Remove(c);discard.Add(c);return true;}
        public bool Exhaust(string id) {var c=hand.FirstOrDefault(x=>x.InstanceId==id);if(c==null)return false;hand.Remove(c);exhaust.Add(c);return true;}
        public bool Recall(string id) {var c=discard.FirstOrDefault(x=>x.InstanceId==id);if(c==null||hand.Count>=10)return false;discard.Remove(c);hand.Add(c);return true;}
        public void EndTurn() {foreach(var c in hand.ToArray())if(!data.Card(c.CardId,c.UpgradeLevel).Retains&&!retained.Contains(c.InstanceId)){discard.Add(c);hand.Remove(c);}retained.Clear();}
        public void NextTurn() { Draw(data.Config.HandSize); }
    }
}

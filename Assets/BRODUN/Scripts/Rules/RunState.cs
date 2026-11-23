using System;
using System.Collections.Generic;
namespace Brodun
{
    public sealed class CardInstance
    {
        public string InstanceId { get; }
        public string CardId { get; }
        public int UpgradeLevel { get; internal set; } // One upgrade per instance, copied into battle decks.
        public CardInstance(string instanceId, string cardId, int upgradeLevel = 0)
        { InstanceId = instanceId; CardId = cardId; UpgradeLevel = upgradeLevel; }
    }
    // One in-memory run; not a SaveData, singleton, MonoBehaviour, or ScriptableObject.
    public sealed partial class RunState
    {
        public int SchemaVersion => 1;
        public string RunId { get; }
        public uint Seed { get; }
        public CharacterId SelectedCharacter { get; }
        public string ThemeId { get; }
        public RunStatus Status { get; internal set; } = RunStatus.InProgress;
        public int CurrentHP { get; internal set; }
        public int MaxHP { get; }
        public int Gold { get; private set; }
        public int CurrentArea { get; internal set; } = 1;
        public int Floor { get; internal set; } = 1;
        public string CurrentNode { get; internal set; } = "start";
        public string ActiveRoom { get; internal set; }
        internal readonly List<CardInstance> deck = new List<CardInstance>();
        internal readonly List<string> acquired = new List<string>(), completed = new List<string>(), resolved = new List<string>();
        internal readonly Dictionary<string, RoomResult> results = new Dictionary<string, RoomResult>();
        internal readonly Dictionary<string, RoomVisit> visits = new Dictionary<string, RoomVisit>();
        public IReadOnlyDictionary<string, RoomVisit> RoomVisits => visits;
        public IReadOnlyList<CardInstance> Deck => deck.AsReadOnly();
        public IReadOnlyList<string> AcquiredCards => acquired.AsReadOnly();
        public IReadOnlyList<string> CompletedNodes => completed.AsReadOnly();
        public IReadOnlyList<string> ResolvedRewards => resolved.AsReadOnly();
        public IReadOnlyDictionary<string, RoomResult> RoomResults => results;
        public IReadOnlyList<RoomNode> Map { get; }
        public string MapGeneratorVersion { get; }
        public GeneratedDungeon GeneratedDungeon { get; }
        private int nextSerial;
        private readonly GameDefinitions definitions;
        public RunState(GameDefinitions data, CharacterId character, uint seed, string runId = null,GeneratedDungeon dungeon = null,string themeId = DungeonThemes.DefaultId)
        {
            if(!DungeonThemes.IsAvailable(data,themeId))throw new ArgumentException("Unavailable dungeon theme: "+themeId,nameof(themeId));
            ThemeId=themeId;
            definitions = data; SelectedCharacter = character; Seed = seed;
            RunId = runId ?? "run-" + Guid.NewGuid().ToString("N");
            var hero = data.Character(character); CurrentHP = MaxHP = hero.MaxHP;
            foreach (string id in hero.StartingDeck) deck.Add(new CardInstance("card-" + nextSerial++, id));
            GeneratedDungeon=dungeon;
            MapGeneratorVersion=dungeon?.GeneratorVersion??"web-v1";
            Map = dungeon?.Rooms??DungeonRuntime.Generate(seed).AsReadOnly();
        }
        public CardInstance AddCard(string id)
        {
            definitions.Card(id); // Validate before mutating serial/deck.
            var card = new CardInstance("card-" + nextSerial++, id);
            deck.Add(card); acquired.Add(card.InstanceId); return card;
        }
        public bool UpgradeCard(string instanceId) {var c=deck.Find(x=>x.InstanceId==instanceId);if(c==null||c.UpgradeLevel>0||!definitions.Card(c.CardId).IsReworked)return false;c.UpgradeLevel=1;return true;}
        public bool RemoveCard(string instanceId) {var c=deck.Find(x=>x.InstanceId==instanceId);return c!=null&&deck.Count>1&&deck.Remove(c);}
        public void AddGold(int amount)
        { if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount)); Gold = checked(Gold + amount); }
        public bool SpendGold(int amount)
        { if (amount < 0 || amount > Gold) return false; Gold -= amount; return true; }
        public void Heal(int amount)
        { if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount)); CurrentHP = Math.Min(MaxHP, CurrentHP + amount); }
    }
    public sealed class RoomResult
    {
        public RewardType Reward { get; }
        public string CardId { get; }
        public int Gold { get; }
        public string Content { get; }
        public RoomResult(RewardType reward, string cardId, int gold,string content="reward") { Reward = reward; CardId = cardId; Gold = gold; Content=content; }
    }
}

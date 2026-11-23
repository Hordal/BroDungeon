using System;
using System.Collections.Generic;
namespace Brodun
{
    public sealed class CardInstance
    {
        public string InstanceId { get; }
        public string CardId { get; }
        public int UpgradeLevel { get; internal set; } // Web currently always 0.
        public CardInstance(string instanceId, string cardId, int upgradeLevel = 0)
        { InstanceId = instanceId; CardId = cardId; UpgradeLevel = upgradeLevel; }
    }
    // One in-memory run; not a SaveData, singleton, MonoBehaviour, or ScriptableObject.
    public sealed partial class RunState
    {
        public int SchemaVersion => 1;
        public string RunId { get; }
        public uint Seed { get; }
        public string ThemeId => visitedThemes?[CurrentArea-1] ?? DungeonThemes.DefaultId;
        public int AreaCount => IsThemedRun ? DungeonThemes.AreasPerRun : 1;
        public bool IsThemedRun => visitedThemes != null;
        public uint AreaSeed => CurrentArea==1?Seed:unchecked(Seed+((uint)CurrentArea-1)*0x9E3779B9u);
        public int TotalClearedNodes => priorClearedNodes+completed.Count;
        private readonly List<string> visitedThemes;
        public bool HasVisitedTheme(string themeId) => visitedThemes?.Contains(themeId)==true;
        public bool CanAdvanceArea => IsThemedRun&&Status==RunStatus.InProgress&&CurrentArea<AreaCount&&
            ActiveRoom==null&&DungeonRuntime.Node(this,CurrentNode)?.Type==RoomType.Boss&&completed.Contains(CurrentNode);
        private int priorClearedNodes;
        public CharacterId SelectedCharacter { get; }
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
        internal readonly Dictionary<string,PendingReward> pendingBattleRewards=new Dictionary<string,PendingReward>();
        internal readonly HashSet<string> paidVictoryRooms=new HashSet<string>();
        internal readonly Dictionary<string, RoomResult> results = new Dictionary<string, RoomResult>();
        internal readonly Dictionary<string, RoomVisit> visits = new Dictionary<string, RoomVisit>();
        public IReadOnlyDictionary<string, RoomVisit> RoomVisits => visits;
        public IReadOnlyList<CardInstance> Deck => deck.AsReadOnly();
        public IReadOnlyList<string> AcquiredCards => acquired.AsReadOnly();
        public IReadOnlyList<string> CompletedNodes => completed.AsReadOnly();
        public IReadOnlyList<string> ResolvedRewards => resolved.AsReadOnly();
        public IReadOnlyDictionary<string, RoomResult> RoomResults => results;
        public IReadOnlyList<RoomNode> Map { get; private set; }
        public string MapGeneratorVersion { get; private set; }
        public GeneratedDungeon GeneratedDungeon { get; private set; }
        private int nextSerial;
        private readonly GameDefinitions definitions;
        public RunState(GameDefinitions data, CharacterId character, uint seed, string runId = null,GeneratedDungeon dungeon = null,string firstTheme = null)
        {
            definitions = data; SelectedCharacter = character; Seed = seed; visitedThemes = firstTheme==null?null:new List<string>{firstTheme};
            RunId = runId ?? "run-" + Guid.NewGuid().ToString("N");
            var hero = data.Character(character); CurrentHP = MaxHP = hero.MaxHP;
            foreach (string id in hero.StartingDeck)
            {
                RequireOwnedCard(id);
                deck.Add(new CardInstance("card-" + nextSerial++, id));
            }
            SetMap(dungeon);
        }
        private void SetMap(GeneratedDungeon dungeon)
        {
            GeneratedDungeon=dungeon;
            MapGeneratorVersion=dungeon?.GeneratorVersion??"web-v1";
            Map=dungeon?.Rooms??DungeonRuntime.Generate(AreaSeed).AsReadOnly();
        }
        public bool TryAdvanceArea(string themeId)
        {
            if(!CanAdvanceArea||HasVisitedTheme(themeId)||!DungeonThemes.IsAvailable(definitions,themeId))return false;
            visitedThemes.Add(themeId);
            CurrentArea++;
            SetMap(LayeredMapGenerator.Generate(AreaSeed));
            CurrentNode="start";Floor=1;
            priorClearedNodes+=completed.Count;
            completed.Clear();resolved.Clear();visits.Clear();results.Clear();
            pendingBattleRewards.Clear();paidVictoryRooms.Clear();
            return true;
        }
        public CardInstance AddCard(string id)
        {
            RequireOwnedCard(id); // Validate before mutating serial/deck.
            var card = new CardInstance("card-" + nextSerial++, id);
            deck.Add(card); acquired.Add(card.InstanceId); return card;
        }
        internal void RemoveCard(CardInstance card)
        {
            deck.Remove(card);
            acquired.Remove(card.InstanceId);
        }
        private void RequireOwnedCard(string id)
        {
            var definition=definitions.Card(id);
            if(definition.Owner.HasValue&&definition.Owner.Value!=SelectedCharacter)
                throw new InvalidOperationException($"Card {id} belongs to {definition.Owner.Value}.");
        }
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

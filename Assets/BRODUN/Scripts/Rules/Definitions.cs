using System;
using System.Collections.Generic;
using System.Linq;

namespace Brodun
{
    public enum CharacterId { Warrior, Mage, Priest }
    // Earth/Stone/Explosion retain their serialized values for the legacy catalog only.
    public enum ElementType { None, Earth, Stone, Wind, Water, Ice, Poison, Fire, Lightning, Explosion }
    // Presentation classification only; the web has no rarity or type-based gameplay filters.
    public enum CardType { Attack, Guard, Skill, Power }
    public enum CardRarity { Unspecified, Basic, Low, Mid, High }
    public enum EnemyType { Normal, Elite, Boss }
    public enum RoomType { Start, Battle, Puzzle, Event, Boss }
    public enum EventKind { None, Shop, Altar, RestSite }
    public enum IntentType { Attack, Block, Buff }
    public enum RewardType { Gold, Card, Skip }
    public enum StatusEffectType { Strength, Weak, Poison, Block }
    public enum EffectType { Damage, ConditionalDamage, Block, Heal, Draw, Poison, Weak, Strength, Energy, Exhaust }
    public enum ConditionType { None, Block, Poison, Water, Fire }
    public enum RunStatus { InProgress, Complete, Failed }
    public enum BattleResult { Active, Won, Lost }

    public sealed class CardEffect
    {
        public EffectType Type { get; }
        public int Amount { get; }
        public int Hits { get; }
        public ConditionType Condition { get; }
        public CardEffect(EffectType type, int amount = 0, int hits = 1, ConditionType condition = ConditionType.None)
        { Type = type; Amount = amount; Hits = Math.Max(1, hits); Condition = condition; }
    }
    public sealed class CardDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public int Cost { get; }
        public ElementType Element { get; }
        public IReadOnlyList<CardEffect> Effects { get; }
        public bool IsReworked { get; private set; }
        public CharacterId? Owner { get; private set; }
        private CardType? reworkedType;
        private CardRarity reworkedRarity;
        private bool selfExhaust;
        public bool Retains { get; private set; }
        public bool XCost { get; private set; }
        public int UpgradeLevel { get; private set; }
        public CardType Type => reworkedType ?? InferredType;
        private CardType InferredType => Effects.Any(e => e.Type == EffectType.Damage) ? CardType.Attack :
            Effects.Any(e => e.Type == EffectType.Block) ? CardType.Guard : CardType.Skill;
        public CardRarity Rarity => reworkedRarity;
        public CardDefinition Configure(CharacterId? owner, CardType type, CardRarity rarity, bool exhaust, bool retain, bool x, int upgrade)
        {IsReworked=true;Owner=owner;reworkedType=type;reworkedRarity=rarity;selfExhaust=exhaust;Retains=retain;XCost=x;UpgradeLevel=upgrade;return this;}
        public bool Exhausts => selfExhaust || Effects.Any(e => e.Type == EffectType.Exhaust);
        public int BaseDamage => Effects.Where(e => e.Type == EffectType.Damage).Sum(e => e.Amount * e.Hits);
        public int Block => Effects.Where(e => e.Type == EffectType.Block).Sum(e => e.Amount);
        public CardDefinition(string id, string name, string description, int cost, ElementType element, IEnumerable<CardEffect> effects)
        { Id = id; Name = name; Description = description; Cost = cost; Element = element; Effects = Array.AsReadOnly(effects.ToArray()); }
    }
    public sealed class CharacterDefinition
    {
        public CharacterId Id { get; }
        public string Name { get; }
        public string Description { get; }
        public int MaxHP { get; }
        public int Energy { get; }
        public IReadOnlyList<string> StartingDeck { get; }
        public CharacterDefinition(CharacterId id, string name, string description, int hp, int energy, IEnumerable<string> deck)
        { Id = id; Name = name; Description = description; MaxHP = hp; Energy = energy; StartingDeck = Array.AsReadOnly(deck.ToArray()); }
    }
    public sealed class IntentData
    {
        public IntentType Type { get; }
        public int Amount { get; }
        public int Hits { get; }
        public int PatternIndex { get; }
        public IntentData(IntentType type, int amount, int hits = 1, int index = 0)
        { Type = type; Amount = amount; Hits = Math.Max(1, hits); PatternIndex = index; }
        public int ExpectedHPDamage(int block) => Type == IntentType.Attack ? Math.Max(0, Amount * Hits - block) : 0;
    }
    public sealed class EnemyDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public EnemyType Type { get; }
        public int Area { get; }
        public int BaseHP { get; }
        public int AreaHPStep { get; }
        public string AssetId { get; }
        public bool IsLegacy { get; }
        public IReadOnlyList<IntentData> Pattern { get; }
        public EnemyDefinition(string id, string name, EnemyType type, int area, int hp, int step, string asset, IEnumerable<IntentData> pattern, bool legacy = false)
        { Id = id; Name = name; Type = type; Area = area; BaseHP = hp; AreaHPStep = step; AssetId = asset; Pattern = Array.AsReadOnly(pattern.ToArray()); IsLegacy = legacy; }
    }
    public sealed class BattleConfig
    {
        public int HandSize { get; }
        public int EnergyCap { get; }
        public int WeakReduction { get; }
        public int FloorHPStep { get; }
        public int EventHeal { get; }
        public int EventGold { get; }
        public BattleConfig(int hand, int cap, int weak, int floor, int eventHeal, int eventGold)
        { HandSize = hand; EnergyCap = cap; WeakReduction = weak; FloorHPStep = floor; EventHeal = eventHeal; EventGold = eventGold; }
    }
    public sealed class RewardDefinition
    {
        public int Gold { get; }
        public int CandidateCount { get; }
        public RewardDefinition(int gold, int count) { Gold = gold; CandidateCount = count; }
    }
    public sealed class ElementDefinition
    {
        public ElementType Id { get; }
        public string Name { get; }
        public ElementDefinition(ElementType id, string name) { Id = id; Name = name; }
    }
    public sealed class GameDefinitions
    {
        public IReadOnlyList<CardDefinition> Cards { get; }
        public IReadOnlyList<CharacterDefinition> Characters { get; }
        public IReadOnlyList<EnemyDefinition> Enemies { get; }
        public BattleConfig Config { get; }
        public RewardDefinition Reward { get; }
        public IReadOnlyList<ElementDefinition> Elements { get; }
        private readonly Dictionary<string, CardDefinition> cards;
        public GameDefinitions(IEnumerable<CardDefinition> c, IEnumerable<CharacterDefinition> h, IEnumerable<EnemyDefinition> e, BattleConfig config, RewardDefinition reward, IEnumerable<ElementDefinition> elements)
        { Cards = Array.AsReadOnly(c.ToArray()); Characters = Array.AsReadOnly(h.ToArray()); Enemies = Array.AsReadOnly(e.ToArray()); Config = config; Reward = reward; cards = Cards.ToDictionary(x => x.Id); Elements = Array.AsReadOnly(elements.ToArray()); }
        public CardDefinition Card(string id) => cards[id];
        public string ElementName(ElementType element) => element==ElementType.None?string.Empty:Elements.Single(x=>x.Id==element).Name;
        public CardDefinition Card(string id,int upgrade) => upgrade>0 && cards[id].IsReworked?Card70Catalog.Card(id,true):cards[id];
        public CharacterDefinition Character(CharacterId id) => Characters.Single(x => x.Id == id);
        public EnemyDefinition Enemy(string id) => Enemies.Single(x => x.Id == id);
    }
}

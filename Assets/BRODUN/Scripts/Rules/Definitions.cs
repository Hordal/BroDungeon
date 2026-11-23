using System;
using System.Collections.Generic;
using System.Linq;

namespace Brodun
{
    public enum CharacterId { Warrior, Mage, Priest }
    public enum ElementType { None, Earth, Stone, Wind, Water, Ice, Poison, Fire, Lightning, Explosion }
    // Presentation classification only; the web has no rarity or type-based gameplay filters.
    public enum CardType { Attack, Guard, Skill, Power }
    public enum CardRarity { Unspecified, Starter, Low, Middle, High }
    public enum CardBuild { None, Fortress, Counter, BlockSpend, Blessing, Weakness, Brand, Sniper, Resonance, Annihilation }
    public enum EnemyType { Normal, Elite, Boss }
    public enum RoomType { Start, Battle, Puzzle, Event, Boss }
    public enum EventKind { None, Shop, Altar, RestSite }
    public enum IntentType { Attack, Block, Buff }
    public enum RewardType { Gold, Card, Skip }
    public enum StatusEffectType { Strength, Weak, Poison, Block, RapidFreeze, VenomBurn, Brand }
    public enum EffectType { Damage, ConditionalDamage, Block, Heal, Draw, Poison, Weak, Strength, Energy, Exhaust, SpendBlock, RestoreBlock, RetainBlock, ClearWeak, ClearPoison, Counter, DrawOnFirstSpend, RestoreOnFirstSpend, RecoverSpentBlock, Brand, BlockOnFirstHeal, BlockAtTurnStart, FirstFirePower, NextElementPower, CleansePlayer, ReactionSplash, NextElementAllPower, DiscountShared, DrawOnFirstShared, RecoverShared, DamageOnFirstReaction }
    public enum ArmorPenetration { None, Half, Full }
    public enum EffectValueSource { Fixed, CurrentBlock, LastBlockSpent, BlockSpentBeforeCard, WeakBefore, AttackBrandsTriggered, DirectTargetsHit, OtherEnemiesBefore }
    public enum CardTargetScope { Single, AllEnemies, Chain }
    public enum EffectTargetScope { CardTargets, Primary, Secondary }
    public enum ElementApplication { Legacy, None, SharedAttack }
    public enum ConditionType { None, Block, Poison, Water, Fire, BlockBefore, NoBlockBefore, CurrentBlockAtLeast, SpentBlockThisTurn, CounteredLastEnemyTurn, StrengthBefore, WeakBefore, BrandBefore, MarkBefore, OtherMarkBefore, ReactionOccurred, OnlyEnemyBefore, NoSecondaryBefore, HPHalfBefore, DirectKillThisCard, AnyMarkedEnemy, DrewSharedCard, ReactedThisTurnBefore, MultipleEnemiesBefore }
    public enum RunStatus { InProgress, Complete, Failed }
    public enum BattleResult { Active, Won, Lost }

    public sealed class CardEffect
    {
        public EffectType Type { get; }
        public int Amount { get; }
        public int Hits { get; }
        public ConditionType Condition { get; }
        public EffectTargetScope Targets { get; }
        public EffectRule Rule { get; }
        public bool TargetsEnemy => Type==EffectType.Damage||Type==EffectType.ConditionalDamage||Type==EffectType.Weak||
            Type==EffectType.Poison||Type==EffectType.ClearWeak||Type==EffectType.ClearPoison||Type==EffectType.Brand;
        public CardEffect(EffectType type, int amount = 0, int hits = 1, ConditionType condition = ConditionType.None, EffectTargetScope targets = EffectTargetScope.CardTargets, EffectRule rule = null)
        { Type = type; Amount = amount; Hits = Math.Max(1, hits); Condition = condition; Targets = targets; Rule=rule??EffectRule.Default; }
    }
    // Optional rule data keeps legacy effects unchanged and supports ordered, data-driven warrior effects.
    public sealed class EffectRule
    {
        public static readonly EffectRule Default=new EffectRule();
        public EffectValueSource Source { get; }
        public int Numerator { get; }
        public int Denominator { get; }
        public int Maximum { get; }
        public int Threshold { get; }
        public int TurnLimit { get; }
        public int AttackerLimit { get; }
        public int DrawOnCounter { get; }
        public int FallbackAmount { get; }
        public ArmorPenetration Penetration { get; }
        public bool Persistent { get; }
        public EffectRule(EffectValueSource source=EffectValueSource.Fixed,int numerator=1,int denominator=1,
            int maximum=int.MaxValue,int threshold=1,int turnLimit=int.MaxValue,int attackerLimit=int.MaxValue,
            int drawOnCounter=0,bool persistent=false,int fallbackAmount=0,ArmorPenetration penetration=ArmorPenetration.None)
        {
            Source=source;Numerator=numerator;Denominator=denominator;Maximum=maximum;Threshold=threshold;
            TurnLimit=turnLimit;AttackerLimit=attackerLimit;DrawOnCounter=drawOnCounter;Persistent=persistent;FallbackAmount=fallbackAmount;Penetration=penetration;
            if(numerator<0||denominator<=0||maximum<0||turnLimit<=0||attackerLimit<=0||drawOnCounter<0)
                throw new ArgumentOutOfRangeException(nameof(denominator));
        }
        public int Scale(int value)=>(int)Math.Min(Maximum,(long)value*Numerator/Denominator);
    }
    public sealed class CardDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public int Cost { get; }
        public ElementType Element { get; }
        public ElementApplication ElementApplication { get; }
        public CardTargetScope TargetScope { get; }
        public int SecondaryTargets { get; }
        public IReadOnlyList<CardEffect> Effects { get; }
        public CardType Type => reworkedType ?? InferredType;
        private CardType InferredType => Effects.Any(e => e.Type == EffectType.Damage) ? CardType.Attack :
            Effects.Any(e => e.Type == EffectType.Block) ? CardType.Guard : CardType.Skill;
        public CharacterId? Owner { get; private set; }
        public bool StarterOnly { get; private set; }
        public bool CatalogOnly { get; }
        public bool RewardEligible => !StarterOnly;
        // Asset grouping is preserved; production activation uses the explicit formal catalog below.
        public bool IsReworked { get; private set; }
        public bool Retains { get; private set; }
        public bool XCost { get; private set; }
        public int UpgradeLevel { get; private set; }
        private CardType? reworkedType;
        private bool selfExhaust;

        public CardDefinition Configure(CharacterId? owner, CardType type, CardRarity rarity, bool exhaust, bool retain, bool x, int upgrade)
        {
            IsReworked=true; Owner=owner; reworkedType=type; Rarity=rarity;
            StarterOnly=rarity==CardRarity.Starter; selfExhaust=exhaust; Retains=retain; XCost=x; UpgradeLevel=upgrade;
            return this;
        }

        public bool IsFormalCard
        {
            get
            {
                if(IsReworked)return true;
                if(StarterOnly)return Owner.HasValue&&Element==ElementType.None&&ElementApplication==ElementApplication.None&&
                    (Id==BasicPrefix(Owner.Value)+"01"||Id==BasicPrefix(Owner.Value)+"02");
                if(Rarity!=CardRarity.Low&&Rarity!=CardRarity.Middle&&Rarity!=CardRarity.High)return false;
                if(Owner.HasValue)return Element==ElementType.None&&ElementApplication==ElementApplication.None&&
                    InRange(Id,Owner==CharacterId.Warrior?"W":Owner==CharacterId.Mage?"M":"P",24);
                return ElementApplication==ElementApplication.SharedAttack&&InRange(Id,"E",19);
            }
        }
        private static string BasicPrefix(CharacterId hero)=>hero==CharacterId.Warrior?"B-W":hero==CharacterId.Mage?"B-M":"B-P";
        private static bool InRange(string id,string prefix,int max)=>id!=null&&id.Length==prefix.Length+2&&id.StartsWith(prefix,StringComparison.Ordinal)&&
            int.TryParse(id.Substring(prefix.Length),out int number)&&number>=1&&number<=max&&id==prefix+number.ToString("00");
        public CardRarity Rarity { get; private set; }
        public CardBuild Build { get; }
        public string PresentationId { get; }
        public bool Exhausts => selfExhaust || Effects.Any(e => e.Type == EffectType.Exhaust);
        public bool RequiresEnemyTarget => IsReworked
            ?Type==CardType.Attack||Element==ElementType.Lightning||Element==ElementType.Poison||Id=="P07"||Id=="P10"||Id=="P17"
            :Effects.Any(e=>e.TargetsEnemy);
        public bool RequiresTargetSelection => RequiresEnemyTarget && TargetScope != CardTargetScope.AllEnemies;
        public int BaseDamage => Effects.Where(e => e.Type == EffectType.Damage).Sum(e => e.Amount * e.Hits);
        public int Block => Effects.Where(e => e.Type == EffectType.Block).Sum(e => e.Amount);
        public CardDefinition(string id, string name, string description, int cost, ElementType element, IEnumerable<CardEffect> effects,
            CardTargetScope targetScope = CardTargetScope.Single, ElementApplication elementApplication = ElementApplication.Legacy, int secondaryTargets = 1, CharacterId? owner = null, bool starterOnly = false,
            CardRarity rarity=CardRarity.Unspecified,CardBuild build=CardBuild.None,bool catalogOnly=false,string presentationId=null)
        {
            Id=id; Name=name; Description=description; Cost=cost; Element=element; Effects=Array.AsReadOnly(effects.ToArray());
            Owner=owner;StarterOnly=starterOnly;
            CatalogOnly=catalogOnly;Rarity=starterOnly?CardRarity.Starter:rarity;Build=build;PresentationId=presentationId;
            if(starterOnly&&(!owner.HasValue||element!=ElementType.None||elementApplication!=ElementApplication.None))
                throw new ArgumentException("Starter cards require an owner and cannot apply elements.");
            TargetScope=targetScope; ElementApplication=elementApplication; SecondaryTargets=Math.Max(0,secondaryTargets);
            if(elementApplication==ElementApplication.SharedAttack&&
                (!ElementReactionResolver.Supports(element)||!Effects.Any(e=>e.Type==EffectType.Damage)))
                throw new ArgumentException("A shared elemental card needs a direct attack and one of the six elements.");
        }
    }
    public sealed class CharacterDefinition
    {
        public CharacterId Id { get; }
        public string Name { get; }
        public string Description { get; }
        public int MaxHP { get; }
        public int Energy { get; }
        public IReadOnlyList<string> StartingDeck { get; }
        public IReadOnlyList<string> ProvisionalStartingCards { get; }
        public CharacterDefinition(CharacterId id, string name, string description, int hp, int energy, IEnumerable<string> deck, IEnumerable<string> provisional = null)
        { Id = id; Name = name; Description = description; MaxHP = hp; Energy = energy; StartingDeck = Array.AsReadOnly(deck.ToArray()); ProvisionalStartingCards = Array.AsReadOnly((provisional??Array.Empty<string>()).ToArray()); }
    }
    public sealed class IntentData
    {
        public IntentType Type { get; }
        public int Amount { get; }
        public int Hits { get; }
        public int PatternIndex { get; }
        public int AttackReduction { get; }
        public IntentData(IntentType type, int amount, int hits = 1, int index = 0, int attackReduction = 0)
        { Type = type; Amount = amount; Hits = Math.Max(1, hits); PatternIndex = index; AttackReduction=attackReduction; }
        public int HitAmount(int index) => Math.Max(0, Amount-Math.Max(0,AttackReduction-index*Amount));
        public int TotalDamage => Type==IntentType.Attack?Math.Max(0,Amount*Hits-AttackReduction):0;
        public int ExpectedHPDamage(int block) => Math.Max(0,TotalDamage-block);
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
        public const int DefaultBaseEnergy=5, DefaultEnergyCap=8;
        public const int WarriorRestorePerTurn=5;
        public const int BrandDamage=5;
        public int? BaseEnergy { get; }
        public int HandSize { get; }
        public int EnergyCap { get; }
        public int WeakReduction { get; }
        public int FloorHPStep { get; }
        public int EventHeal { get; }
        public int EventGold { get; }
        public ElementReactionConfig Reactions { get; }
        public BattleConfig(int hand, int cap, int weak, int floor, int eventHeal, int eventGold, int? baseEnergy=null, ElementReactionConfig reactions=null)
        {
            HandSize=hand; EnergyCap=cap; WeakReduction=weak; FloorHPStep=floor; EventHeal=eventHeal; EventGold=eventGold;
            BaseEnergy=baseEnergy; Reactions=reactions??new ElementReactionConfig();
        }
    }
    public sealed class RewardTier
    {
        public int BaseGold { get; }
        public int ExtraGold { get; }
        public IReadOnlyList<int> RarityWeights { get; }
        public RewardTier(int baseGold,int extraGold,int low,int middle,int high)
        {
            if(baseGold<0||extraGold<0||low<0||middle<0||high<0)throw new ArgumentOutOfRangeException(nameof(baseGold));
            BaseGold=baseGold;ExtraGold=extraGold;RarityWeights=Array.AsReadOnly(new[]{low,middle,high});
        }
    }
    public sealed class RewardPolicy
    {
        public int UniqueWeight { get; }
        public int ElementWeight { get; }
        public RewardTier Normal { get; }
        public RewardTier Elite { get; }
        public RewardTier Boss { get; }
        public RewardPolicy(int uniqueWeight=65,int elementWeight=35,RewardTier normal=null,RewardTier elite=null,RewardTier boss=null)
        {
            if(uniqueWeight<0||elementWeight<0||uniqueWeight+elementWeight==0)throw new ArgumentOutOfRangeException(nameof(uniqueWeight));
            UniqueWeight=uniqueWeight;ElementWeight=elementWeight;
            Normal=normal??new RewardTier(10,15,70,25,5);Elite=elite??new RewardTier(20,25,45,40,15);Boss=boss??new RewardTier(30,0,0,0,0);
        }
        public RewardTier For(EnemyType type)=>type==EnemyType.Boss?Boss:type==EnemyType.Elite?Elite:Normal;
    }
    public sealed class RewardDefinition
    {
        public int Gold { get; } // Preserved web/legacy QA configuration only.
        public int CandidateCount { get; }
        public RewardPolicy Policy { get; }
        public RewardDefinition(int gold,int count,RewardPolicy policy=null) { Gold=gold;CandidateCount=count;Policy=policy; }
    }
    public sealed class ElementDefinition
    {
        public ElementType Id { get; }
        public string Name { get; }
        public ElementDefinition(ElementType id, string name) { Id = id; Name = name; }
    }
    public sealed class GameDefinitions
    {
        // Cards remains the compatibility pool for untouched team RoomRuntime consumers. General combat uses GameplayCards/RewardCards.
        public IReadOnlyList<CardDefinition> Cards { get; }
        public IReadOnlyList<CardDefinition> AllCards { get; }
        public IReadOnlyList<CardDefinition> GameplayCards { get; }
        public IReadOnlyList<CardDefinition> RewardCards { get; }
        public IReadOnlyList<CardDefinition> RewardCardsFor(CharacterId hero)=>Array.AsReadOnly(RewardCards.Where(c=>!c.Owner.HasValue||c.Owner==hero).ToArray());
        public IReadOnlyList<CharacterDefinition> Characters { get; }
        public IReadOnlyList<EnemyDefinition> Enemies { get; }
        public BattleConfig Config { get; }
        public RewardDefinition Reward { get; }
        public IReadOnlyList<ElementDefinition> Elements { get; }
        private readonly Dictionary<string, CardDefinition> cards;
        public GameDefinitions(IEnumerable<CardDefinition> c, IEnumerable<CharacterDefinition> h, IEnumerable<EnemyDefinition> e, BattleConfig config, RewardDefinition reward, IEnumerable<ElementDefinition> elements)
        { AllCards = Array.AsReadOnly(c.ToArray()); GameplayCards=Array.AsReadOnly(AllCards.Where(x=>x.IsFormalCard).OrderBy(x=>x.Id,StringComparer.Ordinal).ToArray()); RewardCards=Array.AsReadOnly(GameplayCards.Where(x=>!x.StarterOnly).ToArray()); Cards = Array.AsReadOnly(AllCards.Where(x=>x.RewardEligible&&!x.CatalogOnly).ToArray()); Characters = Array.AsReadOnly(h.ToArray()); Enemies = Array.AsReadOnly(e.ToArray()); Config = config; Reward = reward; cards = AllCards.ToDictionary(x => x.Id); Elements = Array.AsReadOnly(elements.ToArray()); }
        public CardDefinition Card(string id) => cards[id];
        public CardDefinition CardVersion(string id,int upgrade) => upgrade>0&&cards[id].IsReworked?Card70Catalog.Card(id,true):cards[id];
        public string ElementName(ElementType id) => id==ElementType.None?"무속성":Elements.Single(e=>e.Id==id).Name;
        public CharacterDefinition Character(CharacterId id) => Characters.Single(x => x.Id == id);
        public int BaseEnergyFor(CharacterId id) => Config.BaseEnergy??Character(id).Energy;
        public EnemyDefinition Enemy(string id) => Enemies.Single(x => x.Id == id);
    }
}

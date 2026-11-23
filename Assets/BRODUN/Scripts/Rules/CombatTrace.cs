using System;
using System.Collections.Generic;
using System.Linq;

namespace Brodun
{
    public enum CombatEventKind { EnemyAction, Damage, Block, Heal, Status, ElementMark, LegacyElement, Reaction, Energy, Draw, CardDiscarded, CardExhausted, BrandTriggered, CardRecovered }
    public enum CombatCause
    {
        Card, DirectAttack, Conditional, EnemyAction, PoisonTick, Reaction, Spread, Counter,
        BlockGain, BlockSpent, BlockAbsorbed, BlockRestored, BlockRetained, BlockReset, TurnStart, CardCost, Brand, FirstHeal, Death, Enhancement, Cleanse, KillRefund, Resonance, Recovery
    }
    public enum CombatCue { None, Hit, Heal, Block, Status, ElementMark, Reaction }

    public sealed class CombatHit
    {
        public int Amount { get; }
        public int HPBefore { get; }
        public int HPAfter { get; }
        public int BlockBefore { get; }
        public int BlockAfter { get; }
        public int ReactionBonus { get; }
        public int BaseDamage { get; }
        public int StrengthBonus { get; }
        public int CardBonus { get; }
        public int IgnoredBlock { get; }
        public DamageResult Result => new DamageResult(BlockBefore-BlockAfter,HPBefore-HPAfter);
        public bool Killed => HPBefore>0&&HPAfter==0;
        internal CombatHit(int amount,int hpBefore,int hpAfter,int blockBefore,int blockAfter,int reactionBonus=0,int? baseDamage=null,int strengthBonus=0,int cardBonus=0,int ignoredBlock=0)
        {
            Amount=amount; HPBefore=hpBefore; HPAfter=hpAfter; BlockBefore=blockBefore; BlockAfter=blockAfter;
            ReactionBonus=reactionBonus;BaseDamage=baseDamage??amount;StrengthBonus=strengthBonus;CardBonus=cardBonus;IgnoredBlock=ignoredBlock;
        }
    }

    public sealed class CombatEvent
    {
        public int Index { get; }
        public int Turn { get; }
        public int ActionId { get; }
        public string ActorId { get; }
        public string TargetId { get; }
        public string CardId { get; }
        public CombatEventKind Kind { get; }
        public CombatCause Cause { get; }
        public int Before { get; }
        public int After { get; }
        public int? ParentEventIndex { get; }
        public string EffectId { get; }
        public CombatHit Hit { get; }
        public StatusEffectType? Status { get; }
        public ElementReaction? Reaction { get; }
        public ElementType OldElement { get; }
        public ElementType NewElement { get; }
        public int RequestedAmount { get; }
        public IReadOnlyList<string> CardInstances { get; }
        public int ActualHealing => Kind==CombatEventKind.Heal?After-Before:0;
        public CombatCue Cue => Kind switch
        {
            CombatEventKind.Damage=>CombatCue.Hit, CombatEventKind.Heal=>CombatCue.Heal,
            CombatEventKind.Block=>CombatCue.Block, CombatEventKind.Status=>CombatCue.Status,
            CombatEventKind.ElementMark=>CombatCue.ElementMark, CombatEventKind.Reaction=>CombatCue.Reaction,
            CombatEventKind.BrandTriggered=>CombatCue.Reaction, _=>CombatCue.None
        };
        internal CombatEvent(int index,int turn,int actionId,string actor,string target,string card,CombatEventKind kind,
            CombatCause cause,int before,int after,CombatHit hit,StatusEffectType? status,ElementReaction? reaction,
            ElementType oldElement,ElementType newElement,int? parent,string effectId,int requestedAmount,IEnumerable<string> cardInstances)
        {
            Index=index; Turn=turn; ActionId=actionId; ActorId=actor; TargetId=target; CardId=card; Kind=kind; Cause=cause;
            Before=before; After=after; Hit=hit; Status=status; Reaction=reaction; OldElement=oldElement; NewElement=newElement;
            ParentEventIndex=parent; EffectId=effectId;
            RequestedAmount=requestedAmount;CardInstances=Array.AsReadOnly((cardInstances??Array.Empty<string>()).ToArray());
        }
    }

    public sealed class EnemyCombatSnapshot
    {
        public int HP { get; }
        public int MaxHP { get; }
        public int Block { get; }
        public int Weak { get; }
        public int Poison { get; }
        public int Strength { get; }
        public int RapidFreeze { get; }
        public int VenomBurn { get; }
        public ElementType Mark { get; }
        public int Brand { get; }
        internal EnemyCombatSnapshot(EnemyInstance enemy)
        {
            HP=enemy.CurrentHP;MaxHP=enemy.MaxHP;Block=enemy.Block; Weak=enemy.Status.Weak; Poison=enemy.Status.Poison;
            Strength=enemy.Status.Strength; RapidFreeze=enemy.Status.RapidFreeze; VenomBurn=enemy.Status.VenomBurn; Mark=enemy.ElementMark;
            Brand=enemy.Status.Brand;
        }
    }

    public sealed class EnemyEffectResult
    {
        public string TargetId { get; }
        public EnemyCombatSnapshot Initial { get; }
        public EnemyCombatSnapshot Final { get; }
        public IReadOnlyList<CombatEvent> Events { get; }
        public int? KilledByEvent => Events.FirstOrDefault(e=>e.Hit?.Killed==true)?.Index;
        internal EnemyEffectResult(EnemyInstance enemy,EnemyCombatSnapshot initial,IEnumerable<CombatEvent> events)
        { TargetId=enemy.TargetId; Initial=initial; Final=new EnemyCombatSnapshot(enemy); Events=Array.AsReadOnly(events.ToArray()); }
    }

    public sealed class CombatTrace
    {
        public const string PlayerId="player";
        private readonly List<CombatEvent> events=new List<CombatEvent>();
        private readonly Dictionary<string,EnemyCombatSnapshot> initial;
        public int Turn { get; }
        public int ActionId { get; }
        public string CardId { get; }
        public IReadOnlyList<CombatEvent> Events { get; }
        public IReadOnlyList<EnemyEffectResult> EnemyResults { get; private set; }=Array.Empty<EnemyEffectResult>();
        internal CombatTrace(BattleRuntime battle,int actionId,string cardId)
        {
            Turn=battle.Turn; ActionId=actionId; CardId=cardId;
            initial=battle.Enemies.ToDictionary(e=>e.TargetId,e=>new EnemyCombatSnapshot(e));
            Events=events.AsReadOnly();
        }
        internal CombatEvent Add(CombatEventKind kind,CombatCause cause,string actor,string target,
            int before=0,int after=0,CombatHit hit=null,StatusEffectType? status=null,ElementReaction? reaction=null,
            ElementType oldElement=ElementType.None,ElementType newElement=ElementType.None,int? parent=null,string effectId=null,int requestedAmount=0,IEnumerable<string> cardInstances=null)
        {
            var entry=new CombatEvent(events.Count,Turn,ActionId,actor,target,CardId,kind,cause,before,after,hit,status,reaction,oldElement,newElement,parent,effectId,requestedAmount,cardInstances);
            events.Add(entry);
            return entry;
        }
        internal void Complete(IEnumerable<EnemyInstance> enemies)
        {
            var byTarget=events.ToLookup(e=>e.TargetId);
            EnemyResults=Array.AsReadOnly(enemies.Where(e=>byTarget.Contains(e.TargetId))
                .Select(e=>new EnemyEffectResult(e,initial[e.TargetId],byTarget[e.TargetId])).ToArray());
        }
    }
}

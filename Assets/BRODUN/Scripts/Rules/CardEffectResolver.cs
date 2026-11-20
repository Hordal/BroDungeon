using System;
using System.Collections.Generic;
using System.Linq;
namespace Brodun
{
    public sealed class CardResult
    {
        public string CardId { get; }
        public int Damage { get; internal set; }
        public int Blocked { get; internal set; }
        public bool ConditionalTriggered { get; internal set; } // Effect trace for presentation, not a second calculation.
        private readonly List<DamageResult> hits=new List<DamageResult>();
        public IReadOnlyList<DamageResult> Hits => hits.AsReadOnly();
        internal void RecordHit(DamageResult hit) { hits.Add(hit); }
        public CardResult(string id) { CardId=id; }
    }
    internal sealed class EffectContext
    {
        public readonly BattleRuntime Battle;
        public readonly ElementType Previous;
        public readonly CardResult Result;
        public EffectContext(BattleRuntime battle,CardDefinition card)
        { Battle=battle; Previous=battle.Enemy.Element; Result=new CardResult(card.Id); }
        public void Hit(int amount)
        {
            int hp=Battle.Enemy.CurrentHP,block=Battle.Enemy.Block;
            var hit=DamageCalculator.Hit(amount,ref hp,ref block);
            Battle.Enemy.CurrentHP=hp; Battle.Enemy.Block=block;
            Result.Damage+=hit.HPDamage; Result.Blocked+=hit.Blocked;
            Result.RecordHit(hit);
        }
    }
    internal interface ICardEffectHandler { void Apply(EffectContext context,CardEffect effect); }
    internal sealed class DamageEffectHandler : ICardEffectHandler
    {
        public void Apply(EffectContext c,CardEffect e)
        { for(int i=0;i<e.Hits;i++)c.Hit(e.Amount+c.Battle.Strength); }
    }
    internal sealed class ConditionalEffectHandler : ICardEffectHandler
    {
        public void Apply(EffectContext c,CardEffect e)
        {
            if(ElementRules.Matches(e.Condition,c.Battle.Block,c.Battle.Enemy.Status.Poison,c.Previous))
            {c.Result.ConditionalTriggered=true;c.Hit(e.Amount);} // Deliberately NO Strength on conditional bonus (web parity).
        }
    }
    internal sealed class StatusEffectHandler : ICardEffectHandler
    {
        public void Apply(EffectContext c,CardEffect e)
        {
            switch(e.Type)
            {
                case EffectType.Poison:c.Battle.Enemy.Status.AddPoison(e.Amount);break;
                case EffectType.Weak:c.Battle.Enemy.Status.ApplyWeak(e.Amount);break;
                case EffectType.Strength:c.Battle.Strength+=e.Amount;break;
            }
        }
    }
    internal sealed class ResourceEffectHandler : ICardEffectHandler
    {
        public void Apply(EffectContext c,CardEffect e)
        {
            switch(e.Type)
            {
                case EffectType.Block:c.Battle.Block+=e.Amount;break;
                case EffectType.Heal:c.Battle.Run.Heal(e.Amount);break;
                case EffectType.Draw:c.Battle.Deck.Draw(e.Amount);break;
                case EffectType.Energy:c.Battle.Energy=Math.Min(c.Battle.Data.Config.EnergyCap,c.Battle.Energy+e.Amount);break;
                case EffectType.Exhaust:break; // Already moved by DeckRuntime.Use, before effects.
            }
        }
    }
    public static class CardEffectResolver
    {
        private static readonly IReadOnlyDictionary<EffectType,ICardEffectHandler> Handlers=CreateHandlers();
        private static IReadOnlyDictionary<EffectType,ICardEffectHandler> CreateHandlers()
        {
            var status=new StatusEffectHandler(); var resources=new ResourceEffectHandler();
            return new Dictionary<EffectType,ICardEffectHandler> {
                {EffectType.Damage,new DamageEffectHandler()},{EffectType.ConditionalDamage,new ConditionalEffectHandler()},
                {EffectType.Poison,status},{EffectType.Weak,status},{EffectType.Strength,status},
                {EffectType.Block,resources},{EffectType.Heal,resources},{EffectType.Draw,resources},
                {EffectType.Energy,resources},{EffectType.Exhaust,resources}
            };
        }
        // Low-level resolution deliberately independent of UI and hand validation.
        public static CardResult Resolve(BattleRuntime battle,CardDefinition card)
        {
            var context=new EffectContext(battle,card);
            foreach(var effect in card.Effects) Handlers[effect.Type].Apply(context,effect);
            if(card.Effects.Any(e=>e.Type==EffectType.Damage||e.Type==EffectType.Poison||e.Type==EffectType.Weak))
                battle.Enemy.Element=card.Element;
            return context.Result;
        }
    }
}

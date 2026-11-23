using System;
using System.Linq;
namespace Brodun.Data
{
    // Editor import DTOs only. Runtime uses detached immutable definitions, never JavaScript files.
    [Serializable] public sealed class WebSnapshot
    {
        public int schema; public string sourceCommit;
        public ConfigRecord config;
        public CardRecord[] cards; public CharacterRecord[] characters; public EnemyRecord[] enemies;
        public ElementRecord[] elements; public FixtureRecord[] fixtures;
    }
    [Serializable] public sealed class ConfigRecord
    {
        public int max_hp, max_cost, energy_cap, hand_size, gold_reward, weak_reduction, normal_hp_step;
        public EventRecord @event;
        public BattleConfig ToDefinition(int? baseEnergy=null,int? energyCap=null,int? handSize=null) =>
            new BattleConfig(handSize??hand_size,energyCap??energy_cap,weak_reduction,normal_hp_step,@event.heal,@event.gold,baseEnergy);
    }
    [Serializable] public sealed class EventRecord { public int heal, gold; }
    [Serializable] public sealed class EffectRecord
    {
        public string type, condition, targets; public int amount, hits;
        public string value_source,penetration;
        public int rule_version,numerator=1,denominator=1,maximum=int.MaxValue,threshold=1,turn_limit=int.MaxValue,attacker_limit=int.MaxValue,draw_on_counter,fallback_amount;
        public bool persistent;
        public CardEffect ToDefinition() => new CardEffect(Ids.Parse<EffectType>(type),amount,hits,
            string.IsNullOrEmpty(condition)?ConditionType.None:Ids.Parse<ConditionType>(condition),
            targets:string.IsNullOrEmpty(targets)?EffectTargetScope.CardTargets:Ids.Parse<EffectTargetScope>(targets),
            rule:rule_version==0?null:new EffectRule(string.IsNullOrEmpty(value_source)?EffectValueSource.Fixed:Ids.Parse<EffectValueSource>(value_source),
                numerator,denominator,maximum,threshold,turn_limit,attacker_limit,draw_on_counter,persistent,fallback_amount,string.IsNullOrEmpty(penetration)?ArmorPenetration.None:Ids.Parse<ArmorPenetration>(penetration)));
    }
    [Serializable] public sealed class CardRecord
    {
        public string id, name, description, element; public int cost; public EffectRecord[] effects;
        public string owner;
        public bool starter_only;
        public bool catalog_only;
        public string rarity,build,target_scope,presentation_id,element_application;
        public CardDefinition ToDefinition() => new CardDefinition(id,name,description,cost,Ids.Parse<ElementType>(element),
            effects.Select(x=>x.ToDefinition()),targetScope:string.IsNullOrEmpty(target_scope)?CardTargetScope.Single:Ids.Parse<CardTargetScope>(target_scope),
            elementApplication:!string.IsNullOrEmpty(element_application)?Ids.Parse<ElementApplication>(element_application):
                starter_only||catalog_only?ElementApplication.None:ElementApplication.Legacy,
            owner:string.IsNullOrEmpty(owner)?null:Ids.Parse<CharacterId>(owner),starterOnly:starter_only,
            rarity:string.IsNullOrEmpty(rarity)?CardRarity.Unspecified:Ids.Parse<CardRarity>(rarity),
            build:string.IsNullOrEmpty(build)?CardBuild.None:Ids.Parse<CardBuild>(build),
            catalogOnly:catalog_only,presentationId:presentation_id);
    }
    [Serializable] public sealed class CharacterRecord
    {
        public string id, name, desc; public int maxHP, energy; public string[] starter;
        public CharacterDefinition ToDefinition(string[] deckOverride=null) =>
            new CharacterDefinition(Ids.Parse<CharacterId>(id),name,desc,maxHP,energy,deckOverride??starter,
                deckOverride?.Intersect(starter));
    }
    [Serializable] public sealed class IntentRecord
    {
        public string type; public int amount, hits;
        public IntentData ToDefinition(int attackBonus = 0)
        {
            var intent = Ids.Parse<IntentType>(type);
            return new IntentData(intent, intent == IntentType.Attack ? amount + attackBonus : amount, hits);
        }
    }
    [Serializable] public sealed class EnemyRecord
    {
        public string id, name, rank, asset_id; public int area_index, hp, area_hp_step, display_height;
        public bool legacy;
        public IntentRecord[] patterns;
        public EnemyDefinition ToDefinition(int attackBonus = 0, int healthBonus = 0) => new EnemyDefinition(id, name, Ids.Parse<EnemyType>(rank), area_index, hp + healthBonus, area_hp_step, asset_id, patterns.Select(x => x.ToDefinition(attackBonus)), legacy);
    }
    [Serializable] public sealed class ElementRecord { public string id, name; }
    [Serializable] public sealed class FixtureRecord { public long seed; public string[] hand, reward; public NodeRecord[] map; }
    [Serializable] public sealed class NodeRecord
    {
        public string id, room_type, encounter_type, event_kind;
        public int column, floor_index; public double x, y; public string[] next_room_ids;
    }
    public static class Ids
    {
        public static T Parse<T>(string value) where T : struct =>
            (T)Enum.Parse(typeof(T), value.Replace("_", ""), true);
    }
}

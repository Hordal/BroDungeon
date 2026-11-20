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
        public BattleConfig ToDefinition() => new BattleConfig(hand_size, energy_cap, weak_reduction, normal_hp_step, @event.heal, @event.gold);
    }
    [Serializable] public sealed class EventRecord { public int heal, gold; }
    [Serializable] public sealed class EffectRecord
    {
        public string type, condition; public int amount, hits;
        public CardEffect ToDefinition() => new CardEffect(Ids.Parse<EffectType>(type), amount, hits, string.IsNullOrEmpty(condition) ? ConditionType.None : Ids.Parse<ConditionType>(condition));
    }
    [Serializable] public sealed class CardRecord
    {
        public string id, name, description, element; public int cost; public EffectRecord[] effects;
        public CardDefinition ToDefinition() => new CardDefinition(id, name, description, cost, Ids.Parse<ElementType>(element), effects.Select(x => x.ToDefinition()));
    }
    [Serializable] public sealed class CharacterRecord
    {
        public string id, name, desc; public int maxHP, energy; public string[] starter;
        public CharacterDefinition ToDefinition() => new CharacterDefinition(Ids.Parse<CharacterId>(id), name, desc, maxHP, energy, starter);
    }
    [Serializable] public sealed class IntentRecord
    {
        public string type; public int amount, hits;
        public IntentData ToDefinition() => new IntentData(Ids.Parse<IntentType>(type), amount, hits);
    }
    [Serializable] public sealed class EnemyRecord
    {
        public string id, name, rank, asset_id; public int area_index, hp, area_hp_step, display_height;
        public bool legacy;
        public IntentRecord[] patterns;
        public EnemyDefinition ToDefinition() => new EnemyDefinition(id, name, Ids.Parse<EnemyType>(rank), area_index, hp, area_hp_step, asset_id, patterns.Select(x => x.ToDefinition()), legacy);
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

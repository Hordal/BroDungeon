using System;
using UnityEngine;
using UnityEngine.Events;

namespace DeckbuilderMonsterCombat
{
    [Serializable] public sealed class MonsterRecord
    {
        public string id, floor, tier, nameKo, appearanceKo, combatRoleKo, spritePath;
        public string attackStyle, idleStyle, skillNameKo;
        public float variation = 1f;
        public int index;
    }
    [Serializable] public sealed class MonsterCatalog { public MonsterRecord[] monsters; }

    [RequireComponent(typeof(Animator)), RequireComponent(typeof(MonsterCombatFx))]
    public sealed class MonsterCombatActor : MonoBehaviour
    {
        public string monsterId, displayName, skillName, attackStyle, element;
        public float bodyHeight = 2.4f;
        public UnityEvent onAttackImpact = new UnityEvent();
        public UnityEvent onSkillImpact = new UnityEvent();
        public UnityEvent onDeathFinished = new UnityEvent();
        private Animator animator;
        private MonsterCombatFx fx;
        private bool dead;
        private static readonly string[] Triggers = { "Attack", "Skill", "Hit", "Block", "Death", "Spawn" };

        private void Awake() { animator = GetComponent<Animator>(); fx = GetComponent<MonsterCombatFx>(); }
        private void Play(string trigger)
        {
            if (dead && trigger != "Spawn") return;
            if (animator == null) animator = GetComponent<Animator>();
            foreach (string t in Triggers) animator.ResetTrigger(t);
            animator.SetTrigger(trigger);
        }
        public void PlayAttack() { Play("Attack"); }
        public void PlaySkill() { Play("Skill"); }
        public void PlayHit() { Play("Hit"); }
        public void PlayBlock() { Play("Block"); }
        public void PlayDeath() { if (dead) return; Play("Death"); dead = true; }
        public void PlaySpawn() { dead = false; Play("Spawn"); }

        // Animation events are presentation hooks. Connect actual damage in your game.
        public void OnAttackImpact()
        {
            if (fx == null) fx = GetComponent<MonsterCombatFx>();
            fx.PlayEffect(attackStyle, element, bodyHeight, false);
            onAttackImpact.Invoke();
        }
        public void OnSkillImpact()
        {
            if (fx == null) fx = GetComponent<MonsterCombatFx>();
            fx.PlayEffect(attackStyle, element, bodyHeight, true);
            onSkillImpact.Invoke();
        }
        public void OnDeathFinished() { onDeathFinished.Invoke(); }
        public bool IsDead { get { return dead; } }
    }
}

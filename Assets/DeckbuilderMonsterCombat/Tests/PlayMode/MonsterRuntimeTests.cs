#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace DeckbuilderMonsterCombat.Tests.PlayMode
{
    public sealed class MonsterRuntimeTests
    {
        private GameObject instance;
        [UnityTearDown] public IEnumerator Cleanup(){if(instance!=null)Object.Destroy(instance);yield return null;}
        [UnityTest] public IEnumerator FireBossAttackSkillDeathAndRespawn(){yield return Exercise("fire_crowned_dragon");}
        [UnityTest] public IEnumerator FrostBossAttackSkillDeathAndRespawn(){yield return Exercise("frost_funeral_queen");}
        [UnityTest] public IEnumerator VoidBossAttackSkillDeathAndRespawn(){yield return Exercise("void_eclipse_prellate");}
        private IEnumerator Exercise(string id)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DeckbuilderMonsterCombat/Prefabs/"+id+".prefab");
            instance=Object.Instantiate(prefab);yield return null;
            var actor=instance.GetComponent<MonsterCombatActor>();
            var image=instance.transform.Find("Visual").GetComponent<SpriteRenderer>();
            int attacks=0,skills=0,deaths=0;
            actor.onAttackImpact.AddListener(()=>attacks++);actor.onSkillImpact.AddListener(()=>skills++);actor.onDeathFinished.AddListener(()=>deaths++);
            actor.PlayAttack();yield return new WaitForSecondsRealtime(1.8f);Assert.That(attacks,Is.EqualTo(1));
            actor.PlaySkill();yield return new WaitForSecondsRealtime(2.7f);Assert.That(skills,Is.EqualTo(1));
            actor.PlayHit();yield return new WaitForSecondsRealtime(.7f);Assert.That(image.color.a,Is.EqualTo(1).Within(.01));
            actor.PlayBlock();yield return new WaitForSecondsRealtime(.9f);Assert.That(image.color.a,Is.EqualTo(1).Within(.01));
            actor.PlayDeath();yield return new WaitForSecondsRealtime(1.5f);
            Assert.That(actor.IsDead,Is.True);Assert.That(image.color.a,Is.LessThan(.02));Assert.That(deaths,Is.EqualTo(1));
            actor.PlayAttack();yield return new WaitForSecondsRealtime(.2f);Assert.That(attacks,Is.EqualTo(1),"Dead monsters must not attack");
            actor.PlaySpawn();yield return new WaitForSecondsRealtime(1.1f);
            Assert.That(actor.IsDead,Is.False);Assert.That(image.color.a,Is.GreaterThan(.98));
            Assert.That(instance.transform.position,Is.EqualTo(Vector3.zero));
        }
    }
}
#endif

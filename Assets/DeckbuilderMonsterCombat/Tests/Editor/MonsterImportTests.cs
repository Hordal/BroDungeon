using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeckbuilderMonsterCombat.Tests
{
    public sealed class MonsterImportTests
    {
        private const string Root="Assets/DeckbuilderMonsterCombat";
        private static IEnumerable<string> Ids()
        {
            return JsonUtility.FromJson<MonsterCatalog>(File.ReadAllText(Root+"/Data/monsters.json")).monsters.Select(m=>m.id);
        }
        [TestCaseSource(nameof(Ids))]
        public void ImportedMonsterHasWorkingRigClipsAndImpactHooks(string id)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/"+id+".prefab");
            Assert.That(prefab,Is.Not.Null,id);
            var instance=Object.Instantiate(prefab);
            try
            {
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(instance),Is.Zero,id);
                Assert.That(instance.GetComponent<Animator>().runtimeAnimatorController,Is.Not.Null);
                Assert.That(instance.GetComponent<MonsterCombatActor>().monsterId,Is.EqualTo(id));
                var visual=instance.transform.Find("Visual");
                Assert.That(visual.GetComponent<SpriteRenderer>().sprite,Is.Not.Null);
                foreach(string motion in new[]{"Idle","Attack","Skill","Hit","Block","Death","Spawn"})
                {
                    var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Animations/"+id+"/"+id+"_"+motion+".anim");
                    Assert.That(clip,Is.Not.Null,motion);Assert.That(clip.length,Is.GreaterThan(0));
                    var bindings=AnimationUtility.GetCurveBindings(clip);
                    Assert.That(bindings.All(b=>b.path=="Visual"),Is.True,"Root gameplay coordinates must never be animated");
                    clip.SampleAnimation(instance,clip.length);
                    Assert.That(instance.transform.position,Is.EqualTo(Vector3.zero));
                    Assert.That(visual.GetComponent<SpriteRenderer>().color.a,Is.EqualTo(motion=="Death"?0:1).Within(.01));
                    if(motion=="Attack" || motion=="Skill")
                    {
                        var hit=AnimationUtility.GetAnimationEvents(clip).Single();
                        Assert.That(hit.functionName,Is.EqualTo(motion=="Attack"?"OnAttackImpact":"OnSkillImpact"));
                        Assert.That(hit.time,Is.GreaterThan(0).And.LessThan(clip.length));
                    }
                }
            }
            finally{Object.DestroyImmediate(instance);}
        }
        [Test]
        public void DemoReferencesAll57DistinctPrefabs()
        {
            var scene=EditorSceneManager.OpenScene(Root+"/Scenes/MonsterCombatDemo.unity",OpenSceneMode.Additive);
            try
            {
                var demo=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MonsterCombatDemo>(true)).Single();
                Assert.That(demo.monsters.Length,Is.EqualTo(57));
                Assert.That(demo.monsters.All(p=>p!=null),Is.True);
                Assert.That(demo.monsters.Distinct().Count(),Is.EqualTo(57));
            }
            finally{EditorSceneManager.CloseScene(scene,true);}
        }
    }
}

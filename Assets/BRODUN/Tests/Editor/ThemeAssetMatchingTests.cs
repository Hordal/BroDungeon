using System.Linq;
using Brodun.Data;
using Brodun.Editor;
using Brodun.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Brodun.Tests
{
    public sealed class ThemeAssetMatchingTests
    {
        [Test]
        public void EverySpawnableMonsterHasAnImageAndAllMotionSlots()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<GameCatalogData>(DataAssetBuilder.CatalogPath);
            var data=DungeonThemes.WithThemeEnemies(catalog.ToDefinitions());
            var art=Resources.Load<BattlePresentationAssets>("BrodunBattle");
            foreach(var theme in DungeonThemes.All)
            {
                Assert.NotNull(BattleBackgrounds.Resolve(theme.Id,false,null));
                Assert.NotNull(BattleBackgrounds.Resolve(theme.Id,true,null));
                foreach(var id in theme.EnemyIds)
                {
                    var visual=art.Enemy(data.Enemy(id).Id);
                    foreach(var sequence in visual.animations)
                    {
                        Assert.Greater(sequence.frames.Length,0);
                        Assert.IsTrue(sequence.frames.All(f=>f!=null),id);
                    }
                }
            }
            Assert.AreEqual("BRODUN/Backgrounds/03_foundry_boss",BattleBackgrounds.ResourcePath("fire",true));
            Assert.AreEqual("BRODUN/Backgrounds/02_waterway_normal",BattleBackgrounds.ResourcePath("frost",false));
            Assert.AreEqual("BRODUN/Backgrounds/04_storm_boss",BattleBackgrounds.ResourcePath("void",true));
        }
    }
}

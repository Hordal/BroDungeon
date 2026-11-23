using System.Linq;
using Brodun.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Brodun.Tests
{
    public sealed class BattleBackgroundTests
    {
        [Test]
        public void TeamThemesResolveAllBackgroundsAndMonsterPrefabs()
        {
            var roster=Resources.Load<ThemeMonsterArt>("ThemeMonsterRoster");
            Assert.NotNull(roster);
            Assert.AreEqual(57,ThemeEnemyCatalog.Entries.Count);
            foreach(var theme in DungeonThemes.All)
            {
                Assert.AreEqual(theme.BackgroundKey+"_normal",BattleBackgrounds.Resolve(theme.Id,false,null).name);
                Assert.AreEqual(theme.BackgroundKey+"_boss",BattleBackgrounds.Resolve(theme.Id,true,null).name);
            }
            foreach(var entry in ThemeEnemyCatalog.Entries)
            {
                Assert.Contains(entry.Id,DungeonThemes.Find(entry.ThemeId).EnemyIds.ToArray());
                var prefab=roster.Find(entry.Id);
                Assert.NotNull(prefab,entry.Id);
                var renderer=prefab.GetComponentInChildren<SpriteRenderer>();
                Assert.AreEqual(entry.Id,renderer.sprite.name);
                var attack=prefab.GetComponent<Animator>().runtimeAnimatorController.animationClips
                    .Single(clip=>clip.name.EndsWith("_Attack"));
                Assert.IsTrue(attack.events.Any(evt=>evt.functionName=="OnAttackImpact"),entry.Id);
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void EachThemeImportsDistinctNormalAndBossSprites(int area)
        {
            var normal = BattleBackgrounds.Resolve(area, false, null);
            var boss = BattleBackgrounds.Resolve(area, true, null);
            Assert.NotNull(normal);
            Assert.NotNull(boss);
            Assert.AreNotSame(normal, boss);
            foreach (var sprite in new[] { normal, boss })
            {
                Assert.AreEqual(1672, sprite.texture.width);
                Assert.AreEqual(941, sprite.texture.height);
                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite)) as TextureImporter;
                Assert.NotNull(importer);
                Assert.AreEqual(TextureImporterType.Sprite, importer.textureType);
                Assert.AreEqual(SpriteImportMode.Single, importer.spriteImportMode);
                Assert.AreEqual(FilterMode.Bilinear, importer.filterMode);
                Assert.IsFalse(importer.mipmapEnabled);
            }
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(5)]
        public void UnsupportedAreaKeepsExistingBackground(int area)
        {
            var texture = new Texture2D(2, 2);
            var fallback = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero);
            try
            {
                Assert.IsNull(BattleBackgrounds.ResourcePath(area, false));
                Assert.AreSame(fallback, BattleBackgrounds.Resolve(area, false, fallback));
                Assert.AreSame(fallback, BattleBackgrounds.Resolve(area, true, fallback));
            }
            finally
            {
                Object.DestroyImmediate(fallback);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void UnknownAreaWithoutFallbackRemainsNull()
        {
            Assert.IsNull(BattleBackgrounds.Resolve(99, true, null));
            Assert.AreEqual("구역 99", BattleBackgrounds.ThemeName(99));
        }
    }
}

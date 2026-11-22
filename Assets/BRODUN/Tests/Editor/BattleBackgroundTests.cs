using Brodun.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Brodun.Tests
{
    public sealed class BattleBackgroundTests
    {
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

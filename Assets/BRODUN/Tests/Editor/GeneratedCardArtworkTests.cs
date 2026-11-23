using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Brodun.Tests
{
    public sealed class GeneratedCardArtworkTests : RulesTest
    {
        [Test]
        public void SavedIllustrationsLoadForTheirCardIdsWithoutReplacingExistingArt()
        {
            var preserved = new HashSet<string>
            {
                "W01", "W02", "W05", "M01", "M02", "M03", "M04", "P01", "P02"
            };
            var cards = Card70Catalog.Apply(Data);
            var sprites = Resources.LoadAll<Sprite>("Card70Illustrations");
            Assert.AreEqual(41, sprites.Length);
            var seen = new HashSet<Sprite>();
            foreach (var sprite in sprites)
            {
                var card = cards.Card(sprite.name);
                Assert.IsNotNull(sprite, card.Id + " / " + card.Name);
                Assert.AreSame(sprite, Resources.Load<Sprite>("Card70Illustrations/" + card.Id));
                Assert.IsTrue(seen.Add(sprite), card.Id + " shares an illustration");
                Assert.AreEqual(sprite.rect.width, sprite.rect.height, card.Id);
            }
            foreach (var id in preserved)
                Assert.IsNull(Resources.Load<Sprite>("Card70Illustrations/" + id),
                    id + " should keep its existing artwork");
        }
    }
}

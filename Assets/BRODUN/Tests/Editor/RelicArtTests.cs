using System.Collections.Generic;
using Brodun.Presentation;
using NUnit.Framework;
using UnityEngine;
namespace Brodun.Tests {
public sealed class RelicArtTests {
 [Test] public void EveryRelicLoadsItsOwnSprite(){
  var seen=new HashSet<Sprite>();
  foreach(var item in Item50Catalog.All){
   var sprite=RelicArt.Find(item.Id);
   Assert.IsNotNull(sprite,"Missing sprite for "+item.Id+" / "+item.Name);
   Assert.IsTrue(seen.Add(sprite),"Duplicate sprite for "+item.Id);
  }
  Assert.AreEqual(50,seen.Count);
 }
 [Test] public void UnknownRelicDoesNotLoadArt(){
  Assert.IsNull(RelicArt.Find(null));Assert.IsNull(RelicArt.Find("../I01"));
 }
}
}

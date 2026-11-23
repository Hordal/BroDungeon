using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace Brodun.Presentation {
public static class RelicArt {
 static readonly Dictionary<string,Sprite> cache=new Dictionary<string,Sprite>();
 public static Sprite Find(string id){
  if(string.IsNullOrEmpty(id)||Item50Catalog.Find(id)==null)return null;
  if(!cache.TryGetValue(id,out var sprite)){sprite=Resources.Load<Sprite>("BrodunRelics/"+id);if(sprite!=null)cache[id]=sprite;}
  return sprite;
 }
 public static void Add(Transform parent,string id,Vector2 min,Vector2 max){
  var sprite=Find(id);if(sprite==null)return;
  var image=BattleUi.Image(parent,"RelicArt_"+id,sprite,Color.white,min,max);
  image.preserveAspect=true;
 }
}
}

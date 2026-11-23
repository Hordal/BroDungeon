using System;
using System.Collections.Generic;
using System.Linq;
namespace Brodun {
public sealed class ItemDefinition {
 public string Id {get;} public string Name {get;} public string Description {get;}
 public int Tier {get;} public int BuyPrice {get;} public int SellPrice=>BuyPrice/2;
 public string ThemeId {get;} public string Synergy {get;} public int GrowthCap {get;}
 public ItemDefinition(string id,string name,string description,int tier,int price,string theme=null,string synergy="",int growthCap=0){Id=id;Name=name;Description=description;Tier=tier;BuyPrice=price;ThemeId=theme;Synergy=synergy;GrowthCap=growthCap;}
}
public sealed class RunItem {
 public string ItemId {get;} public int Progress {get;internal set;}
 internal RunItem(string id){ItemId=id;}
}
public sealed partial class RunState {
 readonly List<RunItem> items=new List<RunItem>();
 internal readonly HashSet<string> itemVictoryRooms=new HashSet<string>();
 public string LastItemReward {get;internal set;}
 public IReadOnlyList<RunItem> Items=>items.AsReadOnly();
 public bool HasItem(string id)=>items.Any(i=>i.ItemId==id);
 public int ItemProgress(string id)=>items.FirstOrDefault(i=>i.ItemId==id)?.Progress??0;
 public bool AddItem(string id){var item=Item50Catalog.Find(id);if(item==null||HasItem(id))return false;items.Add(new RunItem(id));return true;}
 internal bool RemoveItem(string id){var item=items.FirstOrDefault(i=>i.ItemId==id);return item!=null&&items.Remove(item);}
 internal void GrowItem(string id,int amount=1){var item=items.FirstOrDefault(i=>i.ItemId==id);if(item!=null)item.Progress=Math.Min(Item50Catalog.Find(id).GrowthCap,item.Progress+amount);}
}
}

using System;
using System.Collections.Generic;
using System.Linq;
namespace Brodun {
public sealed class ItemOffer {
 public string ItemId {get;} public int Price=>Item50Catalog.Find(ItemId).BuyPrice;
 public bool Purchased {get;internal set;}
 internal ItemOffer(string id){ItemId=id;}
}
public static class SanctuaryPrices {
 public static int CardBuy(CardDefinition c)=>c.Rarity==CardRarity.Basic?12:(c.Rarity==CardRarity.High?70:c.Rarity==CardRarity.Mid?40:20)+Math.Max(0,c.Cost)*3+(c.Type==CardType.Power?5:0);
 public static int CardSell(CardDefinition c,int upgrade=0)=>(c.Rarity==CardRarity.Basic?3:CardBuy(c)/3)+(upgrade>0?5:0);
}
public sealed partial class RoomRuntime {
 public bool IsSanctuary=>Node.Type==RoomType.Event&&(Node.Event==EventKind.Shop||Node.Event==EventKind.Altar);
 public const int MinimumSaleDeck=8,CardSalesPerVisit=2;
 internal void PopulateItemStock(RoomVisit visit){
  var rng=new SeededRandom(EncounterPool.NodeSeed(Run.Seed,Node.Id,0x17E440u));
  var eligible=Item50Catalog.Available(Run.ThemeId).Where(i=>!Run.HasItem(i.Id)).ToArray();
  var themed=Shuffling.Shuffle(eligible.Where(i=>i.ThemeId==Run.ThemeId),rng).Take(1).ToList();
  var ordinary=Shuffling.Shuffle(eligible.Where(i=>i.ThemeId==null),rng).Take(4-themed.Count);
  var selected=themed.Concat(ordinary).ToList();
  selected.AddRange(Shuffling.Shuffle(eligible.Where(i=>selected.All(s=>s.Id!=i.Id)),rng).Take(4-selected.Count).ToArray());
  visit.ItemOffers=selected.Select(i=>new ItemOffer(i.Id)).ToList().AsReadOnly();
 }
 public bool CanBuyItem(int index)=>CanInteract&&IsSanctuary&&index>=0&&index<Visit.ItemOffers.Count&&!Visit.ItemOffers[index].Purchased&&!Run.HasItem(Visit.ItemOffers[index].ItemId)&&Run.Gold>=Visit.ItemOffers[index].Price;
 public bool BuyItem(int index){if(!CanBuyItem(index))return false;var offer=Visit.ItemOffers[index];if(!Run.AddItem(offer.ItemId))return false;Run.SpendGold(offer.Price);offer.Purchased=true;Visit.Feedback=Item50Catalog.Find(offer.ItemId).Name+" 획득 / −"+offer.Price+" GOLD";return true;}
 public int CardSalePrice(string id){var card=Run.Deck.FirstOrDefault(c=>c.InstanceId==id);return card==null?0:SanctuaryPrices.CardSell(Data.Card(card.CardId),card.UpgradeLevel);}
 public bool CanSellCard(string id)=>CanInteract&&IsSanctuary&&Visit.CardsSold<CardSalesPerVisit&&Run.Deck.Count>MinimumSaleDeck&&Run.Deck.Any(c=>c.InstanceId==id)&&(long)Run.Gold+CardSalePrice(id)<=int.MaxValue;
 public bool SellCard(string id){if(!CanSellCard(id))return false;var card=Run.Deck.First(c=>c.InstanceId==id);int price=CardSalePrice(id);Run.RemoveCard(card.InstanceId);Run.AddGold(price);Visit.CardsSold++;Visit.Feedback=Data.Card(card.CardId).Name+" 판매 / +"+price+" GOLD";return true;}
 public bool CanSellItem(string id)=>CanInteract&&IsSanctuary&&Run.HasItem(id)&&(long)Run.Gold+Item50Catalog.Find(id).SellPrice<=int.MaxValue;
 public bool SellItem(string id){if(!CanSellItem(id))return false;var item=Item50Catalog.Find(id);Run.RemoveItem(id);Run.AddGold(item.SellPrice);Visit.Feedback=item.Name+" 판매 / +"+item.SellPrice+" GOLD"+(item.GrowthCap>0?" · 누적 성장도 소멸":"");return true;}
}
public sealed partial class RoomVisit {
 public IReadOnlyList<ItemOffer> ItemOffers {get;internal set;}=Array.Empty<ItemOffer>();
 public int CardsSold {get;internal set;}
}
}

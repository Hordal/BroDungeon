using System;
using System.Collections.Generic;
using System.Linq;
namespace Brodun
{
    // New room tuning only. Combat and card definitions remain untouched.
    public sealed class RoomRules
    {
        public int ShopPrice { get; }
        public int ShopStock { get; }
        public int SacrificeHP { get; }
        public int HealPrice { get; }
        public int PuzzleDamage { get; }
        public RoomRules(int shopPrice=30,int stock=3,int sacrificeHP=6,int healPrice=10,int puzzleDamage=6)
        {
            if(shopPrice<0||stock<1||sacrificeHP<1||healPrice<0||puzzleDamage<1)throw new ArgumentOutOfRangeException(nameof(shopPrice));
            ShopPrice=shopPrice;ShopStock=stock;SacrificeHP=sacrificeHP;HealPrice=healPrice;PuzzleDamage=puzzleDamage;
        }
    }
    public sealed class ShopOffer
    {
        public string CardId { get; }
        public int Price { get; }
        public bool Purchased { get; internal set; }
        internal ShopOffer(string id,int price){CardId=id;Price=price;}
    }
    // Serializable-shaped domain state: only values/IDs, never Unity Scene objects.
    public sealed partial class RoomVisit
    {
        public string NodeId { get; }
        public bool Resolved { get; internal set; }
        public bool ChoiceMade { get; internal set; }
        public bool Upgraded {get;internal set;} public bool Removed {get;internal set;}
        public string Feedback { get; internal set; }
        public bool PuzzleTreasure { get; }
        public IReadOnlyList<ShopOffer> Offers { get; }
        internal RoomVisit(string id,bool treasure,IEnumerable<ShopOffer> offers)
        {NodeId=id;PuzzleTreasure=treasure;Offers=Array.AsReadOnly(offers.ToArray());}
    }
    public enum RoomChoice { Rest, Sacrifice, PaidHeal, Explore, Leave }
    public sealed partial class RoomRuntime
    {
        public RunState Run { get; }
        public RoomNode Node { get; }
        public RoomVisit Visit { get; }
        public GameDefinitions Data { get; }
        public RoomRules Rules { get; }
        public bool CanInteract => Run.Status==RunStatus.InProgress && Run.CurrentHP>0 && Run.ActiveRoom==Node.Id && !Visit.Resolved;
        public RoomRuntime(GameDefinitions data,RunState run,RoomNode node,RoomRules rules=null)
        {
            if(node==null||run.ActiveRoom!=node.Id||(node.Type!=RoomType.Event&&node.Type!=RoomType.Puzzle))throw new InvalidOperationException("Enter a content room first.");
            Data=data;Run=run;Node=node;Rules=rules??new RoomRules();
            if(!run.visits.TryGetValue(node.Id,out var visit))
            {
                var rng=new SeededRandom(EncounterPool.NodeSeed(run.Seed,node.Id,0x82D311u));
                var candidates=Shuffling.Shuffle(data.Cards.Where(c=>c.IsReworked?Card70Rewards.CanAcquire(data,run,c.Id):!data.Character(run.SelectedCharacter).StartingDeck.Contains(c.Id)),rng);
                visit=new RoomVisit(node.Id,rng.Next()<.65,IsSanctuary?
                    candidates.Take(Rules.ShopStock).Select(c=>new ShopOffer(c.Id,c.IsReworked?SanctuaryPrices.CardBuy(c):Rules.ShopPrice)):Enumerable.Empty<ShopOffer>());
                if(IsSanctuary)PopulateItemStock(visit);
                run.visits.Add(node.Id,visit);
            }
            Visit=visit;
        }
        public bool CanBuy(int index)=>CanInteract && IsSanctuary && index>=0 && index<Visit.Offers.Count &&
            !Visit.Offers[index].Purchased && Run.Gold>=Visit.Offers[index].Price && Card70Rewards.CanAcquire(Data,Run,Visit.Offers[index].CardId);
        public bool Buy(int index)
        {
            if(!CanBuy(index))return false;
            var offer=Visit.Offers[index];Data.Card(offer.CardId);
            if(!Run.SpendGold(offer.Price))return false;
            Run.AddCard(offer.CardId);offer.Purchased=true;Visit.Feedback=Data.Card(offer.CardId).Name+" 획득 · GOLD −"+offer.Price;return true;
        }
        public bool Upgrade(string id) {
            bool rest=Node.Event==EventKind.RestSite,shop=IsSanctuary;
            var card=Run.Deck.FirstOrDefault(c=>c.InstanceId==id);
            if(!CanInteract||Visit.Upgraded||!(rest||shop)||rest&&Visit.ChoiceMade||card==null||card.UpgradeLevel>0||!Data.Card(card.CardId).IsReworked||shop&&Run.Gold<30)return false;
            if(shop)Run.SpendGold(30);Run.UpgradeCard(id);Visit.Upgraded=true;if(rest)Visit.ChoiceMade=true;Visit.Feedback=Data.Card(card.CardId).Name+" 강화 완료";return true;
        }
        public bool Remove(string id) {
            if(!CanInteract||!IsSanctuary||Visit.Removed||Run.Gold<40||Run.Deck.Count<=1||!Run.Deck.Any(c=>c.InstanceId==id))return false;
            Run.SpendGold(40);Run.RemoveCard(id);Visit.Removed=true;Visit.Feedback="카드 제거 완료";return true;
        }
        public bool Choose(RoomChoice choice)
        {
            if(!CanInteract||Visit.ChoiceMade)return false;
            bool rest=Node.Type==RoomType.Event&&Node.Event==EventKind.RestSite;
            bool altar=Node.Type==RoomType.Event&&Node.Event==EventKind.Altar;
            bool explore=Node.Type==RoomType.Puzzle||(Node.Type==RoomType.Event&&Node.Event==EventKind.None);
            if(choice==RoomChoice.Rest&&rest)
            {int before=Run.CurrentHP;Run.Heal(Data.Config.EventHeal);Visit.Feedback="휴식 / HP +"+(Run.CurrentHP-before);}
            else if(choice==RoomChoice.Sacrifice&&altar)
            {
                if(Run.CurrentHP<=Rules.SacrificeHP||(long)Run.Gold+Data.Config.EventGold>int.MaxValue)return false;
                Run.CurrentHP-=Rules.SacrificeHP;Run.AddGold(Data.Config.EventGold);Visit.Feedback="HP −"+Rules.SacrificeHP+" / GOLD +"+Data.Config.EventGold;
            }
            else if(choice==RoomChoice.PaidHeal&&altar)
            {
                if(!Run.SpendGold(Rules.HealPrice))return false;
                int before=Run.CurrentHP;Run.Heal(Data.Config.EventHeal);Visit.Feedback="HP +"+(Run.CurrentHP-before)+" / GOLD −"+Rules.HealPrice;
            }
            else if(choice==RoomChoice.Explore&&explore)
            {
                if(Visit.PuzzleTreasure)
                {
                    if((long)Run.Gold+Data.Config.EventGold>int.MaxValue)return false;
                    Run.AddGold(Data.Config.EventGold);Visit.Feedback="숨겨진 주머니 / GOLD +"+Data.Config.EventGold;
                }
                else {Run.CurrentHP=Math.Max(0,Run.CurrentHP-Rules.PuzzleDamage);Visit.Feedback="함정 / HP −"+Rules.PuzzleDamage;}
            }
            else if(choice==RoomChoice.Leave){Visit.Feedback="지나갔습니다.";}
            else return false;
            Visit.ChoiceMade=true;
            if(Run.CurrentHP==0)Run.Status=RunStatus.Failed;
            return true;
        }
        public bool Finish()
        {
            if(!CanInteract || (!IsSanctuary&&!Visit.ChoiceMade))return false;
            Visit.Resolved=true;Run.results[Node.Id]=new RoomResult(RewardType.Skip,null,0,Node.Type==RoomType.Puzzle?"puzzle":Node.Event.ToString());
            return DungeonRuntime.Complete(Run);
        }
    }
}

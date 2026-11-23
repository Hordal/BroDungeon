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
        public int PuzzleDamage { get; }
        public RoomRules(int shopPrice=30,int stock=3,int puzzleDamage=6)
        {
            if(shopPrice<0||stock<1||puzzleDamage<1)throw new ArgumentOutOfRangeException(nameof(shopPrice));
            ShopPrice=shopPrice;ShopStock=stock;PuzzleDamage=puzzleDamage;
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
    public sealed class RoomVisit
    {
        public string NodeId { get; }
        public bool Resolved { get; internal set; }
        public bool ChoiceMade { get; internal set; }
        public string Feedback { get; internal set; }
        public bool PuzzleTreasure { get; }
        public IReadOnlyList<ShopOffer> Offers { get; }
        public IReadOnlyList<AltarTrade> AltarTrades { get; internal set; } = Array.Empty<AltarTrade>();
        public IReadOnlyList<string> PendingAltarCards { get; internal set; } = Array.Empty<string>();
        public string SwappedOutCardId { get; internal set; }
        public string SwappedInCardId { get; internal set; }
        internal RoomVisit(string id,bool treasure,IEnumerable<ShopOffer> offers)
        {NodeId=id;PuzzleTreasure=treasure;Offers=Array.AsReadOnly(offers.ToArray());}
    }
    public enum RoomChoice { Rest, Explore, Leave }
    public enum AltarTrade { GoldForHealth, DraftForHealth, RemoveForHealth, Coin, BlindDoubleDraft, BlindSwap }
    public enum CoinFace { Heads, Tails }
    public sealed class RoomRuntime
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
                var rng=new SeededRandom(EncounterPool.NodeSeed(run.AreaSeed,node.Id,0x82D311u));
                var candidates=Shuffling.Shuffle(data.Cards.Where(c=>c.IsReworked?Card70Rewards.CanAcquire(data,run,c.Id):!data.Character(run.SelectedCharacter).StartingDeck.Contains(c.Id)),rng);
                visit=new RoomVisit(node.Id,rng.Next()<.65,Node.Event==EventKind.Shop?
                    candidates.Take(Rules.ShopStock).Select(c=>new ShopOffer(c.Id,Rules.ShopPrice)):Enumerable.Empty<ShopOffer>());
                if(node.Event==EventKind.Altar)
                    visit.AltarTrades=Shuffling.Shuffle(Enum.GetValues(typeof(AltarTrade)).Cast<AltarTrade>(),
                        AltarRandom(0xA17A0001u)).Take(3).ToList().AsReadOnly();
                run.visits.Add(node.Id,visit);
            }
            Visit=visit;
        }
        public bool CanBuy(int index)=>CanInteract && Node.Event==EventKind.Shop && index>=0 && index<Visit.Offers.Count &&
            !Visit.Offers[index].Purchased && Run.Gold>=Visit.Offers[index].Price;
        public bool Buy(int index)
        {
            if(!CanBuy(index))return false;
            var offer=Visit.Offers[index];Data.Card(offer.CardId);
            if(!Run.SpendGold(offer.Price))return false;
            Run.AddCard(offer.CardId);offer.Purchased=true;Visit.Feedback=Data.Card(offer.CardId).Name+" 획득 · GOLD −"+offer.Price;return true;
        }
        public bool Choose(RoomChoice choice)
        {
            if(!CanInteract||Visit.ChoiceMade)return false;
            bool rest=Node.Type==RoomType.Event&&Node.Event==EventKind.RestSite;
            bool explore=Node.Type==RoomType.Puzzle||(Node.Type==RoomType.Event&&Node.Event==EventKind.None);
            if(choice==RoomChoice.Rest&&rest)
            {int before=Run.CurrentHP;Run.Heal(Data.Config.EventHeal);Visit.Feedback="휴식 / HP +"+(Run.CurrentHP-before);}
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
        public const int AltarGoldHealth=8, AltarDraftHealth=10, AltarRemoveHealth=8;
        public const int AltarGoldPayout=25, AltarCoinStake=10, AltarCoinPayout=25;
        private const int MinimumFinalDeck=1;
        private SeededRandom AltarRandom(uint salt)=>new SeededRandom(EncounterPool.NodeSeed(Run.AreaSeed,Node.Id,salt));

        public string AltarTradeUnavailable(AltarTrade trade)
        {
            if(!CanInteract||Visit.ChoiceMade||!Visit.AltarTrades.Contains(trade))return "거래할 수 없습니다.";
            switch(trade)
            {
                case AltarTrade.GoldForHealth:
                    if(Run.CurrentHP<=AltarGoldHealth)return "거래 후 HP가 1 이상 남아야 합니다.";
                    if((long)Run.Gold+AltarGoldPayout>int.MaxValue)return "골드 한도에 도달했습니다.";
                    break;
                case AltarTrade.DraftForHealth:
                    if(Run.CurrentHP<=AltarDraftHealth)return "거래 후 HP가 1 이상 남아야 합니다.";
                    if(Data.RewardCardsFor(Run.SelectedCharacter).Count<3)return "보상 카드가 부족합니다.";
                    break;
                case AltarTrade.RemoveForHealth:
                    if(Run.CurrentHP<=AltarRemoveHealth)return "거래 후 HP가 1 이상 남아야 합니다.";
                    if(Run.Deck.Count<=MinimumFinalDeck)return "덱에 카드가 1장 이상 남아야 합니다.";
                    break;
                case AltarTrade.Coin:
                    if(Run.Gold<AltarCoinStake)return "골드가 부족합니다.";
                    if((long)Run.Gold-AltarCoinStake+AltarCoinPayout>int.MaxValue)return "골드 한도에 도달했습니다.";
                    break;
                case AltarTrade.BlindDoubleDraft:
                    if(Run.Deck.Count<2)return "덱에 카드가 2장 이상 필요합니다.";
                    if(Data.RewardCardsFor(Run.SelectedCharacter).Count<3)return "보상 카드가 부족합니다.";
                    break;
                case AltarTrade.BlindSwap:
                    if(!SwapTargets().Any())return "교환할 카드나 받을 카드가 부족합니다.";
                    break;
            }
            return null;
        }

        private IReadOnlyList<string> AltarDraft(uint salt)=>
            RewardResolver.SampleCandidates(Data,Run.SelectedCharacter,EnemyType.Normal,AltarRandom(salt),3);

        public IReadOnlyList<string> PreviewAltarDraft() => AltarTradeUnavailable(AltarTrade.DraftForHealth)==null
            ?AltarDraft(0xA17A0002u):Array.Empty<string>();

        private IEnumerable<CardInstance> SwapTargets()
        {
            var pool=Data.RewardCardsFor(Run.SelectedCharacter);
            return Run.Deck.Where(card=>pool.Any(replacement=>replacement.Id!=card.CardId));
        }

        public bool TradeAtAltar(AltarTrade trade,string selected=null,CoinFace? guess=null)
        {
            if(AltarTradeUnavailable(trade)!=null)return false;
            switch(trade)
            {
                case AltarTrade.GoldForHealth:
                    Run.CurrentHP-=AltarGoldHealth;
                    Run.AddGold(AltarGoldPayout);
                    Visit.Feedback=$"HP −{AltarGoldHealth} / 골드 +{AltarGoldPayout}";
                    break;
                case AltarTrade.DraftForHealth:
                    if(!PreviewAltarDraft().Contains(selected))return false;
                    Run.AddCard(selected);
                    Run.CurrentHP-=AltarDraftHealth;
                    Visit.Feedback=$"{Data.Card(selected).Name} 획득 / HP −{AltarDraftHealth}";
                    break;
                case AltarTrade.RemoveForHealth:
                    var removal=Run.Deck.FirstOrDefault(card=>card.InstanceId==selected);
                    if(removal==null)return false;
                    Run.RemoveCard(removal);
                    Run.CurrentHP-=AltarRemoveHealth;
                    Visit.Feedback=$"{Data.Card(removal.CardId).Name} 제거 / HP −{AltarRemoveHealth}";
                    break;
                case AltarTrade.Coin:
                    if(guess!=CoinFace.Heads&&guess!=CoinFace.Tails)return false;
                    var result=AltarRandom(0xA17A0003u).Next()<.5?CoinFace.Heads:CoinFace.Tails;
                    Run.SpendGold(AltarCoinStake);
                    bool won=result==guess;
                    if(won)Run.AddGold(AltarCoinPayout);
                    Visit.Feedback=$"동전 결과는 {(result==CoinFace.Heads?"앞면":"뒷면")} · {(won?$"성공 / 순이익 +{AltarCoinPayout-AltarCoinStake}골드":$"실패 / −{AltarCoinStake}골드")}";
                    break;
                case AltarTrade.BlindDoubleDraft:
                    var candidates=AltarDraft(0xA17A0004u);
                    var losses=Shuffling.Shuffle(Run.Deck,AltarRandom(0xA17A0005u)).Take(2).ToArray();
                    foreach(var card in losses)Run.RemoveCard(card);
                    Visit.Feedback="제거한 카드 · "+string.Join(", ",losses.Select(card=>Data.Card(card.CardId).Name));
                    Visit.PendingAltarCards=candidates;
                    break;
                case AltarTrade.BlindSwap:
                    var rng=AltarRandom(0xA17A0006u);
                    var lost=Shuffling.Shuffle(SwapTargets(),rng).First();
                    var replacement=Shuffling.Shuffle(Data.RewardCardsFor(Run.SelectedCharacter)
                        .Where(card=>card.Id!=lost.CardId),rng).First();
                    Run.RemoveCard(lost);
                    Run.AddCard(replacement.Id);
                    Visit.SwappedOutCardId=lost.CardId;
                    Visit.SwappedInCardId=replacement.Id;
                    Visit.Feedback=$"{Data.Card(lost.CardId).Name} 제거 / {replacement.Name} 획득";
                    break;
                default:return false;
            }
            Visit.ChoiceMade=true;
            return true;
        }

        public bool ClaimAltarCard(string cardId)
        {
            if(!CanInteract||!Visit.PendingAltarCards.Contains(cardId))return false;
            Run.AddCard(cardId);
            Visit.Feedback+=" / "+Data.Card(cardId).Name+" 획득";
            Visit.PendingAltarCards=Array.Empty<string>();
            return true;
        }

        public bool Finish()
        {
            if(!CanInteract||Visit.PendingAltarCards.Count>0||
                (Node.Event!=EventKind.Shop&&Node.Event!=EventKind.Altar&&!Visit.ChoiceMade))return false;
            Visit.Resolved=true;Run.results[Node.Id]=new RoomResult(RewardType.Skip,null,0,Node.Type==RoomType.Puzzle?"puzzle":Node.Event.ToString());
            return DungeonRuntime.Complete(Run);
        }
    }
}

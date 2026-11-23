using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Brodun.Flow;
using UnityEngine;
using UnityEngine.UI;
namespace Brodun.Presentation
{
    // A shared noncombat screen. Offers/choices live in RunState, not in these buttons.
    public sealed partial class RoomView : MonoBehaviour
    {
        private GameFlow flow;
        private RoomRuntime room;
        private RectTransform root;
        private Text hud,feedback;
        private readonly List<StyledButton> buy=new List<StyledButton>(),choices=new List<StyledButton>();
        private readonly List<CardView> cards=new List<CardView>();
        private StyledButton leave;
        private RectTransform altarPanel;
        private AltarTrade? activeTrade;
        private string selectedCard;
        private CoinFace? guess;
        private bool revealingSwap;
        private bool IsAltar=>room.Node.Event==EventKind.Altar;
        private PresentationAssets Art=>flow.Assets;
        public void Build(GameFlow owner,RectTransform canvas)
        {
            flow=owner;room=flow.Session.Room;root=canvas;
            var backgrounds=Resources.Load<RoomPresentationAssets>("BrodunRooms");
            var bg=backgrounds==null?null:backgrounds.Background(room.Node);
            if(bg!=null){BattleUi.Image(root,"Room background",bg,Color.white,Vector2.zero,Vector2.one);BattleUi.Image(root,"Room veil",null,new Color(.01f,.02f,.03f,.65f),Vector2.zero,Vector2.one);}
            BattleUi.Panel(root,"Room panel",new Vector2(.07f,.035f),new Vector2(.93f,.97f));
            Label("RoomTitle",GameScreenView.RoomName(room.Node),48,BattleUi.Gold,.87f,.95f);
            hud=Label("RoomHUD","",28,BattleUi.Pale,.79f,.85f);
            feedback=Label("RoomFeedback","",27,BattleUi.Gold,.145f,.195f);
            if(room.IsSanctuary){BuildSanctuary();return;}
            bool shop=room.Node.Type==RoomType.Event&&room.Node.Event==EventKind.Shop;
            if(shop)BuildShop();else if(!IsAltar)BuildChoice();
            leave=BattleUi.Button(root,"RoomLeave","던전 맵으로",Art,new Vector2(.68f,.055f),new Vector2(.88f,.135f),()=>
            {if(!flow.Session.IsTransitioning)flow.ReturnFromRoom();});
            Refresh();
            if(IsAltar)BuildAltar();
        }
        private Text Label(string name,string text,int size,Color color,float min,float max)=>BattleUi.Text(root,name,text,Art.font,size,color,new Vector2(.10f,min),new Vector2(.90f,max));
        private void BuildShop()
        {
            Label("RoomBody","상품은 한 번만 구매할 수 있습니다. 구매하지 않고 떠나도 됩니다.",26,BattleUi.Pale,.71f,.77f);
            var art=Resources.Load<BattlePresentationAssets>("BrodunBattle");
            for(int i=0;i<room.Visit.Offers.Count;i++)
            {
                int index=i;var offer=room.Visit.Offers[i];float x=.28f+i*.22f;
                var card=Instantiate(art.cardPrefab,root);card.name="ShopCard_"+i;
                var rect=(RectTransform)card.transform;rect.anchorMin=rect.anchorMax=new Vector2(x,.46f);rect.anchoredPosition=Vector2.zero;rect.localScale=Vector3.one*1.1f;
                card.Bind(room.Data.Card(offer.CardId),"shop-"+i,art,room.Data,_=>Buy(index));cards.Add(card);
                buy.Add(BattleUi.Button(root,"ShopBuy_"+i,"GOLD "+offer.Price+" / 구매",Art,new Vector2(x-.085f,.21f),new Vector2(x+.085f,.285f),()=>Buy(index)));
            }
        }
        private void BuildChoice()
        {
            bool rest=room.Node.Event==EventKind.RestSite;
            string body=rest?$"HP {room.Data.Config.EventHeal} 회복 (최대 HP까지).":
                $"봉인된 문 뒤를 탐색할까요? 성공 시 골드 +{room.Data.Config.EventGold}, 함정에 걸리면 HP −{room.Rules.PuzzleDamage}.";
            Label("RoomBody",body,30,BattleUi.Pale,.57f,.73f);
            if(rest)AddChoice("RestChoice","휴식 / HP +"+room.Data.Config.EventHeal,RoomChoice.Rest,.24f,.53f);
            else AddChoice("ExploreChoice","문을 열고 탐색",RoomChoice.Explore,.24f,.53f);
            AddChoice("PassChoice","그냥 지나간다",RoomChoice.Leave,.57f,.79f);
        }

        private static string TradeLabel(AltarTrade trade)=>trade switch
        {
            AltarTrade.GoldForHealth=>$"HP {RoomRuntime.AltarGoldHealth}을 내고 골드 {RoomRuntime.AltarGoldPayout} 얻기",
            AltarTrade.DraftForHealth=>$"HP {RoomRuntime.AltarDraftHealth}을 내고 카드 3장 중 1장 얻기",
            AltarTrade.RemoveForHealth=>$"HP {RoomRuntime.AltarRemoveHealth}을 내고 덱에서 카드 1장 제거하기",
            AltarTrade.Coin=>$"골드 {RoomRuntime.AltarCoinStake}을 걸고 동전 앞·뒷면 맞히기",
            AltarTrade.BlindDoubleDraft=>"무작위 카드 2장을 제거하고 이후 공개되는 후보 중 1장 얻기",
            _=>"무작위 카드 1장을 제거하고 무작위 카드 1장 얻기"
        };

        private StyledButton AltarButton(Transform parent,string name,string label,float bottom,float top,Action action,
            float left=.04f,float right=.96f)
            =>BattleUi.Button(parent,name,label,Art,new Vector2(left,bottom),new Vector2(right,top),
                ()=>{if(!flow.Session.IsTransitioning)action();});

        private void AltarText(string message,float bottom,float top)
            =>BattleUi.Text(altarPanel,"Altar information",message,Art.font,25,BattleUi.Pale,
                new Vector2(.04f,bottom),new Vector2(.96f,top));

        private void BuildAltar(bool animateSwap=false)
        {
            if(altarPanel!=null){altarPanel.gameObject.SetActive(false);Destroy(altarPanel.gameObject);}
            altarPanel=BattleUi.Rect(root,"Altar trades",new Vector2(.13f,.21f),new Vector2(.87f,room.IsSanctuary?.68f:.77f));
            if(room.Visit.PendingAltarCards.Count>0)
            {
                AltarText("받을 카드 한 장을 선택하면 거래가 끝납니다.",.87f,1);
                BuildAltarCardList(room.Visit.PendingAltarCards.Select(id=>(id,id)),id=>
                {
                    if(room.ClaimAltarCard(id))OpenAltarTrade(null);
                });
                return;
            }
            if(room.Visit.ChoiceMade)
            {
                if(room.Visit.SwappedOutCardId!=null)BuildSwapResult(animateSwap);
                return;
            }
            if(!activeTrade.HasValue)
            {
                AltarText("이번 방문에서 거래를 하나 선택할 수 있습니다.",.85f,1);
                for(int i=0;i<room.Visit.AltarTrades.Count;i++)
                {
                    var trade=room.Visit.AltarTrades[i];
                    string reason=room.AltarTradeUnavailable(trade);
                    float top=.81f-i*.24f;
                    var button=AltarButton(altarPanel,"Altar_"+trade,
                        reason==null?TradeLabel(trade):TradeLabel(trade)+"\n"+reason,top-.19f,top,()=>OpenAltarTrade(trade));
                    button.interactable=reason==null;
                }
                return;
            }

            var offer=activeTrade.Value;
            AltarText(TradeLabel(offer),.86f,1);
            switch(offer)
            {
                case AltarTrade.DraftForHealth:
                    BuildAltarCardList(room.PreviewAltarDraft().Select(id=>(id,id)),SelectCard);
                    break;
                case AltarTrade.RemoveForHealth:
                    BuildAltarCardList(room.Run.Deck.Select(card=>(card.InstanceId,card.CardId)),SelectCard);
                    break;
                case AltarTrade.Coin:
                    AltarText($"앞면·뒷면 각각 50% / 성공 +{RoomRuntime.AltarCoinPayout-RoomRuntime.AltarCoinStake}골드 / 실패 −{RoomRuntime.AltarCoinStake}골드",.61f,.78f);
                    AltarButton(altarPanel,"AltarHeads",guess==CoinFace.Heads?"앞면 선택됨":"앞면",.32f,.52f,
                        ()=>{guess=CoinFace.Heads;BuildAltar();},.13f,.46f);
                    AltarButton(altarPanel,"AltarTails",guess==CoinFace.Tails?"뒷면 선택됨":"뒷면",.32f,.52f,
                        ()=>{guess=CoinFace.Tails;BuildAltar();},.54f,.87f);
                    break;
                case AltarTrade.BlindDoubleDraft:
                    AltarText("카드 2장이 영구 제거된 뒤에만 받을 카드 후보가 공개됩니다.",.4f,.72f);
                    break;
                case AltarTrade.BlindSwap:
                    AltarText("제거되는 카드와 새 카드는 거래를 확정한 뒤 공개됩니다.",.4f,.72f);
                    break;
            }
            AltarButton(altarPanel,"AltarBack","거래 목록",0,.12f,()=>OpenAltarTrade(null),.04f,.43f);
            var confirm=AltarButton(altarPanel,"AltarConfirm","거래 확정",0,.12f,()=>
            {
                if(room.TradeAtAltar(offer,selectedCard,guess))OpenAltarTrade(null,offer==AltarTrade.BlindSwap);
            },.57f,.96f);
            confirm.interactable=offer==AltarTrade.Coin?guess.HasValue:
                (offer!=AltarTrade.DraftForHealth&&offer!=AltarTrade.RemoveForHealth)||selectedCard!=null;
        }

        private void BuildAltarCardList(IEnumerable<(string Key,string CardId)> entries,Action<string> select)
        {
            var list=entries.ToArray();
            var viewport=BattleUi.Image(altarPanel,"Altar card choices",null,BattleUi.Ink,
                new Vector2(.04f,.17f),new Vector2(.96f,.84f));
            viewport.raycastTarget=true;
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport=viewport.rectTransform;
            scroll.horizontal=false;
            scroll.scrollSensitivity=45;
            scroll.movementType=ScrollRect.MovementType.Clamped;
            const float rowHeight=108;
            var content=BattleUi.Rect(viewport.transform,"Cards",new Vector2(0,1),Vector2.one);
            content.pivot=new Vector2(.5f,1);
            content.sizeDelta=new Vector2(0,list.Length*rowHeight);
            scroll.content=content;
            for(int i=0;i<list.Length;i++)
            {
                var entry=list[i];
                var card=room.Data.Card(entry.CardId);
                string label=$"{(selectedCard==entry.Key?"✓ ":"")}{card.Name}  ·  {card.Cost} AP\n{card.Description}";
                float top=1-(float)i/list.Length;
                var button=AltarButton(content,"AltarCard_"+i,label,top-.95f/list.Length,top,()=>select(entry.Key),.015f,.985f);
                var caption=button.GetComponentInChildren<Text>();
                caption.resizeTextForBestFit=true;
                caption.resizeTextMinSize=18;
                caption.resizeTextMaxSize=25;
            }
        }

        private void BuildSwapResult(bool animate)
        {
            var removed=SwapCard(room.Visit.SwappedOutCardId,"AltarRemovedCard","제거한 카드",.31f,new Color(.94f,.48f,.38f));
            var gained=SwapCard(room.Visit.SwappedInCardId,"AltarGainedCard","얻은 카드",.69f,BattleUi.Gold);
            BattleUi.Text(altarPanel,"Swap direction","→",Art.font,67,BattleUi.Gold,
                new Vector2(.45f,.31f),new Vector2(.55f,.63f));
            if(animate)StartCoroutine(RevealSwap(removed,gained));
            else removed.ink.alpha=.66f;
        }

        private CardView SwapCard(string id,string objectName,string caption,float x,Color tint)
        {
            BattleUi.Text(altarPanel,objectName+" label",caption,Art.font,30,tint,
                new Vector2(x-.15f,.84f),new Vector2(x+.15f,.98f));
            var art=Resources.Load<BattlePresentationAssets>("BrodunBattle");
            var card=Instantiate(art.cardPrefab,altarPanel,false);
            card.name=objectName;
            var rect=(RectTransform)card.transform;
            rect.anchorMin=rect.anchorMax=new Vector2(x,.46f);
            rect.anchoredPosition=Vector2.zero;
            card.Bind(room.Data.Card(id),objectName,art,room.Data,null);
            card.ink.blocksRaycasts=false;
            card.ink.interactable=false;
            return card;
        }

        private IEnumerator RevealSwap(CardView removed,CardView gained)
        {
            revealingSwap=true;
            Refresh();
            var loss=new Color(1,.41f,.30f);
            var outgoing=BattleVfx.CreateCardRelease(removed.visual,"Altar loss sparks");
            var incoming=BattleVfx.CreateCardRelease(gained.visual,"Altar gain sparks");
            incoming.gameObject.SetActive(false);
            for(float time=0;time<.78f;time+=Time.unscaledDeltaTime)
            {
                float fade=Mathf.SmoothStep(0,1,Mathf.Clamp01(time/.44f));
                float reveal=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.27f,.74f,time));
                float afterimage=Mathf.SmoothStep(0,.66f,Mathf.InverseLerp(.48f,.74f,time));
                removed.ink.alpha=Mathf.Max(1-fade,afterimage);
                removed.transform.localScale=Vector3.one*(1-.075f*Mathf.Sin(fade*Mathf.PI));
                outgoing.UpdateCardRelease(fade,loss,true);
                gained.ink.alpha=reveal;
                gained.transform.localScale=Vector3.one*Mathf.Lerp(.84f,1,reveal);
                if(time>=.27f)
                {
                    incoming.gameObject.SetActive(true);
                    incoming.UpdateCardRelease(Mathf.InverseLerp(.27f,.78f,time),BattleUi.Gold,false,entering:true);
                }
                yield return null;
            }
            removed.ink.alpha=.66f;
            gained.ink.alpha=1;
            removed.transform.localScale=gained.transform.localScale=Vector3.one;
            Destroy(outgoing.gameObject);
            Destroy(incoming.gameObject);
            revealingSwap=false;
            Refresh();
        }

        private void OpenAltarTrade(AltarTrade? trade,bool animateSwap=false)
        {
            activeTrade=trade;
            selectedCard=null;
            guess=null;
            Refresh();
            BuildAltar(animateSwap);
        }

        private void SelectCard(string key)
        {
            float scrollPosition=altarPanel.GetComponentInChildren<ScrollRect>().verticalNormalizedPosition;
            selectedCard=key;
            BuildAltar();
            Canvas.ForceUpdateCanvases();
            altarPanel.GetComponentInChildren<ScrollRect>().verticalNormalizedPosition=scrollPosition;
        }

        private void AddChoice(string name,string label,RoomChoice choice,float min,float max,float bottom=.40f,float top=.50f)
        {choices.Add(BattleUi.Button(root,name,label,Art,new Vector2(min,bottom),new Vector2(max,top),()=>Choose(choice)));}
        private void Buy(int index)
        {if(flow.Session.IsTransitioning)return;room.Buy(index);Refresh();}
        private void Choose(RoomChoice choice)
        {
            if(flow.Session.IsTransitioning||!room.Choose(choice))return;
            Refresh();if(room.Run.Status==RunStatus.Failed)flow.FinishRun();
        }
        private void Refresh()
        {
            var r=room.Run;hud.text=room.Data.Character(r.SelectedCharacter).Name+"    HP "+r.CurrentHP+" / "+r.MaxHP+"    GOLD "+r.Gold+"    유물 "+r.Items.Count+"    덱 "+r.Deck.Count+"    "+r.CurrentArea+"구역 "+r.Floor+"층";
            feedback.text=room.Visit.Feedback??"";
            foreach(var choice in choices)
            {
                choice.interactable=room.CanInteract&&!room.Visit.ChoiceMade;
            }
            for(int i=0;i<buy.Count;i++)
            {
                buy[i].interactable=room.CanBuy(i);cards[i].SetAvailable(room.CanBuy(i));
                buy[i].GetComponentInChildren<Text>().text=room.Visit.Offers[i].Purchased?"구매 완료":r.Gold<room.Visit.Offers[i].Price?"골드 부족 / "+room.Visit.Offers[i].Price:"GOLD "+room.Visit.Offers[i].Price+" / 구매";
            }
            leave.interactable=!revealingSwap&&room.CanInteract&&room.Visit.PendingAltarCards.Count==0&&
                (IsAltar||room.Node.Event==EventKind.Shop||room.Visit.ChoiceMade);
            if(IsAltar)leave.GetComponentInChildren<Text>().text=room.Visit.ChoiceMade?"던전 맵으로":"그냥 나가기";
        }
    }
}

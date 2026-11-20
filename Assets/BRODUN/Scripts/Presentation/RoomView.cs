using System.Collections.Generic;
using Brodun.Flow;
using UnityEngine;
using UnityEngine.UI;
namespace Brodun.Presentation
{
    // A shared noncombat screen. Offers/choices live in RunState, not in these buttons.
    public sealed class RoomView : MonoBehaviour
    {
        private GameFlow flow;
        private RoomRuntime room;
        private RectTransform root;
        private Text hud,feedback;
        private readonly List<StyledButton> buy=new List<StyledButton>(),choices=new List<StyledButton>();
        private readonly List<CardView> cards=new List<CardView>();
        private StyledButton leave;
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
            bool shop=room.Node.Type==RoomType.Event&&room.Node.Event==EventKind.Shop;
            if(shop)BuildShop();else BuildChoice();
            leave=BattleUi.Button(root,"RoomLeave","던전 맵으로",Art,new Vector2(.68f,.055f),new Vector2(.88f,.135f),()=>
            {if(!flow.Session.IsTransitioning)flow.ReturnFromRoom();});
            Refresh();
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
            bool rest=room.Node.Event==EventKind.RestSite,altar=room.Node.Event==EventKind.Altar;
            string body=rest?"불씨 곁에서 휴식합니다. HP "+room.Data.Config.EventHeal+" 회복 (최대 HP까지).":altar?
                "대가를 치르고 축복을 선택하세요. 제단은 한 번만 사용할 수 있습니다.":
                "봉인된 문 뒤를 탐색할까요? 주머니를 찾거나 함정에 다칠 수 있습니다.\n성공: GOLD +"+room.Data.Config.EventGold+" / 함정: HP −"+room.Rules.PuzzleDamage;
            Label("RoomBody",body,30,BattleUi.Pale,.57f,.73f);
            if(rest)AddChoice("RestChoice","휴식 / HP +"+room.Data.Config.EventHeal,RoomChoice.Rest,.24f,.53f);
            else if(altar)
            {
                AddChoice("AltarSacrifice","HP −"+room.Rules.SacrificeHP+" / GOLD +"+room.Data.Config.EventGold,RoomChoice.Sacrifice,.15f,.46f);
                AddChoice("AltarHeal","GOLD −"+room.Rules.HealPrice+" / HP +"+room.Data.Config.EventHeal,RoomChoice.PaidHeal,.54f,.85f);
            }
            else AddChoice("ExploreChoice","문을 열고 탐색",RoomChoice.Explore,.24f,.53f);
            if(!altar)AddChoice("PassChoice","그냥 지나간다",RoomChoice.Leave,.57f,.79f);
            else AddChoice("PassChoice","축복을 거절한다",RoomChoice.Leave,.35f,.65f,.27f,.35f);
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
            var r=room.Run;hud.text=room.Data.Character(r.SelectedCharacter).Name+"    HP "+r.CurrentHP+" / "+r.MaxHP+"    GOLD "+r.Gold+"    덱 "+r.Deck.Count+"    "+r.CurrentArea+"구역 "+r.Floor+"층";
            feedback.text=room.Visit.Feedback??"";
            foreach(var choice in choices)
            {
                choice.interactable=room.CanInteract&&!room.Visit.ChoiceMade;
                if(choice.name=="AltarSacrifice")choice.interactable &= r.CurrentHP>room.Rules.SacrificeHP;
                if(choice.name=="AltarHeal")choice.interactable &= r.Gold>=room.Rules.HealPrice;
            }
            for(int i=0;i<buy.Count;i++)
            {
                buy[i].interactable=room.CanBuy(i);cards[i].SetAvailable(room.CanBuy(i));
                buy[i].GetComponentInChildren<Text>().text=room.Visit.Offers[i].Purchased?"구매 완료":r.Gold<room.Visit.Offers[i].Price?"골드 부족 / "+room.Visit.Offers[i].Price:"GOLD "+room.Visit.Offers[i].Price+" / 구매";
            }
            leave.interactable=room.CanInteract&&(room.Node.Event==EventKind.Shop||room.Visit.ChoiceMade);
        }
    }
}

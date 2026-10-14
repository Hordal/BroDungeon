using System;
using System.Collections;
using System.Collections.Generic;
using Brodun.Flow;
using UnityEngine;
using UnityEngine.UI;
namespace Brodun.Presentation
{
    public sealed class BattleView : MonoBehaviour
    {
        public RectTransform Root { get; private set; }
        public EnemyView Enemy { get; private set; }
        public IReadOnlyList<CardView> Cards => cards.AsReadOnly();
        public Text EnergyLabel { get; private set; }
        public StyledButton EndTurn { get; private set; }
        private readonly List<CardView> cards=new List<CardView>();
        private BattleContext context;private BattlePresentationAssets art;
        private Text hp,block,status,turn,gold,piles,banner;
        private Image hpBar,player;
        private RectTransform stage;
        private PlayerBattleActor actor;
        public PlayerBattleActor PlayerActor => actor;
        private RectTransform hand;
        private GameObject overlay;
        private RectTransform rewardPanel;
        private float shownHP;
        private bool holdPlayerHealth;
        private BattleBanner turnBanner;
        public HandPresentation HandLayout { get; private set; }
        private int shownEnergy=-1;
        private float energyPulse;
        public void Build(BattleContext value,BattlePresentationAssets assets,Action<CardView> cardClick,Action endTurn)
        {
            context=value;art=assets;var common=art.common;
            Root=BattleUi.Rect(transform,"Battle Canvas",Vector2.zero,Vector2.one);
            var canvas=Root.gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=Root.gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            Root.gameObject.AddComponent<GraphicRaycaster>();
            stage=BattleUi.Rect(Root,"Combat stage",Vector2.zero,Vector2.one);
            BattleUi.Image(stage,"Battle background",art.background,Color.white,Vector2.zero,Vector2.one);
            BattleUi.Image(stage,"Battle veil",null,new Color(.01f,.025f,.04f,.28f),Vector2.zero,Vector2.one);
            BattleUi.Image(Root,"Hand table",null,new Color(.025f,.04f,.055f,.88f),Vector2.zero,new Vector2(1,.345f));
            BattleUi.Image(Root,"Hand gold rule",null,new Color(.65f,.51f,.27f,.65f),new Vector2(.02f,.343f),new Vector2(.98f,.345f));
            BattleUi.Image(Root,"Player status panel",null,new Color(.02f,.045f,.06f,.82f),new Vector2(.055f,.81f),new Vector2(.32f,.95f));
            BattleUi.Text(Root,"Battle title","잊혀진 회랑  /  "+(context.Room.Encounter==EnemyType.Elite?"엘리트 전투":context.Room.Type==RoomType.Boss?"보스 전투":"일반 전투"),common.font,24,BattleUi.Gold,new Vector2(.34f,.94f),new Vector2(.66f,.99f));
            BattleUi.Text(Root,"Player name",context.Runtime.Data.Character(context.Run.SelectedCharacter).Name,common.font,28,BattleUi.Gold,new Vector2(.07f,.905f),new Vector2(.17f,.95f));
            hp=BattleUi.Text(Root,"Player HP","",common.font,25,BattleUi.Pale,new Vector2(.16f,.905f),new Vector2(.31f,.95f));
            hpBar=BattleUi.Bar(Root,"Player HP Bar",new Vector2(.07f,.88f),new Vector2(.305f,.892f),BattleUi.Teal);
            block=BattleUi.Text(Root,"Player block","",common.font,23,BattleUi.Gold,new Vector2(.07f,.825f),new Vector2(.21f,.87f));
            status=BattleUi.Text(Root,"Player status","",common.font,22,BattleUi.Teal,new Vector2(.21f,.825f),new Vector2(.31f,.87f));
            player=BattleUi.Image(stage,"Player sprite",common.Portrait(context.Run.SelectedCharacter),Color.white,new Vector2(.25f,.38f),new Vector2(.25f,.38f));player.preserveAspect=true;
            player.rectTransform.sizeDelta=new Vector2(360,390);player.rectTransform.pivot=new Vector2(.5f,0);
            actor=player.gameObject.AddComponent<PlayerBattleActor>();actor.Bind(player,context.Run.SelectedCharacter);
            var enemyRoot=BattleUi.Rect(stage,"Enemy stage",new Vector2(.59f,.38f),new Vector2(.95f,.86f));
            Enemy=Instantiate(art.enemyPrefab,enemyRoot,false);Enemy.Bind(context.Runtime.Enemy,art.Enemy(context.Runtime.Enemy.Definition.Id));
            EnergyLabel=BattleUi.Text(Root,"Energy","",common.font,43,BattleUi.Gold,new Vector2(.03f,.15f),new Vector2(.185f,.285f));
            turn=BattleUi.Text(Root,"Turn","",common.font,20,BattleUi.Pale,new Vector2(.03f,.08f),new Vector2(.185f,.15f));
            gold=BattleUi.Text(Root,"Gold","",common.font,23,BattleUi.Gold,new Vector2(.84f,.95f),new Vector2(.98f,.995f));
            piles=BattleUi.Text(Root,"Deck piles","",common.font,20,BattleUi.Pale,new Vector2(.26f,.355f),new Vector2(.77f,.405f));
            banner=BattleUi.Text(Root,"Battle feedback","",common.font,27,BattleUi.Gold,new Vector2(.34f,.82f),new Vector2(.64f,.91f));
            hand=BattleUi.Rect(Root,"Hand",new Vector2(.22f,.025f),new Vector2(.79f,.345f));
            EndTurn=BattleUi.Button(Root,"EndTurn","턴 종료  ▶",common,new Vector2(.835f,.10f),new Vector2(.975f,.22f),endTurn);
            turnBanner=gameObject.AddComponent<BattleBanner>();turnBanner.Build(Root,common);
            RefreshHand(cardClick);shownHP=context.Run.CurrentHP;Refresh();
        }
        public void Refresh(IntentData intentOverride=null,bool holdIntent=false)
        {
            var b=context.Runtime;if(!holdPlayerHealth)hp.text="HP  "+context.Run.CurrentHP+" / "+context.Run.MaxHP;
            block.text="BLOCK  "+b.Block;status.text=b.Strength>0?"힘 +"+b.Strength:"";
            EnergyLabel.text="ENERGY\n"+b.Energy+" / "+b.MaxEnergy;
            if(shownEnergy!=b.Energy){shownEnergy=b.Energy;energyPulse=.25f;}
            turn.text="TURN  "+b.Turn+"\n"+(context.Phase==BattlePhase.EnemyTurn?"ENEMY TURN":context.Phase==BattlePhase.PlayerTurn?"PLAYER TURN":"진행 중");gold.text="GOLD  "+context.Run.Gold;
            piles.text="뽑을 카드 "+b.Deck.DrawPile.Count+"   ·   버림 "+b.Deck.DiscardPile.Count+"   ·   소멸 "+b.Deck.ExhaustPile.Count;
            Enemy.Refresh(b.Result!=BattleResult.Active?null:holdIntent?intentOverride:b.CurrentIntent,b.Block);
            EndTurn.interactable=context.Phase==BattlePhase.PlayerTurn;
            foreach(var card in cards)card.SetAvailable(context.CanPlay(card.InstanceId));
        }
        public void SetInput(bool enabled)
        {EndTurn.interactable=enabled&&context.Phase==BattlePhase.PlayerTurn;HandLayout?.SetInput(enabled&&context.Phase==BattlePhase.PlayerTurn);foreach(var card in cards)card.SetAvailable(enabled&&context.CanPlay(card.InstanceId));}
        public void RefreshHand(Action<CardView> click)
        {
            foreach(var card in cards){card.gameObject.SetActive(false);Destroy(card.gameObject);}cards.Clear();
            HandLayout?.Dispose();
            var entries=context.Runtime.Deck.Hand;float spacing=Mathf.Min(230,1040f/Mathf.Max(1,entries.Count));
            for(int i=0;i<entries.Count;i++)
            {
                var card=Instantiate(art.cardPrefab,hand,false);card.name="Hand_"+entries[i].InstanceId;
                var rect=(RectTransform)card.transform;rect.anchoredPosition=new Vector2((i-(entries.Count-1)*.5f)*spacing,0);
                card.Bind(context.Runtime.Data.Card(entries[i].CardId),entries[i].InstanceId,art,context.Runtime.Data,click);cards.Add(card);
            }
            HandLayout=new HandPresentation(Root,art.common,cards);
        }
        public void Message(string message){banner.text=message;}
        public void Announce(string message){turnBanner.Show(message);}
        public void HideHandForEnemyTurn(){foreach(var card in cards)card.gameObject.SetActive(false);}
        public IEnumerator FloatNumber(string text,bool enemy,Color tint)
        {
            var label=BattleUi.Text(Root,"Floating feedback",text,art.common.font,46,tint,new Vector2(enemy?.66f:.12f,.56f),new Vector2(enemy?.90f:.36f,.66f));
            yield return label.gameObject.AddComponent<DamageNumber>().Present(label,tint);
        }
        public IEnumerator PlayerHit()
        {
            HoldPlayerHealth(false);StartCoroutine(ImpactShake(4));yield return actor.Hit();
        }
        public void HoldPlayerHealth(bool hold)
        {holdPlayerHealth=hold;if(!hold)hp.text="HP  "+context.Run.CurrentHP+" / "+context.Run.MaxHP;}
        public IEnumerator PresentCard(CardDefinition definition,Action impact)
        {
            bool attack=definition.Type==CardType.Attack;
            if(attack)StartCoroutine(actor.Attack(context.Run.SelectedCharacter==CharacterId.Warrior));
            var rect=BattleUi.Rect(stage,"Card VFX "+definition.Id,Vector2.zero,Vector2.one);
            var fx=rect.gameObject.AddComponent<BattleVfx>();
            yield return fx.Present(definition,()=>
            {
                Enemy.HoldHealth(false);
                if(attack){StartCoroutine(ImpactShake(definition.BaseDamage>=12?7:4));StartCoroutine(Enemy.Play(SpriteMotion.Hurt));}
                impact?.Invoke();
            },context.Runtime.LastCardResult.ConditionalTriggered);
        }
        private IEnumerator ImpactShake(float amount)
        {
            for(float t=0;t<.16f;t+=Time.unscaledDeltaTime)
            {float damping=1-t/.16f;stage.anchoredPosition=new Vector2(Mathf.Sin(t*135)*amount*damping,Mathf.Cos(t*105)*amount*.45f*damping);yield return null;}
            stage.anchoredPosition=Vector2.zero;
        }
        public void ShowReward(Action<RewardType,string> choose)
        {
            if(overlay!=null)return;
            var panel=Overlay("RewardOverlay");var reward=context.Runtime.Reward;
            rewardPanel=panel;
            BattleUi.Text(panel,"Reward title","전리품  /  보상 1개 선택",art.common.font,43,BattleUi.Gold,new Vector2(.08f,.85f),new Vector2(.92f,.97f));
            BattleUi.Text(panel,"Reward instructions","카드 1장 또는 골드를 선택하세요. 원하지 않으면 스킵할 수 있습니다.",art.common.font,22,BattleUi.Pale,new Vector2(.05f,.77f),new Vector2(.95f,.85f));
            var row=BattleUi.Rect(panel,"Reward cards",new Vector2(.14f,.27f),new Vector2(.86f,.76f));
            for(int i=0;i<reward.Candidates.Count;i++)
            {
                string id=reward.Candidates[i];var card=Instantiate(art.cardPrefab,row,false);card.name="Reward_"+id;
                ((RectTransform)card.transform).anchoredPosition=new Vector2((i-(reward.Candidates.Count-1)*.5f)*265,0);
                card.Bind(context.Runtime.Data.Card(id),id,art,context.Runtime.Data,_=>choose(RewardType.Card,id));
                ((RectTransform)card.transform).localScale=Vector3.one*1.08f;
            }
            BattleUi.Button(panel,"RewardGold","골드 +"+reward.Gold,art.common,new Vector2(.20f,.09f),new Vector2(.48f,.21f),()=>choose(RewardType.Gold,null));
            BattleUi.Button(panel,"RewardSkip","SKIP / 건너뛰기",art.common,new Vector2(.53f,.09f),new Vector2(.81f,.21f),()=>choose(RewardType.Skip,null));
        }
        public void ShowDefeat(Action title)
        {
            var panel=Overlay("DefeatOverlay");
            BattleUi.Text(panel,"Defeat title","여정이 끝났습니다",art.common.font,48,BattleUi.Gold,new Vector2(.1f,.62f),new Vector2(.9f,.84f));
            BattleUi.Text(panel,"Defeat body","전투에서 패배했습니다. 이 방은 완료되지 않았습니다.",art.common.font,26,BattleUi.Pale,new Vector2(.1f,.41f),new Vector2(.9f,.62f));
            BattleUi.Button(panel,"DefeatTitle","타이틀로",art.common,new Vector2(.33f,.18f),new Vector2(.67f,.32f),title);
        }
        public void LockReward()
        {if(overlay!=null)overlay.GetComponent<CanvasGroup>().interactable=false;}
        public IEnumerator RewardConfirmation(string message)
        {
            var label=BattleUi.Text(rewardPanel,"Reward confirmation",message,art.common.font,25,BattleUi.Gold,new Vector2(.08f,.215f),new Vector2(.92f,.265f));
            for(float t=0;t<.65f;t+=Time.unscaledDeltaTime)
            {label.color=new Color(BattleUi.Gold.r,BattleUi.Gold.g,BattleUi.Gold.b,Mathf.Min(1,t*8));yield return null;}
        }
        private RectTransform Overlay(string name)
        {
            var shade=BattleUi.Image(Root,name,null,new Color(.01f,.02f,.03f,.95f),Vector2.zero,Vector2.one);shade.raycastTarget=true;
            overlay=shade.gameObject;overlay.AddComponent<CanvasGroup>();
            var panel=BattleUi.Panel(shade.transform,"Reward panel",new Vector2(.15f,.08f),new Vector2(.85f,.92f));
            StartCoroutine(RevealOverlay(overlay.GetComponent<CanvasGroup>()));return panel;
        }
        private IEnumerator RevealOverlay(CanvasGroup group)
        {
            group.alpha=0;
            for(float t=0;t<.18f;t+=Time.unscaledDeltaTime){group.alpha=t/.18f;yield return null;}
            group.alpha=1;
        }
        private void Update()
        {
            if(context==null)return;if(!holdPlayerHealth)shownHP=Mathf.MoveTowards(shownHP,context.Run.CurrentHP,Time.unscaledDeltaTime*context.Run.MaxHP*3);BattleUi.SetBar(hpBar,shownHP/context.Run.MaxHP);
            energyPulse=Mathf.Max(0,energyPulse-Time.unscaledDeltaTime);EnergyLabel.transform.localScale=Vector3.one*(1+energyPulse*.18f);
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        private readonly List<BattleHudGlyph> energyPips=new List<BattleHudGlyph>();
        private Coroutine shakeRoutine;
        private float blockPulse;
        private bool holdPlayerBlock;
        public void HoldPlayerBlock(bool hold)
        {holdPlayerBlock=hold;if(!hold&&context!=null)block.text=context.Runtime.Block.ToString();}
        public void Build(BattleContext value,BattlePresentationAssets assets,Action<CardView> cardClick,Action endTurn)
        {
            context=value;art=assets;var common=art.common;
            Root=BattleUi.Rect(transform,"Battle Canvas",Vector2.zero,Vector2.one);
            var canvas=Root.gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=Root.gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            Root.gameObject.AddComponent<GraphicRaycaster>();
            stage=BattleUi.Rect(Root,"Combat stage",Vector2.zero,Vector2.one);
            int area=context.Runtime.Enemy.Definition.Area;
            bool boss=context.Room.Type==RoomType.Boss;
            var background=BattleBackgrounds.Resolve(area,boss,art.background);
            BattleUi.Image(stage,"Battle background",background,Color.white,Vector2.zero,Vector2.one);
            BattleUi.Image(stage,"Battle veil",null,new Color(.01f,.025f,.04f,.15f),Vector2.zero,Vector2.one);
            // A soft floor-to-hand scrim, rather than a black rectangular table and gold border.
            for(int i=0;i<12;i++)BattleUi.Image(Root,"Hand shade "+i,null,new Color(.015f,.025f,.035f,.94f*(1-i/12f)),new Vector2(0,i*.029f),new Vector2(1,(i+1)*.029f));
            BattleUi.Text(Root,"Battle title",BattleBackgrounds.ThemeName(area)+"  ·  "+(boss?"보스":context.Room.Encounter==EnemyType.Elite?"엘리트":"일반 전투"),common.font,20,BattleUi.Gold,new Vector2(.35f,.95f),new Vector2(.65f,.99f));
            BattleUi.Text(Root,"Player name",context.Runtime.Data.Character(context.Run.SelectedCharacter).Name,common.font,22,BattleUi.Gold,new Vector2(.14f,.893f),new Vector2(.36f,.925f));
            hp=BattleUi.Text(Root,"Player HP","",common.font,40,BattleUi.Pale,new Vector2(.13f,.815f),new Vector2(.37f,.866f));
            hpBar=BattleUi.Bar(Root,"Player HP Bar",new Vector2(.13f,.825f),new Vector2(.37f,.877f),new Color(.11f,.43f,.36f));
            var hpTrack=(RectTransform)hpBar.transform.parent;
            hpTrack.GetComponent<Image>().color=new Color(.015f,.025f,.035f,.96f);
            hp.rectTransform.SetParent(hpTrack,false);hp.rectTransform.anchorMin=Vector2.zero;hp.rectTransform.anchorMax=Vector2.one;
            hp.rectTransform.offsetMin=hp.rectTransform.offsetMax=Vector2.zero;
            hp.verticalOverflow=VerticalWrapMode.Overflow;hp.horizontalOverflow=HorizontalWrapMode.Overflow;
            BattleUi.Glyph(Root,"Player shield",BattleHudGlyph.Symbol.Shield,BattleUi.Teal,new Vector2(.14f,.790f),new Vector2(.157f,.825f));
            block=BattleUi.Text(Root,"Player block","",common.font,25,BattleUi.Teal,new Vector2(.16f,.790f),new Vector2(.25f,.825f));
            status=BattleUi.Text(Root,"Player status","",common.font,20,BattleUi.Teal,new Vector2(.25f,.790f),new Vector2(.36f,.825f));
            player=BattleUi.Image(stage,"Player sprite",common.Portrait(context.Run.SelectedCharacter),Color.white,new Vector2(.25f,.44f),new Vector2(.25f,.44f));player.preserveAspect=true;
            player.rectTransform.sizeDelta=new Vector2(342,368);player.rectTransform.pivot=new Vector2(.5f,0);
            actor=player.gameObject.AddComponent<PlayerBattleActor>();actor.Bind(player,context.Run.SelectedCharacter);
            var enemyRoot=BattleUi.Rect(stage,"Enemy stage",new Vector2(.60f,.44f),new Vector2(.94f,.80f));
            Enemy=Instantiate(art.enemyPrefab,enemyRoot,false);Enemy.Bind(context.Runtime.Enemy,art.Enemy(context.Runtime.Enemy.Definition.Id));
            BattleUi.Text(Root,"Energy caption","행동력 / ENERGY",common.font,21,BattleUi.Teal,new Vector2(.025f,.25f),new Vector2(.17f,.28f));
            EnergyLabel=BattleUi.Text(Root,"Energy","",common.font,68,BattleUi.Gold,new Vector2(.025f,.105f),new Vector2(.17f,.205f));
            BattleUi.Glyph(Root,"Energy emblem",BattleHudGlyph.Symbol.Energy,BattleUi.Teal,new Vector2(.080f,.205f),new Vector2(.115f,.249f));
            for(int i=0;i<6;i++)
            {
                var pip=BattleUi.Glyph(Root,"Energy pip "+i,BattleHudGlyph.Symbol.Energy,BattleUi.Gold,new Vector2(.036f+i*.021f,.075f),new Vector2(.051f+i*.021f,.104f));
                energyPips.Add(pip);
            }
            turn=BattleUi.Text(Root,"Turn","",common.font,17,BattleUi.Pale,new Vector2(.025f,.015f),new Vector2(.17f,.073f));
            gold=BattleUi.Text(Root,"Gold","",common.font,23,BattleUi.Gold,new Vector2(.84f,.95f),new Vector2(.98f,.995f));
            piles=BattleUi.Text(Root,"Deck piles","",common.font,17,BattleUi.Pale,new Vector2(.81f,.035f),new Vector2(.99f,.09f));
            banner=BattleUi.Text(Root,"Battle feedback","",common.font,23,BattleUi.Gold,new Vector2(.36f,.79f),new Vector2(.64f,.84f));
            hand=BattleUi.Rect(Root,"Hand",new Vector2(.185f,.005f),new Vector2(.815f,.37f));
            EndTurn=BattleUi.Button(Root,"EndTurn","턴 종료  ▶",common,new Vector2(.85f,.11f),new Vector2(.975f,.185f),endTurn);
            turnBanner=gameObject.AddComponent<BattleBanner>();turnBanner.Build(Root,common);
            foreach(var label in new[]{hp,block,status,EnergyLabel,turn,gold})BattleUi.Readable(label);
            RefreshHand(cardClick);shownHP=context.Run.CurrentHP;Refresh();
        }
        public void Refresh(IntentData intentOverride=null,bool holdIntent=false)
        {
            var b=context.Runtime;if(!holdPlayerHealth)hp.text=context.Run.CurrentHP+" / "+context.Run.MaxHP;
            if(!holdPlayerBlock)block.text=b.Block.ToString();status.text=b.Strength>0?"힘 +"+b.Strength:"";
            EnergyLabel.text=b.Energy+" / "+b.MaxEnergy;
            int pipCount=Mathf.Min(energyPips.Count,Mathf.Max(b.Energy,b.MaxEnergy));
            for(int i=0;i<energyPips.Count;i++)
            {
                energyPips[i].gameObject.SetActive(i<pipCount);energyPips[i].color=i<b.Energy?BattleUi.Gold:new Color(.20f,.25f,.25f,.7f);
                float x=.0975f-(pipCount*.021f-.006f)*.5f+i*.021f;
                energyPips[i].rectTransform.anchorMin=new Vector2(x,.075f);energyPips[i].rectTransform.anchorMax=new Vector2(x+.015f,.104f);
            }
            if(shownEnergy!=b.Energy){energyPulse=shownEnergy>=0&&b.Energy>shownEnergy?.55f:.25f;shownEnergy=b.Energy;}
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
            float scroll=HandLayout?.ScrollPosition??0;
            HandLayout?.Dispose();HandLayout=null;
            var existing=cards.ToDictionary(card=>card.InstanceId);cards.Clear();
            var entries=context.Runtime.Deck.Hand;
            for(int i=0;i<entries.Count;i++)
            {
                string id=entries[i].InstanceId;
                if(existing.TryGetValue(id,out var card)&&!card.IsPresenting)existing.Remove(id);
                else
                {
                    card=Instantiate(art.cardPrefab,hand,false);card.name="Hand_"+id;
                    card.Bind(context.Runtime.Data.Card(entries[i].CardId),id,art,context.Runtime.Data,click);
                }
                cards.Add(card);
            }
            foreach(var removed in existing.Values){removed.gameObject.SetActive(false);Destroy(removed.gameObject);}
            HandLayout=new HandPresentation(Root,hand,art.common,cards,scroll);
        }
        public void Message(string message){banner.text=message;}
        public void Announce(string message){turnBanner.Show(message);}
        public void HideHandForEnemyTurn(){HandLayout?.SetVisible(false);foreach(var card in cards)card.gameObject.SetActive(false);}
        public IEnumerator FloatNumber(string text,bool enemy,Color tint)
        {
            var label=BattleUi.Text(Root,"Floating feedback",text,art.common.font,46,tint,new Vector2(enemy?.66f:.12f,.56f),new Vector2(enemy?.90f:.36f,.66f));
            var target=enemy?Enemy.sprite.rectTransform:player.rectTransform;
            var rect=label.rectTransform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(360,80);
            rect.position=target.TransformPoint(target.rect.min+Vector2.Scale(target.rect.size,new Vector2(.5f,.60f)));
            yield return label.gameObject.AddComponent<DamageNumber>().Present(label,tint);
        }
        public IEnumerator PlayerHit()
        {
            HoldPlayerHealth(false);Shake(5);yield return actor.Hit();
        }
        public void HoldPlayerHealth(bool hold)
        {holdPlayerHealth=hold;if(!hold)hp.text=context.Run.CurrentHP+" / "+context.Run.MaxHP;}
        public IEnumerator PresentCard(CardDefinition definition,Action impact,Action delivered=null)
        {
            bool attack=definition.Type==CardType.Attack;
            float pause=BattleVfx.HitPause(definition);
            bool heavy=definition.BaseDamage>=12||context.Runtime.LastCardResult.ConditionalTriggered;
            int hits=context.Runtime.LastCardResult.Hits.Count;
            if(attack)StartCoroutine(actor.Attack(context.Run.SelectedCharacter==CharacterId.Warrior));
            else if(definition.Id=="stone_guard"||definition.Id=="wind_read")StartCoroutine(actor.CastSupport(definition.Id=="stone_guard"));
            var rect=BattleUi.Rect(stage,"Card VFX "+definition.Id,Vector2.zero,Vector2.one);
            var fx=rect.gameObject.AddComponent<BattleVfx>();
            fx.BindActors(player.rectTransform,Enemy.sprite.rectTransform);
            yield return fx.Present(definition,()=>
            {
                Enemy.HoldHealth(false);
                if(definition.Id=="stone_guard"){HoldPlayerBlock(false);blockPulse=.30f;}
                if(attack)
                {
                    actor.HoldImpact(pause);
                    Shake(definition.Id=="earth_strike"?8:definition.Id=="lightning_strike"?4:heavy?10:hits>1?3:definition.BaseDamage<=4?3.5f:6);
                    StartCoroutine(Enemy.Play(SpriteMotion.Hurt,pause,heavy?1.35f:.8f,BattleVfx.IsSignature(definition.Id)?definition.Id:null));
                }
                impact?.Invoke();
            },context.Runtime.LastCardResult.ConditionalTriggered,_=>
            {
                if(attack){Shake(heavy?7:4);StartCoroutine(Enemy.Play(SpriteMotion.Hurt,.018f,heavy?1.1f:.7f));}
            },hits,delivered);
        }
        private void Shake(float amount)
        {if(shakeRoutine!=null)StopCoroutine(shakeRoutine);shakeRoutine=StartCoroutine(ImpactShake(amount));}
        private IEnumerator ImpactShake(float amount)
        {
            for(float t=0;t<.21f;t+=Time.unscaledDeltaTime)
            {float damping=Mathf.Exp(-t*20);stage.anchoredPosition=new Vector2(Mathf.Cos(t*140)*amount*damping,Mathf.Sin(t*109)*amount*.4f*damping);yield return null;}
            stage.anchoredPosition=Vector2.zero;shakeRoutine=null;
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
                ((RectTransform)card.transform).anchoredPosition=new Vector2((i-(reward.Candidates.Count-1)*.5f)*284,0);
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
            HandLayout?.Tick(Time.unscaledDeltaTime);
            if(context==null)return;if(!holdPlayerHealth)shownHP=Mathf.MoveTowards(shownHP,context.Run.CurrentHP,Time.unscaledDeltaTime*context.Run.MaxHP*3);BattleUi.SetBar(hpBar,shownHP/context.Run.MaxHP);
            energyPulse=Mathf.Max(0,energyPulse-Time.unscaledDeltaTime);EnergyLabel.transform.localScale=Vector3.one*(1+energyPulse*.18f);
            EnergyLabel.color=Color.Lerp(BattleUi.Gold,Color.white,energyPulse);
            blockPulse=Mathf.Max(0,blockPulse-Time.unscaledDeltaTime);block.transform.localScale=Vector3.one*(1+blockPulse*.65f);
        }
    }
}

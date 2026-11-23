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
        public EnemyView Enemy => EnemyFor(context.Runtime.Enemy);
        private readonly Dictionary<EnemyInstance,EnemyView> enemies=new Dictionary<EnemyInstance,EnemyView>();
        public IReadOnlyList<CardView> Cards => cards.AsReadOnly();
        public Text EnergyLabel { get; private set; }
        public StyledButton EndTurn { get; private set; }
        private readonly List<CardView> cards=new List<CardView>();
        private BattleContext context;private BattlePresentationAssets art;
        private Text hp,block,status,turn,gold,effectStatus,effectTooltip;
        private Image hpBar,player;
        private Outline playerTargetOutline;
        private RectTransform stage;
        private PlayerBattleActor actor;
        public PlayerBattleActor PlayerActor => actor;
        private RectTransform hand;
        private GameObject overlay;
        private RectTransform rewardPanel;
        private RectTransform cleansePanel;
        private float shownHP;
        private bool holdPlayerHealth;
        private BattleBanner turnBanner;
        public HandPresentation HandLayout { get; private set; }
        private int shownEnergy=-1;
        private float energyPulse;
        private Text targetingHint;
        private RectTransform playerAction;
        private BattleTargetArrow targetArrow;
        private Vector2 dragPoint;
        private bool dragOverTarget;
        private CardView selectedCard;
        private float blockPulse;
        private bool holdPlayerBlock;
        private bool enemyPresentation;
        private int presentedHP,presentedBlock,presentedEnergy;
        private bool cardPresentation;
        private EnemyView cardTarget;
        private Vector2 playerHome;
        private int cardHPBefore,cardBlockBefore,cardStrengthBefore;
        private int cardHealGain,cardBlockGain,cardStrengthGain;
        private readonly List<DamageNumber> playerNotices=new List<DamageNumber>();
        private readonly Dictionary<EnemyView,List<DamageNumber>> enemyNotices=new Dictionary<EnemyView,List<DamageNumber>>();
        private readonly Dictionary<string,Vector2> impactAnchors=new Dictionary<string,Vector2>();
        public BattleAudio Sounds { get; private set; }
        public void HoldPlayerBlock(bool hold)
        {holdPlayerBlock=hold;if(!hold&&context!=null)BattleUi.SetBlockReadout(block,context.Runtime.Block);}
        public EnemyView EnemyFor(EnemyInstance enemy)=>enemy!=null&&enemies.TryGetValue(enemy,out var view)?view:null;
        private EnemyView HitView(CombatEvent entry)
            => EnemyFor(context.Runtime.Enemies.FirstOrDefault(enemy=>enemy.TargetId==entry.TargetId));
        public void BeginCardPresentation(EnemyInstance target,bool enemyCard)
        {
            CaptureImpactAnchors();
            cardPresentation=true;cardHPBefore=context.Run.CurrentHP;cardBlockBefore=context.Runtime.Block;cardStrengthBefore=context.Runtime.Strength;
            cardTarget=enemyCard?EnemyFor(target??context.Runtime.Enemies.FirstOrDefault(enemy=>enemy.IsAlive)):null;
            // Healing can trigger brands on enemies outside the selected card target.
            foreach(var view in enemies.Values){view.HoldHealth(true);view.HoldActionHud(true);}
            HoldPlayerHealth(true);HoldPlayerBlock(true);
        }
        public void RecordCardGains()
        {
            // Notices use resolved gains, including healing capped at MaxHP.
            cardHealGain=Mathf.Max(0,context.Run.CurrentHP-cardHPBefore);
            cardBlockGain=context.Runtime.LastCardResult.Events
                .Where(e=>e.Kind==CombatEventKind.Block&&e.TargetId==CombatTrace.PlayerId&&e.After>e.Before)
                .Sum(e=>e.After-e.Before);
            cardStrengthGain=Mathf.Max(0,context.Runtime.Strength-cardStrengthBefore);
        }
        public void EndCardPresentation()
        {
            cardPresentation=false;
            foreach(var view in enemies.Values){view.HoldHealth(false);view.HoldActionHud(false);}
            cardTarget=null;
            HoldPlayerHealth(false);HoldPlayerBlock(false);
        }
        public void Build(BattleContext value,BattlePresentationAssets assets,Action<CardView> cardClick,Action endTurn,Action<EnemyInstance> targetClick=null)
        {
            context=value;art=assets;var common=art.common;
            Sounds=gameObject.AddComponent<BattleAudio>();
            Root=BattleUi.Rect(transform,"Battle Canvas",Vector2.zero,Vector2.one);
            var canvas=Root.gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=Root.gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            Root.gameObject.AddComponent<GraphicRaycaster>();
            stage=BattleUi.Rect(Root,"Combat stage",Vector2.zero,Vector2.one);
            int area=context.Runtime.Enemy.Definition.Area;
            bool boss=context.Room.Type==RoomType.Boss;
            var theme=context.Run.IsThemedRun?DungeonThemes.Find(context.Run.ThemeId):null;
            var background=theme!=null?BattleBackgrounds.Resolve(theme.Id,boss,art.background):BattleBackgrounds.Resolve(area,boss,art.background);
            // Normal and elite art places the walkable floor below the distant railings.
            float floor=background?.name switch
            {
                "01_ruins_normal"=>.375f,
                "04_storm_normal"=>.401f,
                _=>BattleUi.FloorY
            };
            playerHome=new Vector2(BattleUi.PlayerHomeX,floor);
            float rearFloor=floor+(floor<BattleUi.FloorY?.028f:.055f);
            BattleUi.Image(stage,"Battle background",background,Color.white,Vector2.zero,Vector2.one);
            BattleUi.Image(stage,"Battle veil",null,new Color(.01f,.025f,.04f,.07f),Vector2.zero,Vector2.one);
            BattleUi.Surface(stage,"Dungeon atmosphere",Vector2.zero,Vector2.one,BattleChrome.Form.Vignette,BattleUi.Bronze);
            BattleUi.Surface(Root,"Hand shadow",Vector2.zero,new Vector2(1,HandPresentation.HoverTop),BattleChrome.Form.HandShade,Color.black);
            string encounter=context.Room.Type==RoomType.Boss?"보스":context.Room.Encounter==EnemyType.Elite?"엘리트":"일반 전투";
            var chapter=BattleUi.Text(Root,"Battle title",(theme?.Name??BattleBackgrounds.ThemeName(area))+"  ·  "+encounter,common.font,18,BattleUi.Pale,new Vector2(.36f,.95f),new Vector2(.64f,.986f));
            BattleUi.Readable(chapter);
            var playerHud=BattleUi.UnitReadout(Root,"Player vitals",playerHome.x,playerHome.y);
            var playerName=BattleUi.Text(playerHud,"Player name",context.Runtime.Data.Character(context.Run.SelectedCharacter).Name,common.font,18,BattleUi.Pale,Vector2.zero,Vector2.one);
            hp=BattleUi.Text(playerHud,"Player HP","",common.font,20,BattleUi.Pale,Vector2.zero,Vector2.one);
            hpBar=BattleUi.Bar(playerHud,"Player HP Bar",Vector2.zero,Vector2.one,BattleUi.HpRed);
            BattleUi.LayoutUnitReadout(playerHud,playerName,hp,hpBar);
            block=BattleUi.BlockReadout(playerHud,"Player block",common.font);
            status=BattleUi.Text(playerHud,"Player status","",common.font,16,BattleUi.Steel,new Vector2(-.26f,.36f),new Vector2(0,1));
            effectStatus=BattleUi.Text(playerHud,"Player effect status","",common.font,16,BattleUi.Teal,new Vector2(.03f,-.47f),new Vector2(.97f,.05f));
            effectStatus.resizeTextForBestFit=true;effectStatus.resizeTextMinSize=12;effectStatus.resizeTextMaxSize=16;BattleUi.Readable(effectStatus);
            var effectTip=BattleUi.Surface(Root,"Player effect details",new Vector2(.035f,.47f),new Vector2(.31f,.63f),BattleChrome.Form.Hud,BattleUi.Steel);
            effectTooltip=BattleUi.Text(effectTip,"Active effect descriptions","",common.font,20,BattleUi.Pale,new Vector2(.05f,.08f),new Vector2(.95f,.92f));
            effectTooltip.resizeTextForBestFit=true;effectTooltip.resizeTextMinSize=12;effectTooltip.resizeTextMaxSize=20;
            effectTip.gameObject.SetActive(false);effectStatus.raycastTarget=true;
            effectStatus.gameObject.AddComponent<IntentHover>().tooltip=effectTip.gameObject;
            var playerShadow=BattleUi.Surface(stage,"Player contact shadow",playerHome,playerHome,BattleChrome.Form.Shadow,Color.black);
            playerShadow.sizeDelta=new Vector2(176,28);playerShadow.anchoredPosition=Vector2.down*5;
            player=BattleUi.Image(stage,"Player sprite",common.Portrait(context.Run.SelectedCharacter),Color.white,playerHome,playerHome);player.preserveAspect=true;
            player.rectTransform.sizeDelta=new Vector2(300,324);player.rectTransform.pivot=new Vector2(.5f,0);
            playerTargetOutline=player.gameObject.AddComponent<Outline>();
            playerTargetOutline.effectDistance=new Vector2(2.5f,-2.5f);
            playerTargetOutline.enabled=false;
            actor=player.gameObject.AddComponent<PlayerBattleActor>();actor.Bind(player,context.Run.SelectedCharacter);
            var models=context.Runtime.Enemies;
            if(models.Count==2)
            {
                BuildEnemy(models[1],new Vector2(.86f,rearFloor),.66f,targetClick);
                BuildEnemy(models[0],new Vector2(.66f,floor),.74f,targetClick);
            }
            else BuildEnemy(models[0],new Vector2(BattleUi.EnemyHomeX,floor),.88f,targetClick);
            var energyReadout=BattleUi.Rect(Root,"Energy readout",new Vector2(.03f,.087f),new Vector2(.17f,.236f));
            var energyCaption=BattleUi.Text(energyReadout,"Energy caption","행동력",common.font,23,BattleUi.Pale,new Vector2(0,.70f),Vector2.one);
            BattleUi.Readable(energyCaption);
            EnergyLabel=BattleUi.Text(energyReadout,"Energy","",common.font,46,BattleUi.Gold,new Vector2(0,.22f),new Vector2(1,.72f));
            EnergyLabel.fontStyle=FontStyle.Bold;
            EnergyLabel.resizeTextForBestFit=true;EnergyLabel.resizeTextMinSize=18;EnergyLabel.resizeTextMaxSize=46;
            BattleUi.Image(energyReadout,"Energy rule",null,new Color(.56f,.46f,.29f,.55f),new Vector2(.28f,.225f),new Vector2(.72f,.235f));
            turn=BattleUi.Text(energyReadout,"Turn","",common.font,19,BattleUi.Steel,Vector2.zero,new Vector2(1,.19f));
            gold=BattleUi.Text(Root,"Gold","",common.font,23,BattleUi.Gold,new Vector2(.915f,.966f),new Vector2(.992f,.997f));
            BattleUi.Button(Root,"BattleItems","유물 "+context.Run.Items.Count,common,new Vector2(.79f,.955f),new Vector2(.90f,.997f),()=>ItemInventoryPanel.Show(Root,common,context.Run));
            gold.alignment=TextAnchor.MiddleRight;
            gold.resizeTextForBestFit=true;gold.resizeTextMinSize=16;gold.resizeTextMaxSize=23;
            playerAction=BattleUi.Surface(Root,"Selected action",playerHome,playerHome,BattleChrome.Form.Action,BattleUi.Bronze);
            playerAction.sizeDelta=new Vector2(276,39);
            targetingHint=BattleUi.Text(playerAction,"Target instruction","",common.font,21,BattleUi.Pale,new Vector2(.05f,0),new Vector2(.95f,1));
            targetingHint.resizeTextForBestFit=true;
            targetingHint.resizeTextMinSize=15;
            targetingHint.resizeTextMaxSize=21;
            playerAction.gameObject.SetActive(false);
            var arrowRect=BattleUi.Rect(Root,"Card targeting arrow",Vector2.zero,Vector2.one);
            targetArrow=arrowRect.gameObject.AddComponent<BattleTargetArrow>();
            targetArrow.raycastTarget=false;
            arrowRect.gameObject.SetActive(false);
            hand=BattleUi.Rect(Root,"Hand",new Vector2(.185f,.012f),new Vector2(.815f,HandPresentation.HandTop));
            EndTurn=BattleUi.CommandButton(Root,"EndTurn","턴 종료",common,new Vector2(.831f,.129f),new Vector2(.966f,.185f),endTurn,BattleChrome.Form.Action);
            turnBanner=gameObject.AddComponent<BattleBanner>();turnBanner.Build(Root,common);
            var flow=GameFlow.Instance;
            BattleUi.CommandButton(Root,"CodexButton","도감",common,new Vector2(.022f,.942f),new Vector2(.098f,.988f),()=>flow?.OpenCardLibrary(Root));
            BattleUi.CommandButton(Root,"DeckButton","현재 덱",common,new Vector2(.795f,.942f),new Vector2(.897f,.988f),()=>flow?.OpenCardLibrary(Root,true));
            var audioButton=BattleUi.CommandButton(Root,"Battle audio settings","음량",common,new Vector2(.105f,.948f),new Vector2(.163f,.982f),()=>GameAudio.Ensure().OpenSettings(common,Root),BattleChrome.Form.Action);
            var audioCaption=audioButton.GetComponentInChildren<Text>();
            audioCaption.fontSize=18;
            audioCaption.verticalOverflow=VerticalWrapMode.Overflow;
            foreach(var label in new[]{hp,block,status,EnergyLabel,turn,gold})BattleUi.Readable(label);
            RefreshHand(cardClick);shownHP=context.Run.CurrentHP;Refresh();
        }
        private void BuildEnemy(EnemyInstance model,Vector2 home,float scale,Action<EnemyInstance> targetClick)
        {
            var slot=BattleUi.Rect(stage,"Enemy stage "+model.TargetId,new Vector2(home.x-.12f,home.y),new Vector2(home.x+.12f,.84f));
            var shadow=BattleUi.Surface(slot,"Contact shadow",new Vector2(.5f,0),new Vector2(.5f,0),BattleChrome.Form.Shadow,Color.black);
            shadow.sizeDelta=new Vector2(model.Definition.Type==EnemyType.Boss?300:230,36)*scale;
            shadow.anchoredPosition=Vector2.down*5;
            var view=Instantiate(art.enemyPrefab,slot,false);
            var imported=context.Run.IsThemedRun?Resources.Load<ThemeMonsterArt>("ThemeMonsterRoster")?.Find(model.Definition.Id):null;
            var legacyVisual=imported==null?art.Enemy(model.Definition.Id):null;
            view.Bind(model,legacyVisual,Root,scale,home,targetClick,imported);
            enemies.Add(model,view);
            enemyNotices.Add(view,new List<DamageNumber>());
        }
        public void SetTargeting(CardView card,bool dragging=false)
        {
            if(selectedCard!=null)selectedCard.SetTargeting(false);
            selectedCard=card;
            bool targetEnemy=card!=null&&card.Definition.RequiresEnemyTarget;
            bool targetPlayer=card!=null&&dragging&&!targetEnemy;
            if(card!=null)
            {
                card.SetTargeting(true);
                string instruction=targetPlayer?"내 캐릭터에 놓기":dragging?(card.Definition.TargetScope==CardTargetScope.AllEnemies?"적 위에 놓기 · 적 전체":"적 위에 놓기"):"대상 클릭";
                targetingHint.text=card.Definition.Name+"  ·  "+instruction;
                BattleUi.PositionAbove(playerAction,actor.IdleTop);
            }
            playerAction.gameObject.SetActive(card!=null);
            if(!dragging)targetArrow.gameObject.SetActive(false);
            player.raycastTarget=targetPlayer;
            playerTargetOutline.enabled=targetPlayer;
            if(targetPlayer)SetPlayerTargetHighlight(false);
            foreach(var view in enemies.Values)view.SetTargeting(targetEnemy);
            EndTurn.interactable=card==null&&context.Phase==BattlePhase.PlayerTurn;
        }
        internal bool IsPlayerTarget(GameObject hit)=>hit==player.gameObject;

        private void SetPlayerTargetHighlight(bool pointed)
        {
            var tint=BattleUi.Teal;
            tint.a=pointed?.96f:.50f;
            playerTargetOutline.effectColor=tint;
        }
        public void ShowTargetDrag(Vector2 screenPoint,Camera camera,bool overTarget)
        {
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(targetArrow.rectTransform,screenPoint,camera,out dragPoint))return;
            dragOverTarget=overTarget;
            if(playerTargetOutline.enabled)SetPlayerTargetHighlight(overTarget);
            if(targetArrow.gameObject.activeSelf)return;
            targetArrow.transform.SetAsLastSibling();
            targetArrow.gameObject.SetActive(true);
        }
        private void LateUpdate()
        {
            if(selectedCard==null||!targetArrow.gameObject.activeSelf)return;
            var card=selectedCard.visual;
            var upperEdge=card.TransformPoint(new Vector3(card.rect.center.x,card.rect.yMax-4));
            var origin=targetArrow.transform.InverseTransformPoint(upperEdge);
            targetArrow.SetPath(origin,dragPoint,dragOverTarget,!selectedCard.Definition.RequiresEnemyTarget);
        }
        public IEnumerator PresentDeaths()
        {
            foreach(var model in context.Runtime.Enemies)
                if(!model.IsAlive)yield return EnemyFor(model).Play(SpriteMotion.Death);
        }
        public void Refresh()
        {
            var b=context.Runtime;if(!holdPlayerHealth)hp.text=context.Run.CurrentHP+" / "+context.Run.MaxHP;
            if(!holdPlayerBlock)BattleUi.SetBlockReadout(block,b.Block);
            var stats=new[]{b.Strength>0?"힘 +"+b.Strength:null,
                context.Run.SelectedCharacter==CharacterId.Mage&&b.UsesReworkedCards?"공명 "+b.Resonance:null,
                context.Run.SelectedCharacter==CharacterId.Priest&&b.UsesReworkedCards?"은총 "+b.Grace:null};
            status.text=string.Join(" · ",stats.Where(value=>value!=null));
            var active=b.UsesReworkedCards
                ?b.Powers.Select(entry=>b.Data.Card(entry.Key).Name+(entry.Value>1?" ×"+entry.Value:"")).ToArray()
                :b.ActiveCardEffects.Select(id=>id==nameof(EffectType.DiscountShared)?"원소 비용 −1":b.Data.Card(id).Name).ToArray();
            effectStatus.text=string.Join(" · ",active.Take(2))+(active.Length>2?" +"+(active.Length-2):"");
            effectTooltip.text=b.UsesReworkedCards?string.Join("\n",active):string.Join("\n",b.ActiveCardEffects.Select(id=>id==nameof(EffectType.DiscountShared)?"다음 공용 원소 카드 비용 −1":b.Data.Card(id).Name+(b.Data.Card(id).Effects.Any(e=>e.Rule.Persistent)?" (전투 지속)":" (이번 턴 예약)")));
            int energy=enemyPresentation?presentedEnergy:b.Energy;
            EnergyLabel.text=energy+"/"+b.MaxEnergy;
            if(shownEnergy!=energy){energyPulse=shownEnergy>=0&&energy>shownEnergy?.55f:.25f;shownEnergy=energy;}
            turn.text="턴 "+b.Turn;
            gold.text="GOLD  "+context.Run.Gold;
            foreach(var entry in enemies)
            {
                var model=entry.Key;
                var intent=enemyPresentation
                    ?b.LastEnemyActions.FirstOrDefault(action=>action.Actor==model)?.Intent
                    :model.IsAlive&&b.Result==BattleResult.Active?IntentResolver.Preview(model,b.Data.Config):null;
                entry.Value.Refresh(intent,enemyPresentation?presentedBlock:b.Block);
            }
            EndTurn.interactable=selectedCard==null&&context.Phase==BattlePhase.PlayerTurn;
            foreach(var card in cards)card.SetAvailable(context.CanPlay(card.InstanceId));
        }
        public void SetInput(bool enabled)
        {
            enabled=enabled&&!(GameAudio.Instance!=null&&GameAudio.Instance.IsSettingsOpen);
            EndTurn.interactable=enabled&&selectedCard==null&&context.Phase==BattlePhase.PlayerTurn;HandLayout?.SetInput(enabled&&context.Phase==BattlePhase.PlayerTurn);foreach(var card in cards)card.SetAvailable(enabled&&context.CanPlay(card.InstanceId));
        }
        public void RefreshHand(Action<CardView> click,bool rebuildPresentation=false)
        {
            float scroll=HandLayout?.ScrollPosition??0;
            HandLayout?.Dispose();HandLayout=null;
            if(rebuildPresentation)
            {
                foreach(var card in cards){card.gameObject.SetActive(false);Destroy(card.gameObject);}
                cards.Clear();
            }
            var existing=cards.ToDictionary(card=>card.InstanceId);cards.Clear();
            var entries=context.Runtime.Deck.Hand;
            for(int i=0;i<entries.Count;i++)
            {
                string id=entries[i].InstanceId;
                if(existing.TryGetValue(id,out var card)&&!card.IsPresenting)existing.Remove(id);
                else
                {
                    card=Instantiate(art.cardPrefab,hand,false);card.name="Hand_"+id;
                    card.Bind(context.Runtime.Definition(entries[i]),id,art,context.Runtime.Data,click);
                }
                card.SetClickHandler(click);
                cards.Add(card);
            }
            foreach(var removed in existing.Values){removed.gameObject.SetActive(false);Destroy(removed.gameObject);}
            HandLayout=new HandPresentation(Root,hand,art.common,cards,scroll);
        }
        public GameObject BeginDiscardSelection(Action<CardView> choose)
        {
            HandLayout?.SetInput(true);
            foreach(var card in cards){card.SetClickHandler(choose);card.SetAvailable(true);}
            var prompt=BattleUi.Surface(Root,"Discard prompt",new Vector2(.30f,.35f),new Vector2(.70f,.411f),BattleChrome.Form.Hud,BattleUi.Gold);
            BattleUi.Text(prompt,"Discard instruction","버릴 카드 1장을 현재 손패에서 선택",art.common.font,26,
                BattleUi.Gold,new Vector2(.04f,.10f),new Vector2(.96f,.90f)).verticalOverflow=VerticalWrapMode.Overflow;
            return prompt.gameObject;
        }
        public void Announce(string message){turnBanner.Show(message);}
        public void HideHandForEnemyTurn(){HandLayout?.SetVisible(false);foreach(var card in cards)card.gameObject.SetActive(false);}
        public IEnumerator Feedback(DamageNumber.Kind kind,int amount,EnemyView target=null)
        {if(amount>0)yield return FloatNumber("    "+DamageNumber.Caption(kind,amount),target,DamageNumber.Tint(kind),kind);}
        private void ShowDamage(DamageResult hit,EnemyView target,Vector2 anchor)
        {
            if(hit.Blocked>0)StartCoroutine(FloatNumber("    "+DamageNumber.Caption(DamageNumber.Kind.BlockLoss,hit.Blocked),
                target,DamageNumber.Tint(DamageNumber.Kind.BlockLoss),DamageNumber.Kind.BlockLoss,anchor));
            if(hit.HPDamage>0)StartCoroutine(FloatNumber("    "+DamageNumber.Caption(DamageNumber.Kind.Damage,hit.HPDamage),
                target,DamageNumber.Tint(DamageNumber.Kind.Damage),DamageNumber.Kind.Damage,anchor));
        }
        public IEnumerator FloatNumber(string text,EnemyView target,Color tint,DamageNumber.Kind? kind=null,Vector2? contact=null)
        {
            var label=BattleUi.Text(Root,"Floating feedback "+(target!=null?"enemy":"player"),text,art.common.font,36,tint,new Vector2(.5f,.5f),new Vector2(.5f,.5f));
            var rect=label.rectTransform;rect.sizeDelta=new Vector2(285,52);label.alignment=TextAnchor.MiddleLeft;
            label.horizontalOverflow=HorizontalWrapMode.Overflow;label.verticalOverflow=VerticalWrapMode.Overflow;
            if(kind.HasValue)BattleUi.Glyph(rect,"Result icon",DamageNumber.Symbol(kind.Value),tint,new Vector2(.005f,.19f),new Vector2(.105f,.81f));
            var numbers=label.gameObject.AddComponent<Canvas>();numbers.overrideSorting=true;numbers.sortingOrder=7;
            BattleUi.Readable(label);
            var number=label.gameObject.AddComponent<DamageNumber>();
            var lane=target!=null?enemyNotices[target]:playerNotices;
            lane.RemoveAll(item=>item==null);lane.Insert(0,number);
            var bounds=Root.rect;
            float gap=Mathf.Min(58,bounds.height*.25f/Mathf.Max(1,lane.Count-1));
            var anchor=target!=null?target.Home:playerHome;
            var home=bounds.min+Vector2.Scale(bounds.size,contact??(anchor+Vector2.up*.06f));
            home.x=Mathf.Clamp(home.x,bounds.xMin+155,bounds.xMax-155);
            for(int i=0;i<lane.Count;i++)lane[i].Place(home+Vector2.up*(i*gap));
            yield return number.Present(label,tint);
        }
        public void BeginEnemyPresentation()
        {
            CaptureImpactAnchors();
            enemyPresentation=true;presentedHP=context.Run.CurrentHP;presentedBlock=context.Runtime.Block;
            presentedEnergy=context.Runtime.Energy;
            HoldPlayerHealth(true);HoldPlayerBlock(true);
            foreach(var enemy in enemies.Values){enemy.HoldHealth(true);enemy.HoldActionHud(true);}
        }
        public void EndEnemyPresentation()
        {
            enemyPresentation=false;HoldPlayerHealth(false);HoldPlayerBlock(false);
            foreach(var enemy in enemies.Values){enemy.HoldHealth(false);enemy.HoldActionHud(false);}
        }
        private void CaptureImpactAnchors()
        {
            impactAnchors.Clear();
            TryImpactAnchor(CombatTrace.PlayerId,player.rectTransform,out _);
            foreach(var pair in enemies)TryImpactAnchor(pair.Key.TargetId,pair.Value.sprite.rectTransform,out _);
        }

        private bool TryImpactAnchor(string id,RectTransform target,out Vector2 point)
        {
            var area=Root.rect;
            if(target!=null&&target.gameObject.activeInHierarchy&&area.width>0&&area.height>0)
            {
                var rect=target.rect;
                var chest=new Vector2(rect.center.x,Mathf.Lerp(rect.yMin,rect.yMax,.52f));
                Vector2 local=Root.InverseTransformPoint(target.TransformPoint(chest));
                point=new Vector2((local.x-area.xMin)/area.width,(local.y-area.yMin)/area.height);
                impactAnchors[id]=point;
                return true;
            }
            return impactAnchors.TryGetValue(id,out point);
        }

        private void ImpactEffect(CombatEvent entry,Vector2 anchor,BattleVfx.ImpactLook look,bool heavy,Color? tint=null)
        {
            var rect=BattleUi.Rect(Root,$"Contact VFX {entry.ActionId}:{entry.Index} {entry.TargetId}",anchor,anchor);
            rect.sizeDelta=new Vector2(160,160);
            var effect=rect.gameObject.AddComponent<BattleVfx>();
            StartCoroutine(effect.PresentImpact(look,heavy,tint));
        }

        private void PresentTraceEvent(CombatEvent entry,BattleVfx.ImpactLook attackLook,BattleSound sound,bool heavy=false,Color? tint=null)
        {
            bool playerTarget=entry.TargetId==CombatTrace.PlayerId;
            var target=playerTarget?null:HitView(entry);
            var sprite=playerTarget?player:target!=null?target.sprite:null;
            if(!TryImpactAnchor(entry.TargetId,sprite!=null?sprite.rectTransform:null,out var anchor))return;

            if(entry.Hit==null)
            {
                ImpactEffect(entry,anchor,BattleVfx.ImpactLook.Reaction,false);
                return;
            }

            var hit=entry.Hit.Result;
            if(playerTarget)
            {
                presentedHP=entry.Hit.HPAfter;presentedBlock=entry.Hit.BlockAfter;
                shownHP=presentedHP;hp.text=presentedHP+" / "+context.Run.MaxHP;
                BattleUi.SetBlockReadout(block,presentedBlock);
                if(hit.Blocked>0)blockPulse=.24f;
            }
            else if(target!=null)target.PresentVitals(entry.Hit.HPAfter,entry.Hit.BlockAfter);

            if(hit.Blocked==0&&hit.HPDamage==0)return;
            bool shielded=hit.HPDamage==0;
            bool reaction=entry.Cause==CombatCause.Reaction||entry.Cause==CombatCause.Spread||entry.Cause==CombatCause.Brand||entry.Cause==CombatCause.Resonance;
            var look=shielded?BattleVfx.ImpactLook.Guard:reaction?BattleVfx.ImpactLook.Reaction:attackLook;
            heavy|=hit.HPDamage>=12;
            ImpactEffect(entry,anchor,look,heavy,shielded||reaction?null:tint);
            ShowDamage(hit,target,anchor);
            Sounds.Play(shielded?BattleSound.EnemyBlock:sound);
            if(shielded)return;
            if(playerTarget)StartCoroutine(actor.Hit());
            else if(target!=null)StartCoroutine(target.Play(SpriteMotion.Hurt,heavy?1.15f:1));
        }

        public IEnumerator PresentEnemyTurn(CombatTrace trace,IReadOnlyList<EnemyActionResult> actions,Action<IntentType> announce)
        {
            bool poisoned=false;
            foreach(var entry in trace.Events.Where(e=>e.Hit!=null&&e.Cause==CombatCause.PoisonTick))
            {
                PresentTraceEvent(entry,BattleVfx.ImpactLook.Magic,BattleSound.EnemyHurt);
                poisoned=true;
            }
            if(poisoned)yield return new WaitForSecondsRealtime(BattleFeedback.AttackRecovery);

            var actionMarkers=trace.Events.Where(e=>e.Kind==CombatEventKind.EnemyAction).ToArray();
            for(int i=0;i<actions.Count;i++)
            {
                int parent=actionMarkers[i].Index;
                var hits=trace.Events.Where(e=>e.ParentEventIndex==parent&&e.Hit!=null).ToArray();
                announce?.Invoke(actions[i].Intent.Type);
                yield return PresentEnemy(actions[i],hits);
            }
            foreach(var entry in trace.Events.Where(e=>e.Kind==CombatEventKind.Block&&e.Cause==CombatCause.TurnStart))
                PresentResourceEvent(entry);
        }

        private IEnumerator PresentEnemy(EnemyActionResult result,CombatEvent[] hits)
        {
            var source=EnemyFor(result.Actor);
            if(source==null)yield break;
            var look=BattleFeedback.Load()?.Look(result.Actor.Definition.AssetId)??EnemyAttackLook.Claw;
            bool ranged=look!=EnemyAttackLook.Claw&&look!=EnemyAttackLook.Heavy;
            var hitsOnPlayer=hits.Where(e=>e.TargetId==CombatTrace.PlayerId).ToArray();
            var counters=hits.Where(e=>e.Cause==CombatCause.Counter).ToArray();
            var rect=BattleUi.Rect(stage,"Enemy action VFX",Vector2.zero,Vector2.one);
            var fx=rect.gameObject.AddComponent<BattleVfx>();
            fx.BindActors(source.sprite.rectTransform,player.rectTransform);
            StartCoroutine(source.PresentAction(result.Intent,hitsOnPlayer.Length));
            yield return fx.PresentEnemy(result.Intent,look,hitsOnPlayer.Length,index=>
            {
                source.RevealActionHud(result.Intent.Type!=IntentType.Attack);
                if(result.Intent.Type==IntentType.Attack)
                {
                    var sound=look==EnemyAttackLook.Heavy?BattleSound.EnemyHeavy:look==EnemyAttackLook.Magic?BattleSound.EnemyMagic:
                        look==EnemyAttackLook.Venom?BattleSound.EnemyVenom:look==EnemyAttackLook.Fire?BattleSound.EnemyFire:
                        look==EnemyAttackLook.Spark?BattleSound.EnemySpark:BattleSound.EnemyClaw;
                    PresentTraceEvent(hitsOnPlayer[index],ranged?BattleVfx.ImpactLook.Magic:BattleVfx.ImpactLook.Strike,sound,look==EnemyAttackLook.Heavy);
                    if(index==hitsOnPlayer.Length-1)
                    {
                        foreach(var counter in counters)PresentTraceEvent(counter,BattleVfx.ImpactLook.Strike,BattleSound.EnemyHurt);
                    }
                }
                else if(result.Intent.Type==IntentType.Block)
                {Sounds.Play(BattleSound.EnemyBlock);StartCoroutine(Feedback(DamageNumber.Kind.BlockGain,result.Intent.Amount,source));}
                else if(result.Intent.Type==IntentType.Buff)
                {Sounds.Play(BattleSound.EnemyBuff);StartCoroutine(Feedback(DamageNumber.Kind.Strength,result.Intent.Amount,source));}
            });
        }

        public void HoldPlayerHealth(bool hold)
        {holdPlayerHealth=hold;if(!hold)hp.text=context.Run.CurrentHP+" / "+context.Run.MaxHP;}

        private void RevealCardResources()
        {
            HoldPlayerHealth(false);HoldPlayerBlock(false);
            if(!cardPresentation)return;
            if(cardBlockGain>0){blockPulse=.24f;StartCoroutine(Feedback(DamageNumber.Kind.BlockGain,cardBlockGain));}
            if(cardHealGain>0)StartCoroutine(Feedback(DamageNumber.Kind.Heal,cardHealGain));
            if(cardStrengthGain>0)StartCoroutine(Feedback(DamageNumber.Kind.Strength,cardStrengthGain));
        }

        public IEnumerator PresentCard(CardDefinition definition,Action<CombatEvent> contact,Action delivered=null)
        {
            var result=context.Runtime.LastCardResult;
            bool priest=definition.Build==CardBuild.Blessing||definition.Build==CardBuild.Weakness||definition.Build==CardBuild.Brand;
            if(definition.IsReworked||priest||definition.Owner==CharacterId.Mage&&!definition.StarterOnly||definition.ElementApplication==ElementApplication.SharedAttack||result.Events.Any(e=>e.Cause==CombatCause.Brand||e.Cause==CombatCause.FirstHeal))
            {
                yield return PresentOrderedCard(definition,result,contact);
                yield break;
            }
            bool attack=definition.Type==CardType.Attack&&cardTarget!=null;
            // Ground-collapse clips remain available as assets. Battle uses a short, target-local contact instead.
            if(attack&&(definition.StarterOnly||definition.Build!=CardBuild.None||definition.Id=="earth_strike"||definition.Id=="earth_crush"||
                definition.TargetScope!=CardTargetScope.Single||definition.ElementApplication==ElementApplication.SharedAttack))
            {
                yield return PresentLocalCard(definition,result,contact);
                yield break;
            }

            var target=cardTarget??EnemyFor(context.Runtime.Enemies.FirstOrDefault(enemy=>enemy.IsAlive))??Enemy;
            bool enhanced=BattleFeedback.Enhanced&&BattleVfx.HasWarriorClip(definition.Id);
            if(enhanced&&definition.Id=="stone_guard")Sounds.Play(BattleSound.StoneAssemble);
            else if(enhanced&&definition.Id=="wind_read")Sounds.Play(BattleSound.WindRead);
            bool starterGuard=(definition.StarterOnly||definition.Build!=CardBuild.None)&&definition.Type!=CardType.Attack;
            if(starterGuard||definition.Id=="stone_guard"||definition.Id=="wind_read")
                StartCoroutine(actor.CastSupport(starterGuard||definition.Id=="stone_guard"));

            var hits=result.HitEvents;
            var rect=BattleUi.Rect(stage,"Card VFX "+definition.Id,Vector2.zero,Vector2.one);
            var fx=rect.gameObject.AddComponent<BattleVfx>();
            fx.BindActors(player.rectTransform,target.sprite.rectTransform);
            void Hit(int index)
            {
                var entry=hits[index];
                ShowCardContact(definition,entry);
                contact?.Invoke(entry);
            }
            yield return fx.Present(definition,()=>
            {
                if(attack)actor.ShowAttackContact();
                if(cardTarget!=null&&hits.Count==0){cardTarget.HoldHealth(false);cardTarget.RevealActionHud(false);}
                RevealCardResources();
                if(starterGuard||enhanced&&definition.Id=="stone_guard")Sounds.Play(BattleSound.StoneLock);
                if(hits.Count>0)Hit(0);
            },result.ConditionalTriggered,Hit,hits.Count,()=>
            {
                if(enhanced&&definition.Id=="wind_read")Sounds.Play(BattleSound.CardDraw);
                delivered?.Invoke();
            },()=>{if(enhanced&&definition.Id=="wind_cut")Sounds.Play(BattleSound.WindRelease);},
            seconds=>{if(attack)StartCoroutine(actor.Attack(seconds));});
            while(attack&&actor.IsAttacking)yield return null;
        }
        private IEnumerator PresentOrderedCard(CardDefinition card,CardResult result,Action<CombatEvent> contact)
        {
            bool attack=card.Type==CardType.Attack&&cardTarget!=null;
            if(attack)StartCoroutine(actor.Attack(BattleFeedback.PlayerContact));
            else StartCoroutine(actor.CastSupport(false));
            yield return new WaitForSecondsRealtime(BattleFeedback.PlayerContact);
            if(attack)actor.ShowAttackContact();
            CombatEvent previous=null;
            foreach(var entry in result.Events)
            {
                bool mark=entry.Kind==CombatEventKind.ElementMark;
                bool reaction=entry.Kind==CombatEventKind.Reaction;
                if(entry.Kind==CombatEventKind.Draw&&entry.After>entry.Before)Sounds.Play(BattleSound.CardDraw);
                if(entry.Kind==CombatEventKind.CardRecovered)
                {StartCoroutine(FloatNumber("카드 회수 +1",null,BattleUi.Teal));Sounds.Play(BattleSound.CardDraw);}
                if(entry.Kind==CombatEventKind.Energy&&entry.Cause==CombatCause.KillRefund&&entry.After>entry.Before)
                    StartCoroutine(FloatNumber("행동력 +"+(entry.After-entry.Before),null,BattleUi.Gold));
                bool statusEvent=entry.Kind==CombatEventKind.Status;
                bool resource=entry.Kind==CombatEventKind.Heal||entry.Kind==CombatEventKind.Block;
                if(entry.Hit==null&&!statusEvent&&!resource&&!mark&&!reaction)continue;
                bool areaBeat=card.TargetScope==CardTargetScope.AllEnemies&&previous!=null&&
                    previous.Kind==entry.Kind&&previous.Cause==entry.Cause&&previous.TargetId!=entry.TargetId;
                if(previous!=null&&!areaBeat)yield return new WaitForSecondsRealtime(.06f);
                if(entry.Hit!=null){ShowCardContact(card,entry);contact?.Invoke(entry);}
                else if(mark||reaction)
                {
                    var target=HitView(entry);
                    if(mark)target?.PresentMark(entry.NewElement);
                    else
                    {
                        string caption=entry.Reaction switch
                        {
                            ElementReaction.RapidFreeze=>"급속 빙결",ElementReaction.FrostGust=>"서리 돌풍",
                            ElementReaction.PoisonSpread=>"독 확산",ElementReaction.VenomBurn=>"맹독 연소",
                            ElementReaction.Overload=>"과부하",ElementReaction.Shock=>"감전",_=>null
                        };
                        if(caption!=null)StartCoroutine(FloatNumber(caption,target,BattleUi.Gold));
                        PresentTraceEvent(entry,BattleVfx.ImpactLook.Reaction,BattleSound.EnemyMagic);
                    }
                }
                else if(resource||entry.TargetId==CombatTrace.PlayerId)PresentResourceEvent(entry);
                else
                {
                    var target=HitView(entry);
                    target?.PresentStatus(entry);
                    if(entry.After!=entry.Before&&entry.Cause!=CombatCause.Death)
                    {
                        string caption=entry.Status==StatusEffectType.Brand?(entry.After>0?"낙인 +1":"낙인 소모"):
                            entry.Status==StatusEffectType.Weak?"약화 "+entry.After:
                            entry.Status==StatusEffectType.Poison?"독 "+entry.After:
                            entry.Status==StatusEffectType.RapidFreeze?"다음 공격 −"+entry.After:
                            entry.Status==StatusEffectType.VenomBurn?"독 강화 "+entry.After+"회":null;
                        if(caption!=null)StartCoroutine(FloatNumber(caption,target,BattleUi.Teal));
                        if(entry.After>entry.Before)PresentTraceEvent(entry,BattleVfx.ImpactLook.Magic,BattleSound.EnemyMagic);
                    }
                }
                previous=entry;
            }
            yield return new WaitForSecondsRealtime(BattleFeedback.AttackRecovery);
            while(attack&&actor.IsAttacking)yield return null;
        }
        private void PresentResourceEvent(CombatEvent entry)
        {
            if(entry.TargetId!=CombatTrace.PlayerId)return;
            if(entry.Cause==CombatCause.Cleanse)
            {
                string state=entry.Status==StatusEffectType.Poison?"독":"약화";
                StartCoroutine(FloatNumber(state+" 정화",null,BattleUi.Teal));return;
            }
            if(entry.Kind==CombatEventKind.Heal)
            {
                presentedHP=entry.After;shownHP=entry.After;hp.text=entry.After+" / "+context.Run.MaxHP;
                if(entry.ActualHealing>0)StartCoroutine(Feedback(DamageNumber.Kind.Heal,entry.ActualHealing));
            }
            else if(entry.Kind==CombatEventKind.Block)
            {
                presentedBlock=entry.After;BattleUi.SetBlockReadout(block,entry.After);
                if(entry.After>entry.Before)
                {
                    blockPulse=.24f;StartCoroutine(Feedback(DamageNumber.Kind.BlockGain,entry.After-entry.Before));
                }
            }
            else if(entry.Kind==CombatEventKind.Status&&entry.Status==StatusEffectType.Strength)
                StartCoroutine(Feedback(DamageNumber.Kind.Strength,entry.After-entry.Before));
            else if(entry.Kind==CombatEventKind.Status&&entry.After>entry.Before&&entry.EffectId!=null)
            {
                var card=context.Runtime.Data.AllCards.FirstOrDefault(c=>c.Id==entry.EffectId);
                if(card!=null)StartCoroutine(FloatNumber(card.Name,null,BattleUi.Teal));
            }
            if(entry.After>entry.Before)
            {
                if(TryImpactAnchor(CombatTrace.PlayerId,player.rectTransform,out var anchor))
                    ImpactEffect(entry,anchor,entry.Kind==CombatEventKind.Block?BattleVfx.ImpactLook.Guard:BattleVfx.ImpactLook.Magic,false);
                Sounds.Play(BattleSound.StoneLock);
            }
        }

        private IEnumerator PresentLocalCard(CardDefinition card,CardResult result,Action<CombatEvent> contact)
        {
            if(card.Id=="earth_strike"||card.PresentationId=="earth_strike"||card.PresentationId=="earth_crush")Sounds.Play(BattleSound.EarthPrepare);
            StartCoroutine(actor.Attack(BattleFeedback.PlayerContact));
            yield return new WaitForSecondsRealtime(BattleFeedback.PlayerContact);
            actor.ShowAttackContact();
            RevealCardResources();

            var reactionsWithDamage=new HashSet<int>(result.HitEvents.Where(hit=>hit.Cause==CombatCause.Reaction&&hit.Hit.Result.HPDamage>0&&hit.ParentEventIndex.HasValue)
                .Select(hit=>hit.ParentEventIndex.Value));
            CombatEvent previous=null;
            foreach(var entry in result.Events)
            {
                bool reactionCue=entry.Reaction.HasValue&&!reactionsWithDamage.Contains(entry.Index)||
                    entry.Kind==CombatEventKind.Status&&entry.Cause==CombatCause.Spread;
                if(entry.Hit==null&&!reactionCue)continue;
                // Consecutive area hits share a beat, while chain and reaction events retain their order.
                bool areaBeat=card.TargetScope==CardTargetScope.AllEnemies&&previous!=null&&
                    previous.Cause==CombatCause.DirectAttack&&entry.Cause==CombatCause.DirectAttack&&previous.TargetId!=entry.TargetId;
                if(previous!=null&&!areaBeat)yield return new WaitForSecondsRealtime(BattleVfx.MultiHitInterval*.60f);
                ShowCardContact(card,entry);
                if(entry.Hit!=null)contact?.Invoke(entry);
                previous=entry;
            }
            yield return new WaitForSecondsRealtime(BattleFeedback.AttackRecovery);
            while(actor.IsAttacking)yield return null;
        }

        private void ShowCardContact(CardDefinition card,CombatEvent entry)
        {
            bool martial=card.StarterOnly?card.Owner==CharacterId.Warrior:
                card.Element==ElementType.None&&card.Owner==CharacterId.Warrior||card.Element==ElementType.Earth||card.Element==ElementType.Stone||card.Element==ElementType.Wind;
            var look=martial?BattleVfx.ImpactLook.Strike:BattleVfx.ImpactLook.Magic;
            var cue=card.PresentationId??card.Id;
            var sound=cue=="earth_strike"||cue=="earth_crush"?BattleSound.EarthHit:cue=="wind_cut"?BattleSound.WindHit:BattleSound.EnemyHurt;
            if(sound==BattleSound.EnemyHurt)
            {
                switch(card.Element)
                {
                    case ElementType.Fire:case ElementType.Explosion:sound=BattleSound.EnemyFire;break;
                    case ElementType.Lightning:sound=BattleSound.EnemySpark;break;
                    case ElementType.Poison:sound=BattleSound.EnemyVenom;break;
                    default:if(!martial)sound=BattleSound.EnemyMagic;break;
                }
            }
            if(card.IsFormalCard)
            {
                look=card.Element switch
                {
                    ElementType.Wind=>BattleVfx.ImpactLook.Gust,
                    ElementType.Fire=>BattleVfx.ImpactLook.Flame,
                    ElementType.Ice=>BattleVfx.ImpactLook.Frost,
                    ElementType.Lightning=>BattleVfx.ImpactLook.Lightning,
                    ElementType.None when cue=="earth_strike"||cue=="earth_crush"=>BattleVfx.ImpactLook.Stone,
                    ElementType.None when card.Owner==CharacterId.Priest=>BattleVfx.ImpactLook.Holy,
                    ElementType.None when card.Owner==CharacterId.Mage=>BattleVfx.ImpactLook.Arcane,
                    _=>look
                };
            }
            Color? tint=card.Element==ElementType.None?(Color?)null:CardView.ElementTint(card.Element);
            PresentTraceEvent(entry,look,sound,card.BaseDamage>=12,tint);
        }
        public void ShowRecoveryChoice(IReadOnlyList<CardInstance> candidates,Action<string> choose,Action cancel)
        {
            CloseCleanseChoice();
            cleansePanel=BattleUi.Panel(Root,"Recovery choice",new Vector2(.27f,.23f),new Vector2(.73f,.77f));
            BattleUi.Text(cleansePanel,"Recovery title","회수할 카드 한 장 선택",art.common.font,28,BattleUi.Pale,new Vector2(.05f,.85f),new Vector2(.95f,.97f));
            var viewport=BattleUi.Rect(cleansePanel,"Recovery viewport",new Vector2(.07f,.20f),new Vector2(.93f,.82f));
            var surface=viewport.gameObject.AddComponent<Image>();surface.color=new Color(.02f,.04f,.05f,.8f);surface.raycastTarget=true;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content=BattleUi.Rect(viewport,"Recovery entries",new Vector2(0,1),Vector2.one);content.pivot=new Vector2(.5f,1);
            content.sizeDelta=new Vector2(0,candidates.Count*64);
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;
            scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;
            for(int i=0;i<candidates.Count;i++)
            {
                var instance=candidates[i];int row=i;
                var button=BattleUi.Button(content,"Recover_"+instance.InstanceId,context.Runtime.Data.Card(instance.CardId).Name+"  ·  "+(row+1),art.common,
                    new Vector2(0,1),Vector2.one,()=>choose(instance.InstanceId));
                var rect=(RectTransform)button.transform;rect.pivot=new Vector2(.5f,1);rect.sizeDelta=new Vector2(0,56);rect.anchoredPosition=new Vector2(0,-row*64);
            }
            BattleUi.Button(cleansePanel,"RecoveryCancel","취소",art.common,new Vector2(.34f,.04f),new Vector2(.66f,.15f),cancel);
        }
        public void ShowCleanseChoice(Action<StatusEffectType> choose,Action cancel)
        {
            CloseCleanseChoice();
            cleansePanel=BattleUi.Panel(Root,"Cleanse choice",new Vector2(.30f,.39f),new Vector2(.70f,.64f));
            BattleUi.Text(cleansePanel,"Cleanse title","제거할 상태 한 종류 선택",art.common.font,28,BattleUi.Pale,new Vector2(.06f,.66f),new Vector2(.94f,.94f));
            BattleUi.Button(cleansePanel,"CleansePoison","독 전체 제거",art.common,new Vector2(.05f,.30f),new Vector2(.48f,.59f),()=>choose(StatusEffectType.Poison));
            BattleUi.Button(cleansePanel,"CleanseWeak","약화 전체 제거",art.common,new Vector2(.52f,.30f),new Vector2(.95f,.59f),()=>choose(StatusEffectType.Weak));
            BattleUi.Button(cleansePanel,"CleanseCancel","취소",art.common,new Vector2(.34f,.04f),new Vector2(.66f,.25f),cancel);
        }
        public void CloseCleanseChoice()
        {if(cleansePanel!=null){cleansePanel.gameObject.SetActive(false);Destroy(cleansePanel.gameObject);cleansePanel=null;}}
        public void ShowReward(Action<RewardType,string> choose)
        {
            if(overlay!=null)return;
            var panel=Overlay("RewardOverlay");var reward=context.Runtime.Reward;
            foreach(var diagnostic in reward.Diagnostics)Debug.LogWarning(diagnostic);
            rewardPanel=panel;
            BattleUi.Text(panel,"Reward title",reward.Formal?"전투 승리  /  보상 1개 선택":"전리품  /  보상 1개 선택",art.common.font,43,BattleUi.Gold,new Vector2(.08f,.85f),new Vector2(.92f,.97f));
            string choice=reward.IsOpeningReward?"고른 카드는 다음 전투의 첫 손패에 들어갑니다.":"카드 1장 또는 추가 골드를 선택하세요.";
            string instructions=reward.Formal?$"기본 골드 +{reward.BaseGold}G 지급 완료 · 보유 {context.Run.Gold}G\n{choice}":
                "카드 1장 또는 골드를 선택하세요. 원하지 않으면 스킵할 수 있습니다.";
            if(context.Run.LastItemReward!=null)instructions+="\n유물 획득: "+Item50Catalog.Find(context.Run.LastItemReward).Name;
            BattleUi.Text(panel,"Reward instructions",instructions,art.common.font,22,BattleUi.Pale,new Vector2(.05f,.77f),new Vector2(.95f,.85f));
            var row=BattleUi.Rect(panel,"Reward cards",new Vector2(.045f,.245f),new Vector2(.955f,.755f));
            for(int i=0;i<reward.Candidates.Count;i++)
            {
                string id=reward.Candidates[i];var card=Instantiate(art.cardPrefab,row,false);card.name="Reward_"+id;
                card.Bind(context.Runtime.Data.Card(id),id,art,context.Runtime.Data,_=>choose(RewardType.Card,id));
                if(reward.IsOpeningReward)
                    card.element.text=card.Definition.Build switch
                    {
                        CardBuild.Fortress=>"철벽",
                        CardBuild.Counter=>"반격",
                        CardBuild.BlockSpend=>"방어도 소모",
                        CardBuild.Sniper=>"비전 저격",
                        CardBuild.Resonance=>"원소 공명",
                        CardBuild.Annihilation=>"광역 섬멸",
                        CardBuild.Blessing=>"축복",
                        CardBuild.Weakness=>"약화",
                        CardBuild.Brand=>"낙인",
                        _=>card.element.text
                    };
                var slot=(RectTransform)card.transform;
                float count=reward.Candidates.Count;
                slot.anchorMin=new Vector2(i/count,0);slot.anchorMax=new Vector2((i+1)/count,1);
                slot.offsetMin=new Vector2(12,4);slot.offsetMax=new Vector2(-12,-4);
            }
            BattleUi.Button(panel,"RewardGold",(reward.Formal?"카드 대신 추가 골드 +":"골드 +")+reward.Gold,art.common,reward.Formal?new Vector2(.23f,.09f):new Vector2(.20f,.09f),reward.Formal?new Vector2(.77f,.21f):new Vector2(.48f,.21f),()=>choose(RewardType.Gold,null));
            if(!reward.Formal)BattleUi.Button(panel,"RewardSkip","SKIP / 건너뛰기",art.common,new Vector2(.53f,.09f),new Vector2(.81f,.21f),()=>choose(RewardType.Skip,null));
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
            if(selectedCard!=null)BattleUi.PositionAbove(playerAction,actor.IdleTop);
            if(context==null)return;if(!holdPlayerHealth)shownHP=Mathf.MoveTowards(shownHP,context.Run.CurrentHP,Time.unscaledDeltaTime*context.Run.MaxHP*3);BattleUi.SetBar(hpBar,shownHP/context.Run.MaxHP);
            energyPulse=Mathf.Max(0,energyPulse-Time.unscaledDeltaTime);
            EnergyLabel.color=Color.Lerp(BattleUi.Gold,Color.white,energyPulse);
            blockPulse=Mathf.Max(0,blockPulse-Time.unscaledDeltaTime);block.transform.localScale=Vector3.one*(1+blockPulse*.65f);
        }
    }
}

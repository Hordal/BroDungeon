using System;
using System.Collections;
using System.Linq;
using Brodun.Flow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
namespace Brodun.Presentation
{
    public sealed partial class BattleController : MonoBehaviour
    {
        public BattleContext Context { get; private set; }
        public BattleView View { get; private set; }
        public event Action<string> AudioCue;
        private GameFlow flow;
        private bool ready;
        private GameAudio gameAudio;
        private string pendingCardId,pendingCleanseId,pendingRecoveryId;
        private CardView draggedCard;
        public void Bind(GameFlow owner,BattleContext context,BattlePresentationAssets art)
        {
            if(context==null||art==null)throw new InvalidOperationException("Battle scene needs persistent context and presentation references.");
            flow=owner;Context=context;View=gameObject.AddComponent<BattleView>();View.Build(context,art,CardClicked,EndTurn,ChooseTarget);
            View.SetInput(false);
            gameAudio=GameAudio.Ensure();gameAudio.SettingsVisibilityChanged+=AudioSettingsVisibility;
        }
        private void AudioSettingsVisibility(bool visible)
        {
            if(flow.IsPauseMenuOpen)return;
            if(visible)CancelTargeting();
            View.SetInput(ready&&!visible);
        }
        public void Ready()
        {
            ready=true;
            // A scene reload reattaches to the resolved model; it never replays the effect or rerolls.
            Context.FinishActionPresentation();
            if(Context.Phase==BattlePhase.Victory)StartCoroutine(FinishBattle());
            else if(Context.Phase==BattlePhase.Reward){View.ShowReward(ChooseReward);View.SetInput(false);}
            else if(Context.Phase==BattlePhase.Defeat){View.SetInput(false);flow.FinishRun();}
            else if(Context.Phase==BattlePhase.Finished){if(Context.Run.Status==RunStatus.Complete)flow.FinishRun();else flow.ReturnFromBattle();}
            else if(Context.Runtime.PendingDiscard)StartCoroutine(ResumeDiscard());
            else {View.Refresh();View.SetInput(true);View.Announce("PLAYER TURN");}
        }
        private void CardClicked(CardView card){PlayCard(card.InstanceId);}
        private bool CanInput=>!selecting&&pendingCleanseId==null&&pendingRecoveryId==null&&ready&&!(gameAudio!=null&&gameAudio.IsSettingsOpen)&&!flow.Session.IsTransitioning&&!flow.IsPauseMenuOpen&&!flow.IsCardLibraryOpen;
        public bool PlayCard(string id)
        {
            if(draggedCard!=null||!CanInput||!Context.CanPlay(id))return false;
            var card=View.Cards.FirstOrDefault(c=>c.InstanceId==id);
            var definition=Context.Runtime.Definition(Context.Runtime.Deck.Hand.Single(c=>c.InstanceId==id));
            var target=definition.RequiresTargetSelection?Context.Runtime.ResolveEnemyTarget(null):null;
            if(definition.RequiresTargetSelection&&target==null)
            {
                if(card==null)return false;
                if(pendingCardId==id)CancelTargeting();
                else SelectTargetCard(card);
                return true;
            }
            return CommitCard(id,card,definition,target);
        }
        private void ChooseTarget(EnemyInstance target)
        {
            if(draggedCard==null)TryChooseTarget(target);
        }
        private bool TryChooseTarget(EnemyInstance target)
        {
            if(pendingCardId==null||!CanInput||!Context.CanPlay(pendingCardId)||target==null||!target.IsAlive)return false;
            var card=View.Cards.FirstOrDefault(candidate=>candidate.InstanceId==pendingCardId);
            return card!=null&&CommitCard(card.InstanceId,card,card.Definition,card.Definition.RequiresTargetSelection?target:null);
        }
        private void SelectTargetCard(CardView card)
        {
            pendingCardId=card.InstanceId;
            View.SetTargeting(card,draggedCard!=null);
        }
        internal void BeginTargetDrag(CardView card,PointerEventData pointer)
        {
            if(draggedCard!=null||!CanInput||!Context.CanPlay(card.InstanceId))return;
            draggedCard=card;
            SelectTargetCard(card);
            MoveTargetDrag(card,pointer);
        }
        internal void MoveTargetDrag(CardView card,PointerEventData pointer)
        {
            if(draggedCard!=card)return;
            bool overTarget=card.Definition.RequiresEnemyTarget
                ?TargetUnderPointer(pointer)!=null
                :View.IsPlayerTarget(pointer.pointerCurrentRaycast.gameObject);
            View.ShowTargetDrag(pointer.position,pointer.pressEventCamera,overTarget);
        }
        internal void EndTargetDrag(CardView card,PointerEventData pointer)
        {
            if(draggedCard!=card)return;
            bool applied=card.Definition.RequiresEnemyTarget
                ?TryChooseTarget(TargetUnderPointer(pointer))
                :View.IsPlayerTarget(pointer.pointerCurrentRaycast.gameObject)&&CanInput&&Context.CanPlay(card.InstanceId)
                    &&CommitCard(card.InstanceId,card,card.Definition,null);
            if(!applied)CancelTargeting();
        }
        private static EnemyInstance TargetUnderPointer(PointerEventData pointer)
        {
            var hit=pointer.pointerCurrentRaycast.gameObject;
            var enemy=hit!=null?hit.GetComponentInParent<EnemyView>()?.Model:null;
            return enemy!=null&&enemy.IsAlive?enemy:null;
        }
        internal void CancelTargetDrag(CardView card)
        {
            if(draggedCard==card)CancelTargeting();
        }
        private void CancelTargeting()
        {
            if(pendingCardId==null&&pendingCleanseId==null&&pendingRecoveryId==null)return;
            bool cleansing=pendingCleanseId!=null||pendingRecoveryId!=null;pendingCleanseId=null;pendingRecoveryId=null;View.CloseCleanseChoice();
            pendingCardId=null;
            draggedCard=null;
            View.SetTargeting(null);
            if(cleansing)View.SetInput(CanInput);
        }
        private bool CommitCard(string id,CardView card,CardDefinition definition,EnemyInstance target,StatusEffectType? cleanse=null,string recovery=null,CardPlayOptions options=null)
        {
            if(options==null&&definition.IsReworked&&
                (definition.Id=="N05"||definition.Id=="N10"||definition.Id=="N16"||definition.Id=="M11"&&Context.Runtime.Energy>=definition.Cost+2))
            {
                CancelTargeting();
                StartCoroutine(SelectAndPlay(id,target));
                return true;
            }
            var recoveryChoices=Context.Runtime.RecoveryChoices(definition);
            if(recovery==null&&recoveryChoices.Count>1)
            {
                CancelTargeting();pendingRecoveryId=id;pendingCardId=id;View.SetInput(false);
                View.ShowRecoveryChoice(recoveryChoices,chosen=>
                {
                    if(pendingRecoveryId!=id)return;
                    CancelTargeting();
                    if(CanInput&&Context.CanPlay(id))CommitCard(id,card,definition,target,cleanse,chosen);
                },CancelTargeting);
                return true;
            }
            if(!cleanse.HasValue&&Context.Runtime.CleanseChoices(definition).Count>1)
            {
                CancelTargeting();pendingCleanseId=id;pendingCardId=id;View.SetInput(false);
                View.ShowCleanseChoice(choice=>
                {
                    if(pendingCleanseId!=id)return;
                    CancelTargeting();
                    if(CanInput&&Context.CanPlay(id))CommitCard(id,card,definition,target,choice);
                },CancelTargeting);
                return true;
            }
            View.BeginCardPresentation(target,definition.RequiresEnemyTarget);
            if(!Context.Play(id,target?.TargetId,cleanse,recovery,options)){View.EndCardPresentation();return false;}
            CancelTargeting();
            View.RecordCardGains();
            AudioCue?.Invoke("card");View.Refresh();View.SetInput(false);
            StartCoroutine(ResolveCard(card,definition));return true;
        }
        private IEnumerator ResolveCard(CardView card,CardDefinition definition)
        {
            // Wind up while the card leaves the hand. CommitCard already locked input.
            var usedCard=card!=null?StartCoroutine(card.PresentUse(definition.Exhausts)):null;
            bool handDelivered=false;
            Action delivery=definition.Id=="wind_read"?(Action)(()=>
            {
                View.RefreshHand(CardClicked);View.Refresh();View.SetInput(false);handDelivered=true;
            }):null;
            yield return View.PresentCard(definition,entry=>
            {
                if(entry.Hit.Result.HPDamage>0)AudioCue?.Invoke("hit");
            },delivery);
            if(usedCard!=null)yield return usedCard;
            if(Context.Runtime.PendingDiscard)
            {
                View.RefreshHand(CardClicked);
                View.SetInput(false);
                yield return ChooseDiscard();
            }
            if(Context.Runtime.Result!=BattleResult.Won)yield return View.PresentDeaths();
            if(!handDelivered)View.RefreshHand(CardClicked);View.EndCardPresentation();Context.FinishActionPresentation();View.Refresh();
            if(Context.Phase==BattlePhase.Victory)yield return FinishBattle();else View.SetInput(true);
        }
        public void EndTurn(){TryEndTurn();}
        public bool TryEndTurn()
        {
            if(!CanInput||pendingCardId!=null||Context.Phase!=BattlePhase.PlayerTurn)return false;
            View.BeginEnemyPresentation();
            if(!Context.EndTurn()){View.EndEnemyPresentation();return false;}
            View.SetInput(false);View.Refresh();StartCoroutine(EnemyTurn());return true;
        }
        private IEnumerator EnemyTurn()
        {
            View.Announce("ENEMY TURN");View.HideHandForEnemyTurn();
            var runtime=Context.Runtime;
            yield return View.PresentEnemyTurn(runtime.LastTurnResult,runtime.LastEnemyActions,
                type=>AudioCue?.Invoke(type==IntentType.Attack?"attack":type==IntentType.Block?"block":"buff"));
            // A counter can kill the acting enemy. Retire views after their recorded contacts.
            if(runtime.Result!=BattleResult.Won)yield return View.PresentDeaths();
            View.EndEnemyPresentation();View.RefreshHand(CardClicked);Context.FinishActionPresentation();View.Refresh();
            if(Context.Phase==BattlePhase.Victory)yield return FinishBattle();
            else if(Context.Phase==BattlePhase.Defeat){AudioCue?.Invoke("defeat");View.SetInput(false);yield return new WaitForSecondsRealtime(.35f);flow.FinishRun();}
            else {View.SetInput(true);View.Announce("PLAYER TURN");}
        }
        private IEnumerator FinishBattle()
        {
            GameAudio.Ensure().BattleComplete();
            View.SetInput(false);AudioCue?.Invoke("death");yield return View.PresentDeaths();
            View.Announce("VICTORY");AudioCue?.Invoke("victory");yield return new WaitForSecondsRealtime(.75f);
            if(Context.Room.Type==RoomType.Boss)flow.FinishRun();
            else if(Context.OpenReward())View.ShowReward(ChooseReward);
        }
        public void ChooseReward(RewardType type,string cardId){TryChooseReward(type,cardId);}
        public bool TryChooseReward(RewardType type,string cardId=null)
        {
            if(!ready||flow.Session.IsTransitioning||flow.IsPauseMenuOpen||flow.IsCardLibraryOpen||gameAudio.IsSettingsOpen||!Context.ResolveReward(type,cardId))return false;
            View.LockReward();View.Refresh();View.SetInput(false);AudioCue?.Invoke("reward");
            StartCoroutine(RewardFeedback(type,cardId));return true;
        }
        private IEnumerator RewardFeedback(RewardType type,string card)
        {
            string feedback=type==RewardType.Gold?"+"+Context.Runtime.Reward.Gold+" GOLD":type==RewardType.Card?Context.Runtime.Data.Card(card).Name+" 획득":"보상을 건너뛰었습니다";
            yield return View.RewardConfirmation(feedback);flow.ReturnFromBattle();
        }
        private void Start(){if(GameFlow.Instance==null)GameFlow.LoadBoot();}
        internal bool TryCancelSelection()
        {
            if(selecting||Context.Runtime.PendingDiscard)return true;
            if(pendingCardId==null&&pendingCleanseId==null&&pendingRecoveryId==null)return false;
            CancelTargeting();
            return true;
        }
        private void Update()
        {
            if(Mouse.current?.rightButton.wasPressedThisFrame==true)TryCancelSelection();
        }
        private void OnDestroy(){if(gameAudio!=null)gameAudio.SettingsVisibilityChanged-=AudioSettingsVisibility;}
    }
}

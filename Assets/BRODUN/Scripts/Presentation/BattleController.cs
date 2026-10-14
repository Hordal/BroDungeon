using System;
using System.Collections;
using System.Linq;
using Brodun.Flow;
using UnityEngine;
namespace Brodun.Presentation
{
    public sealed class BattleController : MonoBehaviour
    {
        public BattleContext Context { get; private set; }
        public BattleView View { get; private set; }
        public event Action<string> AudioCue;
        private GameFlow flow;
        private bool ready;
        public void Bind(GameFlow owner,BattleContext context,BattlePresentationAssets art)
        {
            if(context==null||art==null)throw new InvalidOperationException("Battle scene needs persistent context and presentation references.");
            flow=owner;Context=context;View=gameObject.AddComponent<BattleView>();View.Build(context,art,CardClicked,EndTurn);
            View.SetInput(false);
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
            else {View.Refresh();View.SetInput(true);View.Announce("PLAYER TURN");}
        }
        private void CardClicked(CardView card){PlayCard(card.InstanceId);}
        public bool PlayCard(string id)
        {
            if(!ready||flow.Session.IsTransitioning||!Context.CanPlay(id)){View.Message("지금은 사용할 수 없습니다");return false;}
            var card=View.Cards.FirstOrDefault(c=>c.InstanceId==id);
            var definition=Context.Runtime.Data.Card(Context.Runtime.Deck.Hand.Single(c=>c.InstanceId==id).CardId);
            View.Enemy.HoldHealth(true);
            if(!Context.Play(id,Context.EnemyTargetId)){View.Enemy.HoldHealth(false);return false;}
            AudioCue?.Invoke("card");View.Refresh();View.SetInput(false);
            StartCoroutine(ResolveCard(card,definition));return true;
        }
        private IEnumerator ResolveCard(CardView card,CardDefinition definition)
        {
            if(card!=null)yield return card.PresentUse(definition.Exhausts);
            var result=Context.Runtime.LastCardResult;
            yield return View.PresentCard(definition,()=>StartCoroutine(CardHitFeedback(result)));
            if(result.ConditionalTriggered)
                View.Message("COMBO / "+definition.Name);
            else View.Message(definition.Name+(definition.Exhausts?" · 소멸":" · 버림"));
            // Let hit reaction / multi-hit numbers settle, including the lethal hit, before Death.
            yield return new WaitForSecondsRealtime(Mathf.Max(.18f,result.Hits.Count*.13f));
            View.RefreshHand(CardClicked);Context.FinishActionPresentation();View.Refresh();
            if(Context.Phase==BattlePhase.Victory)yield return FinishBattle();else View.SetInput(true);
        }
        private IEnumerator CardHitFeedback(CardResult result)
        {
            foreach(var hit in result.Hits)
            {
                if(hit.HPDamage>0){AudioCue?.Invoke("hit");StartCoroutine(View.FloatNumber("−"+hit.HPDamage,true,BattleUi.Gold));}
                if(hit.Blocked>0)StartCoroutine(View.FloatNumber("방어 −"+hit.Blocked,true,BattleUi.Teal));
                if(result.Hits.Count>1)yield return new WaitForSecondsRealtime(.13f);
            }
        }
        public void EndTurn(){TryEndTurn();}
        public bool TryEndTurn()
        {
            if(!ready||flow.Session.IsTransitioning||Context.Phase!=BattlePhase.PlayerTurn)return false;
            var intent=Context.Runtime.CurrentIntent;
            View.HoldPlayerHealth(true);
            if(!Context.EndTurn()){View.HoldPlayerHealth(false);return false;}
            View.SetInput(false);View.Refresh(intent,true);StartCoroutine(EnemyTurn(intent));return true;
        }
        private IEnumerator EnemyTurn(IntentData announced)
        {
            View.Message("적의 행동");
            View.Announce("ENEMY TURN");View.HideHandForEnemyTurn();
            // Runtime.EndTurn is atomic. Keep its immutable executed intent visible during presentation.
            if(Context.Runtime.Result!=BattleResult.Won)
            {
                if(announced.Type==IntentType.Attack){AudioCue?.Invoke("attack");yield return View.Enemy.Play(SpriteMotion.Attack);}
                else yield return new WaitForSecondsRealtime(.30f);
                var action=Context.Runtime.LastEnemyAction;
                foreach(var hit in action.Hits)
                {
                    if(hit.HPDamage>0){StartCoroutine(View.FloatNumber("−"+hit.HPDamage,false,new Color(1,.42f,.33f)));StartCoroutine(View.PlayerHit());}
                    if(hit.Blocked>0)StartCoroutine(View.FloatNumber("방어 "+hit.Blocked,false,BattleUi.Teal));
                    yield return new WaitForSecondsRealtime(.16f);
                }
                // Let the last tiny hit flash settle before accepting the next action.
                if(action.Hits.Count>0)yield return new WaitForSecondsRealtime(.10f);
            }
            View.HoldPlayerHealth(false);View.RefreshHand(CardClicked);Context.FinishActionPresentation();View.Refresh();
            if(Context.Phase==BattlePhase.Victory)yield return FinishBattle();
            else if(Context.Phase==BattlePhase.Defeat){AudioCue?.Invoke("defeat");View.SetInput(false);yield return new WaitForSecondsRealtime(.35f);flow.FinishRun();}
            else {View.Message("카드를 선택하세요");View.SetInput(true);View.Announce("PLAYER TURN");}
        }
        private IEnumerator FinishBattle()
        {
            View.SetInput(false);AudioCue?.Invoke("death");yield return View.Enemy.Play(SpriteMotion.Death);
            View.Message("VICTORY");View.Announce("VICTORY");AudioCue?.Invoke("victory");yield return new WaitForSecondsRealtime(.75f);
            if(Context.Room.Type==RoomType.Boss)flow.FinishRun();
            else if(Context.OpenReward())View.ShowReward(ChooseReward);
        }
        public void ChooseReward(RewardType type,string cardId){TryChooseReward(type,cardId);}
        public bool TryChooseReward(RewardType type,string cardId=null)
        {
            if(!ready||flow.Session.IsTransitioning||!Context.ResolveReward(type,cardId))return false;
            View.LockReward();View.Refresh();View.SetInput(false);AudioCue?.Invoke("reward");
            StartCoroutine(RewardFeedback(type,cardId));return true;
        }
        private IEnumerator RewardFeedback(RewardType type,string card)
        {
            string feedback=type==RewardType.Gold?"+"+Context.Runtime.Reward.Gold+" GOLD":type==RewardType.Card?Context.Runtime.Data.Card(card).Name+" 획득":"보상을 건너뛰었습니다";
            View.Message(feedback);yield return View.RewardConfirmation(feedback);flow.ReturnFromBattle();
        }
        private void Start(){if(GameFlow.Instance==null)GameFlow.LoadBoot();}
    }
}

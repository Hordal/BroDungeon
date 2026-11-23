using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Brodun.Presentation {
public sealed partial class BattleController {
 bool selecting;
 IEnumerator SelectAndPlay(string id,EnemyInstance target) {
  if(selecting||!ready||!Context.CanPlay(id))yield break;
  selecting=true;View.SetInput(false);var b=Context.Runtime;
  var instance=b.Deck.Hand.Single(c=>c.InstanceId==id);var d=b.Definition(instance);var options=new CardPlayOptions();
  if(d.Id=="N05"||d.Id=="N10") {
   bool done=false;CardChoicePanel.Show(View.Root,flow.Assets,b.Data,b.Deck.Hand.Where(c=>c.InstanceId!=id),d.Id=="N10"?"소멸할 카드 선택":"다음 턴까지 보류할 카드 선택",chosen=>{options.SelectedInstance=chosen;done=true;});
   while(!done)yield return null;
   if(options.SelectedInstance==null){selecting=false;View.SetInput(true);yield break;}
  }
  if(d.Id=="N16") {
   var ids=new List<string>();
   for(int i=0;i<2;i++) {
    var pool=b.Deck.DiscardPile.Where(c=>!ids.Contains(c.InstanceId)).ToArray();if(pool.Length==0)break;
    bool done=false;string chosen=null;CardChoicePanel.Show(View.Root,flow.Assets,b.Data,pool,"회수할 카드 선택 (최대 2장)",x=>{chosen=x;done=true;});while(!done)yield return null;
    if(chosen==null)break;ids.Add(chosen);
   }options.RecallInstances=ids.ToArray();
  }
  if(d.Id=="M11"&&b.Energy>=d.Cost+2) {
   bool done=false;CardChoicePanel.Show(View.Root,flow.Assets,b.Data,new[]{instance},"추가 AP 2 지불? 카드 선택: 추가 타격 / 취소: 기본 타격",x=>{options.ExtraEnergy=x!=null;done=true;});while(!done)yield return null;
  }
  selecting=false;if(!CommitCard(id,View.Cards.FirstOrDefault(c=>c.InstanceId==id),d,target,options:options))View.SetInput(true);
 }
 IEnumerator ChooseDiscard() {
  var battle=Context.Runtime;
  selecting=true;
  CardView chosen=null;
  var prompt=View.BeginDiscardSelection(card=>{if(chosen==null)chosen=card;});
  while(chosen==null)yield return null;
  Destroy(prompt);
  View.SetInput(false);
  int previousEventCount=battle.LastCardResult.Events.Count;
  View.Announce("카드 버림");
  yield return chosen.PresentUse(false,discard:true);
  if(battle.ResolveDiscard(chosen.InstanceId))
  {
   View.RefreshHand(CardClicked);
   View.SetInput(false);
   var drawn=new HashSet<string>(battle.LastCardResult.Events.Skip(previousEventCount)
     .Where(e=>e.Kind==CombatEventKind.Draw).SelectMany(e=>e.CardInstances));
   var incoming=View.Cards.Where(card=>drawn.Contains(card.InstanceId)).ToArray();
   if(incoming.Length>0)
   {
    View.Announce("카드 "+incoming.Length+"장 뽑기");
    View.Sounds.Play(BattleSound.CardDraw);
    var animations=incoming.Select(card=>StartCoroutine(card.PresentDraw())).ToArray();
    foreach(var animation in animations)yield return animation;
   }
  }
  selecting=false;
 }
 IEnumerator ResumeDiscard() {View.SetInput(false);yield return ChooseDiscard();Context.FinishActionPresentation();View.RefreshHand(CardClicked);View.Refresh();View.SetInput(true);}
}
}

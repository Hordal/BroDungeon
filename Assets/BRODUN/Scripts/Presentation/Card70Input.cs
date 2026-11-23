using System.Collections;
using System.Collections.Generic;
using System.Linq;
namespace Brodun.Presentation {
public sealed partial class BattleController {
 bool selecting;
 IEnumerator SelectAndPlay(string id) {
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
  selecting=false;if(!PlaySelected(id,options))View.SetInput(true);
 }
 IEnumerator ChooseDiscard() {
  bool done=false;var b=Context.Runtime;
  CardChoicePanel.Show(View.Root,flow.Assets,b.Data,b.Deck.Hand,"뽑은 뒤 버릴 카드 1장 선택",id=>{done=b.ResolveDiscard(id);},false);
  while(!done)yield return null;
 }
 IEnumerator ResumeDiscard() {View.SetInput(false);yield return ChooseDiscard();View.RefreshHand(CardClicked);View.Refresh();View.SetInput(true);}
}
}

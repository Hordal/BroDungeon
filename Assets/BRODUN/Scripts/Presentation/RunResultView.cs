using Brodun.Flow;
using UnityEngine;
namespace Brodun.Presentation
{
    public static class RunResultView
    {
        public static void Build(GameFlow flow,RectTransform root)
        {
            var result=flow.Session.Summary;bool clear=result.Status==RunStatus.Complete;var art=flow.Assets;
            BattleUi.Panel(root,"Run result panel",new Vector2(.16f,.12f),new Vector2(.84f,.88f));
            BattleUi.Text(root,"RunResultTitle",clear?"RUN CLEAR":"RUN FAILED",art.logoFont,78,clear?BattleUi.Gold:new Color(.91f,.49f,.37f),new Vector2(.2f,.70f),new Vector2(.8f,.84f));
            BattleUi.Text(root,"RunResultCharacter",flow.Session.Definitions.Character(result.Character).Name+" · "+(clear?"잊혀진 회랑 돌파":"여정 종료"),art.font,34,BattleUi.Pale,new Vector2(.2f,.61f),new Vector2(.8f,.70f));
            string stats="도달: "+result.Area+"구역 "+result.Floor+"층\n최종 HP: "+result.HP+" / "+result.MaxHP+
                "\nGOLD: "+result.Gold+"    덱: "+result.DeckSize+"장\n완료한 방: "+result.ClearedNodes;
            BattleUi.Text(root,"RunResultStats",stats,art.font,32,BattleUi.Pale,new Vector2(.25f,.31f),new Vector2(.75f,.59f));
            BattleUi.Button(root,"ResultNewRun","NEW RUN / 새로운 여정",art,new Vector2(.22f,.18f),new Vector2(.49f,.28f),()=>flow.StartNewRun());
            BattleUi.Button(root,clear?"ResultTitle":"DefeatTitle","TITLE / 시작 화면",art,new Vector2(.53f,.18f),new Vector2(.78f,.28f),()=>flow.ReturnToTitle());
        }
    }
}

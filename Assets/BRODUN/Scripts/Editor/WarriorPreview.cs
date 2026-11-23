using System.Linq;
using Brodun.Presentation;
using UnityEditor;
using UnityEngine;

namespace Brodun.Editor
{
    // Editor-only decks replace a single preview run. The catalog and normal ten-card decks are untouched.
    public static class WarriorPreview
    {
        public static string[] Deck(CardBuild build)
        {
            int first=build==CardBuild.Fortress?1:build==CardBuild.Counter?9:17;
            return Enumerable.Range(first,8).Select(i=>"W"+i.ToString("00")).Concat(new[]{"B-W01","B-W02"}).ToArray();
        }
        public static GameDefinitions Definitions(GameDefinitions data,CardBuild build)
        {
            var heroes=data.Characters.Select(hero=>hero.Id==CharacterId.Warrior
                ?new CharacterDefinition(hero.Id,hero.Name,hero.Description,hero.MaxHP,hero.Energy,Deck(build)):hero);
            return new GameDefinitions(data.AllCards,heroes,data.Enemies,data.Config,data.Reward,data.Elements);
        }
        [MenuItem("BRODUN/QA/Warrior/Fortress test deck")]
        public static void Fortress()=>Prepare(CardBuild.Fortress);
        [MenuItem("BRODUN/QA/Warrior/Counter test deck")]
        public static void Counter()=>Prepare(CardBuild.Counter);
        [MenuItem("BRODUN/QA/Warrior/Block spend test deck")]
        public static void BlockSpend()=>Prepare(CardBuild.BlockSpend);
        private static void Prepare(CardBuild build)
        {
            var flow=GameFlow.Instance;
            if(!EditorApplication.isPlaying||flow==null||
                !flow.StartEditorPreviewRun(Definitions(flow.Session.Definitions,build),17))
            {
                Debug.Log("Play BrodunBoot and use this preview from the title screen.");
                return;
            }
            Debug.Log("Warrior "+build+" test deck ready. Select Warrior and enter the first normal battle. A normal new run restores the production deck.");
        }
    }
}
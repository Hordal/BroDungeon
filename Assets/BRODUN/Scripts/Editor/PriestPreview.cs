using System;
using System.Linq;
using Brodun.Presentation;
using UnityEditor;
using UnityEngine;

namespace Brodun.Editor
{
    public static class PriestPreview
    {
        public static string[] Deck(CardBuild build)
        {
            int first=build switch{CardBuild.Blessing=>1,CardBuild.Weakness=>9,CardBuild.Brand=>17,
                _=>throw new ArgumentOutOfRangeException(nameof(build))};
            return Enumerable.Range(first,8).Select(i=>"P"+i.ToString("00")).Concat(new[]{"B-P01","B-P02"}).ToArray();
        }
        public static GameDefinitions Definitions(GameDefinitions data,CardBuild build)
        {
            var heroes=data.Characters.Select(hero=>hero.Id==CharacterId.Priest
                ?new CharacterDefinition(hero.Id,hero.Name,hero.Description,hero.MaxHP,hero.Energy,Deck(build)):hero);
            return new GameDefinitions(data.AllCards,heroes,data.Enemies,data.Config,data.Reward,data.Elements);
        }
        [MenuItem("BRODUN/QA/Priest/Blessing test deck")]
        public static void Blessing()=>Prepare(CardBuild.Blessing);
        [MenuItem("BRODUN/QA/Priest/Weakness test deck")]
        public static void Weakness()=>Prepare(CardBuild.Weakness);
        [MenuItem("BRODUN/QA/Priest/Brand test deck")]
        public static void Brand()=>Prepare(CardBuild.Brand);
        private static void Prepare(CardBuild build)
        {
            var flow=GameFlow.Instance;
            if(!EditorApplication.isPlaying||flow==null||
                !flow.StartEditorPreviewRun(Definitions(flow.Session.Definitions,build),17))
            {
                Debug.Log("Play BrodunBoot and open the Priest preview from the title screen.");
                return;
            }
            Debug.Log("Priest "+build+" test deck ready. Select Priest and enter the first normal battle. A normal new run restores the normal starting deck.");
        }
    }
}
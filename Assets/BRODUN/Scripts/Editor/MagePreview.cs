using System;
using System.Linq;
using Brodun.Presentation;
using UnityEditor;
using UnityEngine;
namespace Brodun.Editor
{
    public static class MagePreview
    {
        public static string[] Deck(CardBuild build,bool areaResonance=false)
        {
            if(build==CardBuild.Resonance&&areaResonance)return new[]{"M09","M10","M12","M13","M14","M16","E04","E14","E18","E19"};
            int first=build switch{CardBuild.Sniper=>1,CardBuild.Resonance=>9,CardBuild.Annihilation=>17,_=>throw new ArgumentOutOfRangeException(nameof(build))};
            return Enumerable.Range(first,8).Select(i=>"M"+i.ToString("00")).Concat(build==CardBuild.Resonance?new[]{"E13","E17"}:new[]{"B-M01","B-M02"}).ToArray();
        }
        public static GameDefinitions Definitions(GameDefinitions data,CardBuild build,bool areaResonance=false)
        {
            var heroes=data.Characters.Select(h=>h.Id==CharacterId.Mage?new CharacterDefinition(h.Id,h.Name,h.Description,h.MaxHP,h.Energy,Deck(build,areaResonance)):h);
            return new GameDefinitions(data.AllCards,heroes,data.Enemies,data.Config,data.Reward,data.Elements);
        }
        [MenuItem("BRODUN/QA/Mage/Arcane sniper")] public static void Sniper()=>Prepare(CardBuild.Sniper);
        [MenuItem("BRODUN/QA/Mage/Elemental resonance")] public static void Resonance()=>Prepare(CardBuild.Resonance);
        [MenuItem("BRODUN/QA/Mage/Resonance with area and chain")] public static void ResonanceArea()=>Prepare(CardBuild.Resonance,true);
        [MenuItem("BRODUN/QA/Mage/Area annihilation")] public static void Area()=>Prepare(CardBuild.Annihilation);
        private static void Prepare(CardBuild build,bool area=false)
        {
            var flow=GameFlow.Instance;
            if(!EditorApplication.isPlaying||flow==null||!flow.StartEditorPreviewRun(Definitions(flow.Session.Definitions,build,area),17,
                build==CardBuild.Annihilation||area?flow.Session.Definitions.Enemy("ruin_gargoyle"):null))
            {Debug.Log("Play BrodunBoot and open Mage QA from the title screen.");return;}
            Debug.Log("Mage "+build+" test deck ready. Select Mage and enter the first battle. Normal new runs restore the production deck.");
        }
    }
}

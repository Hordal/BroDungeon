using System;
using System.Linq;
using Brodun.Presentation;
using UnityEditor;
using UnityEngine;
namespace Brodun.Editor
{
    public enum ElementPreviewMode { RapidFreeze, FrostGust, PoisonSpread, VenomBurn, Overload, Shock, AreaChain, SameAndUnpaired, ReactionCap, PriestHealing, Enhancement }
    public static class ElementPreview
    {
        public static string[] Deck(ElementPreviewMode mode,CharacterId hero)
        {
            string[] probes=mode switch
            {
                ElementPreviewMode.RapidFreeze=>new[]{"E05","E08","E07","E09","E06","E08","E05","E07"},
                ElementPreviewMode.FrostGust=>new[]{"E08","E01","E02","E03","E08","E01","E07","E09"},
                ElementPreviewMode.PoisonSpread=>new[]{"E01","E10","E12","E02","E11","E03","E01","E10"},
                ElementPreviewMode.VenomBurn=>new[]{"E10","E13","E11","E12","E15","E16","E10","E13"},
                ElementPreviewMode.Overload=>new[]{"E13","E17","E14","E18","E15","E19","E13","E17"},
                ElementPreviewMode.Shock=>new[]{"E05","E17","E04","E18","E06","E19","E05","E17"},
                ElementPreviewMode.AreaChain=>new[]{"E04","E18","E02","E12","E14","E16","E17","E01"},
                ElementPreviewMode.SameAndUnpaired=>new[]{"E01","E03","E13","E08","E01","E03","E13","E08"},
                ElementPreviewMode.ReactionCap=>new[]{"E05","E17","E06","E17","E08","E01","E10","E13"},
                ElementPreviewMode.PriestHealing=>new[]{"P06","P23","E06","E05","E17","E06","P20","P19"},
                ElementPreviewMode.Enhancement=>new[]{"E15","E19","E14","E13","E18","E16","E19","E17"},
                _=>throw new ArgumentOutOfRangeException(nameof(mode))
            };
            string prefix=hero==CharacterId.Warrior?"B-W":hero==CharacterId.Mage?"B-M":"B-P";
            return probes.Concat(new[]{prefix+"01",prefix+"02"}).ToArray();
        }
        public static GameDefinitions Definitions(GameDefinitions data,ElementPreviewMode mode)
        {
            var heroes=data.Characters.Select(hero=>mode==ElementPreviewMode.PriestHealing&&hero.Id!=CharacterId.Priest?hero:
                new CharacterDefinition(hero.Id,hero.Name,hero.Description,hero.MaxHP,hero.Energy,Deck(mode,hero.Id)));
            return new GameDefinitions(data.AllCards,heroes,data.Enemies,data.Config,data.Reward,data.Elements);
        }
        [MenuItem("BRODUN/QA/Elements/Rapid freeze")] public static void Freeze()=>Prepare(ElementPreviewMode.RapidFreeze);
        [MenuItem("BRODUN/QA/Elements/Frost gust")] public static void Frost()=>Prepare(ElementPreviewMode.FrostGust);
        [MenuItem("BRODUN/QA/Elements/Poison spread - two enemies")] public static void Spread()=>Prepare(ElementPreviewMode.PoisonSpread,true);
        [MenuItem("BRODUN/QA/Elements/Venom burn")] public static void Burn()=>Prepare(ElementPreviewMode.VenomBurn);
        [MenuItem("BRODUN/QA/Elements/Overload")] public static void Overload()=>Prepare(ElementPreviewMode.Overload);
        [MenuItem("BRODUN/QA/Elements/Shock - two enemies")] public static void Shock()=>Prepare(ElementPreviewMode.Shock,true);
        [MenuItem("BRODUN/QA/Elements/Area and chain - two enemies")] public static void Area()=>Prepare(ElementPreviewMode.AreaChain,true);
        [MenuItem("BRODUN/QA/Elements/Same and unpaired elements")] public static void Same()=>Prepare(ElementPreviewMode.SameAndUnpaired);
        [MenuItem("BRODUN/QA/Elements/Reaction cap")] public static void Cap()=>Prepare(ElementPreviewMode.ReactionCap);
        [MenuItem("BRODUN/QA/Elements/Priest healing and brands - two enemies")] public static void Priest()=>Prepare(ElementPreviewMode.PriestHealing,true);
        [MenuItem("BRODUN/QA/Elements/Fire and charge enhancement")] public static void Power()=>Prepare(ElementPreviewMode.Enhancement,true);
        private static void Prepare(ElementPreviewMode mode,bool pair=false)
        {
            var flow=GameFlow.Instance;
            if(!EditorApplication.isPlaying||flow==null||!flow.StartEditorPreviewRun(Definitions(flow.Session.Definitions,mode),17,
                pair?flow.Session.Definitions.Enemy("ruin_gargoyle"):null))
            {Debug.Log("Play BrodunBoot and open Elements QA from the title screen.");return;}
            Debug.Log("Element "+mode+" test deck ready. Select "+(mode==ElementPreviewMode.PriestHealing?"Priest":"any character")+" and enter the first battle. Normal new runs keep the production deck.");
        }
    }
}

using System.Linq;
using Brodun.Presentation;
using UnityEditor;
using UnityEngine;

namespace Brodun.Editor
{
    // Entire assembly is Editor-only. The catalog and all ScriptableObjects remain untouched.
    public static class CapturePreparation
    {
        public const uint Seed=7;
        public const int PreviewHP=14;
        public static GameDefinitions PreviewDefinitions(GameDefinitions source)
        {
            var enemies=source.Enemies.Select(e=>e.Id=="ruin_gargoyle"
                ?new EnemyDefinition(e.Id,e.Name,e.Type,e.Area,PreviewHP,e.AreaHPStep,e.AssetId,e.Pattern,e.IsLegacy):e);
            return new GameDefinitions(source.Cards,source.Characters,enemies,source.Config,source.Reward,source.Elements);
        }
        [MenuItem("BRODUN/Capture/Prepare character selection (seed 7)")]
        public static void Prepare()
        {
            var flow=GameFlow.Instance;
            if(!EditorApplication.isPlaying || flow==null ||
                !flow.StartEditorPreviewRun(PreviewDefinitions(flow.Session.Definitions),Seed))
            {Debug.Log("Capture: Play BrodunBoot, stay on Title, then use this menu.");return;}
            Debug.Log("Capture ON for this Editor Run only. Choose Warrior, lower first battle r2. HP 14; original rules/assets unchanged. Stop Play or start a normal new game to turn OFF.");
        }
    }
}

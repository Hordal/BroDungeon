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
            var enemies=source.Enemies.Select(e=>e.Area==1 && e.Type==EnemyType.Normal && !e.IsLegacy
                ?new EnemyDefinition(e.Id,e.Name,e.Type,e.Area,PreviewHP,e.AreaHPStep,e.AssetId,e.Pattern,e.IsLegacy):e);
            return new GameDefinitions(source.AllCards,source.Characters,enemies,source.Config,source.Reward,source.Elements);
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
        [MenuItem("BRODUN/QA/Preview two enemies")]
        public static void PreviewTwoEnemies()
        {
            var flow=GameFlow.Instance;
            if(!EditorApplication.isPlaying||flow==null)
            {
                Debug.Log("Play BrodunBoot and open this menu from the title screen.");
                return;
            }
            var definitions=flow.Session.Definitions;
            var companion=definitions.Enemies.FirstOrDefault(enemy=>enemy.Id=="moss_slime");
            if(companion==null||!flow.StartEditorPreviewRun(definitions,Seed,companion))
            {
                Debug.Log("The preview requires the title screen and the existing moss slime.");
                return;
            }
            Debug.Log("Two-enemy preview: select a character and enter a normal battle in the first layer.");
        }
    }
}

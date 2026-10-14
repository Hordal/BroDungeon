using Brodun.Flow;
using Brodun.Presentation;
using UnityEditor;
using UnityEngine;
namespace Brodun.Editor
{
    public static class Phase4Qa
    {
        [MenuItem("BRODUN/QA/PHASE 4 - Start seed 7")]
        public static void StartSeed7()
        {
            var flow=GameFlow.Instance;
            if(!EditorApplication.isPlaying||flow==null||flow.Session.Screen!=FlowScreen.Title)
            {Debug.Log("Open BrodunBoot in Play Mode and stay on Title first.");return;}
            flow.StartNewRun(7);
            Debug.Log("QA seed 7. Normal → normal → elite route: r2 → r4 → r8. No stats, deck or rewards modified.");
        }
    }
}

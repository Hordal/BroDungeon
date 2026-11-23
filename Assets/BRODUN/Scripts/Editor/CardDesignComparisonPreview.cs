using System;
using System.Collections.Generic;
using Brodun.Flow;
using Brodun.Presentation;
using UnityEditor;
using UnityEngine;
namespace Brodun.Editor
{
    public static class CardDesignComparisonPreview
    {
        public const string ArtPath="Assets/BRODUN/Art/QA/CardDesignComparison/";
        [MenuItem("BRODUN/QA/Card UI comparison/A - Illustration")]
        public static void Illustration()=>Select(CardDesignVariant.Illustration);
        [MenuItem("BRODUN/QA/Card UI comparison/B - Symbols")]
        public static void Symbols()=>Select(CardDesignVariant.Symbols);
        [MenuItem("BRODUN/QA/Card UI comparison/C - Hybrid")]
        public static void Hybrid()=>Select(CardDesignVariant.Hybrid);
        [MenuItem("BRODUN/QA/Card UI comparison/Restore normal UI")]
        public static void Stop()
        {
            CardDesignComparison.Reset();
            var controller=UnityEngine.Object.FindFirstObjectByType<BattleController>();
            if(controller!=null){controller.View.RefreshHand(c=>controller.PlayCard(c.InstanceId),true);controller.View.Refresh();}
            Debug.Log("Card comparison UI disabled. Return to the title and start a new run for the production deck.");
        }
        public static IReadOnlyDictionary<string,Sprite> LoadArtwork()
        {
            var images=new Dictionary<string,Sprite>();
            foreach(var id in CardEffectSummary.PreviewIds)
            {
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath+id+".png");
                if(sprite==null)throw new InvalidOperationException("Missing QA artwork "+id);images.Add(id,sprite);
            }
            return images;
        }
        private static void Select(CardDesignVariant variant)
        {
            var flow=GameFlow.Instance;
            if(!EditorApplication.isPlaying||flow==null){Debug.Log("Play BrodunBoot first, then choose Card UI comparison from the title screen.");return;}
            if(CardDesignComparison.Active&&flow.Session.Screen==FlowScreen.Battle)
            {CardDesignComparison.SetVariant(variant);return;}
            if(flow.Session.Screen!=FlowScreen.Title){Debug.Log("Return to the title to start the comparison QA run.");return;}
            var data=CardDesignComparison.Definitions(flow.Session.Definitions);uint seed=CardDesignComparison.ComparisonSeed(data);
            if(!flow.StartEditorPreviewRun(data,seed))return;
            CardDesignComparison.Enable(flow.Session,LoadArtwork(),variant);
            Debug.Log("Card UI comparison: select Warrior, then the first battle. Six-card QA deck, normal 5-card draw, seed="+seed+". A/B/C only redraw the same hand; normal new runs use the unchanged UI and deck.");
        }
    }
}

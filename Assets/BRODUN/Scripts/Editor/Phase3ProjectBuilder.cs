using System;
using System.IO;
using System.Linq;
using Brodun.Data;
using Brodun.Flow;
using Brodun.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Brodun.Editor
{
    public static class Phase3ProjectBuilder
    {
        public const string AssetsPath="Assets/BRODUN/Data/Presentation.asset";
        public const string ScenesPath="Assets/BRODUN/Scenes/";
        [MenuItem("BRODUN/PHASE 3/Build screens and register scenes")]
        public static void Build()
        {
            if(Application.unityVersion!="6000.0.47f1")throw new InvalidOperationException("Use Unity 6000.0.47f1.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            foreach(string png in Directory.GetFiles("Assets/BRODUN/Art","*.png",SearchOption.AllDirectories))
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(png.Replace('\\','/'));
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;
                importer.alphaIsTransparency=true;importer.SaveAndReimport();
            }
            var assets=AssetDatabase.LoadAssetAtPath<PresentationAssets>(AssetsPath);
            if(assets==null){assets=ScriptableObject.CreateInstance<PresentationAssets>();AssetDatabase.CreateAsset(assets,AssetsPath);}
            assets.font=AssetDatabase.LoadAssetAtPath<Font>("Assets/BRODUN/Art/Fonts/NotoSansCJKkr-Regular.otf");
            assets.logoFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/BRODUN/Art/Fonts/Silkscreen-Regular.ttf");
            assets.inputActions=AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            assets.titleBackground=Sprite("Backgrounds/title_background");assets.mapBackground=Sprite("Backgrounds/area01_forgotten_corridor");
            assets.warrior=Sprite("Characters/warrior");assets.mage=Sprite("Characters/mage");assets.priest=Sprite("Characters/priest");
            assets.buttonNormal=Sprite("UI/button_normal");assets.buttonHover=Sprite("UI/button_hover");
            assets.buttonPressed=Sprite("UI/button_pressed");assets.buttonDisabled=Sprite("UI/button_disabled");
            assets.nodeNormal=Sprite("UI/map_choice_normal");assets.nodeHover=Sprite("UI/map_choice_hover");
            assets.nodeSelected=Sprite("UI/map_choice_selected");assets.nodeLocked=Sprite("UI/map_choice_locked");
            assets.battle=Sprite("UI/icon_battle");assets.boss=Sprite("UI/icon_boss");assets.puzzle=Sprite("UI/icon_puzzle");assets.roomEvent=Sprite("UI/icon_event");
            assets.shop=Sprite("UI/event_shop");assets.altar=Sprite("UI/event_altar");assets.rest=Sprite("UI/event_rest");
            assets.current=Sprite("UI/node_current");assets.completed=Sprite("UI/node_complete");
            if(assets.font==null || assets.logoFont==null || assets.inputActions==null)throw new InvalidDataException("Font/UI input missing.");
            EditorUtility.SetDirty(assets);AssetDatabase.SaveAssets();
            Directory.CreateDirectory(ScenesPath);AssetDatabase.Refresh();
            var screens=new[]{FlowScreen.Boot,FlowScreen.Title,FlowScreen.CharacterSelect,FlowScreen.DungeonMap};
            foreach(FlowScreen screen in screens)
            {
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var camera=new GameObject("UI Camera",typeof(Camera));camera.tag="MainCamera";
                camera.GetComponent<Camera>().clearFlags=CameraClearFlags.SolidColor;camera.GetComponent<Camera>().backgroundColor=Color.black;
                camera.GetComponent<Camera>().orthographic=true;camera.transform.position=new Vector3(0,0,-10);
                if(screen==FlowScreen.Boot)
                {
                    var flow=new GameObject("BRODUN GameFlow",typeof(GameFlow)).GetComponent<GameFlow>();
                    var so=new SerializedObject(flow);
                    so.FindProperty("catalog").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameCatalogData>(DataAssetBuilder.CatalogPath);
                    // NewScene may unload unused assets: reacquire the persisted asset for the new scene.
                    so.FindProperty("assets").objectReferenceValue=AssetDatabase.LoadAssetAtPath<PresentationAssets>(AssetsPath);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    if(so.FindProperty("assets").objectReferenceValue==null)throw new InvalidDataException("Boot presentation reference missing.");
                }
                else new GameObject("BRODUN "+screen+" View",typeof(GameScreenView));
                EditorSceneManager.SaveScene(scene,ScenesPath+GameFlow.SceneName(screen)+".unity");
            }
            var generated=screens.Select(s=>new EditorBuildSettingsScene(ScenesPath+GameFlow.SceneName(s)+".unity",true)).ToArray();
            // Preserve old team scenes and their enabled flags; Boot must be first for a normal player build.
            var existing=EditorBuildSettings.scenes.Where(s=>!generated.Any(g=>g.path==s.path)).ToArray();
            EditorBuildSettings.scenes=generated.Concat(existing).ToArray();
            AssetDatabase.SaveAssets();
            if(setup.Any(s=>s.isLoaded && s.isActive && !string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.OpenScene(ScenesPath+GameFlow.SceneName(FlowScreen.Boot)+".unity");
            Debug.Log("BRODUN PHASE 3: 4 scenes built, original team scenes preserved, 22 PNG sprites imported.");
        }
        private static Sprite Sprite(string name)
        {
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/BRODUN/Art/"+name+".png");
            if(sprite==null)throw new InvalidDataException("Missing sprite "+name);return sprite;
        }
    }
}

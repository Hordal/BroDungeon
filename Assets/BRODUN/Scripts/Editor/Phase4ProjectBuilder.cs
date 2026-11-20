using System;
using System.IO;
using System.Linq;
using Brodun.Data;
using Brodun.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Brodun.Editor
{
    public static class Phase4ProjectBuilder
    {
        [Serializable] private sealed class Manifest {public AssetRecord[] assets;public AnimationRecord[] animations;}
        [Serializable] private sealed class AssetRecord {public string id,file;public FrameRecord[] frames;}
        [Serializable] private sealed class FrameRecord {public string name,file;public float pivotX,pivotY;}
        [Serializable] private sealed class AnimationRecord {public string name;public float fps;public bool loop;}
        public const string AssetPath="Assets/BRODUN/Resources/BrodunBattle.asset";
        public const string ScenePath="Assets/BRODUN/Scenes/BrodunBattle.unity";
        private const string Prefabs="Assets/BRODUN/Prefabs/";
        [MenuItem("BRODUN/PHASE 4/Build battle presentation")]
        public static void Build()
        {
            if(Application.unityVersion!="6000.0.47f1")throw new InvalidOperationException("Use Unity 6000.0.47f1.");
            var addon=JsonUtility.FromJson<Manifest>(File.ReadAllText("WebPrototype/Assets/BRODUNArt/animation_manifest.json"));
            var legacy=JsonUtility.FromJson<Manifest>(File.ReadAllText("WebPrototype/manifest.json"));
            AssetDatabase.Refresh();Directory.CreateDirectory("Assets/BRODUN/Resources");Directory.CreateDirectory(Prefabs);AssetDatabase.Refresh();
            var visual=new System.Collections.Generic.List<EnemyVisual>();
            foreach(string id in new[]{"ruin_gargoyle","gale_warden","fallen_colossus","moss_slime"})
            {
                var manifest=id=="moss_slime"?legacy:addon;
                var record=manifest.assets.Single(a=>a.id==id || Path.GetFileNameWithoutExtension(a.file)==id);
                var animations=new System.Collections.Generic.List<SpriteSequence>();
                foreach(SpriteMotion motion in Enum.GetValues(typeof(SpriteMotion)))
                {
                    string name=motion.ToString().ToLowerInvariant();var meta=manifest.animations.Single(a=>a.name==name);
                    var frames=record.frames.Where(f=>f.name.StartsWith(name+"_",StringComparison.Ordinal)).OrderBy(f=>f.name).ToArray();
                    if(frames.Length!=4)throw new InvalidDataException(id+" "+name+" must have 4 source frames.");
                    var sprites=frames.Select(f=>Import("Assets/BRODUN/Art/Battle/"+id+"/"+f.name+".png",new Vector2(f.pivotX,f.pivotY))).ToArray();
                    animations.Add(new SpriteSequence{motion=motion,fps=meta.fps,loop=meta.loop,frames=sprites});
                }
                visual.Add(new EnemyVisual{enemy=AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/BRODUN/Data/Generated/Enemies/"+id+".asset"),
                    displayHeight=id=="gale_warden"?300:id=="fallen_colossus"?320:280,animations=animations.ToArray()});
            }
            var art=AssetDatabase.LoadAssetAtPath<BattlePresentationAssets>(AssetPath);
            if(art==null){art=ScriptableObject.CreateInstance<BattlePresentationAssets>();AssetDatabase.CreateAsset(art,AssetPath);}
            art.common=AssetDatabase.LoadAssetAtPath<PresentationAssets>(Phase3ProjectBuilder.AssetsPath);
            art.background=art.common.mapBackground;art.enemies=visual.ToArray();
            art.attackFrame=Import("Assets/BRODUN/Art/Battle/card_attack.png",new Vector2(.5f,.5f));
            art.guardFrame=Import("Assets/BRODUN/Art/Battle/card_defense.png",new Vector2(.5f,.5f));
            art.skillFrame=Import("Assets/BRODUN/Art/Battle/card_back.png",new Vector2(.5f,.5f));
            if(art.cardPrefab==null)
            {var card=CardView.Create(null,art.common);art.cardPrefab=PrefabUtility.SaveAsPrefabAsset(card.gameObject,Prefabs+"CardView.prefab").GetComponent<CardView>();UnityEngine.Object.DestroyImmediate(card.gameObject);}
            if(art.enemyPrefab==null)
            {var enemy=EnemyView.Create(null,art.common);art.enemyPrefab=PrefabUtility.SaveAsPrefabAsset(enemy.gameObject,Prefabs+"EnemyView.prefab").GetComponent<EnemyView>();UnityEngine.Object.DestroyImmediate(enemy.gameObject);}
            EditorUtility.SetDirty(art);AssetDatabase.SaveAssets();
            // Add only the new scene. Never rebuild PHASE 3 or team scenes.
            if(!File.Exists(ScenePath))
            {
                var setup=EditorSceneManager.GetSceneManagerSetup();
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var camera=new GameObject("UI Camera",typeof(Camera));camera.tag="MainCamera";camera.GetComponent<Camera>().backgroundColor=Color.black;
                camera.GetComponent<Camera>().clearFlags=CameraClearFlags.SolidColor;camera.GetComponent<Camera>().orthographic=true;camera.transform.position=new Vector3(0,0,-10);
                new GameObject("BRODUN Battle Controller",typeof(BattleController));
                EditorSceneManager.SaveScene(scene,ScenePath);
                if(setup.Any(s=>s.isLoaded&&!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.OpenScene("Assets/BRODUN/Scenes/BrodunBoot.unity");
            }
            if(!EditorBuildSettings.scenes.Any(s=>s.path==ScenePath))
                EditorBuildSettings.scenes=EditorBuildSettings.scenes.Concat(new[]{new EditorBuildSettingsScene(ScenePath,true)}).ToArray();
            Debug.Log("PHASE 4: BattleScene + shared CardView/EnemyView prefabs + 64 frame references. Existing scenes/data preserved.");
        }
        private static Sprite Import(string path,Vector2 pivot)
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            if(importer==null)throw new InvalidDataException(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=pivot;importer.SetTextureSettings(settings);importer.spritePixelsPerUnit=100;
            importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}

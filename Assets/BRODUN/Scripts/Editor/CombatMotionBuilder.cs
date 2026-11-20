using System;
using System.IO;
using System.Linq;
using Brodun.Presentation;
using UnityEditor;
using UnityEngine;
namespace Brodun.Editor
{
    public static class CombatMotionBuilder
    {
        [Serializable] private sealed class Manifest {public Record[] assets;public Animation[] animations;}
        [Serializable] private sealed class Record {public string file;public Frame[] frames;}
        [Serializable] private sealed class Frame {public string name,file;public float pivotX,pivotY;}
        [Serializable] private sealed class Animation {public string name;public float fps;public bool loop;}
        [MenuItem("BRODUN/PHASE 6.5/Import existing hero motions")]
        public static void Build()
        {
            if(Application.unityVersion!="6000.0.47f1")throw new InvalidOperationException("Use Unity 6000.0.47f1.");
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText("WebPrototype/manifest.json"));
            var heroes=Enum.GetValues(typeof(CharacterId)).Cast<CharacterId>().Select(id=>
            {
                string name=id.ToString().ToLowerInvariant();
                var record=manifest.assets.Single(a=>Path.GetFileNameWithoutExtension(a.file)==name);
                var sequences=new[]{SpriteMotion.Idle,SpriteMotion.Attack,SpriteMotion.Hurt}.Select(motion=>
                {
                    string key=motion.ToString().ToLowerInvariant();var meta=manifest.animations.Single(a=>a.name==key);
                    var frames=record.frames.Where(f=>f.name.StartsWith(key+"_",StringComparison.Ordinal)).OrderBy(f=>f.name).ToArray();
                    if(frames.Length!=4)throw new InvalidDataException(name+"/"+key+": expected four existing frames");
                    return new SpriteSequence{motion=motion,fps=meta.fps,loop=meta.loop,frames=frames.Select(f=>Import(name,f)).ToArray()};
                }).ToArray();
                return new HeroMotion{character=id,animations=sequences};
            }).ToArray();
            const string path="Assets/BRODUN/Resources/BrodunCombatMotion.asset";
            var asset=AssetDatabase.LoadAssetAtPath<CombatMotionAssets>(path);
            if(asset==null){asset=ScriptableObject.CreateInstance<CombatMotionAssets>();AssetDatabase.CreateAsset(asset,path);}
            asset.heroes=heroes;EditorUtility.SetDirty(asset);AssetDatabase.SaveAssets();
            Debug.Log("PHASE 6.5 imported 36 existing hero frames. No rules/scenes/prefabs/Room content modified.");
        }
        private static Sprite Import(string hero,Frame frame)
        {
            string folder="Assets/BRODUN/Art/CombatMotion/"+hero;Directory.CreateDirectory(folder);
            string target=folder+"/"+frame.name+".png";
            if(!File.Exists(target))File.Copy("WebPrototype/Assets/BRODUNArt/"+frame.file,target);
            AssetDatabase.ImportAsset(target);
            var importer=(TextureImporter)AssetImporter.GetAtPath(target);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=new Vector2(frame.pivotX,frame.pivotY);importer.SetTextureSettings(settings);
            importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(target);
        }
    }
}

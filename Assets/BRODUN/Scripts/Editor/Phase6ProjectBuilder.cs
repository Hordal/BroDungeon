using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Brodun.Presentation;
namespace Brodun.Editor
{
    public static class Phase6ProjectBuilder
    {
        // Additive import only. Never rebuild old scene/prefab/data builders here.
        public static void Build()
        {
            if(Application.unityVersion!="6000.0.47f1")throw new InvalidOperationException("Use 6000.0.47f1.");
            const string folder="Assets/BRODUN/Art/Rooms";
            Directory.CreateDirectory(folder);
            foreach(var file in new[]{"shop","altar","rest_site"})
            {
                string target=folder+"/"+file+".png";
                if(!File.Exists(target))File.Copy("WebPrototype/Assets/BRODUNArt/EventRooms/"+file+".png",target);
                AssetDatabase.ImportAsset(target);
                var importer=(TextureImporter)AssetImporter.GetAtPath(target);importer.textureType=TextureImporterType.Sprite;
                importer.spriteImportMode=SpriteImportMode.Single;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;importer.SaveAndReimport();
            }
            const string path="Assets/BRODUN/Resources/BrodunRooms.asset";
            var assets=AssetDatabase.LoadAssetAtPath<RoomPresentationAssets>(path);
            if(assets==null){assets=ScriptableObject.CreateInstance<RoomPresentationAssets>();AssetDatabase.CreateAsset(assets,path);}
            assets.shop=AssetDatabase.LoadAssetAtPath<Sprite>(folder+"/shop.png");
            assets.altar=AssetDatabase.LoadAssetAtPath<Sprite>(folder+"/altar.png");
            assets.rest=AssetDatabase.LoadAssetAtPath<Sprite>(folder+"/rest_site.png");
            EditorUtility.SetDirty(assets);AssetDatabase.SaveAssets();
            Debug.Log("PHASE 6: imported only three existing room backgrounds and one room presentation asset. No scenes/packages/combat data rebuilt.");
        }
    }
}

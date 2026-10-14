using System;
using System.IO;
using System.Linq;
using Brodun.Data;
using UnityEditor;
using UnityEngine;
namespace Brodun.Editor
{
    public static class DataAssetBuilder
    {
        public const string CatalogPath = "Assets/BRODUN/Data/Generated/GameCatalog.asset";
        [MenuItem("BRODUN/Data/Rebuild web-rule assets")]
        public static void Build()
        {
            var source = JsonUtility.FromJson<WebSnapshot>(File.ReadAllText("Assets/BRODUN/Data/WebRulesSnapshot.json"));
            if (source.schema != 1 || source.cards.Length != 27 || source.characters.Length != 3 || source.enemies.Length != 12)
                throw new InvalidDataException("Unexpected snapshot. Review data before importing.");
            string root = "Assets/BRODUN/Data/Generated";
            Directory.CreateDirectory(root + "/Cards");
            Directory.CreateDirectory(root + "/Characters");
            Directory.CreateDirectory(root + "/Enemies");
            AssetDatabase.Refresh();
            var cards = source.cards.Select(x => Upsert<CardData>(root + "/Cards/" + x.id + ".asset", a => a.Initialize(x))).ToArray();
            var characters = source.characters.Select(x => Upsert<CharacterData>(root + "/Characters/" + x.id + ".asset", a => a.Initialize(x))).ToArray();
            var enemies = source.enemies.Select(x => Upsert<EnemyData>(root + "/Enemies/" + x.id + ".asset", a => a.Initialize(x))).ToArray();
            var config = Upsert<BattleConfigData>(root + "/BattleConfig.asset", a => a.Initialize(source.config));
            var reward = Upsert<RewardData>(root + "/Reward.asset", a => a.Initialize(source.config.gold_reward, 3));
            Upsert<GameCatalogData>(CatalogPath, a => a.Initialize(cards, characters, enemies, config, reward, source.elements));
            AssetDatabase.SaveAssets();
            Debug.Log("BRODUN data generated: 27 cards, 3 characters, 12 enemies, config + reward + catalog = 45 assets.");
        }
        private static T Upsert<T>(string path, Action<T> update) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                if (File.Exists(path)) throw new InvalidDataException("Refusing to overwrite incompatible asset " + path);
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            update(asset);
            EditorUtility.SetDirty(asset);
            return asset;
        }
    }
}

using System;
using System.IO;
using Brodun.Data;
using Brodun.Editor;
using UnityEditor;
using UnityEngine;
namespace Brodun.Tests
{
    public abstract class RulesTest
    {
        protected GameDefinitions Data;
        [NUnit.Framework.SetUp] public void LoadDefinitions()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<GameCatalogData>(DataAssetBuilder.CatalogPath);
            NUnit.Framework.Assert.NotNull(catalog,"Run BRODUN/Data/Rebuild web-rule assets first.");
            Data=catalog.ToDefinitions();
        }
        protected RunState Run(CharacterId character=CharacterId.Warrior,uint seed=17) => new RunState(Data,character,seed,"test-run");
        protected BattleRuntime Battle(RunState run=null)
        { run=run??Run(); return new BattleRuntime(Data,run,DungeonRuntime.Enter(run,"r1")); }
        protected static WebSnapshot Snapshot() => JsonUtility.FromJson<WebSnapshot>(File.ReadAllText("Assets/BRODUN/Data/WebRulesSnapshot.json"));
        protected static GoldenFile Golden() => JsonUtility.FromJson<GoldenFile>(File.ReadAllText("Assets/BRODUN/Tests/Editor/WebGoldenResults.json"));
    }
    [Serializable] public sealed class GoldenFile { public GoldenCard[] cases; public GoldenTrace[] traces; }
    [Serializable] public sealed class GoldenCard
    {
        public string id,previous,element;
        public int hp,enemyBlock,poison,weak,playerHP,block,strength,energy,damage,blocked;
    }
    [Serializable] public sealed class GoldenTrace { public string character; public long seed; public GoldenStep[] steps; }
    [Serializable] public sealed class GoldenStep
    {
        public string kind,instance,intentType;
        public int hp,enemyHP,enemyBlock,block,strength,poison,weak,energy,turn,intentAmount,intentHits;
        public string[] hand,draw,discard,exhaust;
    }
}

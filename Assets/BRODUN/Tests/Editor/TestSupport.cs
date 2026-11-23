using System;
using System.IO;
using System.Linq;
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
        protected static CardInstance DrawCard(BattleRuntime battle,string id)
        {
            while(!battle.Deck.Hand.Any(c=>c.CardId==id)&&(battle.Deck.DrawPile.Count>0||battle.Deck.DiscardPile.Count>0))
                battle.Deck.Draw(1);
            return battle.Deck.Hand.First(c=>c.CardId==id);
        }
        protected static WebSnapshot Snapshot() => JsonUtility.FromJson<WebSnapshot>(File.ReadAllText("Assets/BRODUN/Data/WebRulesSnapshot.json"));
        protected static GoldenFile Golden() => JsonUtility.FromJson<GoldenFile>(File.ReadAllText("Assets/BRODUN/Tests/Editor/WebGoldenResults.json"));
    }
    // Explicit compatibility fixture: old web/prototype contracts remain tested independently
    // of the formal PHASE 9 game. Production tests continue to use RulesTest directly.
    public abstract class LegacyRulesTest:RulesTest
    {
        [NUnit.Framework.SetUp] public void SelectLegacyPrototypeContract()
        {
            var source=Snapshot();
            var heroes=Data.Characters.Select(h=>
            {
                string prefix=h.Id==CharacterId.Warrior?"B-W":h.Id==CharacterId.Mage?"B-M":"B-P";
                var old=source.characters.Single(c=>c.id==h.Id.ToString().ToLowerInvariant()).starter;
                return new CharacterDefinition(h.Id,h.Name,h.Description,h.MaxHP,h.Energy,
                    Enumerable.Repeat(prefix+"01",3).Concat(Enumerable.Repeat(prefix+"02",3)).Concat(old),old);
            });
            Data=new GameDefinitions(Data.AllCards,heroes,Data.Enemies,Data.Config,new RewardDefinition(Data.Reward.Gold,Data.Reward.CandidateCount),Data.Elements);
        }
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

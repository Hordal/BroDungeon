using System.Linq;
using Brodun.Flow;
using Brodun.Presentation;
using Brodun.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Brodun.Tests
{
    public sealed class Phase3FlowTests : LegacyRulesTest
    {
        private FlowSession Title()
        {var flow=new FlowSession(Data);Assert.IsTrue(flow.BeginTransition());flow.FinishTransition(FlowScreen.Title);return flow;}
        private FlowSession Map()
        {var f=Title();f.StartNewRun(17);f.FinishTransition(FlowScreen.CharacterSelect);f.ChooseCharacter(CharacterId.Warrior);f.FinishTransition(FlowScreen.DungeonMap);return f;}
        [Test] public void BootTitleAndStartLockRepeatedInput()
        {
            var f=Title();Assert.IsTrue(f.StartNewRun(17));Assert.IsFalse(f.StartNewRun(99));Assert.IsNull(f.Run);
            Assert.AreEqual(17u,f.PendingSeed);Assert.IsTrue(f.IsTransitioning);
            Assert.IsFalse(f.ChooseCharacter(CharacterId.Warrior));f.FinishTransition(FlowScreen.CharacterSelect);
            Assert.IsTrue(f.ChooseCharacter(CharacterId.Warrior));Assert.IsFalse(f.ChooseCharacter(CharacterId.Mage));
        }
        [TestCase(CharacterId.Warrior)][TestCase(CharacterId.Mage)][TestCase(CharacterId.Priest)]
        public void SelectionUsesExistingCharacterData(CharacterId hero)
        {
            var f=Title();f.StartNewRun(29);f.FinishTransition(FlowScreen.CharacterSelect);f.ChooseCharacter(hero);
            Assert.AreEqual(hero,f.Run.SelectedCharacter);Assert.AreEqual(Data.Character(hero).MaxHP,f.Run.CurrentHP);
            CollectionAssert.AreEqual(Data.Character(hero).StartingDeck,f.Run.Deck.Select(c=>c.CardId));
            Assert.AreEqual(29u,f.Run.Seed);Assert.AreEqual(11,f.Run.Map.Count);
        }
        [Test] public void AvailableAndLockedSelectionsUseDungeonRuntimeWithoutCompleting()
        {
            var f=Map();Assert.AreEqual(NodeVisualState.Current,f.NodeState("start"));
            Assert.AreEqual(NodeVisualState.Available,f.NodeState("r1"));Assert.AreEqual(NodeVisualState.Locked,f.NodeState("boss"));
            Assert.IsFalse(f.SelectRoom("boss"));Assert.IsTrue(f.SelectRoom("r1"));Assert.IsFalse(f.SelectRoom("r1"));
            Assert.AreEqual("r1",f.Run.CurrentNode);Assert.AreEqual("r1",f.Run.ActiveRoom);Assert.IsTrue(f.PendingRoom.IsBattle);
            Assert.AreEqual(0,f.Run.CompletedNodes.Count);Assert.AreEqual(0,DungeonRuntime.Available(f.Run).Count);
        }
        [Test] public void CompletedStateOnlyComesFromRuntimeCompletion()
        {var f=Map();f.SelectRoom("r1");DungeonRuntime.Complete(f.Run);f.SelectRoom("r3");Assert.AreEqual(NodeVisualState.Completed,f.NodeState("r1"));}
        [Test] public void DeckOpenCloseDoesNotMutateDeckAndBlocksNodeSelection()
        {
            var f=Map();var ids=f.Run.Deck.Select(c=>c.InstanceId).ToArray();Assert.IsTrue(f.OpenDeck());
            Assert.IsFalse(f.SelectRoom("r1"));f.CloseDeck();Assert.IsFalse(f.DeckOpen);CollectionAssert.AreEqual(ids,f.Run.Deck.Select(c=>c.InstanceId));
        }
        [Test] public void ReloadKeepsRunAndReconstructsPendingRoom()
        {
            var f=Map();f.Run.AddGold(50);f.Run.AddCard("earth_crush");f.SelectRoom("r1");var run=f.Run;
            Assert.IsTrue(f.BeginTransition());f.RestorePendingRoom();f.FinishTransition(FlowScreen.DungeonMap);
            Assert.AreSame(run,f.Run);Assert.AreEqual(50,f.Run.Gold);Assert.AreEqual(11,f.Run.Deck.Count);Assert.AreEqual("r1",f.PendingRoom.Node.Id);
        }
        [Test] public void LeavingDiscardsRunAndNextGameHasNoOldState()
        {
            var f=Map();f.Run.AddGold(50);f.ReturnToTitle();Assert.IsNull(f.Run);f.FinishTransition(FlowScreen.Title);
            f.StartNewRun(0);f.FinishTransition(FlowScreen.CharacterSelect);f.ChooseCharacter(CharacterId.Priest);
            Assert.AreEqual(0,f.Run.Gold);Assert.AreEqual(0u,f.Run.Seed);Assert.AreEqual("start",f.Run.CurrentNode);
        }
        [Test] public void SceneRegistrationAndRequiredSpritesExist()
        {
            var scenes=EditorBuildSettings.scenes;
            Assert.AreEqual(Phase3ProjectBuilder.ScenesPath+"BrodunBoot.unity",scenes[0].path);
            CollectionAssert.Contains(scenes.Select(s=>s.path),"Assets/Scenes/SampleScene.unity");
            var art=AssetDatabase.LoadAssetAtPath<PresentationAssets>(Phase3ProjectBuilder.AssetsPath);
            Assert.NotNull(art.font);Assert.NotNull(art.inputActions);
            Assert.NotNull(art.logoFont);
            foreach(var field in typeof(PresentationAssets).GetFields().Where(f=>f.FieldType==typeof(Sprite)))Assert.NotNull(field.GetValue(art),field.Name);
            foreach(var scene in scenes.Where(s=>s.path.StartsWith(Phase3ProjectBuilder.ScenesPath)))Assert.NotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path));
            var boot=EditorSceneManager.OpenScene(Phase3ProjectBuilder.ScenesPath+"BrodunBoot.unity",OpenSceneMode.Additive);
            try
            {
                var service=boot.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<GameFlow>()).Single();
                var serialized=new SerializedObject(service);
                Assert.NotNull(serialized.FindProperty("catalog").objectReferenceValue);
                Assert.NotNull(serialized.FindProperty("assets").objectReferenceValue);
            }
            finally { if(UnityEngine.SceneManagement.SceneManager.sceneCount>1)EditorSceneManager.CloseScene(boot,true); }
        }
    }
}

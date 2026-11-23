using System.Collections;
using System.Linq;
using Brodun;
using Brodun.Flow;
using Brodun.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace Brodun.Tests.PlayMode {
public sealed class Card70SceneTests {
 IEnumerator Ready(FlowScreen screen){float deadline=Time.realtimeSinceStartup+20;while((GameFlow.Instance==null||GameFlow.Instance.Session.Screen!=screen||GameFlow.Instance.Session.IsTransitioning)&&Time.realtimeSinceStartup<deadline)yield return null;Assert.NotNull(GameFlow.Instance);Assert.AreEqual(screen,GameFlow.Instance.Session.Screen);Assert.IsFalse(GameFlow.Instance.Session.IsTransitioning);yield return null;}
 [UnityTest] public IEnumerator ProductionBootLoadsNewCatalogAndPlayableStartingCards(){
  if(GameFlow.Instance!=null){Object.Destroy(GameFlow.Instance.gameObject);yield return null;}
  yield return SceneManager.LoadSceneAsync("BrodunBoot");yield return Ready(FlowScreen.Title);
  var flow=GameFlow.Instance;Assert.AreEqual(70,flow.Session.Definitions.Cards.Count);
  Assert.IsTrue(flow.StartNewRun(17));yield return Ready(FlowScreen.CharacterSelect);Assert.IsTrue(flow.ChooseCharacter(CharacterId.Warrior));yield return Ready(FlowScreen.DungeonMap);
  Assert.AreEqual(10,flow.Session.Run.Deck.Count);Assert.IsTrue(flow.Session.Run.Deck.All(c=>c.CardId.StartsWith("W")||c.CardId.StartsWith("N")));
  var node=DungeonRuntime.Available(flow.Session.Run).Select(id=>DungeonRuntime.Node(flow.Session.Run,id)).First(n=>n.Type==RoomType.Battle);
  Assert.IsTrue(flow.SelectRoom(node.Id));yield return Ready(FlowScreen.Battle);
  var controller=Object.FindFirstObjectByType<BattleController>();Assert.NotNull(controller);Assert.IsTrue(controller.View.Cards.All(c=>c.Definition.IsReworked));
  foreach(var view in controller.View.Cards){Assert.AreEqual(view.Definition.Element!=ElementType.None,view.element.gameObject.activeSelf);Assert.AreEqual(flow.Session.Definitions.ElementName(view.Definition.Element),view.element.text);}
  var basic=controller.View.Cards.FirstOrDefault(c=>c.Definition.Id=="W01"||c.Definition.Id=="W02"||c.Definition.Id=="W03"||c.Definition.Id=="W05");Assert.NotNull(basic);Assert.IsTrue(controller.PlayCard(basic.InstanceId));
  float deadline=Time.realtimeSinceStartup+10;while(flow.Session.Battle.Phase==BattlePhase.ResolvingPlayerAction&&Time.realtimeSinceStartup<deadline)yield return null;Assert.Less(Time.realtimeSinceStartup,deadline);
  Object.Destroy(flow.gameObject);yield return null;
 }
}
}

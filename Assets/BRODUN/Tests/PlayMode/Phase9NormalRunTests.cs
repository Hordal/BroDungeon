using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Brodun.Flow;
using Brodun.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Brodun.Tests.PlayMode
{
    // Production GameFlow, generated map, untouched HP/cards/draws. Only UI input is automated.
    public sealed class Phase9NormalRunTests
    {
        private GameFlow Flow=>GameFlow.Instance;
        private BattleController Controller=>UnityEngine.Object.FindFirstObjectByType<BattleController>();
        private RunState Run=>Flow.Session.Run;
        private bool playedAcquired;
        private int reactions;
        [UnitySetUp] public IEnumerator Boot()
        {
            if(Flow!=null){UnityEngine.Object.Destroy(Flow.gameObject);yield return null;}
            yield return SceneManager.LoadSceneAsync("BrodunBoot");yield return Ready(FlowScreen.Title);
#if UNITY_EDITOR
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1920,1080,"BRODUN PHASE9 normal run");
#endif
            yield return null;
        }
        [UnityTearDown] public IEnumerator Clean(){if(Flow!=null)UnityEngine.Object.Destroy(Flow.gameObject);yield return null;}
        private IEnumerator Ready(FlowScreen screen)
        {
            float deadline=Time.realtimeSinceStartup+15;
            while((Flow==null||Flow.Session.Screen!=screen||Flow.Session.IsTransitioning)&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.NotNull(Flow);Assert.AreEqual(screen,Flow.Session.Screen);Assert.IsFalse(Flow.Session.IsTransitioning);yield return null;
        }
        private static StyledButton FindButton(string name)=>UnityEngine.Object.FindObjectsByType<StyledButton>(FindObjectsSortMode.None).FirstOrDefault(b=>b.name==name);
        private static void Pointer(GameObject target)=>ExecuteEvents.Execute(target,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
        private static void Click(string name){var b=FindButton(name);Assert.NotNull(b,name);Assert.IsTrue(b.IsInteractable(),name);Pointer(b.gameObject);}
        private IEnumerator Settled()
        {
            float deadline=Time.realtimeSinceStartup+15;
            while(Flow.Session.IsTransitioning||(Flow.Session.Screen==FlowScreen.Battle&&
                (Flow.Session.Battle.Phase==BattlePhase.ResolvingPlayerAction||Flow.Session.Battle.Phase==BattlePhase.EnemyTurn||Flow.Session.Battle.Phase==BattlePhase.Victory)))
            {Assert.Less(Time.realtimeSinceStartup,deadline,"Presentation stalled");yield return null;}
            yield return null;
        }
        private IEnumerator Start(CharacterId hero,uint seed=17)
        {
            Assert.IsTrue(Flow.StartNewRun(seed));yield return Ready(FlowScreen.CharacterSelect);
            if(hero==CharacterId.Warrior&&Environment.GetEnvironmentVariable("BRODUN_PHASE9_VIDEO")=="1")
            {
                string capture=Environment.GetEnvironmentVariable("BRODUN_PHASE9_FORMAL_CAPTURE_DIR");
                if(!string.IsNullOrEmpty(capture))Flow.gameObject.AddComponent<Phase9RenderedFrames>().Begin(Path.Combine(capture,"Video"));
            }
            yield return Capture(hero+"_CharacterSelect");Click("Character_"+hero);yield return Ready(FlowScreen.ThemeSelect);Click("Theme_ruins");yield return Ready(FlowScreen.DungeonMap);
            Assert.AreEqual("layered-v2",Run.MapGeneratorVersion);Assert.AreEqual(10,Run.Deck.Count);
            Assert.IsTrue(Run.Deck.All(c=>Flow.Session.Definitions.Card(c.CardId).IsFormalCard));
            yield return Capture(hero+"_MapStart");
        }
        private List<string> PathTo(Func<RoomNode,bool> goal,bool avoidElite)
        {
            var queue=new Queue<List<string>>();queue.Enqueue(new List<string>{Run.CurrentNode});var seen=new HashSet<string>();
            while(queue.Count>0)
            {
                var path=queue.Dequeue();var room=DungeonRuntime.Node(Run,path.Last());if(!seen.Add(room.Id))continue;
                if(path.Count>1&&goal(room))return path.Skip(1).ToList();
                if(path.Count>1&&(room.Type==RoomType.Boss||avoidElite&&room.Encounter==EnemyType.Elite))continue;
                foreach(var next in room.Next)queue.Enqueue(path.Concat(new[]{next}).ToList());
            }
            return null;
        }
        private IEnumerator VisitNonBattle(RoomNode room)
        {
            Click("Node_"+room.Id);yield return Ready(FlowScreen.Room);
            if(room.Event==EventKind.RestSite)Click("RestChoice");
            else if(room.Event!=EventKind.Shop&&room.Event!=EventKind.Altar)Click("PassChoice");
            Click("RoomLeave");yield return Ready(FlowScreen.DungeonMap);
        }
        private IEnumerator NavigateToNextNormal()
        {
            var path=PathTo(n=>n.Type==RoomType.Battle&&n.Encounter==EnemyType.Normal,true);Assert.NotNull(path,"Seed must have a connected second normal encounter");
            foreach(var id in path)
            {
                var node=DungeonRuntime.Node(Run,id);
                if(node.Type==RoomType.Battle){Click("Node_"+id);yield return Ready(FlowScreen.Battle);yield break;}
                yield return VisitNonBattle(node);
            }
        }
        private IEnumerator Fight(string acquired=null,bool requireVictory=true)
        {
            int steps=0;
            while(Flow.Session.Screen==FlowScreen.Battle&&Flow.Session.Battle.Phase==BattlePhase.PlayerTurn&&steps++<220)
            {
                var b=Flow.Session.Battle.Runtime;var available=Controller.View.Cards.Where(c=>Flow.Session.Battle.CanPlay(c.InstanceId)).ToArray();
                bool waiting=acquired!=null&&!playedAcquired;
                var chosen=available.FirstOrDefault(c=>waiting&&c.InstanceId==acquired);
                int incoming=b.Enemies.Where(e=>e.IsAlive).Sum(e=>e.CurrentIntent.TotalDamage);
                bool canKill=b.Enemies.Count(e=>e.IsAlive)==1&&available.Sum(c=>c.Definition.BaseDamage)>=b.Enemy.CurrentHP+b.Enemy.Block;
                if(chosen==null&&waiting)chosen=available.FirstOrDefault(c=>c.Definition.BaseDamage==0);
                if(chosen==null&&!waiting)
                    chosen=available.FirstOrDefault(c=>!canKill&&c.Definition.Block>0&&b.Block<incoming)??
                        available.FirstOrDefault(c=>c.Definition.Id=="P17")??
                        available.OrderByDescending(c=>c.Definition.BaseDamage*2+c.Definition.Effects.Where(e=>e.Type==EffectType.Poison).Sum(e=>e.Amount*3)).FirstOrDefault();
                if(chosen!=null)
                {
                    string instance=chosen.InstanceId;Pointer(chosen.gameObject);yield return null;
                    var choice=UnityEngine.Object.FindObjectsByType<StyledButton>(FindObjectsSortMode.None).FirstOrDefault(x=>x.name.StartsWith("Recover_")||x.name=="CleansePoison");
                    if(choice!=null)Pointer(choice.gameObject);
                    // Multiple-target selection, if an ordinary encounter includes more than one enemy.
                    if(b.LastCardResult==null||b.LastCardResult.CardInstanceId!=instance)
                    {
                        var target=Controller.View.EnemyFor(b.Enemies.FirstOrDefault(e=>e.IsAlive));
                        if(target!=null)Pointer(target.gameObject);
                    }
                    if(steps==1&&b.LastCardResult?.CardInstanceId==instance)
                    {
                        Canvas.ForceUpdateCanvases();
                        var release=chosen.GetComponentsInChildren<BattleVfx>().Single(fx=>fx.name=="Card use effect");
                        Assert.IsFalse(release.raycastTarget);
                        Assert.Greater(release.canvasRenderer.GetMesh().vertexCount,0);
                    }
                    yield return Settled();
                    Assert.AreEqual(instance,b.LastCardResult?.CardInstanceId,"UI must actually commit the selected card");
                    if(instance==acquired)playedAcquired=true;
                    reactions+=b.LastCardResult.Events.Count(e=>e.Reaction.HasValue);
                }
                else {Click("EndTurn");yield return Settled();}
            }
            Assert.Less(steps,220,"Finite normal-run card strategy");
            if(requireVictory)Assert.AreEqual(BattlePhase.Reward,Flow.Session.Battle.Phase,"Normal-run strategy must win without editing the game data");
        }
        private static IEnumerator CheckRulesFace(CardView card)
        {
            Canvas.ForceUpdateCanvases();
            Assert.AreEqual(card.Definition.Name,card.title.text);
            Assert.Greater(card.title.cachedTextGenerator.vertexCount,0,card.Definition.Id+" empty title mesh");
            Assert.GreaterOrEqual(card.title.cachedTextGenerator.characterCountVisible,card.title.text.Length-1,card.Definition.Id);
            var text=card.description;
            if(text.text!=card.Definition.Description)
            {
                Assert.IsTrue(text.text.EndsWith("…"));
                card.OnPointerEnter(new PointerEventData(EventSystem.current));yield return null;yield return null;
                var panel=card.GetComponentInParent<Canvas>().rootCanvas.transform.Find("Card rules rim/Full description");
                Assert.NotNull(panel,card.Definition.Id);
                text=panel.GetComponent<UnityEngine.UI.Text>();
            }
            Canvas.ForceUpdateCanvases();
            Assert.AreEqual(card.Definition.Description,text.text);
            Assert.GreaterOrEqual(text.cachedTextGenerator.characterCountVisible,text.text.Length-1,card.Definition.Id+" full rules");
            if(card.Hovered)card.OnPointerExit(new PointerEventData(EventSystem.current));
        }
        [UnityTest] public IEnumerator EveryRewardFaceFitsOrExposesItsFullRules()
        {
            yield return Start(CharacterId.Warrior);yield return NavigateToNextNormal();yield return Fight();
            var face=Controller.View.Root.GetComponentsInChildren<CardView>().First(c=>c.name.StartsWith("Reward_"));
            var rect=(RectTransform)face.transform;var min=rect.offsetMin;var max=rect.offsetMax;
            var art=Resources.Load<BattlePresentationAssets>("BrodunBattle");
            foreach(var card in Flow.Session.Definitions.AllCards)
            {
                face.Bind(card,card.Id,art,Flow.Session.Definitions,_=>{});
                rect.offsetMin=min;rect.offsetMax=max;
                yield return null;yield return null;
                yield return CheckRulesFace(face);
            }
        }
        private IEnumerator Choose(bool card,string capture=null)
        {
            var b=Flow.Session.Battle.Runtime;var reward=b.Reward;int gold=Run.Gold;int count=Run.Deck.Count;var rank=DungeonRuntime.Node(Run,b.RoomId).Encounter;
            Assert.IsTrue(reward.BaseGoldGranted);Assert.AreEqual(3,reward.Candidates.Distinct().Count());Assert.IsNull(FindButton("RewardSkip"));
            yield return null;Canvas.ForceUpdateCanvases();
            foreach(var candidate in Controller.View.Root.GetComponentsInChildren<CardView>().Where(c=>c.name.StartsWith("Reward_")))
            {
                Assert.AreEqual(candidate.Definition.Cost.ToString(),candidate.cost.text);
                yield return CheckRulesFace(candidate);
            }
            if(capture!=null)yield return Capture(capture);
            if(card)
            {
                string id=reward.Candidates.OrderByDescending(x=>b.Data.Card(x).BaseDamage).First();
                Pointer(Controller.View.Root.GetComponentsInChildren<CardView>().Single(c=>c.name=="Reward_"+id).gameObject);
                Assert.AreEqual(count+1,Run.Deck.Count);Assert.AreEqual(gold,Run.Gold);
            }
            else {Click("RewardGold");Assert.AreEqual(gold+(rank==EnemyType.Elite?25:15),Run.Gold);Assert.AreEqual(count,Run.Deck.Count);}
            Assert.IsFalse(Controller.TryChooseReward(RewardType.Gold));yield return Ready(FlowScreen.DungeonMap);
            Assert.Contains(reward.RoomId,Run.CompletedNodes.ToList());Assert.IsNotEmpty(DungeonRuntime.Available(Run));
        }
        private IEnumerator BasicProgress(CharacterId hero)
        {
            playedAcquired=false;reactions=0;yield return Start(hero);yield return NavigateToNextNormal();
            Assert.AreEqual(5,Flow.Session.Battle.Runtime.Deck.Hand.Count);Assert.AreEqual(5,Flow.Session.Battle.Runtime.Energy);
            Assert.AreEqual(1,Flow.Session.Battle.Runtime.Enemies.Count,"The first battle introduces the attack preview in a duel.");
            yield return Capture(hero+"_FormalStart");int gold=Run.Gold;yield return Fight();Assert.AreEqual(gold+10,Run.Gold);
            var offers=Flow.Session.Battle.Runtime.Reward.Candidates.Select(Flow.Session.Definitions.Card).ToArray();
            Assert.AreEqual(3,offers.Select(card=>card.Build).Distinct().Count());
            Assert.IsTrue(offers.All(card=>card.Owner==hero));
            yield return Choose(true,hero+"_NormalCardReward");string acquired=Run.AcquiredCards.Last();
            Click("DeckButton");yield return null;yield return Capture(hero+"_AcquiredDeck");Click("DeckClose");yield return null;
            yield return NavigateToNextNormal();
            var second=Flow.Session.Battle.Runtime;
            CollectionAssert.AreEqual(new[]{IntentType.Attack,IntentType.Buff},second.Enemies.Select(enemy=>enemy.CurrentIntent.Type));
            Assert.AreEqual(5,second.Deck.Hand.Count);
            Assert.IsTrue(second.Deck.Hand.Any(card=>card.InstanceId==acquired),"The first reward replaces a draw in the second opening hand.");
            gold=Run.Gold;
            yield return Fight(acquired);Assert.IsTrue(playedAcquired);Assert.AreEqual(gold+10,Run.Gold);
            yield return Choose(false,hero+"_NormalGoldReward");Assert.AreEqual(gold+25,Run.Gold);yield return Capture(hero+"_MapAfterTwoBattles");
            TestContext.WriteLine("PHASE9_NORMAL "+hero+" seed="+Run.Seed+" acquired="+Run.Deck.Single(c=>c.InstanceId==acquired).CardId+" played=True gold="+Run.Gold+" hp="+Run.CurrentHP+" reactions="+reactions+" completed="+string.Join(",",Run.CompletedNodes));
            var recorder=Flow.GetComponent<Phase9RenderedFrames>();if(recorder!=null)recorder.Finish();
        }
        [UnityTest] public IEnumerator WarriorFormalRunAcquiresUsesThenChoosesGold(){yield return BasicProgress(CharacterId.Warrior);}
        [UnityTest] public IEnumerator MageFormalRunAcquiresUsesThenChoosesGold(){yield return BasicProgress(CharacterId.Mage);}
        [UnityTest] public IEnumerator PriestFormalRunAcquiresUsesThenChoosesGold(){yield return BasicProgress(CharacterId.Priest);}
        [UnityTest] public IEnumerator PriestGeneratedDungeonReachesAnHonestTerminalResult()
        {
            yield return Start(CharacterId.Priest);int victories=0;bool elite=false,boss=false;int eliteChoices=0;
            while(Flow.Session.Screen==FlowScreen.DungeonMap)
            {
                var path=!elite?PathTo(n=>n.Type==RoomType.Battle&&n.Encounter==EnemyType.Elite,false):null;
                path=path??PathTo(n=>n.Type==RoomType.Boss,false);Assert.NotNull(path);
                foreach(var id in path)
                {
                    var node=DungeonRuntime.Node(Run,id);
                    if(node.Type!=RoomType.Battle&&node.Type!=RoomType.Boss){yield return VisitNonBattle(node);continue;}
                    Click("Node_"+id);yield return Ready(FlowScreen.Battle);int gold=Run.Gold;int area=Run.CurrentArea;var rank=node.Type==RoomType.Boss?EnemyType.Boss:node.Encounter;
                    Debug.Log("PHASE9_ROUTE room="+id+" rank="+rank+" hp="+Run.CurrentHP+" gold="+gold+" deck="+Run.Deck.Count);
                    yield return Fight(requireVictory:false);
                    if(Flow.Session.Screen==FlowScreen.RunResult)
                    {
                        boss=rank==EnemyType.Boss&&Run.Status==RunStatus.Complete;
                        if(boss){Assert.AreEqual(gold+30,Run.Gold);Assert.IsFalse(Flow.Session.Battle.Runtime.Reward.AllowsChoice);}
                        break;
                    }
                    if(rank==EnemyType.Boss)
                    {
                        Assert.AreEqual(FlowScreen.ThemeSelect,Flow.Session.Screen);
                        Assert.AreEqual(area,Run.CurrentArea);
                        Assert.AreEqual(gold+30,Run.Gold);
                        string next=DungeonThemes.All.First(theme=>!Run.HasVisitedTheme(theme.Id)).Id;
                        Click("Theme_"+next);
                        yield return Ready(FlowScreen.DungeonMap);
                        Assert.AreEqual(area+1,Run.CurrentArea);
                        break;
                    }
                    Assert.AreEqual(BattlePhase.Reward,Flow.Session.Battle.Phase);victories++;
                    Assert.AreEqual(gold+(rank==EnemyType.Elite?20:10),Run.Gold);
                    bool card=rank!=EnemyType.Elite||eliteChoices++==0;
                    yield return Choose(card,rank==EnemyType.Elite?"Priest_EliteReward_"+victories:null);
                    if(rank==EnemyType.Elite)elite=true;
                }
                if(Flow.Session.Screen==FlowScreen.RunResult)break;
            }
            yield return Ready(FlowScreen.RunResult);yield return Capture("Priest_GeneratedRun_"+Run.Status);
            TestContext.WriteLine("PHASE9_FULL seed="+Run.Seed+" status="+Run.Status+" normalOrEliteWins="+victories+" elite="+elite+" bossCleared="+boss+" hp="+Run.CurrentHP+" gold="+Run.Gold+" deck="+Run.Deck.Count+" completed="+string.Join(",",Run.CompletedNodes));
            Assert.IsTrue(Run.Status==RunStatus.Complete||Run.Status==RunStatus.Failed);
        }
        private IEnumerator Capture(string name)
        {
            string output=Environment.GetEnvironmentVariable("BRODUN_PHASE9_FORMAL_CAPTURE_DIR");if(string.IsNullOrEmpty(output))yield break;
            // Unity queues one screenshot request per frame. Let the named still finish before recording resumes.
            var recorder=Flow.GetComponent<Phase9RenderedFrames>();if(recorder!=null)recorder.Suspended=true;
            yield return new WaitForSecondsRealtime(.25f);Canvas.ForceUpdateCanvases();Directory.CreateDirectory(output);string path=Path.Combine(output,name+".png");
            ScreenCapture.CaptureScreenshot(path);yield return null;yield return null;
            float deadline=Time.realtimeSinceStartup+5;while(!File.Exists(path)&&Time.realtimeSinceStartup<deadline)yield return null;Assert.IsTrue(File.Exists(path));
            if(recorder!=null)recorder.Suspended=false;
        }
    }

    // Test-assembly-only capture of the current Unity renderer; it cannot ship in the game.
    // Real timestamps are retained so an exported review video need not change playback speed.
    public sealed class Phase9RenderedFrames:MonoBehaviour
    {
        private string folder;
        private float started,next;
        private int serial;
        private string previous;
        public bool Suspended { get; set; }
        public void Begin(string output)
        {
            folder=output;Directory.CreateDirectory(folder);started=Time.realtimeSinceStartup;next=started;
            File.WriteAllText(Path.Combine(folder,"frames.tsv"),"file\tseconds\tscreen\tphase\troom\n");
        }
        private void LateUpdate()
        {
            if(folder==null||Suspended||Time.realtimeSinceStartup<next)return;
            float now=Time.realtimeSinceStartup;next=now+.125f;
            var s=GameFlow.Instance.Session;string room=s.Battle?.Room.Id??s.Run?.CurrentNode??"";
            string phase=s.Screen==FlowScreen.Battle?s.Battle?.Phase.ToString()??"":"";
            string file="frame-"+(serial++).ToString("D5")+".png";
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,file));
            string state=s.Screen+"/"+phase+"/"+room;
            File.AppendAllText(Path.Combine(folder,"frames.tsv"),file+"\t"+(now-started).ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+"\t"+s.Screen+"\t"+phase+"\t"+room+"\n");
            if(state!=previous){Debug.Log("PHASE9_CAPTURE "+(now-started).ToString("F2")+" "+state);previous=state;}
        }
        public void Finish(){folder=null;enabled=false;}
    }
}

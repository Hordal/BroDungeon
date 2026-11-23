using System;
using System.Linq;
using System.Collections.Generic;
using Brodun.Flow;
using NUnit.Framework;
namespace Brodun.Tests
{
    public sealed class RandomDungeonTests : RulesTest
    {
        private static string Signature(GeneratedDungeon map)=>string.Join("|",map.Rooms.Select(n=>n.Id+":"+n.Layer+":"+n.X.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+":"+n.Y.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+":"+n.Type+":"+n.Encounter+":"+n.Event+":"+string.Join(",",n.Next)));
        [TestCase(0u)][TestCase(17u)][TestCase(29u)][TestCase(uint.MaxValue)]
        public void SameSeedReproducesEveryNodeConnectionRoomAndPosition(uint seed)
        {Assert.AreEqual(Signature(LayeredMapGenerator.Generate(seed)),Signature(LayeredMapGenerator.Generate(seed)));}
        [Test] public void DifferentSeedsChangeTopologyAndPositionsNotJustRoomTypes()
        {
            var maps=Enumerable.Range(0,32).Select(i=>LayeredMapGenerator.Generate((uint)i)).ToArray();
            Assert.Greater(maps.Select(m=>string.Join("|",m.Rooms.Select(n=>n.Id+":"+string.Join(",",n.Next)))).Distinct().Count(),16);
            Assert.Greater(maps.Select(m=>Signature(m)).Distinct().Count(),24);
        }
        [TestCase(0)][TestCase(256)][TestCase(512)][TestCase(768)]
        public void GraphAndRoomInvariantsAcross256Seeds(int offset)
        {
            for(uint seed=(uint)offset;seed<offset+256;seed++)
            {
                var map=LayeredMapGenerator.Generate(seed);var nodes=map.Rooms;var graph=map.Graph;
                string label="Seed "+seed;
                Assert.That(graph.IntermediateLayers,Is.InRange(6,7),label);Assert.That(nodes.Count,Is.InRange(13,23),label);
                Assert.AreEqual(1,nodes.Count(n=>n.Type==RoomType.Start),label);Assert.AreEqual(1,nodes.Count(n=>n.Type==RoomType.Boss),label);
                var byId=nodes.ToDictionary(n=>n.Id);
                foreach(var n in nodes)
                {
                    Assert.That(n.X,Is.InRange(0d,100d));Assert.That(n.Y,Is.InRange(0d,100d));
                    Assert.AreEqual(n.Layer==0?0:byId.Values.Count(p=>p.Next.Contains(n.Id)),n.Previous.Count,label);
                    if(n.Id!="boss")Assert.That(n.Next.Count,Is.InRange(1,2),label+" "+n.Id);
                    else Assert.AreEqual(0,n.Next.Count,label);
                    if(n.Id!="start")Assert.Greater(n.Previous.Count,0,label+" "+n.Id);
                    foreach(var next in n.Next){Assert.AreEqual(n.Layer+1,byId[next].Layer,label);CollectionAssert.Contains(byId[next].Previous,n.Id);}
                    if(n.Encounter==EnemyType.Elite)Assert.IsFalse(n.Previous.Any(p=>byId[p].Encounter==EnemyType.Elite),label);
                    if(n.Type==RoomType.Event)Assert.IsFalse(n.Previous.Any(p=>byId[p].Type==RoomType.Event&&byId[p].Event==n.Event),label);
                    // Reverse-layer induction: every child reaches boss; no cycles/dead ends possible.
                    var reachable=new HashSet<string>{n.Id};var queue=new Queue<string>();queue.Enqueue(n.Id);
                    while(queue.Count>0)foreach(var next in byId[queue.Dequeue()].Next)if(reachable.Add(next))queue.Enqueue(next);
                    Assert.IsTrue(reachable.Contains("boss"),label+" "+n.Id);
                }
                var reached=new HashSet<string>{"start"};foreach(var n in nodes)if(reached.Contains(n.Id))foreach(var next in n.Next)reached.Add(next);
                Assert.AreEqual(nodes.Count,reached.Count,label);
                foreach(var layer in nodes.Where(n=>n.Layer>0&&n.Id!="boss").GroupBy(n=>n.Layer))
                {
                    if(layer.Key==graph.IntermediateLayers)Assert.AreEqual(1,layer.Count(),label);
                    else Assert.That(layer.Count(),Is.InRange(2,4));
                    if(layer.Key==(graph.IntermediateLayers+1)/2)
                        Assert.IsFalse(layer.Any(n=>n.Type==RoomType.Battle),label);
                }
                Assert.IsTrue(nodes.Any(n=>n.Type==RoomType.Event&&n.Event==EventKind.Shop),label);
                var merchant=byId[byId["boss"].Previous.Single()];
                Assert.AreEqual(EventKind.Shop,merchant.Event,label);
                Assert.AreEqual(RoomType.Event,merchant.Type,label);
                var final=nodes.Where(n=>n.Layer==graph.IntermediateLayers-1).ToArray();
                Assert.IsTrue(final.All(n=>n.Next.Single()==merchant.Id),label);
                Assert.IsTrue(final.All(n=>n.Type==RoomType.Battle),label);Assert.AreEqual(1,final.Count(n=>n.Encounter==EnemyType.Elite),label);
                var edges=graph.Nodes.SelectMany(a=>a.Next.Select(id=>new{A=a,B=graph.Nodes.Single(b=>b.Id==id)})).ToArray();
                foreach(var a in edges)foreach(var b in edges.Where(e=>e.A.Layer==a.A.Layer))
                    Assert.GreaterOrEqual((a.A.Lane-b.A.Lane)*(a.B.Lane-b.B.Lane),0,"Crossed edges "+label);
            }
        }
        [Test]
        public void OpeningPathsLeadToTwoBattlesBeforeTheFirstBreak()
        {
            var config=new MapGenerationConfig(openingEncounters:true);
            for(uint seed=0;seed<128;seed++)
            {
                var dungeon=LayeredMapGenerator.Generate(seed,config);
                foreach(var room in dungeon.Rooms.Where(room=>room.Layer==1||room.Layer==2))
                    Assert.IsTrue(dungeon.IsOpeningBattle(room,room.Layer),room.Id+" / "+seed);
                Assert.IsTrue(dungeon.Rooms.Any(room=>room.Layer==(dungeon.Graph.IntermediateLayers+1)/2 && room.Event==EventKind.Shop));
            }
        }

        [Test] public void AssigningRoomsDoesNotChangeTopology()
        {
            var config=new MapGenerationConfig();var graph=LayeredMapGenerator.GenerateGraph(17,config);
            var a=RoomAssignment.Assign(graph,17,config);var b=RoomAssignment.Assign(graph,17,new MapGenerationConfig(battleWeight:10,eventWeight:70));
            CollectionAssert.AreEqual(a.Select(n=>string.Join(",",n.Next)),b.Select(n=>string.Join(",",n.Next)));
            CollectionAssert.AreEqual(graph.Nodes.Select(n=>n.Id),a.Select(n=>n.Id));
        }
        [Test] public void ConnectedChoicesOnlyAndStateChangesAfterExplicitRoomCompletion()
        {
            var f=new FlowSession(Data,s=>LayeredMapGenerator.Generate(s));f.FinishTransition(FlowScreen.Title);f.StartNewRun(17);
            f.FinishTransition(FlowScreen.CharacterSelect);f.ChooseCharacter(CharacterId.Warrior);f.FinishTransition(FlowScreen.DungeonMap);
            var r=f.Run;Assert.AreEqual("layered-v2",r.MapGeneratorVersion);
            Assert.IsFalse(f.SelectRoom("boss"));Assert.IsTrue(f.SelectRoom("r1"));Assert.AreEqual(0,DungeonRuntime.Available(r).Count);
            Assert.AreEqual(0,r.CompletedNodes.Count);Assert.IsFalse(f.SelectRoom("r2"));DungeonRuntime.Complete(r);
            CollectionAssert.AreEquivalent(DungeonRuntime.Node(r,"r1").Next,DungeonRuntime.Available(r));
            Assert.IsFalse(DungeonRuntime.Available(r).Contains("r2"));Assert.IsFalse(f.SelectRoom("boss"));
            string next=DungeonRuntime.Available(r)[0];Assert.IsTrue(f.SelectRoom(next));Assert.AreEqual(next,r.CurrentNode);
            f.BeginTransition();f.RestorePendingRoom();f.FinishTransition(FlowScreen.DungeonMap);Assert.AreSame(r,f.Run);Assert.AreEqual(next,f.PendingRoom.Node.Id);
        }
        [TestCase(9,1800,3110)][TestCase(11,1800,3730)][TestCase(1,1800,1800)]
        public void ContentWidthGrowsByLayerCount(int last,double viewport,double expected)
        {Assert.AreEqual(expected,DungeonMapLayout.ContentWidth(last,viewport));}
        [Test] public void LegacyModeStillMatchesWebMap()
        {Assert.AreEqual(11,Run().Map.Count);Assert.AreEqual("web-v1",Run().MapGeneratorVersion);}
    }
}

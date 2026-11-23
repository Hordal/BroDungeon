using System;
using System.Collections.Generic;
using System.Linq;
using Brodun.Data;
using Brodun.Flow;
using Brodun.Presentation;
using UnityEditor;
using UnityEngine;
namespace Brodun.Editor
{
    // Editor-only reproducibility aid. Chooses an unmodified production seed; never plays for the user.
    public static class Phase6Qa
    {
        private sealed class Guide {public uint Seed;public string[] Route;public int Score;}
        private static Guide Find(GameDefinitions data)
        {
            Guide best=null;
            for(uint seed=0;seed<256;seed++)
            {
                var map=LayeredMapGenerator.Generate(seed);var shopVisits=new Dictionary<string,RoomVisit>();
                var nodes=map.Rooms.ToDictionary(n=>n.Id);
                foreach(var path in Paths(nodes,"start",new List<string>()))
                {
                    var route=path.Select(id=>nodes[id]).ToArray();var shop=route.FirstOrDefault(n=>n.Event==EventKind.Shop);
                    if(shop==null||shop.Layer!=2||!route.Any(n=>n.Encounter==EnemyType.Elite)||
                        route.Where(n=>n.Type==RoomType.Event||n.Type==RoomType.Puzzle).Select(n=>n.Type+":"+n.Event).Distinct().Count()<2)continue;
                    if(!shopVisits.TryGetValue(shop.Id,out var visit))
                    {
                        var probe=new RunState(data,CharacterId.Priest,seed,dungeon:map);
                        DungeonRuntime.Enter(probe,path[0]);DungeonRuntime.Complete(probe);DungeonRuntime.Enter(probe,shop.Id);
                        visit=new RoomRuntime(data,probe,shop).Visit;shopVisits.Add(shop.Id,visit);
                    }
                    var offers=visit.Offers;
                    if(!offers.Any(o=>o.CardId=="ice_shard"))continue;
                    int score=route.Count(n=>n.Type==RoomType.Battle)*20-route.Count(n=>n.Event==EventKind.RestSite)*7-route.Count(n=>n.Type==RoomType.Puzzle)*3;
                    if(best==null||score<best.Score)best=new Guide{Seed=seed,Route=path,Score=score};
                }
                if(best!=null&&best.Score<=65)break;
            }
            if(best==null)throw new InvalidOperationException("No native QA path found.");return best;
        }
        private static IEnumerable<string[]> Paths(Dictionary<string,RoomNode> nodes,string id,List<string> path)
        {
            if(id=="boss"){yield return path.Concat(new[]{id}).ToArray();yield break;}
            foreach(string next in nodes[id].Next)
                foreach(var route in Paths(nodes,next,id=="start"?path:path.Concat(new[]{id}).ToList()))yield return route;
        }
        public static void Report()
        {
            var data=AssetDatabase.LoadAssetAtPath<GameCatalogData>(DataAssetBuilder.CatalogPath).ToDefinitions();var guide=Find(data);
            Debug.Log("PHASE6 NATIVE SEED "+guide.Seed+" ROUTE "+string.Join(" → ",guide.Route));
            var run=new RunState(data,CharacterId.Priest,guide.Seed,dungeon:LayeredMapGenerator.Generate(guide.Seed));
            foreach(string id in guide.Route)
            {
                var node=DungeonRuntime.Enter(run,id);if(node==null)throw new InvalidOperationException("Invalid guide edge "+id);
                if(node.Type==RoomType.Battle||node.Type==RoomType.Boss)
                {
                    var battle=new BattleContext(data,run,node);int actions=0;
                    while(battle.Phase==BattlePhase.PlayerTurn&&actions++<400)
                    {
                        var b=battle.Runtime;var available=b.Deck.Hand.Where(c=>battle.CanPlay(c.InstanceId)).ToArray();
                        var selected=available.FirstOrDefault(c=>data.Card(c.CardId).Cost==0)??
                            available.FirstOrDefault(c=>data.Card(c.CardId).Effects.Any(e=>e.Type==EffectType.Weak)&&b.CurrentIntent.Type==IntentType.Attack)??
                            available.FirstOrDefault(c=>data.Card(c.CardId).Effects.Any(e=>e.Type==EffectType.Strength))??
                            available.FirstOrDefault(c=>data.Card(c.CardId).Block>0&&b.Block<b.CurrentIntent.ExpectedHPDamage(0))??
                            available.OrderByDescending(c=>data.Card(c.CardId).BaseDamage+data.Card(c.CardId).Effects.Where(e=>e.Type==EffectType.Poison).Sum(e=>e.Amount*3)).FirstOrDefault();
                        if(selected!=null)battle.Play(selected.InstanceId);else battle.EndTurn();battle.FinishActionPresentation();
                    }
                    Debug.Log("GUIDE "+id+" "+battle.Runtime.Enemy.Definition.Id+" "+node.Encounter+" HP="+run.CurrentHP+" turns="+battle.Runtime.Turn+" "+battle.Phase);
                    if(battle.Phase!=BattlePhase.Victory)throw new InvalidOperationException("Guide cannot win unchanged combat "+id);
                    if(node.Type==RoomType.Boss)
                    {
                        if(!battle.CompleteBoss())throw new InvalidOperationException("Guide cannot complete boss "+id);
                        continue;
                    }
                    battle.OpenReward();
                    if(node.Layer==1){battle.ResolveReward(RewardType.Gold);Debug.Log("GUIDE reward Gold 50");}
                    else
                    {
                        string card=battle.Runtime.Reward.Candidates.OrderByDescending(c=>data.Card(c).BaseDamage+data.Card(c).Effects.Where(e=>e.Type==EffectType.Weak).Sum(e=>e.Amount*4)).First();
                        battle.ResolveReward(RewardType.Card,card);Debug.Log("GUIDE reward "+card);
                    }
                }
                else
                {
                    var room=new RoomRuntime(data,run,node);
                    if(node.Event==EventKind.Shop){int i=room.Visit.Offers.ToList().FindIndex(o=>o.CardId=="ice_shard");if(i>=0)room.Buy(i);}
                    else room.Choose(node.Event==EventKind.RestSite?RoomChoice.Rest:RoomChoice.Leave);
                    room.Finish();Debug.Log("GUIDE "+id+" "+node.Type+" "+node.Event+" HP="+run.CurrentHP+" GOLD="+run.Gold+" "+room.Visit.Feedback);
                }
            }
            Debug.Log("GUIDE FINAL "+run.Status+" HP="+run.CurrentHP+" GOLD="+run.Gold+" DECK="+run.Deck.Count);
        }
        [MenuItem("BRODUN/QA/PHASE 6 - Start reproducible normal run")]
        public static void Start()
        {
            var flow=GameFlow.Instance;if(!EditorApplication.isPlaying||flow==null||flow.Session.Screen!=FlowScreen.Title){Debug.Log("Stay on Title in Play Mode first.");return;}
            var guide=Find(flow.Session.Definitions);flow.StartNewRun(guide.Seed);
            Debug.Log("QA seed "+guide.Seed+". Priest route: "+string.Join(",",guide.Route)+". No stats/deck/rules changed; play with the mouse.");
        }
    }
}

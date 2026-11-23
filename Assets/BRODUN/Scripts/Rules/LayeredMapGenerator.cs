using System;
using System.Collections.Generic;
using System.Linq;
namespace Brodun
{
    public readonly struct NodePlacement
    {
        public double X { get; }
        public double Y { get; }
        public NodePlacement(double x,double y){X=x;Y=y;}
    }
    public sealed class GraphNode
    {
        public string Id { get; }
        public int Layer { get; }
        public int Lane { get; }
        internal readonly List<string> next=new List<string>(),previous=new List<string>();
        public IReadOnlyList<string> Next => next.AsReadOnly();
        public IReadOnlyList<string> Previous => previous.AsReadOnly();
        internal GraphNode(string id,int layer,int lane){Id=id;Layer=layer;Lane=lane;}
    }
    public sealed class DungeonGraph
    {
        public int IntermediateLayers { get; }
        public IReadOnlyList<GraphNode> Nodes { get; }
        public IReadOnlyDictionary<string,NodePlacement> Placements { get; }
        internal DungeonGraph(int count,List<GraphNode> nodes,Dictionary<string,NodePlacement> positions)
        {IntermediateLayers=count;Nodes=nodes.AsReadOnly();Placements=positions;}
    }
    public sealed class GeneratedDungeon
    {
        public string GeneratorVersion => "layered-v2";
        public DungeonGraph Graph { get; }
        public IReadOnlyList<RoomNode> Rooms { get; }
        private readonly bool openingEncounters;
        internal GeneratedDungeon(DungeonGraph graph,IReadOnlyList<RoomNode> rooms,bool openingEncounters=false)
        {Graph=graph;Rooms=rooms;this.openingEncounters=openingEncounters;}
        internal bool IsOpeningBattle(RoomNode room,int layer) => openingEncounters &&
            room.Layer==layer && room.Type==RoomType.Battle && room.Encounter==EnemyType.Normal;
    }
    public static class LayeredMapGenerator
    {
        private static int Pick(IRandomSource rng,int min,int max)=>min+(int)(rng.Next()*(max-min+1));
        public static GeneratedDungeon Generate(uint seed,MapGenerationConfig config=null)
        {
            config=config??MapGenerationConfig.Default;
            var graph=GenerateGraph(seed,config);
            return new GeneratedDungeon(graph,RoomAssignment.Assign(graph,seed,config),config.OpeningEncounters);
        }
        // Stage A: monotone neighbouring-layer coverage, then safe optional branch/merge edges.
        public static DungeonGraph GenerateGraph(uint seed,MapGenerationConfig config)
        {
            var rng=new SeededRandom(seed);int count=Pick(rng,config.MinLayers,config.MaxLayers),serial=1;
            var nodes=new List<GraphNode>();var layers=new List<List<GraphNode>>();
            layers.Add(new List<GraphNode>{new GraphNode("start",0,0)});
            for(int layer=1;layer<=count;layer++)
            {
                // The last two battle lanes converge at one shop, immediately before the boss.
                int width=layer==count?1:layer==1 || layer==count-1?2:Pick(rng,config.MinNodes,config.MaxNodes);
                var row=new List<GraphNode>();
                for(int lane=0;lane<width;lane++)row.Add(new GraphNode("r"+serial++,layer,lane));
                layers.Add(row);
            }
            layers.Add(new List<GraphNode>{new GraphNode("boss",count+1,0)});
            var positions=new Dictionary<string,NodePlacement>();
            var layoutRng=new SeededRandom(unchecked(seed+0x51EA77u));
            foreach(var row in layers)
                foreach(var node in row)
                {
                    nodes.Add(node);
                    // Layout is separate from GraphNode and has its own deterministic jitter stream.
                    double y=row.Count==1?.5:.15+node.Lane*.70/(row.Count-1)+(layoutRng.Next()-.5)*.012;
                    positions.Add(node.Id,new NodePlacement((double)node.Layer/(count+1),y));
                }
            Connect(layers[0][0],layers[1][0]);Connect(layers[0][0],layers[1][1]);
            for(int layer=1;layer<count;layer++)
            {
                var a=layers[layer];var b=layers[layer+1];
                for(int j=0;j<b.Count;j++)Connect(a[j*a.Count/b.Count],b[j]);
                for(int i=0;i<a.Count;i++)if(a[i].Next.Count==0)Connect(a[i],b[i*b.Count/a.Count]);
                foreach(int i in Shuffling.Shuffle(Enumerable.Range(0,a.Count),rng))
                {
                    if(a[i].Next.Count>=2 || rng.Next()>.55)continue;
                    int low=i==0?0:a[i-1].Next.Max(id=>b.FindIndex(n=>n.Id==id));
                    int high=i==a.Count-1?b.Count-1:a[i+1].Next.Min(id=>b.FindIndex(n=>n.Id==id));
                    var candidates=Enumerable.Range(low,high-low+1).Where(j=>!a[i].Next.Contains(b[j].Id)).ToArray();
                    if(candidates.Length>0)Connect(a[i],b[candidates[(int)(rng.Next()*candidates.Length)]]);
                }
            }
            foreach(var last in layers[count])Connect(last,layers[count+1][0]);
            return new DungeonGraph(count,nodes,positions);
        }
        private static void Connect(GraphNode a,GraphNode b)
        {if(a.next.Contains(b.Id))return;a.next.Add(b.Id);b.previous.Add(a.Id);}
    }
    public static class RoomAssignment
    {
        private sealed class Assignment {public RoomType Type;public EnemyType Enemy;public EventKind Event;}
        // Stage B: independent RNG, so room probability tuning cannot perturb graph topology.
        public static IReadOnlyList<RoomNode> Assign(DungeonGraph graph,uint seed,MapGenerationConfig config)
        {
            var rng=new SeededRandom(unchecked(seed+0xA35F19u));
            var byId=graph.Nodes.ToDictionary(n=>n.Id);var rooms=new Dictionary<string,Assignment>();
            foreach(var node in graph.Nodes)
            {
                var room=new Assignment{Type=RoomType.Battle,Enemy=EnemyType.Normal};
                if(node.Id=="start")room.Type=RoomType.Start;
                else if(node.Id=="boss"){room.Type=RoomType.Boss;room.Enemy=EnemyType.Boss;}
                else if(node.Layer>(config.OpeningEncounters?2:1))
                {
                    int total=config.BattleWeight+config.EliteWeight+config.PuzzleWeight+config.EventWeight;
                    int roll=(int)(rng.Next()*total);
                    if(roll<config.BattleWeight) { }
                    else if(roll<config.BattleWeight+config.EliteWeight)
                    {
                        // No connected consecutive elites; no elite on first or second layer.
                        if(node.Layer>2 && !node.Previous.Any(p=>rooms[p].Enemy==EnemyType.Elite))room.Enemy=EnemyType.Elite;
                    }
                    else if(roll<config.BattleWeight+config.EliteWeight+config.PuzzleWeight)room.Type=RoomType.Puzzle;
                    else
                    {
                        room.Type=RoomType.Event;
                        room.Event=new[]{EventKind.Shop,EventKind.Altar,EventKind.RestSite}[(int)(rng.Next()*3)];
                        if(node.Previous.Any(p=>rooms[p].Type==RoomType.Event && rooms[p].Event==room.Event))
                        {room.Type=RoomType.Battle;room.Event=EventKind.None;}
                    }
                }
                rooms.Add(node.Id,room);
            }
            // Give every route a respite halfway through. Adjacent lanes offer different activities.
            var pause=graph.Nodes.Where(n=>n.Layer==(graph.IntermediateLayers+1)/2).ToArray();
            int restLane=(int)(rng.Next()*pause.Length);
            foreach(var node in pause)
            {
                rooms[node.Id]=node.Lane==restLane
                    ?new Assignment{Type=RoomType.Event,Event=EventKind.RestSite}
                    :new Assignment{Type=RoomType.Puzzle};
            }
            // The main run buys time for a second battle by moving the early shop to the break.
            var shop=config.OpeningEncounters?pause.First(n=>n.Lane!=restLane):graph.Nodes.First(n=>n.Layer==2);
            rooms[shop.Id]=new Assignment{Type=RoomType.Event,Event=EventKind.Shop};
            foreach(var previous in shop.Previous)
                if(rooms[previous].Type==RoomType.Event&&rooms[previous].Event==EventKind.Shop)
                    rooms[previous]=new Assignment{Type=RoomType.Puzzle};
            // Keep the elite/normal choice on the approach to the shop.
            var merchant=graph.Nodes.Single(n=>n.Layer==graph.IntermediateLayers);
            rooms[merchant.Id]=new Assignment{Type=RoomType.Event,Event=EventKind.Shop};
            var final=graph.Nodes.Where(n=>n.Layer==graph.IntermediateLayers-1).ToArray();
            foreach(var node in final)rooms[node.Id]=new Assignment{Type=RoomType.Battle,Enemy=EnemyType.Normal};
            var elite=final[(int)(rng.Next()*final.Length)];rooms[elite.Id].Enemy=EnemyType.Elite;
            foreach(var previous in elite.Previous)rooms[previous].Enemy=EnemyType.Normal;
            // Prevent adjacent same altar/rest/shop, including the guaranteed shop override.
            foreach(var node in graph.Nodes.Where(n=>n.Layer>1))
                if(rooms[node.Id].Type==RoomType.Event && node.Previous.Any(p=>rooms[p].Type==RoomType.Event && rooms[p].Event==rooms[node.Id].Event))
                    rooms[node.Id]=new Assignment{Type=RoomType.Puzzle};
            var result=new List<RoomNode>();
            foreach(var node in graph.Nodes)
            {
                var r=rooms[node.Id];var pos=graph.Placements[node.Id];
                result.Add(new RoomNode(node.Id,r.Type,node.Layer,pos.Y*100,node.Next,r.Event,
                    pos.X*100,node.Layer==0?1:node.Layer,r.Enemy,node.Previous));
            }
            return result.AsReadOnly();
        }
    }
    // Pure layout math, shared by view and tests. Units are reference-canvas pixels, not gameplay.
    public static class DungeonMapLayout
    {
        public const double LayerSpacing=310,HorizontalPadding=160;
        public static double ContentWidth(int lastLayer,double viewportWidth)=>Math.Max(viewportWidth,2*HorizontalPadding+lastLayer*LayerSpacing);
        public static double X(int layer)=>HorizontalPadding+layer*LayerSpacing;
    }
}

using System.Collections.Generic;
using System.Linq;
namespace Brodun
{
    public sealed class RoomNode
    {
        public string Id { get; }
        public RoomType Type { get; }
        public int Column { get; }
        public int Floor { get; }
        public double X { get; }
        public double Y { get; }
        public EnemyType Encounter { get; }
        public EventKind Event { get; }
        public IReadOnlyList<string> Next { get; }
        public IReadOnlyList<string> Previous { get; }
        public int Layer => Column;
        public bool IsBossSanctuary => Type==RoomType.Event&&Event==EventKind.Shop&&Next.Contains("boss");
        internal RoomNode(string id, RoomType type, int column, double y, IEnumerable<string> next, EventKind kind,
            double? normalizedX = null,int? floor = null,EnemyType? encounter = null,IEnumerable<string> previous = null)
        {
            Id = id; Type = type; Column = column; Floor = column < 2 ? 1 : column < 4 ? 2 : 3;
            X = normalizedX ?? 7 + column * 17.2; Y = y;
            if(floor.HasValue)Floor=floor.Value;
            Encounter = encounter ?? (id == "r9" ? EnemyType.Elite : type == RoomType.Boss ? EnemyType.Boss : EnemyType.Normal);
            Event = kind; Next = System.Array.AsReadOnly(next.ToArray());
            Previous=System.Array.AsReadOnly((previous??new string[0]).ToArray());
        }
    }
    public static class DungeonRuntime
    {
        private static readonly string[][] Links = {
            new[]{"r1"}, new[]{"r2","r3","r4"}, new[]{"r5","r6"}, new[]{"r5","r6","r7"},
            new[]{"r6","r7"}, new[]{"r8"}, new[]{"r8","r9"}, new[]{"r9"}, new[]{"boss"}, new[]{"boss"}, new string[0]
        };
        public static List<RoomNode> Generate(uint seed)
        {
            var middle = Shuffling.Shuffle(new[]{RoomType.Battle, RoomType.Puzzle, RoomType.Event, RoomType.Battle, RoomType.Puzzle, RoomType.Event}, new SeededRandom(seed));
            var types = new[]{RoomType.Start,RoomType.Battle,middle[0],middle[1],middle[2],middle[3],middle[4],middle[5],RoomType.Battle,RoomType.Battle,RoomType.Boss};
            var columns = new[]{0,1,2,2,2,3,3,3,4,4,5};
            var ys = new double[]{50,50,20,50,80,20,50,80,32,68,50};
            var events = new[]{EventKind.Shop, EventKind.Altar, EventKind.RestSite};
            var rng = new SeededRandom(unchecked(seed + 5801u));
            var nodes = new List<RoomNode>();
            for (int i=0;i<types.Length;i++)
                nodes.Add(new RoomNode(i==0?"start":i==10?"boss":"r"+i, types[i], columns[i], ys[i], Links[i],
                    types[i]==RoomType.Event ? events[(int)(rng.Next()*3)] : EventKind.None));
            // Preserve legacy topology/results while exposing reverse links for both generators.
            return nodes.Select(n=>new RoomNode(n.Id,n.Type,n.Column,n.Y,n.Next,n.Event,
                previous:nodes.Where(p=>p.Next.Contains(n.Id)).Select(p=>p.Id))).ToList();
        }
        public static RoomNode Node(RunState run, string id) => run.Map.FirstOrDefault(n => n.Id == id);
        public static IReadOnlyList<string> Available(RunState run)
        {
            if (run.Status != RunStatus.InProgress || run.ActiveRoom != null) return new string[0];
            return Node(run, run.CurrentNode).Next.Where(id => !run.CompletedNodes.Contains(id)).ToArray();
        }
        public static RoomNode Enter(RunState run, string id)
        {
            if (!Available(run).Contains(id)) return null;
            var node = Node(run,id); run.CurrentNode = run.ActiveRoom = id; run.Floor = node.Floor; return node;
        }
        public static bool Complete(RunState run)
        {
            string id = run.ActiveRoom;
            if (id == null || run.Status!=RunStatus.InProgress || run.CurrentHP<=0) return false;
            if (!run.completed.Contains(id)) run.completed.Add(id);
            run.ActiveRoom = null;
            if (Node(run,id).Type == RoomType.Boss && run.CurrentArea>=run.AreaCount) run.Status = RunStatus.Complete;
            return true;
        }
    }
}

using System;
using System.Linq;
namespace Brodun.Flow
{
    public enum FlowScreen { Boot, Title, CharacterSelect, DungeonMap, Battle, Room, RunResult }
    public enum NodeVisualState { Locked, Available, Completed, Current }
    public sealed class PendingRoomContext
    {
        public RunState Run { get; }
        public RoomNode Node { get; }
        public bool IsBattle => Node.Type==RoomType.Battle || Node.Type==RoomType.Boss;
        public PendingRoomContext(RunState run,RoomNode node) { Run=run;Node=node; }
    }
    // Scene-independent ownership. One instance is retained by the persistent GameFlow.
    public sealed class FlowSession
    {
        public GameDefinitions Definitions { get; }
        public FlowScreen Screen { get; private set; }=FlowScreen.Boot;
        public RunState Run { get; private set; }
        public uint PendingSeed { get; private set; }
        public PendingRoomContext PendingRoom { get; private set; }
        public BattleContext Battle { get; private set; }
        public RoomRuntime Room { get; private set; }
        public RunSummary Summary { get; private set; }
        public bool IsTransitioning { get; private set; }
        public bool DeckOpen { get; private set; }
        private readonly Func<uint,GeneratedDungeon> dungeonFactory;
        public FlowSession(GameDefinitions data,Func<uint,GeneratedDungeon> mapFactory=null) { Definitions=data;dungeonFactory=mapFactory; }
        public bool BeginTransition() { if(IsTransitioning)return false;IsTransitioning=true;DeckOpen=false;return true; }
        public void FinishTransition(FlowScreen screen) { Screen=screen;IsTransitioning=false; }
        public bool StartNewRun(uint seed)
        {
            if(IsTransitioning || (Screen!=FlowScreen.Title&&Screen!=FlowScreen.RunResult))return false;
            Run=null;PendingRoom=null;Battle=null;Room=null;Summary=null;PendingSeed=seed;return BeginTransition();
        }
        public bool ChooseCharacter(CharacterId id)
        {
            if(IsTransitioning || Screen!=FlowScreen.CharacterSelect || !Definitions.Characters.Any(c=>c.Id==id))return false;
            Run=new RunState(Definitions,id,PendingSeed,dungeon:dungeonFactory?.Invoke(PendingSeed));PendingRoom=null;return BeginTransition();
        }
        public bool ReturnToTitle()
        {
            if(IsTransitioning)return false;
            Run=null;PendingRoom=null;Battle=null;Room=null;Summary=null;PendingSeed=0;return BeginTransition(); // Explicit run abandonment, no fake Save/Continue.
        }
        public bool BeginBattle()
        {
            if(IsTransitioning||Screen!=FlowScreen.DungeonMap||PendingRoom==null||!PendingRoom.IsBattle)return false;
            if(Battle==null)Battle=new BattleContext(Definitions,Run,PendingRoom.Node);
            if(Battle.Room.Id!=PendingRoom.Node.Id)return false;
            return BeginTransition();
        }
        public bool ReturnFromBattle()
        {
            if(IsTransitioning||Screen!=FlowScreen.Battle||Run?.Status!=RunStatus.InProgress||Battle?.Phase!=BattlePhase.Finished)return false;
            PendingRoom=null;Battle=null;return BeginTransition();
        }
        public bool BeginRoom()
        {
            if(IsTransitioning||Screen!=FlowScreen.DungeonMap||PendingRoom==null||PendingRoom.IsBattle)return false;
            Room=new RoomRuntime(Definitions,Run,PendingRoom.Node);return BeginTransition();
        }
        public bool ReturnFromRoom()
        {
            if(IsTransitioning||Screen!=FlowScreen.Room||Room==null||!Room.Finish())return false;
            Room=null;PendingRoom=null;return BeginTransition();
        }
        public bool FinishRun()
        {
            if(IsTransitioning||Run==null||(Screen!=FlowScreen.Battle&&Screen!=FlowScreen.Room))return false;
            if(Screen==FlowScreen.Battle && Battle?.Room.Type==RoomType.Boss && Battle.Phase==BattlePhase.Victory)
            {if(!Battle.CompleteBoss())return false;}
            if(Run.Status==RunStatus.InProgress)return false;
            Summary=new RunSummary(Run);return BeginTransition();
        }
        public bool SelectRoom(string id)
        {
            if(IsTransitioning || DeckOpen || Screen!=FlowScreen.DungeonMap || Run==null)return false;
            var node=DungeonRuntime.Enter(Run,id);
            if(node==null)return false;
            PendingRoom=new PendingRoomContext(Run,node);return true;
        }
        public NodeVisualState NodeState(string id)
        {
            if(Run==null)return NodeVisualState.Locked;
            if(Run.CurrentNode==id)return NodeVisualState.Current;
            if(Run.CompletedNodes.Contains(id))return NodeVisualState.Completed;
            return DungeonRuntime.Available(Run).Contains(id)?NodeVisualState.Available:NodeVisualState.Locked;
        }
        public bool OpenDeck()
        { if(IsTransitioning || Run==null || Screen!=FlowScreen.DungeonMap)return false;DeckOpen=true;return true; }
        public void CloseDeck() { DeckOpen=false; }
        public void RestorePendingRoom()
        {
            // Reconstructed from RunState after reloading a view; UI is never the source of truth.
            PendingRoom=Run?.ActiveRoom==null?null:new PendingRoomContext(Run,DungeonRuntime.Node(Run,Run.ActiveRoom));
        }
    }
}

using System.Linq;
namespace Brodun.Flow
{
    public enum BattlePhase { Initializing, PlayerTurn, ResolvingPlayerAction, EnemyTurn, Victory, Defeat, Reward, Finished }
    // Persistent encounter identity and input gate. All game calculations remain in PHASE 2 Runtime.
    public sealed class BattleContext
    {
        public RunState Run { get; }
        public RoomNode Room { get; }
        public BattleRuntime Runtime { get; }
        public uint Seed => Run.Seed;
        public string EnemyTargetId => "enemy-0";
        public BattlePhase Phase { get; private set; }=BattlePhase.Initializing;
        public BattleContext(GameDefinitions data,RunState run,RoomNode room)
        {Run=run;Room=room;Runtime=new BattleRuntime(data,run,room);Phase=BattlePhase.PlayerTurn;}
        public bool CanPlay(string id)
        {
            var card=Runtime.Deck.Hand.FirstOrDefault(c=>c.InstanceId==id);
            return Phase==BattlePhase.PlayerTurn && card!=null && Runtime.Energy>=Runtime.Data.Card(card.CardId).Cost;
        }
        public bool Play(string id,string targetId=null)
        {
            if(!CanPlay(id) || (targetId!=null&&targetId!=EnemyTargetId))return false;
            Phase=BattlePhase.ResolvingPlayerAction;
            if(Runtime.PlayCard(id))return true;
            Phase=BattlePhase.PlayerTurn;return false;
        }
        public bool EndTurn()
        {
            if(Phase!=BattlePhase.PlayerTurn)return false;
            Phase=BattlePhase.EnemyTurn;
            if(Runtime.EndTurn())return true;
            Phase=BattlePhase.PlayerTurn;return false;
        }
        public void FinishActionPresentation()
        {
            if(Phase!=BattlePhase.ResolvingPlayerAction&&Phase!=BattlePhase.EnemyTurn)return;
            Phase=Runtime.Result==BattleResult.Won?BattlePhase.Victory:Runtime.Result==BattleResult.Lost?BattlePhase.Defeat:BattlePhase.PlayerTurn;
        }
        public bool OpenReward()
        {if(Phase!=BattlePhase.Victory||Room.Type==RoomType.Boss)return false;Phase=BattlePhase.Reward;return true;}
        public bool CompleteBoss()
        {
            if(Room.Type!=RoomType.Boss||Phase!=BattlePhase.Victory||Run.Status!=RunStatus.InProgress)return false;
            if(!DungeonRuntime.Complete(Run))return false;
            Phase=BattlePhase.Finished;return true;
        }
        public bool ResolveReward(RewardType type,string cardId=null)
        {
            if(Phase!=BattlePhase.Reward || !RewardResolver.Resolve(Runtime.Data,Run,Runtime.Reward,type,cardId))return false;
            Phase=BattlePhase.Finished;return true;
        }
    }
}

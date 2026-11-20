using System;
using System.Linq;
namespace Brodun
{
    // Synchronous rules only. PHASE 4 will schedule visuals around these operations.
    public sealed class BattleRuntime
    {
        public GameDefinitions Data { get; }
        public RunState Run { get; }
        public string RoomId { get; }
        public EnemyInstance Enemy { get; }
        public DeckRuntime Deck { get; }
        public int Turn { get; private set; }=1;
        public int Energy { get; internal set; }
        public int MaxEnergy { get; }
        public int Block { get; internal set; }
        public int Strength { get; internal set; }
        public BattleResult Result { get; private set; }=BattleResult.Active;
        public CardResult LastCardResult { get; private set; }
        public EnemyActionResult LastEnemyAction { get; private set; }
        public PendingReward Reward { get; private set; }
        public IntentData CurrentIntent => IntentResolver.Preview(Enemy,Data.Config);
        public BattleRuntime(GameDefinitions data,RunState run,RoomNode room)
        {
            if(room==null || run.Status!=RunStatus.InProgress || run.ActiveRoom!=room.Id ||
                (room.Type!=RoomType.Battle && room.Type!=RoomType.Boss))throw new InvalidOperationException("Enter a battle room first.");
            Data=data; Run=run; RoomId=room.Id;
            Enemy=new EnemyInstance(IntentResolver.Select(data,run,room),room,data.Config);
            Energy=MaxEnergy=data.Character(run.SelectedCharacter).Energy;
            Deck=DeckRuntime.Begin(data,run,room); IntentResolver.Decide(Enemy);
        }
        public bool PlayCard(string instanceId)
        {
            if(Result!=BattleResult.Active||Run.ActiveRoom!=RoomId)return false;
            var instance=Deck.Hand.FirstOrDefault(c=>c.InstanceId==instanceId);
            if(instance==null)return false;
            var card=Data.Card(instance.CardId);
            if(Energy<card.Cost)return false;
            Energy-=card.Cost; Deck.Use(instanceId);
            LastCardResult=CardEffectResolver.Resolve(this,card);
            if(Enemy.CurrentHP<=0)Win();
            return true;
        }
        public bool EndTurn()
        {
            if(Result!=BattleResult.Active||Run.ActiveRoom!=RoomId)return false;
            // Capture before poison/other resolution; the action preview is never re-rolled.
            var snapshot=CurrentIntent;
            Deck.EndTurn();
            Enemy.CurrentHP-=Enemy.Status.TickPoison(Enemy.CurrentHP);
            if(Enemy.CurrentHP<=0){Win();return true;}
            LastEnemyAction=IntentResolver.Execute(Run,this,snapshot);
            if(Run.CurrentHP<=0){Result=BattleResult.Lost;Run.Status=RunStatus.Failed;return true;}
            Block=0; Energy=MaxEnergy; Turn++; Deck.NextTurn(); IntentResolver.Decide(Enemy);
            return true;
        }
        private void Win()
        {
            if(Result!=BattleResult.Active)return;
            Result=BattleResult.Won; Reward=RewardResolver.Create(Data,Run);
            // Active room remains pending until explicit Gold / Card / Skip resolution.
        }
    }
}

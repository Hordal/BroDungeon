using System;
using System.Collections.Generic;
using System.Linq;
namespace Brodun
{
    public sealed class PendingReward
    {
        public string Id { get; }
        public string RoomId { get; }
        public int Gold { get; }
        public IReadOnlyList<string> Candidates { get; }
        public bool Resolved { get; internal set; }
        internal PendingReward(string id,string room,int gold,IEnumerable<string> candidates)
        { Id=id; RoomId=room; Gold=gold; Candidates=Array.AsReadOnly(candidates.ToArray()); }
    }
    public static class RewardResolver
    {
        public static PendingReward Create(GameDefinitions data,RunState run)
        {
            if(run.ActiveRoom==null)throw new InvalidOperationException("No active room for reward.");
            if(data.Cards.Any(c=>c.IsReworked))return Card70Rewards.Create(data,run);
            int index=run.Map.ToList().FindIndex(n=>n.Id==run.ActiveRoom);
            var rng=new SeededRandom(unchecked(run.Seed+(uint)index*719u));
            var starters=data.Character(run.SelectedCharacter).StartingDeck;
            var candidates=Shuffling.Shuffle(data.Cards.Where(c=>!starters.Contains(c.Id)),rng)
                .Take(data.Reward.CandidateCount).Select(c=>c.Id);
            return new PendingReward(run.RunId+":"+run.ActiveRoom,run.ActiveRoom,data.Reward.Gold,candidates);
        }
        public static bool Resolve(GameDefinitions data,RunState run,PendingReward reward,RewardType kind,string cardId=null)
        {
            if(run.Status!=RunStatus.InProgress||run.CurrentHP<=0||reward==null||reward.Resolved||run.ResolvedRewards.Contains(reward.Id)||run.ActiveRoom!=reward.RoomId)return false;
            if(!Enum.IsDefined(typeof(RewardType),kind))return false;
            if(kind==RewardType.Card&&!reward.Candidates.Contains(cardId))return false;
            // Validate all fallible input before changing reward/run state.
            if(kind==RewardType.Card){data.Card(cardId);if(!Card70Rewards.CanAcquire(data,run,cardId))return false;}
            if(kind==RewardType.Gold && (reward.Gold<0 || (long)run.Gold+reward.Gold>int.MaxValue))return false;
            if(kind==RewardType.Gold)run.AddGold(reward.Gold);
            if(kind==RewardType.Card)run.AddCard(cardId);
            reward.Resolved=true; run.resolved.Add(reward.Id);
            run.results[reward.RoomId]=new RoomResult(kind,cardId,kind==RewardType.Gold?reward.Gold:0);
            DungeonRuntime.Complete(run); return true;
        }
    }
}

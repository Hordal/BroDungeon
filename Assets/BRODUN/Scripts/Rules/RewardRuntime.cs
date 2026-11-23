using System;
using System.Collections.Generic;
using System.Linq;
namespace Brodun
{
    public sealed class PendingReward
    {
        public string Id { get; }
        public string RoomId { get; }
        public int Gold { get; } // Optional additional gold, not the automatic victory grant.
        public int BaseGold { get; }
        public bool BaseGoldGranted { get; internal set; }
        public bool Formal { get; }
        public bool IsBoss { get; }
        public bool IsOpeningReward { get; }
        public bool AllowsChoice=>!Formal||!IsBoss;
        public IReadOnlyList<string> Candidates { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public bool Resolved { get; internal set; }
        internal PendingReward(string id,string room,int gold,IEnumerable<string> candidates,int baseGold=0,bool formal=false,bool boss=false,IEnumerable<string> diagnostics=null,bool isOpeningReward=false)
        {
            Id=id;RoomId=room;Gold=gold;BaseGold=baseGold;Formal=formal;IsBoss=boss;IsOpeningReward=isOpeningReward;
            Candidates=Array.AsReadOnly(candidates.ToArray());Diagnostics=Array.AsReadOnly((diagnostics??Array.Empty<string>()).ToArray());
        }
    }
    public static class RewardResolver
    {
        public static IReadOnlyList<string> SampleCandidates(GameDefinitions data,CharacterId hero,EnemyType rank,IRandomSource rng,int count,
            ICollection<string> diagnostics=null)
        {
            var policy=data.Reward.Policy??new RewardPolicy();var tier=policy.For(rank);
            var available=data.RewardCardsFor(hero).ToList();var chosen=new List<string>();
            for(int slot=0;slot<count&&available.Count>0;slot++)
            {
                bool unique=rng.Next()*(policy.UniqueWeight+policy.ElementWeight)<policy.UniqueWeight;
                var category=available.Where(c=>c.Owner.HasValue==unique).ToArray();
                if(category.Length==0)category=available.Where(c=>c.Owner.HasValue!=unique).ToArray();
                var grades=new[]{CardRarity.Low,CardRarity.Middle,CardRarity.High};
                var usable=Enumerable.Range(0,3).Where(i=>category.Any(c=>c.Rarity==grades[i])).ToArray();
                var weights=usable.Select(i=>tier.RarityWeights[i]).ToArray();
                int grade=usable[WeightedIndex(weights,rng)];
                var pool=category.Where(c=>c.Rarity==grades[grade]).OrderBy(c=>c.Id,StringComparer.Ordinal).ToArray();
                var card=pool[Math.Min(pool.Length-1,(int)(rng.Next()*pool.Length))];chosen.Add(card.Id);available.Remove(card);
            }
            if(chosen.Count<count)diagnostics?.Add("Reward catalog has only "+chosen.Count+" distinct eligible cards for "+hero+" / "+rank+".");
            return chosen.AsReadOnly();
        }
        private static IEnumerable<string> OpeningChoices(GameDefinitions data,CharacterId hero)
        {
            var cards=hero switch
            {
                CharacterId.Warrior=>new[]{"W13","W20","W07"},
                CharacterId.Mage=>new[]{"M04","M16","M17"},
                CharacterId.Priest=>new[]{"P12","P24","P07"},
                _=>Array.Empty<string>()
            };
            var eligible=new HashSet<string>(data.RewardCardsFor(hero).Select(card=>card.Id));
            return cards.Where(eligible.Contains);
        }

        private static int WeightedIndex(int[] weights,IRandomSource rng)
        {
            int total=weights.Sum();if(total==0)return Math.Min(weights.Length-1,(int)(rng.Next()*weights.Length));
            double roll=rng.Next()*total;for(int i=0;i<weights.Length;i++){roll-=weights[i];if(roll<0)return i;}
            return weights.Length-1;
        }
        public static PendingReward Create(GameDefinitions data,RunState run)
        {
            if(run.ActiveRoom==null)throw new InvalidOperationException("No active room for reward.");
            if(run.pendingBattleRewards.TryGetValue(run.ActiveRoom,out var cached))return cached;
            var room=DungeonRuntime.Node(run,run.ActiveRoom);bool formal=data.Reward.Policy!=null;
            var rank=room.Type==RoomType.Boss?EnemyType.Boss:room.Encounter;
            bool openingReward=formal&&run.GeneratedDungeon?.IsOpeningBattle(room,1)==true;
            IEnumerable<string> candidates;var diagnostics=new List<string>();int basic=0,extra=data.Reward.Gold;
            if(formal)
            {
                var tier=data.Reward.Policy.For(rank);basic=tier.BaseGold;extra=tier.ExtraGold;
                string catalog=string.Join("|",data.RewardCardsFor(run.SelectedCharacter).Select(c=>c.Id+":"+c.Owner+":"+c.Rarity+":"+c.Cost+":"+c.Element));
                var rng=new SeededRandom(EncounterPool.NodeSeed(run.AreaSeed,run.ActiveRoom+"/"+run.SelectedCharacter+"/"+rank+"/"+catalog,0xB09D91u));
                int count=data.Reward.CandidateCount;
                if(rank==EnemyType.Boss)candidates=Array.Empty<string>();
                else if(data.Cards.Any(c=>c.IsReworked))candidates=Card70Rewards.Candidates(data,run);
                else
                {
                    var opening=openingReward
                        ?OpeningChoices(data,run.SelectedCharacter).Take(count).ToArray()
                        :Array.Empty<string>();
                    candidates=opening.Length==count?opening:opening.Concat(
                        SampleCandidates(data,run.SelectedCharacter,rank,rng,count,diagnostics)).Distinct().Take(count);
                }
            }
            else
            {
                int index=run.Map.ToList().FindIndex(n=>n.Id==run.ActiveRoom);
                var rng=new SeededRandom(unchecked(run.AreaSeed+(uint)index*719u));
                var starters=data.Character(run.SelectedCharacter).StartingDeck;
                candidates=Shuffling.Shuffle(data.Cards.Where(c=>!starters.Contains(c.Id)),rng).Take(data.Reward.CandidateCount).Select(c=>c.Id);
            }
            string areaKey=run.CurrentArea==1?"":run.CurrentArea+":";
            var reward=new PendingReward(run.RunId+":"+areaKey+run.ActiveRoom,run.ActiveRoom,extra,candidates,basic,formal,rank==EnemyType.Boss,diagnostics,openingReward);
            run.pendingBattleRewards.Add(run.ActiveRoom,reward);return reward;
        }
        internal static bool GrantVictory(BattleRuntime battle)
        {
            var reward=battle.Reward;var run=battle.Run;
            if(battle.Result!=BattleResult.Won||reward==null||!reward.Formal||run.ActiveRoom!=reward.RoomId||run.Status!=RunStatus.InProgress)return false;
            if(run.paidVictoryRooms.Contains(reward.RoomId)){reward.BaseGoldGranted=true;return false;}
            run.AddGold(reward.BaseGold);run.paidVictoryRooms.Add(reward.RoomId);reward.BaseGoldGranted=true;
            if(reward.IsBoss)
            {
                reward.Resolved=true;run.resolved.Add(reward.Id);
                run.results[reward.RoomId]=new RoomResult(RewardType.Gold,null,reward.BaseGold);
            }
            return true;
        }
        public static bool Resolve(GameDefinitions data,RunState run,PendingReward reward,RewardType kind,string cardId=null)
        {
            if(run.Status!=RunStatus.InProgress||run.CurrentHP<=0||reward==null||reward.Resolved||run.ResolvedRewards.Contains(reward.Id)||run.ActiveRoom!=reward.RoomId)return false;
            if(!Enum.IsDefined(typeof(RewardType),kind))return false;
            if(reward.Formal&&(!reward.AllowsChoice||!reward.BaseGoldGranted||!run.paidVictoryRooms.Contains(reward.RoomId)||kind==RewardType.Skip))return false;
            if(!run.pendingBattleRewards.TryGetValue(reward.RoomId,out var cached)||!ReferenceEquals(cached,reward))return false;
            if(kind==RewardType.Card)
            {
                if(!reward.Candidates.Contains(cardId))return false;
                var card=data.AllCards.FirstOrDefault(c=>c.Id==cardId);
                if(card==null||card.Owner.HasValue&&card.Owner!=run.SelectedCharacter)return false;
                if(reward.Formal&&(!card.IsFormalCard||card.StarterOnly))return false;
            }
            if(kind==RewardType.Gold&&(reward.Gold<0||(long)run.Gold+reward.Gold>int.MaxValue))return false;
            if(kind==RewardType.Gold)run.AddGold(reward.Gold);
            if(kind==RewardType.Card)run.AddCard(cardId);
            reward.Resolved=true;run.resolved.Add(reward.Id);
            run.results[reward.RoomId]=new RoomResult(kind,cardId,reward.BaseGold+(kind==RewardType.Gold?reward.Gold:0));
            DungeonRuntime.Complete(run);return true;
        }
    }
}

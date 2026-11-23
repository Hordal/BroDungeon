using System;
using System.Collections.Generic;
using System.Linq;
namespace Brodun
{
    // Synchronous rules. Presentation consumes recorded results after resolution.
    public sealed partial class BattleRuntime
    {
        public GameDefinitions Data { get; }
        public RunState Run { get; }
        public string RoomId { get; }
        public IReadOnlyList<EnemyInstance> Enemies { get; }
        public EnemyInstance Enemy => Enemies[0];
        public DeckRuntime Deck { get; }
        public int Turn { get; private set; }=1;
        public bool UsesReworkedCards => Data.Cards.Any(card=>card.IsReworked);
        public int Energy { get; internal set; }
        public int MaxEnergy { get; }
        public int Block { get; internal set; }
        public int Strength { get; internal set; }
        public BattleResult Result { get; private set; }=BattleResult.Active;
        public CardResult LastCardResult { get; private set; }
        public CombatTrace LastTurnResult { get; private set; }
        private readonly List<CombatEvent> blockHistory=new List<CombatEvent>();
        private readonly List<CounterEffect> counters=new List<CounterEffect>();
        private int actionId,nextSharedCostReduction,blockRetention;
        private int nextTurnDraw,spendDraw;
        private string spendDrawId,recycleId;
        private EffectRule recycleRule;
        private readonly HashSet<string> recoveredThisTurn=new HashSet<string>();
        public int BlockSpentThisTurn { get; private set; }
        public int RestoredBlockThisTurn { get; private set; }
        public bool CounteredLastEnemyTurn { get; private set; }
        private bool counteredThisEnemyTurn;
        private string firstHealBlockId,turnStartBlockId;
        private int firstHealBlock,turnStartBlock;
        public bool HealedThisTurn { get; private set; }
        public StatusState PlayerStatus { get; }=new StatusState();
        private string firstFireId,nextElementId;
        private int firstFirePower,nextElementPower;
        private bool firstFireUsed;
        private string allElementId,sharedDrawId,firstReactionId;
        private int allElementPower,sharedDraw,firstReactionDamage;
        private readonly HashSet<string> sharedInstancesThisTurn=new HashSet<string>();
        public int SharedCardsUsedThisTurn { get; private set; }
        public bool ReactedThisTurn { get; private set; }
        public bool RecoveredSharedThisTurn { get; private set; }
        internal string FirstFirePowerId=>firstFireId;
        public IReadOnlyList<CombatEvent> BlockHistory => blockHistory.AsReadOnly();
        public IReadOnlyList<EnemyActionResult> LastEnemyActions { get; private set; }=Array.Empty<EnemyActionResult>();
        public EnemyActionResult LastEnemyAction => LastEnemyActions.LastOrDefault();
        public PendingReward Reward { get; private set; }
        public IntentData CurrentIntent => IntentResolver.Preview(Enemy,Data.Config);
        public BattleRuntime(GameDefinitions data,RunState run,RoomNode room,EnemyDefinition companion=null,
            IEnumerable<EnemyDefinition> additionalEnemies=null)
        {
            if(room==null || run.Status!=RunStatus.InProgress || run.ActiveRoom!=room.Id ||
                (room.Type!=RoomType.Battle && room.Type!=RoomType.Boss))throw new InvalidOperationException("Enter a battle room first.");
            Data=data; Run=run; RoomId=room.Id;
            var primary=new EnemyInstance(IntentResolver.Select(data,run,room),room,data.Config);
            var lineup=new List<EnemyInstance>{primary};
            companion??=EncounterPool.OpeningCompanion(data,run,room);
            if(companion!=null)lineup.Add(new EnemyInstance(companion,room,data.Config,"enemy-1"));
            if(additionalEnemies!=null)
                foreach(var enemy in additionalEnemies)
                    lineup.Add(new EnemyInstance(enemy,room,data.Config,"enemy-"+lineup.Count));
            Enemies=lineup.AsReadOnly();
            Energy=MaxEnergy=data.BaseEnergyFor(run.SelectedCharacter);
            Deck=DeckRuntime.Begin(data,run,room);
            InitializeItems();
            foreach(var enemy in Enemies)IntentResolver.Decide(enemy);
        }
        public EnemyInstance ResolveEnemyTarget(string targetId)
        {
            if(targetId!=null)return Enemies.FirstOrDefault(enemy=>enemy.IsAlive&&enemy.TargetId==targetId);
            EnemyInstance only=null;
            foreach(var enemy in Enemies)
            {
                if(!enemy.IsAlive)continue;
                if(only!=null)return null;
                only=enemy;
            }
            return only;
        }
        public int GetCardCost(CardDefinition card) =>
            Math.Max(0,card.Cost-(card.ElementApplication==ElementApplication.SharedAttack?nextSharedCostReduction:0));
        public void DiscountNextSharedAttack(int amount) => nextSharedCostReduction=Math.Max(nextSharedCostReduction,amount);
        public bool CanPlayCard(string instanceId)
        {
            var instance=Deck.Hand.FirstOrDefault(c=>c.InstanceId==instanceId);
            if(instance!=null&&Data.Card(instance.CardId).IsReworked)
                return Run.ActiveRoom==RoomId&&CanPlay70(instanceId);
            return Result==BattleResult.Active&&Run.ActiveRoom==RoomId&&instance!=null&&
                (!Data.Card(instance.CardId).Owner.HasValue||Data.Card(instance.CardId).Owner==Run.SelectedCharacter)&&
                Energy>=GetCardCost(Data.Card(instance.CardId));
        }
        internal IReadOnlyList<EnemyInstance> SelectCardTargets(CardDefinition card,string targetId)
        {
            if(!card.RequiresEnemyTarget)return targetId==null?Array.Empty<EnemyInstance>():null;
            var living=Enemies.Where(e=>e.IsAlive).ToArray();
            if(card.TargetScope==CardTargetScope.AllEnemies)
                return living.Length>0&&targetId==null?living:null;
            var primary=ResolveEnemyTarget(targetId);
            if(primary==null)return null;
            if(card.TargetScope==CardTargetScope.Chain)
                return new[]{primary}.Concat(living.Where(e=>e!=primary).Take(card.SecondaryTargets)).ToArray();
            return new[]{primary};
        }
        public bool PlayCard(string instanceId,string targetId=null,StatusEffectType? cleanseStatus=null,string recoveryInstanceId=null,CardPlayOptions options=null)
        {
            if(Result!=BattleResult.Active||Run.ActiveRoom!=RoomId)return false;
            var instance=Deck.Hand.FirstOrDefault(c=>c.InstanceId==instanceId);
            if(instance==null)return false;
            var card=Definition(instance);
            if(card.Owner.HasValue&&card.Owner!=Run.SelectedCharacter)return false;
            if(card.IsReworked)
            {
                var chosen=card.RequiresEnemyTarget?ResolveEnemyTarget(targetId):Enemies.FirstOrDefault(e=>e.IsAlive);
                return chosen!=null&&Play70(instance,card,options,chosen);
            }
            int paid=GetCardCost(card);
            if(Energy<paid)return false;
            var targets=SelectCardTargets(card,targetId);
            if(targets==null||!TryCleanseChoice(card,cleanseStatus,out cleanseStatus)||!TryRecoveryChoice(card,recoveryInstanceId,out recoveryInstanceId))return false;
            var trace=NewTrace(card.Id);
            ChangeEnergy(trace,Energy-paid,CombatCause.CardCost);
            if(card.ElementApplication==ElementApplication.SharedAttack)nextSharedCostReduction=0;
            Deck.Use(instanceId);
            trace.Add(card.Exhausts?CombatEventKind.CardExhausted:CombatEventKind.CardDiscarded,CombatCause.Card,
                CombatTrace.PlayerId,CombatTrace.PlayerId,effectId:instanceId);
            LastCardResult=CardEffectResolver.Resolve(this,card,targets,trace,instanceId,paid,cleanseStatus,recoveryInstanceId);
            if(Enemies.All(enemy=>!enemy.IsAlive))Win();
            return true;
        }
        public bool EndTurn()
        {
            if(Result!=BattleResult.Active||Run.ActiveRoom!=RoomId)return false;
            if(PendingDiscard)return false;
            bool reworked=UsesReworkedCards;
            // Capture before poison/other resolution; the action preview is never re-rolled.
            var pending=Enemies.Where(enemy=>enemy.IsAlive)
                .Select(enemy=>(Enemy:enemy,Intent:IntentResolver.Preview(enemy,Data.Config))).ToArray();
            var trace=NewTrace();
            LastTurnResult=trace;
            counteredThisEnemyTurn=false;
            if(reworked)End70(trace);
            Deck.EndTurn();
            foreach(var action in pending)TickPoison(trace,action.Enemy);
            var actions=new List<EnemyActionResult>();
            LastEnemyActions=actions.AsReadOnly();
            if(Enemies.Any(enemy=>enemy.IsAlive))
            {
                foreach(var action in pending)
                {
                    if(!action.Enemy.IsAlive)continue;
                    actions.Add(IntentResolver.Execute(Run,this,action.Intent,action.Enemy,trace));
                    if(Run.CurrentHP<=0)break;
                }
            }
            CounteredLastEnemyTurn=counteredThisEnemyTurn;
            if(Run.CurrentHP<=0){Result=BattleResult.Lost;Run.Status=RunStatus.Failed;ClearBattleEffects();}
            else if(Enemies.All(enemy=>!enemy.IsAlive))Win();
            else if(reworked)
            {
                Start70();
                foreach(var enemy in Enemies.Where(e=>e.IsAlive))IntentResolver.Decide(enemy);
            }
            else
            {
                ChangeBlock(trace,Math.Min(Block,blockRetention),blockRetention>0?CombatCause.BlockRetained:CombatCause.BlockReset);
                blockRetention=0; nextSharedCostReduction=0;
                counters.RemoveAll(c=>c.ExpiresAfterEnemyTurn);
                foreach(var counter in counters)counter.ResetTurn();
                ChangeEnergy(trace,MaxEnergy,CombatCause.TurnStart);
                Turn++; Deck.NextTurn();
                if(nextTurnDraw>0)DrawCards(trace,nextTurnDraw,CombatCause.Counter);
                nextTurnDraw=0;BlockSpentThisTurn=0;RestoredBlockThisTurn=0;
                spendDraw=0;spendDrawId=null;recoveredThisTurn.Clear();
                HealedThisTurn=false;firstFireUsed=false;nextElementPower=0;nextElementId=null;
                allElementPower=0;allElementId=null;SharedCardsUsedThisTurn=0;ReactedThisTurn=false;
                sharedInstancesThisTurn.Clear();RecoveredSharedThisTurn=false;
                if(turnStartBlock>0)ChangeBlock(trace,Block+turnStartBlock,CombatCause.TurnStart,effectId:turnStartBlockId);
                foreach(var enemy in Enemies.Where(enemy=>enemy.IsAlive))IntentResolver.Decide(enemy);
            }
            trace.Complete(Enemies);
            return true;
        }
        internal CombatTrace NewTrace(string cardId=null) => new CombatTrace(this,++actionId,cardId);
        internal void ChangeEnergy(CombatTrace trace,int value,CombatCause cause,int? parent=null,string effectId=null)
        {
            trace.Add(CombatEventKind.Energy,cause,CombatTrace.PlayerId,CombatTrace.PlayerId,Energy,value,parent:parent,effectId:effectId);
            Energy=value;
        }
        internal void ChangeBlock(CombatTrace trace,int value,CombatCause cause,string actor=CombatTrace.PlayerId,int? parent=null,string effectId=null)
        {
            int previous=Block;
            var change=trace.Add(CombatEventKind.Block,cause,actor,CombatTrace.PlayerId,Block,value,parent:parent,effectId:effectId);
            if(Block!=value||cause==CombatCause.BlockRetained)blockHistory.Add(change);
            Block=value;
            if(cause==CombatCause.BlockSpent&&previous>value)
            {
                bool first=BlockSpentThisTurn==0;
                int spent=previous-value;
                BlockSpentThisTurn+=spent;
                if(first)
                {
                    if(spendDraw>0)DrawCards(trace,spendDraw,CombatCause.BlockSpent,spendDrawId,change.Index);
                    if(recycleRule!=null)RestoreSpentBlock(trace,recycleRule.Scale(spent),recycleId,parent:change.Index);
                }
            }
        }
        internal void RetainBlock(int maximum) => blockRetention=Math.Max(blockRetention,maximum);
        internal IReadOnlyList<CardInstance> DrawCards(CombatTrace trace,int amount,CombatCause cause=CombatCause.Card,string effectId=null,int? parent=null)
        {
            int before=Deck.Hand.Count;
            var drawn=Deck.DrawWithResult(amount);
            trace.Add(CombatEventKind.Draw,cause,CombatTrace.PlayerId,CombatTrace.PlayerId,before,Deck.Hand.Count,
                parent:parent,effectId:effectId,cardInstances:drawn.Select(c=>c.InstanceId));
            return drawn;
        }
        internal void RegisterSpendDraw(CombatTrace trace,string id,int amount)
        {
            if(spendDrawId!=null||BlockSpentThisTurn>0)return;
            spendDrawId=id;spendDraw=amount;
            trace.Add(CombatEventKind.Status,CombatCause.Card,CombatTrace.PlayerId,CombatTrace.PlayerId,0,1,effectId:id);
        }
        internal void RegisterBlockRecycle(CombatTrace trace,string id,EffectRule rule)
        {
            if(recycleId!=null)return;
            recycleId=id;recycleRule=rule;
            trace.Add(CombatEventKind.Status,CombatCause.Card,CombatTrace.PlayerId,CombatTrace.PlayerId,0,1,effectId:id);
        }
        internal void RestoreSpentBlock(CombatTrace trace,int amount,string id,int? parent=null)
        {
            int actual=Math.Min(amount,BattleConfig.WarriorRestorePerTurn-RestoredBlockThisTurn);
            if(actual<=0||!recoveredThisTurn.Add(id))return;
            RestoredBlockThisTurn+=actual;
            ChangeBlock(trace,Block+actual,CombatCause.BlockRestored,parent:parent,effectId:id);
        }

        internal CombatEvent HitEnemy(CombatTrace trace,EnemyInstance enemy,int amount,CombatCause cause,int? parent=null,
            string effectId=null,bool ignoreBlock=false,int reactionBonus=0,int? baseDamage=null,int strengthBonus=0,int cardBonus=0,int ignoredBlock=0)
        {
            if(!enemy.IsAlive)return null;
            int hp=enemy.CurrentHP,block=enemy.Block;
            if(ignoreBlock)hp-=Math.Min(hp,Math.Max(0,amount));
            else DamageCalculator.Hit(amount,ref hp,ref block,ignoredBlock);
            var hit=new CombatHit(amount,enemy.CurrentHP,hp,enemy.Block,block,reactionBonus,baseDamage,strengthBonus,cardBonus,ignoreBlock?enemy.Block:ignoredBlock);
            enemy.CurrentHP=hp; enemy.Block=block;
            var entry=trace.Add(CombatEventKind.Damage,cause,cause==CombatCause.PoisonTick?enemy.TargetId:CombatTrace.PlayerId,
                enemy.TargetId,hit:hit,parent:parent,effectId:effectId);
            if(hit.Killed)ClearBrand(trace,enemy,CombatCause.Death,entry.Index);
            return entry;
        }
        internal void ApplyEnemyStatus(CombatTrace trace,EnemyInstance enemy,StatusEffectType status,int amount,
            CombatCause cause,int? parent=null)
        {
            if(!enemy.IsAlive)return;
            int before=enemy.Status.Value(status);
            switch(status)
            {
                case StatusEffectType.Poison: enemy.Status.AddPoison(amount);break;
                case StatusEffectType.Weak: enemy.Status.ApplyWeak(amount);break;
                case StatusEffectType.RapidFreeze: enemy.Status.RapidFreeze=Math.Max(before,amount);break;
                case StatusEffectType.VenomBurn: enemy.Status.VenomBurn=amount;break;
                case StatusEffectType.Brand: enemy.Status.Brand=amount>0?1:0;break;
            }
            trace.Add(CombatEventKind.Status,cause,CombatTrace.PlayerId,enemy.TargetId,before,enemy.Status.Value(status),status:status,parent:parent);
        }
        public IReadOnlyList<CardInstance> RecoveryChoices(CardDefinition card) =>
            RecoveredSharedThisTurn||!card.Effects.Any(e=>e.Type==EffectType.RecoverShared)?Array.Empty<CardInstance>():
            Deck.DiscardPile.Where(c=>sharedInstancesThisTurn.Contains(c.InstanceId)&&Data.Card(c.CardId).Cost==1&&
                Data.Card(c.CardId).ElementApplication==ElementApplication.SharedAttack&&!Data.Card(c.CardId).Exhausts).ToArray();
        internal bool TryRecoveryChoice(CardDefinition card,string requested,out string chosen)
        {
            var choices=RecoveryChoices(card);chosen=requested;
            if(requested!=null)return choices.Any(c=>c.InstanceId==requested);
            if(choices.Count>1)return false;
            chosen=choices.SingleOrDefault()?.InstanceId;return true;
        }
        internal void RecoverShared(CombatTrace trace,string instanceId)
        {
            if(instanceId==null||!RecoveryChoices(Data.Card(trace.CardId)).Any(c=>c.InstanceId==instanceId)||!Deck.Recover(instanceId))return;
            RecoveredSharedThisTurn=true;
            trace.Add(CombatEventKind.CardRecovered,CombatCause.Recovery,CombatTrace.PlayerId,CombatTrace.PlayerId,
                effectId:trace.CardId,cardInstances:new[]{instanceId});
        }
        internal (int Amount,string Id) TakeAllElementPower(CardDefinition card)
        {
            if(card.ElementApplication!=ElementApplication.SharedAttack)return (0,null);
            var power=(allElementPower,allElementId);allElementPower=0;allElementId=null;return power;
        }
        internal void RegisterAllElementPower(CombatTrace trace,string id,int amount)
        {
            int before=allElementPower;allElementPower=amount;allElementId=id;
            trace.Add(CombatEventKind.Status,CombatCause.Card,CombatTrace.PlayerId,CombatTrace.PlayerId,before,amount,effectId:id);
        }
        internal void RegisterSharedDraw(CombatTrace trace,string id,int amount)
        {
            if(sharedDrawId!=null)return;sharedDrawId=id;sharedDraw=amount;
            trace.Add(CombatEventKind.Status,CombatCause.Card,CombatTrace.PlayerId,CombatTrace.PlayerId,0,1,effectId:id);
        }
        internal void RegisterFirstReaction(CombatTrace trace,string id,int amount)
        {
            if(firstReactionId!=null)return;firstReactionId=id;firstReactionDamage=amount;
            trace.Add(CombatEventKind.Status,CombatCause.Card,CombatTrace.PlayerId,CombatTrace.PlayerId,0,1,effectId:id);
        }
        internal void RecordSharedUse(CombatTrace trace,CardDefinition card,string instanceId)
        {
            if(card.ElementApplication!=ElementApplication.SharedAttack)return;
            bool first=SharedCardsUsedThisTurn++==0;
            if(instanceId!=null)sharedInstancesThisTurn.Add(instanceId);
            if(first&&sharedDraw>0)DrawCards(trace,sharedDraw,CombatCause.Resonance,sharedDrawId);
        }
        internal void OnElementReaction(CombatTrace trace,EnemyInstance enemy,int reaction)
        {
            bool first=!ReactedThisTurn;ReactedThisTurn=true;
            if(first&&firstReactionDamage>0&&enemy.IsAlive)
                HitEnemy(trace,enemy,firstReactionDamage,CombatCause.Resonance,reaction,effectId:firstReactionId);
        }
        public IReadOnlyList<string> ActiveCardEffects =>
            new[]{firstHealBlockId,turnStartBlockId,firstFireId,allElementPower>0?allElementId:null,
                nextElementPower>0?nextElementId:null,sharedDrawId,firstReactionId,nextSharedCostReduction>0?nameof(EffectType.DiscountShared):null}
                .Where(id=>id!=null).ToArray();
        public IReadOnlyList<StatusEffectType> CleanseChoices(CardDefinition card) =>
            card.Effects.Any(e=>e.Type==EffectType.CleansePlayer)
                ?new[]{StatusEffectType.Poison,StatusEffectType.Weak}.Where(s=>PlayerStatus.Value(s)>0).ToArray()
                :Array.Empty<StatusEffectType>();
        internal bool TryCleanseChoice(CardDefinition card,StatusEffectType? requested,out StatusEffectType? chosen)
        {
            var choices=CleanseChoices(card);chosen=requested;
            if(requested.HasValue)return choices.Contains(requested.Value);
            if(choices.Count>1)return false;
            chosen=choices.Count==1?choices[0]:(StatusEffectType?)null;return true;
        }
        internal void CleansePlayer(CombatTrace trace,StatusEffectType? chosen)
        {
            if(!chosen.HasValue)return;
            int before=PlayerStatus.Value(chosen.Value);
            if(chosen==StatusEffectType.Poison)PlayerStatus.Poison=0;
            else if(chosen==StatusEffectType.Weak)PlayerStatus.Weak=0;
            trace.Add(CombatEventKind.Status,CombatCause.Cleanse,CombatTrace.PlayerId,CombatTrace.PlayerId,
                before,0,status:chosen);
        }
        internal int TakeFirstFirePower(CardDefinition card)
        {
            if(card.ElementApplication!=ElementApplication.SharedAttack||card.Element!=ElementType.Fire||firstFireUsed||firstFirePower==0)return 0;
            firstFireUsed=true;return firstFirePower;
        }
        internal (int Amount,string Id) TakeNextElementPower(CardDefinition card)
        {
            if(card.ElementApplication!=ElementApplication.SharedAttack)return (0,null);
            var power=(nextElementPower,nextElementId);nextElementPower=0;nextElementId=null;return power;
        }
        internal void RegisterFirePower(CombatTrace trace,string id,int amount)
        {
            if(firstFireId!=null)return;
            firstFireId=id;firstFirePower=amount;
            trace.Add(CombatEventKind.Status,CombatCause.Card,CombatTrace.PlayerId,CombatTrace.PlayerId,0,amount,effectId:id);
        }
        internal void RegisterNextElementPower(CombatTrace trace,string id,int amount)
        {
            int before=nextElementPower;nextElementPower=amount;nextElementId=id;
            trace.Add(CombatEventKind.Status,CombatCause.Card,CombatTrace.PlayerId,CombatTrace.PlayerId,before,amount,effectId:id);
        }
        internal void RegisterHealBlock(CombatTrace trace,string id,int amount)
        {
            if(firstHealBlockId!=null)return;
            firstHealBlockId=id;firstHealBlock=amount;
            trace.Add(CombatEventKind.Status,CombatCause.Card,CombatTrace.PlayerId,CombatTrace.PlayerId,0,1,effectId:id);
        }
        internal void RegisterTurnBlock(CombatTrace trace,string id,int amount)
        {
            if(turnStartBlockId!=null)return;
            turnStartBlockId=id;turnStartBlock=amount;
            trace.Add(CombatEventKind.Status,CombatCause.Card,CombatTrace.PlayerId,CombatTrace.PlayerId,0,1,effectId:id);
        }
        internal void HealPlayer(CombatTrace trace,int amount)
        {
            int before=Run.CurrentHP;
            Run.Heal(amount);
            var heal=trace.Add(CombatEventKind.Heal,CombatCause.Card,CombatTrace.PlayerId,CombatTrace.PlayerId,
                before,Run.CurrentHP,requestedAmount:amount);
            if(heal.ActualHealing==0)return;
            bool first=!HealedThisTurn;
            HealedThisTurn=true;
            if(first&&firstHealBlock>0)
                ChangeBlock(trace,Block+firstHealBlock,CombatCause.FirstHeal,parent:heal.Index,effectId:firstHealBlockId);
            foreach(var enemy in Enemies.Where(e=>e.IsAlive&&e.Status.Brand>0).ToArray())
                TriggerBrand(trace,enemy,heal.Index);
        }
        internal bool TriggerBrand(CombatTrace trace,EnemyInstance enemy,int parent)
        {
            if(!enemy.IsAlive||enemy.Status.Brand==0)return false;
            var trigger=trace.Add(CombatEventKind.BrandTriggered,CombatCause.Brand,CombatTrace.PlayerId,enemy.TargetId,
                parent:parent,effectId:nameof(StatusEffectType.Brand));
            ClearBrand(trace,enemy,CombatCause.Brand,trigger.Index);
            HitEnemy(trace,enemy,BattleConfig.BrandDamage,CombatCause.Brand,trigger.Index);
            return true;
        }
        internal void ClearBrand(CombatTrace trace,EnemyInstance enemy,CombatCause cause,int parent)
        {
            if(enemy.Status.Brand==0)return;
            int before=enemy.Status.Brand;enemy.Status.Brand=0;
            trace.Add(CombatEventKind.Status,cause,CombatTrace.PlayerId,enemy.TargetId,before,0,
                status:StatusEffectType.Brand,parent:parent);
        }
        private void TickPoison(CombatTrace trace,EnemyInstance enemy)
        {
            int poison=enemy.Status.Poison;
            if(poison<=0)return;
            int burn=enemy.Status.VenomBurn;
            int bonus=burn>0?Data.Config.Reactions.VenomBurnDamage:0;
            var hit=HitEnemy(trace,enemy,poison+bonus,CombatCause.PoisonTick,
                effectId:burn>0?nameof(ElementReaction.VenomBurn):null,ignoreBlock:true,reactionBonus:bonus);
            enemy.Status.Poison--;
            trace.Add(CombatEventKind.Status,CombatCause.PoisonTick,enemy.TargetId,enemy.TargetId,poison,poison-1,status:StatusEffectType.Poison,parent:hit.Index);
            enemy.Status.VenomBurn=poison==1?0:Math.Max(0,burn-1);
            if(burn!=enemy.Status.VenomBurn)
                trace.Add(CombatEventKind.Status,CombatCause.PoisonTick,enemy.TargetId,enemy.TargetId,burn,enemy.Status.VenomBurn,status:StatusEffectType.VenomBurn,parent:hit.Index);
        }
        public bool RegisterCounter(string effectId,int damage,int perEnemyTurn=int.MaxValue,int perAttacker=int.MaxValue,bool expiresAfterEnemyTurn=true,int drawOnCounter=0)
        {
            if(string.IsNullOrEmpty(effectId)||damage<=0||perEnemyTurn<=0||perAttacker<=0||counters.Any(c=>c.Id==effectId))return false;
            counters.Add(new CounterEffect(effectId,damage,perEnemyTurn,perAttacker,expiresAfterEnemyTurn,drawOnCounter));
            return true;
        }
        internal void CounterAttack(CombatTrace trace,EnemyInstance attacker,int? parent)
        {
            foreach(var effect in counters)
            {
                if(!attacker.IsAlive)break;
                if(!effect.TryUse(attacker.TargetId))continue;
                HitEnemy(trace,attacker,effect.Damage,CombatCause.Counter,parent,effect.Id);
                counteredThisEnemyTurn=true;
                nextTurnDraw+=effect.DrawOnCounter;
            }
        }
        private void Win()
        {
            if(Result!=BattleResult.Active)return;
            Result=BattleResult.Won;Reward=RewardResolver.Create(Data,Run);RewardResolver.GrantVictory(this);
            ItemVictory();
            ClearBattleEffects();
            // Regular rooms stay pending until the optional card/additional-gold choice; bosses use the existing clear flow.
        }
        private void ClearBattleEffects()
        {
            counters.Clear();recycleId=null;recycleRule=null;spendDrawId=null;spendDraw=0;
            nextTurnDraw=0;blockRetention=0;recoveredThisTurn.Clear();
            firstHealBlockId=null;turnStartBlockId=null;firstHealBlock=0;turnStartBlock=0;
            firstFireId=null;nextElementId=null;firstFirePower=0;nextElementPower=0;firstFireUsed=false;
            allElementId=null;sharedDrawId=null;firstReactionId=null;allElementPower=0;sharedDraw=0;firstReactionDamage=0;
            sharedInstancesThisTurn.Clear();SharedCardsUsedThisTurn=0;ReactedThisTurn=false;RecoveredSharedThisTurn=false;nextSharedCostReduction=0;
        }
    }

    internal sealed class CounterEffect
    {
        private readonly Dictionary<string,int> byAttacker=new Dictionary<string,int>();
        private readonly int turnLimit,attackerLimit;
        private int turnUses;
        public string Id { get; }
        public int Damage { get; }
        public bool ExpiresAfterEnemyTurn { get; }
        public int DrawOnCounter { get; }
        public CounterEffect(string id,int damage,int turnLimit,int attackerLimit,bool expires,int drawOnCounter)
        { Id=id; Damage=damage; this.turnLimit=turnLimit; this.attackerLimit=attackerLimit; ExpiresAfterEnemyTurn=expires;DrawOnCounter=drawOnCounter; }
        public bool TryUse(string attacker)
        {
            byAttacker.TryGetValue(attacker,out int used);
            if(turnUses>=turnLimit||used>=attackerLimit)return false;
            turnUses++; byAttacker[attacker]=used+1; return true;
        }
        public void ResetTurn(){ turnUses=0; byAttacker.Clear(); }
    }
}

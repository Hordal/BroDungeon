namespace Brodun.Flow
{
    // Frozen result snapshot, separate from the next run and from scene presentation.
    public sealed class RunSummary
    {
        public RunStatus Status { get; }
        public CharacterId Character { get; }
        public string ThemeId { get; }
        public int HP { get; }
        public int MaxHP { get; }
        public int Gold { get; }
        public int DeckSize { get; }
        public int ClearedNodes { get; }
        public int Area { get; }
        public int Floor { get; }
        public RunSummary(RunState run)
        {Status=run.Status;Character=run.SelectedCharacter;ThemeId=run.ThemeId;HP=run.CurrentHP;MaxHP=run.MaxHP;Gold=run.Gold;DeckSize=run.Deck.Count;ClearedNodes=run.CompletedNodes.Count;Area=run.CurrentArea;Floor=run.Floor;}
    }
}

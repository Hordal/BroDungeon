using System;
namespace Brodun
{
    // PHASE 3.5 tuning only. Immutable per-generation configuration; no UI or Unity dependencies.
    public sealed class MapGenerationConfig
    {
        public int MinLayers { get; }
        public int MaxLayers { get; }
        public int MinNodes { get; }
        public int MaxNodes { get; }
        public int BattleWeight { get; }
        public int EliteWeight { get; }
        public int PuzzleWeight { get; }
        public int EventWeight { get; }
        public int MaxNodesPerLayer => MaxNodes;
        public static MapGenerationConfig Default => new MapGenerationConfig();
        public MapGenerationConfig(int minLayers=8,int maxLayers=10,int minNodes=2,int maxNodes=4,
            int battleWeight=52,int eliteWeight=12,int puzzleWeight=16,int eventWeight=20)
        {
            if(minLayers<7 || maxLayers<minLayers || maxLayers>12 || minNodes<2 || maxNodes>4 || maxNodes<minNodes)
                throw new ArgumentOutOfRangeException(nameof(minLayers));
            if(battleWeight<1 || eliteWeight<0 || puzzleWeight<0 || eventWeight<0)throw new ArgumentOutOfRangeException(nameof(battleWeight));
            MinLayers=minLayers;MaxLayers=maxLayers;MinNodes=minNodes;MaxNodes=maxNodes;
            BattleWeight=battleWeight;EliteWeight=eliteWeight;PuzzleWeight=puzzleWeight;EventWeight=eventWeight;
        }
    }
}

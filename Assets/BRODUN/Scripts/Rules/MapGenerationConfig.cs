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
        public bool OpeningEncounters { get; }
        public int MaxNodesPerLayer => MaxNodes;
        public static MapGenerationConfig Default => new MapGenerationConfig();
        public MapGenerationConfig(int minLayers=6,int maxLayers=7,int minNodes=2,int maxNodes=4,
            int battleWeight=40,int eliteWeight=10,int puzzleWeight=20,int eventWeight=30,bool openingEncounters=false)
        {
            if(minLayers<6 || maxLayers<minLayers || maxLayers>12 || minNodes<2 || maxNodes>4 || maxNodes<minNodes)
                throw new ArgumentOutOfRangeException(nameof(minLayers));
            if(battleWeight<1 || eliteWeight<0 || puzzleWeight<0 || eventWeight<0)throw new ArgumentOutOfRangeException(nameof(battleWeight));
            MinLayers=minLayers;MaxLayers=maxLayers;MinNodes=minNodes;MaxNodes=maxNodes;
            OpeningEncounters=openingEncounters;
            BattleWeight=battleWeight;EliteWeight=eliteWeight;PuzzleWeight=puzzleWeight;EventWeight=eventWeight;
        }
    }
}

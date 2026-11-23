using System.Linq;
using UnityEngine;
namespace Brodun.Data
{
    [CreateAssetMenu(menuName = "BRODUN/Game Catalog")]
    public sealed class GameCatalogData : ScriptableObject
    {
        [SerializeField] private CardData[] cards;
        [SerializeField] private CardData[] starterCards;
        [SerializeField] private CardData[] expansionCards;
        [SerializeField] private CharacterData[] characters;
        [SerializeField] private EnemyData[] enemies;
        [SerializeField] private BattleConfigData config;
        [SerializeField] private RewardData reward;
        [SerializeField] private ElementRecord[] elements;
        public int ElementCount => elements.Length;
        private System.Collections.Generic.IEnumerable<CardData> AllCardAssets =>
            cards.Concat(starterCards??System.Array.Empty<CardData>()).Concat(expansionCards??System.Array.Empty<CardData>());
        public Sprite CardArtwork(string id) => AllCardAssets.FirstOrDefault(c=>c!=null&&c.Id==id)?.Artwork;
        public GameDefinitions ToDefinitions() => new GameDefinitions(AllCardAssets.Select(x => x.ToDefinition()), characters.Select(x => x.ToDefinition()), enemies.Select(x => x.ToDefinition()), config.ToDefinition(), reward.ToDefinition(), elements.Select(x => new ElementDefinition(Ids.Parse<ElementType>(x.id), x.name)));
        public void Initialize(CardData[] c, CharacterData[] h, EnemyData[] e, BattleConfigData b, RewardData r, ElementRecord[] tags)
        { cards = c; characters = h; enemies = e; config = b; reward = r; elements = tags; }
    }
}

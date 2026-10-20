using System.Linq;
using UnityEngine;
namespace Brodun.Data
{
    [CreateAssetMenu(menuName = "BRODUN/Game Catalog")]
    public sealed class GameCatalogData : ScriptableObject
    {
        [SerializeField] private CardData[] cards;
        [SerializeField] private CharacterData[] characters;
        [SerializeField] private EnemyData[] enemies;
        [SerializeField] private BattleConfigData config;
        [SerializeField] private RewardData reward;
        [SerializeField] private ElementRecord[] elements;
        public int ElementCount => elements.Length;
        public Sprite CardArtwork(string id) => cards.FirstOrDefault(c=>c!=null&&c.Id==id)?.Artwork;
        public GameDefinitions ToDefinitions() => new GameDefinitions(cards.Select(x => x.ToDefinition()), characters.Select(x => x.ToDefinition()), enemies.Select(x => x.ToDefinition()), config.ToDefinition(), reward.ToDefinition(), elements.Select(x => new ElementDefinition(Ids.Parse<ElementType>(x.id), x.name)));
        public void Initialize(CardData[] c, CharacterData[] h, EnemyData[] e, BattleConfigData b, RewardData r, ElementRecord[] tags)
        { cards = c; characters = h; enemies = e; config = b; reward = r; elements = tags; }
    }
}

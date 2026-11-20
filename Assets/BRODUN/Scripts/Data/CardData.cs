using UnityEngine;
namespace Brodun.Data
{
    [CreateAssetMenu(menuName = "BRODUN/Card")]
    public sealed class CardData : ScriptableObject
    {
        [SerializeField] private CardRecord definition;
        [SerializeField] private Sprite artwork;
        public string Id => definition.id;
        public CardDefinition ToDefinition() => definition.ToDefinition();
        public Sprite Artwork => artwork;
        public void Initialize(CardRecord value) { definition = value; }
    }
}

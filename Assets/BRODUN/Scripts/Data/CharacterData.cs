using UnityEngine;
namespace Brodun.Data
{
    [CreateAssetMenu(menuName = "BRODUN/Character")]
    public sealed class CharacterData : ScriptableObject
    {
        [SerializeField] private CharacterRecord definition;
        [SerializeField] private Sprite portrait;
        [SerializeField] private string[] startingDeckOverride;
        public CharacterDefinition ToDefinition() => definition.ToDefinition(startingDeckOverride is { Length: > 0 }?startingDeckOverride:null);
        public Sprite Portrait => portrait;
        public void Initialize(CharacterRecord value) { definition = value; }
    }
}

using UnityEngine;
namespace Brodun.Data
{
    [CreateAssetMenu(menuName = "BRODUN/Character")]
    public sealed class CharacterData : ScriptableObject
    {
        [SerializeField] private CharacterRecord definition;
        [SerializeField] private Sprite portrait;
        public CharacterDefinition ToDefinition() => definition.ToDefinition();
        public Sprite Portrait => portrait;
        public void Initialize(CharacterRecord value) { definition = value; }
    }
}

using System;
using System.Linq;
using UnityEngine;
namespace Brodun.Presentation
{
    [Serializable] public sealed class HeroMotion
    {
        public CharacterId character;
        public SpriteSequence[] animations;
        public SpriteSequence Animation(SpriteMotion motion)=>animations.FirstOrDefault(a=>a.motion==motion);
    }
    // Presentation-only references. No combat values or RunState ownership.
    public sealed class CombatMotionAssets : ScriptableObject
    {
        public HeroMotion[] heroes;
        public HeroMotion Hero(CharacterId id)=>heroes.FirstOrDefault(h=>h.character==id);
    }
}

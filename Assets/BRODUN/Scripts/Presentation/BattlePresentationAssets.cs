using System;
using System.Linq;
using Brodun.Data;
using UnityEngine;
namespace Brodun.Presentation
{
    public enum SpriteMotion { Idle, Attack, Hurt, Death }
    [Serializable] public sealed class SpriteSequence
    {public SpriteMotion motion;public float fps;public bool loop;public Sprite[] frames;}
    [Serializable] public sealed class EnemyVisual
    {
        public EnemyData enemy;public float displayHeight;public SpriteSequence[] animations;
        public SpriteSequence Animation(SpriteMotion motion)=>animations.Single(a=>a.motion==motion);
    }
    [CreateAssetMenu(menuName="BRODUN/Battle Presentation")]
    public sealed class BattlePresentationAssets : ScriptableObject
    {
        public PresentationAssets common;
        public Sprite background,attackFrame,guardFrame,skillFrame;
        public CardView cardPrefab;public EnemyView enemyPrefab;
        public EnemyVisual[] enemies;
        public EnemyVisual Enemy(string id)=>enemies.Single(v=>v.enemy.ToDefinition().Id==id);
    }
}

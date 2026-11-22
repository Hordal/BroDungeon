using System;
using System.Linq;
using System.Collections.Generic;
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
        [NonSerialized] private Dictionary<string,EnemyVisual> themedVisuals;
        public EnemyVisual Enemy(string id)
        {
            var existing=enemies.FirstOrDefault(v=>v.enemy!=null&&v.enemy.ToDefinition().Id==id);
            if(existing!=null)return existing;
            if(!ThemeEnemyCatalog.Entries.Any(e=>e.Id==id))
                throw new InvalidOperationException("Unknown enemy visual: "+id);
            if(themedVisuals==null)themedVisuals=new Dictionary<string,EnemyVisual>();
            if(themedVisuals.TryGetValue(id,out var visual))return visual;
            var sprite=Resources.Load<Sprite>("BRODUN/ThemeMonsters/"+id);
            if(sprite==null)throw new InvalidOperationException("Missing themed monster Sprite: "+id);
            var rank=ThemeEnemyCatalog.Entries.First(e=>e.Id==id).Rank;
            visual=new EnemyVisual
            {
                displayHeight=rank==EnemyType.Boss?320:rank==EnemyType.Elite?285:250,
                animations=Enum.GetValues(typeof(SpriteMotion)).Cast<SpriteMotion>().Select(m=>new SpriteSequence
                {
                    motion=m,fps=8,loop=m==SpriteMotion.Idle,
                    frames=Enumerable.Repeat(sprite,m==SpriteMotion.Idle?1:4).ToArray()
                }).ToArray()
            };
            themedVisuals.Add(id,visual);return visual;
        }
    }
}

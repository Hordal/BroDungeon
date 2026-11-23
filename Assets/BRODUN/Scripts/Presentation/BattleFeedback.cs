using System;
using UnityEngine;

namespace Brodun.Presentation
{
    public enum BattleSound { EarthPrepare, EarthHit, StoneAssemble, StoneLock, WindRelease, WindHit, WindRead, CardDraw, EnemyClaw, EnemyHeavy, EnemyMagic, EnemyVenom, EnemyFire, EnemySpark, EnemyBlock, EnemyBuff, PlayerHurt, EnemyHurt }
    // Cosmetic themes only. The rules still expose exactly Attack / Block / Buff.
    public enum EnemyAttackLook { Claw, Heavy, Magic, Venom, Fire, Spark }
    [Serializable] public sealed class BattleSoundReference
    {
        public BattleSound cue;
        public AudioClip clip;
        [Range(0,1)] public float gain=1;
    }
    [Serializable] public sealed class EnemyFeedbackReference
    {
        public string assetId;
        public EnemyAttackLook look;
    }
    // Presentation-only switches and replaceable clips. Never owns combat data.
    [CreateAssetMenu(menuName="BRODUN/Battle feedback")]
    public sealed class BattleFeedback : ScriptableObject
    {
        public bool enhancedPresentation=true;
        public bool soundEnabled=true;
        [Range(0,1)] public float masterVolume=.72f;
        public BattleSoundReference[] sounds=Array.Empty<BattleSoundReference>();
        public EnemyFeedbackReference[] enemyLooks=Array.Empty<EnemyFeedbackReference>();
        public static BattleFeedback Load()=>Resources.Load<BattleFeedback>("BrodunBattleFeedback");
        public static bool Enhanced { get { var settings=Load();return settings!=null&&settings.enhancedPresentation; } }
        public EnemyAttackLook Look(string assetId)
        {
            foreach(var item in enemyLooks)if(item.assetId==assetId)return item.look;
            return EnemyAttackLook.Claw;
        }
        // Shared visual clocks (seconds), used by the sprite pose and its contact callback.
        public const float PlayerContact=.16f, AttackRecovery=.18f, ContactLifetime=.30f, HitRecoil=15;
        // The first contact frame displaces the struck sprite; a brief brace precedes recovery.
        public static float Recoil(float phase)=>HitRecoil*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.20f,1,phase)));
        public const float EnemyRelease=.09f, EnemyContact=.19f, EnemyHitInterval=.13f;
        public static float EnemyDuration(int hits)=>EnemyContact+Mathf.Max(0,hits-1)*EnemyHitInterval+AttackRecovery;
        public static float WarriorRelease(string id)=>id=="earth_strike"?.325f:id=="wind_cut"?.308f:0;
    }
}

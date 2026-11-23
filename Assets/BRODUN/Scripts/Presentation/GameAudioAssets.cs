using UnityEngine;

namespace Brodun.Presentation
{
    public enum MusicCue { Silent, Title, Dungeon, Battle, Boss }
    public enum AudioChannel { Master, Music, Sfx }

    [CreateAssetMenu(menuName="BRODUN/Game audio")]
    public sealed class GameAudioAssets : ScriptableObject
    {
        public AudioClip title,dungeon,battle,boss;
        [Range(0,1)] public float titleGain=.24f,dungeonGain=.22f,battleGain=.20f;
        [Range(.1f,2)] public float fadeSeconds=.75f;
        public AudioClip Clip(MusicCue cue)=>cue==MusicCue.Title?title:cue==MusicCue.Dungeon?dungeon:cue==MusicCue.Boss?(boss!=null?boss:battle):cue==MusicCue.Battle?battle:null;
        public float Gain(MusicCue cue)=>cue==MusicCue.Title?titleGain:cue==MusicCue.Dungeon?dungeonGain:battleGain;
    }
}

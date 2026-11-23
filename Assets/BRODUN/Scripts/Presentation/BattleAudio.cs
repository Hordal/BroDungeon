using UnityEngine;

namespace Brodun.Presentation
{
    // Bounded 2D voices; no hidden PlayOneShot layers, global audio settings, or BGM.
    public sealed class BattleAudio : MonoBehaviour
    {
        private const int VoiceCount=4;
        private readonly AudioSource[] voices=new AudioSource[VoiceCount];
        private readonly float[] lastPlayed=new float[System.Enum.GetValues(typeof(BattleSound)).Length];
        private BattleFeedback settings;
        private GameAudio gameAudio;
        private readonly float[] voiceGains=new float[VoiceCount];
        private int next;
        private void Awake()
        {
            settings=BattleFeedback.Load();
            for(int i=0;i<lastPlayed.Length;i++)lastPlayed[i]=-100;
            for(int i=0;i<voices.Length;i++)
            {
                var source=gameObject.AddComponent<AudioSource>();
                source.playOnAwake=false;source.loop=false;source.spatialBlend=0;source.dopplerLevel=0;
                source.priority=64;voices[i]=source;
            }
            gameAudio=GameAudio.Ensure();
        }
        private void OnEnable(){if(gameAudio!=null){gameAudio.SettingsChanged+=ApplyVolumes;ApplyVolumes();}}
        private void ApplyVolumes()
        {for(int i=0;i<voices.Length;i++)if(voices[i]!=null)voices[i].volume=voiceGains[i]*(gameAudio!=null?gameAudio.SfxLevel:1);}
        public void Play(BattleSound cue)
        {
            if(settings==null||!settings.soundEnabled||!isActiveAndEnabled)return;
            int key=(int)cue;
            if(Time.unscaledTime-lastPlayed[key]<.065f)return;
            BattleSoundReference sound=null;
            foreach(var entry in settings.sounds)if(entry.cue==cue){sound=entry;break;}
            if(sound==null||sound.clip==null)return;
            lastPlayed[key]=Time.unscaledTime;
            AudioSource voice=null;
            foreach(var source in voices)if(!source.isPlaying){voice=source;break;}
            if(voice==null)voice=voices[next++%VoiceCount];
            voice.Stop();voice.clip=sound.clip;
            // Every WAV peaks below .80. Four concurrent .24-gain voices remain below unity.
            int slot=System.Array.IndexOf(voices,voice);
            voiceGains[slot]=Mathf.Clamp01(settings.masterVolume)*Mathf.Clamp01(sound.gain)*.24f;
            voice.volume=voiceGains[slot]*(gameAudio!=null?gameAudio.SfxLevel:1);
            voice.pitch=1;voice.Play();
        }
        private void OnDisable(){if(gameAudio!=null)gameAudio.SettingsChanged-=ApplyVolumes;foreach(var voice in voices)if(voice!=null)voice.Stop();}
    }
}

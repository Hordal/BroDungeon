using System;
using System.Collections;
using System.Collections.Generic;
using Brodun.Flow;
using UnityEngine;

namespace Brodun.Presentation
{
    // One persistent music/settings service. BattleAudio retains ownership of its SFX pool.
    public sealed class GameAudio : MonoBehaviour
    {
        public static GameAudio Instance { get; private set; }
        private const string Pref="BRODUN.Audio.";
        private readonly AudioSource[] music=new AudioSource[2];
        private readonly float[] weights=new float[2],gains=new float[2];
        private readonly Dictionary<AudioClip,float> positions=new Dictionary<AudioClip,float>();
        private readonly HashSet<MusicCue> warned=new HashSet<MusicCue>();
        private GameAudioAssets assets;
        private AudioListener fallbackListener;
        private Coroutine transition;
        private AudioSettingsView settings;
        private int active=-1;
        private float contextGain=1,contextTarget=1,saveAt;
        private bool dirty;
        public float Master { get; private set; }=1;
        public float Music { get; private set; }=.5f;
        public float Sfx { get; private set; }=.8f;
        public bool MasterMuted { get; private set; }
        public bool MusicMuted { get; private set; }
        public bool SfxMuted { get; private set; }
        public bool IsSettingsOpen=>settings!=null;
        public float MusicLevel=>MasterMuted||MusicMuted?0:Master*Music;
        public float SfxLevel=>MasterMuted||SfxMuted?0:Master*Sfx;
        public event Action SettingsChanged;
        public event Action<bool> SettingsVisibilityChanged;
        public static GameAudio Ensure()
        {
            if(Instance!=null)return Instance;
            return new GameObject("BRODUN Audio").AddComponent<GameAudio>();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic(){Instance=null;}
        private void Awake()
        {
            if(Instance!=null&&Instance!=this){Destroy(gameObject);return;}
            Instance=this;DontDestroyOnLoad(gameObject);
            assets=Resources.Load<GameAudioAssets>("BrodunGameAudio");
            Master=Read("Master",1);Music=Read("Music",.5f);Sfx=Read("Sfx",.8f);
            MasterMuted=PlayerPrefs.GetInt(Pref+"MasterMuted",0)!=0;
            MusicMuted=PlayerPrefs.GetInt(Pref+"MusicMuted",0)!=0;
            SfxMuted=PlayerPrefs.GetInt(Pref+"SfxMuted",0)!=0;
            for(int i=0;i<music.Length;i++)
            {
                music[i]=gameObject.AddComponent<AudioSource>();music[i].playOnAwake=false;
                music[i].loop=true;music[i].spatialBlend=0;music[i].dopplerLevel=0;music[i].priority=160;music[i].volume=0;
            }
            EnsureListener();
        }
        private static float Read(string key,float fallback)
        {float value=PlayerPrefs.GetFloat(Pref+key,fallback);return float.IsNaN(value)||float.IsInfinity(value)?fallback:Mathf.Clamp01(value);}
        private void EnsureListener()
        {
            bool other=false;
            foreach(var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if(listener!=fallbackListener&&listener.isActiveAndEnabled){other=true;break;}
            if(!other&&fallbackListener==null)fallbackListener=gameObject.AddComponent<AudioListener>();
            if(fallbackListener!=null)fallbackListener.enabled=!other;
        }
        public void SetScreen(FlowScreen screen,bool boss=false)
        {
            EnsureListener();contextTarget=1;
            switch(screen)
            {
                case FlowScreen.Title:case FlowScreen.CharacterSelect:case FlowScreen.ThemeSelect:PlayMusic(MusicCue.Title);break;
                case FlowScreen.DungeonMap:case FlowScreen.Room:PlayMusic(MusicCue.Dungeon);break;
                case FlowScreen.Battle:PlayMusic(boss?MusicCue.Boss:MusicCue.Battle);break;
                default:StopMusic();break; // RunResult fades out; it never delays result flow.
            }
        }
        public void BattleComplete(){contextTarget=.30f;}
        public void PlayMusic(MusicCue cue)
        {
            var clip=assets!=null?assets.Clip(cue):null;
            if(clip==null)
            {
                if(cue!=MusicCue.Silent&&warned.Add(cue))Debug.LogWarning("BRODUN: no BGM clip assigned for "+cue+" in BrodunGameAudio.");
                StopMusic();return;
            }
            if(active>=0&&music[active].clip==clip&&music[active].isPlaying)return;
            if(transition!=null){StopCoroutine(transition);transition=null;}
            int incoming=-1;
            for(int i=0;i<music.Length;i++)if(music[i].clip==clip&&music[i].isPlaying)incoming=i;
            if(incoming<0)
            {
                incoming=active<0?(weights[0]<=weights[1]?0:1):1-active;RememberAndStop(incoming);
                music[incoming].clip=clip;weights[incoming]=0;music[incoming].volume=0;music[incoming].Play();
                if(positions.TryGetValue(clip,out var at)&&clip.length>.1f)music[incoming].time=Mathf.Clamp(at,0,clip.length-.05f);
            }
            active=incoming;gains[incoming]=assets.Gain(cue);
            transition=StartCoroutine(Crossfade(incoming));
        }
        public void StopMusic()
        {
            if(transition!=null)StopCoroutine(transition);
            active=-1;transition=StartCoroutine(Crossfade(-1));
        }
        private IEnumerator Crossfade(int incoming)
        {
            float a=weights[0],b=weights[1],seconds=assets!=null?Mathf.Max(.1f,assets.fadeSeconds):.75f;
            for(float t=0;t<seconds;t+=Time.unscaledDeltaTime)
            {
                float u=Mathf.SmoothStep(0,1,t/seconds);
                weights[0]=Mathf.Lerp(a,incoming==0?1:0,u);weights[1]=Mathf.Lerp(b,incoming==1?1:0,u);
                ApplyMix();yield return null;
            }
            for(int i=0;i<music.Length;i++){weights[i]=i==incoming?1:0;if(i!=incoming)RememberAndStop(i);}
            ApplyMix();transition=null;
        }
        private void RememberAndStop(int index)
        {var source=music[index];if(source.clip!=null&&source.isPlaying)positions[source.clip]=source.time;source.Stop();}
        private void ApplyMix()
        {for(int i=0;i<music.Length;i++)music[i].volume=MusicLevel*weights[i]*gains[i]*contextGain;}
        public float Volume(AudioChannel channel)=>channel==AudioChannel.Master?Master:channel==AudioChannel.Music?Music:Sfx;
        public bool Muted(AudioChannel channel)=>channel==AudioChannel.Master?MasterMuted:channel==AudioChannel.Music?MusicMuted:SfxMuted;
        public void SetVolume(AudioChannel channel,float value)
        {
            if(float.IsNaN(value)||float.IsInfinity(value))return;value=Mathf.Clamp01(value);
            if(Mathf.Approximately(Volume(channel),value))return;
            if(channel==AudioChannel.Master)Master=value;else if(channel==AudioChannel.Music)Music=value;else Sfx=value;
            Changed();
        }
        public void SetMuted(AudioChannel channel,bool value)
        {
            if(Muted(channel)==value)return;
            if(channel==AudioChannel.Master)MasterMuted=value;else if(channel==AudioChannel.Music)MusicMuted=value;else SfxMuted=value;
            Changed();
        }
        private void Changed(){dirty=true;saveAt=Time.unscaledTime+.25f;ApplyMix();SettingsChanged?.Invoke();}
        public void Save()
        {
            if(!dirty)return;
            PlayerPrefs.SetFloat(Pref+"Master",Master);PlayerPrefs.SetFloat(Pref+"Music",Music);PlayerPrefs.SetFloat(Pref+"Sfx",Sfx);
            PlayerPrefs.SetInt(Pref+"MasterMuted",MasterMuted?1:0);PlayerPrefs.SetInt(Pref+"MusicMuted",MusicMuted?1:0);PlayerPrefs.SetInt(Pref+"SfxMuted",SfxMuted?1:0);
            PlayerPrefs.Save();dirty=false;
        }
        public void OpenSettings(PresentationAssets art,RectTransform underlying)
        {
            if(settings!=null||art==null||(GameFlow.Instance!=null&&GameFlow.Instance.Session.IsTransitioning))return;
            settings=new GameObject("Audio settings",typeof(RectTransform)).AddComponent<AudioSettingsView>();
            settings.transform.SetParent(transform,false);settings.Build(this,art,underlying);
            SettingsVisibilityChanged?.Invoke(true);
        }
        public void CloseSettings()
        {
            if(settings==null)return;
            var old=settings;settings=null;old.ReleaseInput();Destroy(old.gameObject);Save();SettingsVisibilityChanged?.Invoke(false);
        }
        private void Update()
        {
            float next=Mathf.MoveTowards(contextGain,contextTarget,Time.unscaledDeltaTime*2);
            if(!Mathf.Approximately(next,contextGain)){contextGain=next;ApplyMix();}
            if(dirty&&Time.unscaledTime>=saveAt)Save();
        }
        private void OnApplicationPause(bool paused){if(paused)Save();}
        private void OnApplicationQuit(){Save();}
        private void OnDestroy()
        {if(Instance==this){if(settings!=null)settings.ReleaseInput();Save();Instance=null;}}
    }
}

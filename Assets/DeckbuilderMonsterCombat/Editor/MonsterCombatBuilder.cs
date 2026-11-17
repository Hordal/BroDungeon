using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeckbuilderMonsterCombat.Editor
{
    public static class MonsterCombatBuilder
    {
        public const string Root="Assets/DeckbuilderMonsterCombat";
        public static readonly string[] Motions={"Idle","Attack","Skill","Hit","Block","Death","Spawn"};
        private static MonsterCatalog Catalog() { return JsonUtility.FromJson<MonsterCatalog>(File.ReadAllText(Root+"/Data/monsters.json")); }
        private static void Folder(string path) { Directory.CreateDirectory(path); AssetDatabase.Refresh(); }
        private static void Replace(string path) { if(File.Exists(path)) AssetDatabase.DeleteAsset(path); }

        [MenuItem("Tools/Deckbuilder Monster Combat/Build Animation Assets")]
        public static void BuildAll()
        {
            Folder(Root+"/Animations");Folder(Root+"/Controllers");Folder(Root+"/Prefabs");Folder(Root+"/Scenes");
            var records=Catalog().monsters;
            var prefabs=new List<GameObject>();
            foreach(var record in records)
            {
                ImportSprite(record.spritePath);
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(record.spritePath);
                if(sprite==null) throw new Exception("Sprite missing: "+record.id);
                float bodyHeight=record.tier=="boss"?3.5f:record.tier=="elite"?2.8f:2.15f;
                float baseScale=bodyHeight/sprite.bounds.size.y;
                string clipFolder=Root+"/Animations/"+record.id;
                Folder(clipFolder);
                var clips=new Dictionary<string,AnimationClip>();
                foreach(string motion in Motions)
                {
                    var clip=MakeClip(record,motion,baseScale,bodyHeight);
                    string clipPath=clipFolder+"/"+record.id+"_"+motion+".anim";
                    Replace(clipPath);AssetDatabase.CreateAsset(clip,clipPath);clips[motion]=clip;
                }
                string controllerPath=Root+"/Controllers/"+record.id+".controller";
                Replace(controllerPath);
                var controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                var machine=controller.layers[0].stateMachine;
                var states=new Dictionary<string,AnimatorState>();
                foreach(string motion in Motions)
                {
                    var state=machine.AddState(motion);state.motion=clips[motion];state.writeDefaultValues=false;states[motion]=state;
                    if(motion!="Idle") controller.AddParameter(motion,AnimatorControllerParameterType.Trigger);
                }
                machine.defaultState=states["Idle"];
                foreach(string motion in Motions.Where(m=>m!="Idle"))
                {
                    var enter=machine.AddAnyStateTransition(states[motion]);
                    enter.hasExitTime=false;enter.duration=.045f;enter.canTransitionToSelf=true;
                    enter.AddCondition(AnimatorConditionMode.If,0,motion);
                    if(motion=="Death")continue;
                    var exit=states[motion].AddTransition(states["Idle"]);
                    exit.hasExitTime=true;exit.exitTime=1f;exit.duration=.06f;
                }
                var go=new GameObject(record.id);
                var visual=new GameObject("Visual");visual.transform.SetParent(go.transform,false);
                visual.transform.localScale=Vector3.one*baseScale;
                visual.AddComponent<SpriteRenderer>().sprite=sprite;
                var animator=go.AddComponent<Animator>();animator.runtimeAnimatorController=controller;
                animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                var actor=go.AddComponent<MonsterCombatActor>();
                actor.monsterId=record.id;actor.displayName=record.nameKo;actor.skillName=record.skillNameKo;
                actor.attackStyle=record.attackStyle;actor.element=record.floor;actor.bodyHeight=bodyHeight;
                string prefabPath=Root+"/Prefabs/"+record.id+".prefab";
                prefabs.Add(PrefabUtility.SaveAsPrefabAsset(go,prefabPath));
                UnityEngine.Object.DestroyImmediate(go);
            }
            AssetDatabase.SaveAssets();
            MakeDemo(prefabs.ToArray());
            Validate();
            Debug.Log("MONSTER_COMBAT_BUILD_PASSED: 57 monsters, 399 clips, 57 controllers, 57 animated prefabs");
        }

        private static void ImportSprite(string path)
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=true;
            importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;
            importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=new Vector2(.5f,0);
            settings.spritePixelsPerUnit=100;settings.spriteMeshType=SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);importer.SaveAndReimport();
        }
        public static float Duration(MonsterRecord r,string motion)
        {
            if(motion=="Idle")return (r.idleStyle=="Float"?2.6f:2.1f)*r.variation;
            if(motion=="Attack")return (r.attackStyle=="Dragon"?1.25f:r.attackStyle=="Heavy"||r.attackStyle=="Bell"?1.05f:.78f)*r.variation;
            if(motion=="Skill")return (r.tier=="boss"?1.95f:1.45f)*r.variation;
            if(motion=="Hit")return .42f;
            if(motion=="Block")return .65f;
            if(motion=="Death")return 1.15f;
            return .75f;
        }
        public static float ImpactTime(MonsterRecord r,string motion) { return Duration(r,motion)*(motion=="Skill"?.53f:.43f); }
        private struct Pose
        {
            public float x,y,z,sx,sy,r,g,b,a;
            public static Pose Default { get {return new Pose{sx=1,sy=1,r=1,g=1,b=1,a=1};} }
        }
        private static float Smooth(float a,float b,float t) { return Mathf.SmoothStep(a,b,t); }
        private static float Envelope(float t,float peak)
        {
            if(t<peak)return Smooth(0,1,t/peak);
            return Smooth(1,0,(t-peak)/(1-peak));
        }
        private static Pose Evaluate(MonsterRecord r,string motion,float t,float height)
        {
            var p=Pose.Default;
            float k=height/2.4f;
            if(motion=="Idle")
            {
                float s=Mathf.Sin(t*Mathf.PI*2);
                if(r.idleStyle=="Float"){p.y=(.07f+.065f*s)*k;p.z=s*1.8f;}
                else {p.sy=1+.018f*s;p.sx=1-.009f*s;p.z=s*(r.attackStyle=="Crawl"?.65f: .3f);}
                return p;
            }
            if(motion=="Attack"||motion=="Skill")
            {
                bool skill=motion=="Skill";
                float impact=skill?.53f:.43f;
                float anticipation=t<impact?Mathf.Sin(Mathf.PI*t/impact):0;
                float strike=t<impact?0:Envelope((t-impact)/(1-impact),.14f);
                float amplitude=skill?1.25f:1f;
                p.x=(.14f*anticipation-.28f*strike)*k;
                p.z=5*anticipation-8*strike;
                p.sx=1+.035f*anticipation+.055f*strike;p.sy=1-.035f*anticipation-.025f*strike;
                switch(r.attackStyle)
                {
                    case "Crawl":p.x=(.1f*anticipation-.65f*strike)*k;p.z=-3*strike;p.sy=1-.12f*anticipation+.04f*strike;break;
                    case "Lunge":p.x=(.2f*anticipation-.95f*strike)*k;p.y=Mathf.Sin(Mathf.PI*strike)*.27f*k;p.z=8*anticipation-12*strike;break;
                    case "Heavy":p.x=-.28f*strike*k;p.y=(.16f*anticipation-.06f*strike)*k;p.z=8*anticipation-10*strike;p.sy=1-.08f*strike;break;
                    case "Slash":p.x=(.12f*anticipation-.43f*strike)*k;p.z=12*anticipation-16*strike;break;
                    case "Sting":p.x=(.16f*anticipation-.63f*strike)*k;p.z=3*anticipation-5*strike;p.sx=1-.07f*anticipation+.1f*strike;break;
                    case "Fly":p.y=(.15f+.3f*anticipation+.22f*strike)*k;p.x=-.55f*strike*k;p.z=10*anticipation-13*strike;break;
                    case "Cast":p.x=-.09f*strike*k;p.y=.12f*anticipation*k;p.z=3*anticipation-3*strike;p.sy=1+.055f*anticipation;break;
                    case "Pulse":p.x=0;p.z=0;p.sx=1-.09f*anticipation+.17f*strike;p.sy=1-.06f*anticipation+.12f*strike;break;
                    case "Bell":p.z=10*anticipation-13*strike;p.x=-.08f*strike*k;p.y=.1f*anticipation*k;break;
                    case "Spin":p.z=skill?-360*Smooth(0,1,t):-24*Mathf.Sin(t*Mathf.PI*2);p.y=.12f*Mathf.Sin(Mathf.PI*t)*k;break;
                    case "Dragon":p.x=(.14f*anticipation-.22f*strike)*k;p.z=4*anticipation-4*strike;p.sx=1+.05f*anticipation;p.sy=1+.04f*anticipation-.05f*strike;break;
                    case "Tentacle":p.x=-.22f*strike*k;p.y=.12f*anticipation*k;p.z=4*anticipation-7*strike;p.sx=1+.06f*strike;p.sy=1+.04f*anticipation;break;
                }
                p.x*=amplitude;p.y*=amplitude;p.z*=skill?1.15f:1;
                if(skill){float glow=anticipation*.25f;p.r=1;p.g=1-glow*.45f;p.b=1-glow*.25f;}
                if(t>=.999f && r.attackStyle!="Fly")return Pose.Default;
                return p;
            }
            if(motion=="Hit")
            {
                float e=Envelope(t,.18f);p.x=e*.22f*k;p.z=-6*e;p.sx=1-.08f*e;p.sy=1+.025f*e;
                float flash=t<.28f?Mathf.Sin(t/.28f*Mathf.PI):0;p.g=1-flash*.65f;p.b=1-flash*.65f;return p;
            }
            if(motion=="Block")
            {
                float e=Envelope(t,.25f);p.x=.09f*e*k;p.sy=1-.055f*e;p.sx=1+.035f*e;p.r=1-e*.25f;p.g=1-e*.12f;return p;
            }
            if(motion=="Death")
            {
                p.x=.2f*t*k;p.y=-height*.07f*t;p.z=-16*Smooth(0,1,t);
                p.sx=1+.12f*t;p.sy=1-.55f*Smooth(0,1,t);p.a=1-Smooth(0,1,Mathf.InverseLerp(.28f,1,t));
                p.r=p.g=p.b=1-.6f*t;return p;
            }
            p.a=Smooth(0,1,Mathf.Clamp01(t/.6f));p.y=-.15f*(1-t)*k;
            p.sx=1+.1f*Mathf.Sin(Mathf.PI*t)*(1-t);p.sy=.88f+.12f*Smooth(0,1,t);return p;
        }

        private static AnimationClip MakeClip(MonsterRecord r,string motion,float scale,float height)
        {
            float duration=Duration(r,motion);int frames=Mathf.CeilToInt(duration*30);
            var clip=new AnimationClip{name=r.id+"_"+motion,frameRate=30};
            string[] properties={"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z","localEulerAnglesRaw.z","m_LocalScale.x","m_LocalScale.y","m_LocalScale.z","m_Color.r","m_Color.g","m_Color.b","m_Color.a"};
            var lists=properties.Select(_=>new List<Keyframe>()).ToArray();
            for(int i=0;i<=frames;i++)
            {
                float t=i/(float)frames;var p=Evaluate(r,motion,t,height);
                float[] values={p.x,p.y,0,p.z,p.sx*scale,p.sy*scale,scale,p.r,p.g,p.b,p.a};
                for(int j=0;j<values.Length;j++)lists[j].Add(new Keyframe(t*duration,values[j]));
            }
            for(int j=0;j<properties.Length;j++)
            {
                var curve=new AnimationCurve(lists[j].ToArray());
                for(int i=0;i<curve.length;i++)curve.SmoothTangents(i,0f);
                AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("Visual",j<7?typeof(Transform):typeof(SpriteRenderer),properties[j]),curve);
            }
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=motion=="Idle";AnimationUtility.SetAnimationClipSettings(clip,settings);
            var events=new List<AnimationEvent>();
            if(motion=="Attack"||motion=="Skill")events.Add(new AnimationEvent{time=ImpactTime(r,motion),functionName=motion=="Attack"?"OnAttackImpact":"OnSkillImpact"});
            if(motion=="Death")events.Add(new AnimationEvent{time=duration-.01f,functionName="OnDeathFinished"});
            AnimationUtility.SetAnimationEvents(clip,events.ToArray());
            return clip;
        }
        private static void MakeDemo(GameObject[] prefabs)
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var cameraGo=new GameObject("Main Camera");cameraGo.tag="MainCamera";
            var camera=cameraGo.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=3.2f;
            camera.backgroundColor=new Color(.07f,.08f,.11f);camera.clearFlags=CameraClearFlags.SolidColor;
            cameraGo.transform.position=new Vector3(-.4f,1.8f,-10);
            var demo=new GameObject("Monster Motion Browser").AddComponent<MonsterCombatDemo>();demo.monsters=prefabs;
            EditorSceneManager.SaveScene(scene,Root+"/Scenes/MonsterCombatDemo.unity");
            AssetDatabase.SaveAssets();
        }
        [MenuItem("Tools/Deckbuilder Monster Combat/Validate Animation Assets")]
        public static void Validate()
        {
            var records=Catalog().monsters;
            if(records.Length!=57)throw new Exception("Expected 57 profiles");
            if(AssetDatabase.FindAssets("t:AnimationClip",new[]{Root+"/Animations"}).Length!=399)throw new Exception("Expected 399 clips");
            foreach(var r in records)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/"+r.id+".prefab");
                if(prefab==null||prefab.GetComponent<Animator>().runtimeAnimatorController==null||prefab.GetComponent<MonsterCombatActor>()==null)throw new Exception("Incomplete prefab "+r.id);
                var go=UnityEngine.Object.Instantiate(prefab);
                try
                {
                    foreach(string m in Motions)
                    {
                        var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Animations/"+r.id+"/"+r.id+"_"+m+".anim");
                        if(clip==null||clip.length<=0||AnimationUtility.GetCurveBindings(clip).Length<10)throw new Exception("Invalid clip "+r.id+"/"+m);
                        clip.SampleAnimation(go,clip.length);
                        float alpha=go.transform.Find("Visual").GetComponent<SpriteRenderer>().color.a;
                        if(m=="Death" && alpha>.01f)throw new Exception("Death fade did not complete "+r.id);
                        if(m!="Death" && alpha<.99f)throw new Exception("State did not restore visibility "+r.id+"/"+m);
                        if((m=="Attack"||m=="Skill")&&AnimationUtility.GetAnimationEvents(clip).Length==0)throw new Exception("Impact hook missing "+r.id);
                    }
                }
                finally{UnityEngine.Object.DestroyImmediate(go);}
            }
            string report=Path.GetFullPath("Logs/MonsterCombatValidation.json");
            Directory.CreateDirectory(Path.GetDirectoryName(report));
            File.WriteAllText(report,"{\"passed\":true,\"unityVersion\":\""+Application.unityVersion+"\",\"monsters\":57,\"clips\":399,\"controllers\":57,\"prefabs\":57,\"motionTypes\":7,\"attackArchetypes\":12}");
            Debug.Log("MONSTER_COMBAT_VALIDATION_PASSED");
        }
        public static void BatchBuild()
        {
            try{BuildAll();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        public static void BatchExport()
        {
            try
            {
                Validate();
                string[] paths=AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith(Root+"/",StringComparison.Ordinal)&&!Directory.Exists(p)).ToArray();
                string export=Path.GetFullPath("Builds/DeckbuilderMonsterCombat.unitypackage");
                Directory.CreateDirectory(Path.GetDirectoryName(export));
                AssetDatabase.ExportPackage(paths,export,ExportPackageOptions.Default);
                Debug.Log("MONSTER_COMBAT_EXPORT_PASSED");EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}

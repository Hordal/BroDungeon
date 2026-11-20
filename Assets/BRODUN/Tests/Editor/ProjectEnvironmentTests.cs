using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Brodun.Tests
{
    public sealed class ProjectEnvironmentTests
    {
        [UnityTest] public IEnumerator PackageResolveKeepsExistingVersionsAndNoAssistant()
        {
            Assert.AreEqual("6000.0.47f1",Application.unityVersion);
            var request=Client.List(false,true);
            double deadline=EditorApplication.timeSinceStartup+60;
            while(!request.IsCompleted && EditorApplication.timeSinceStartup<deadline)yield return null;
            Assert.IsTrue(request.IsCompleted,"Package resolve/list timed out.");
            Assert.AreEqual(StatusCode.Success,request.Status,request.Error?.message);
            Assert.IsFalse(request.Result.Any(p=>p.name=="com.unity.ai.assistant"));
            Assert.AreEqual("17.0.4",request.Result.Single(p=>p.name=="com.unity.render-pipelines.universal").version);
            Assert.AreEqual("1.5.1",request.Result.Single(p=>p.name=="com.unity.test-framework").version);
            Assert.AreEqual("1.11.5",request.Result.Single(p=>p.name=="com.unity.nuget.mono-cecil").version);
            Debug.Log("BRODUN Package Resolve/List successful: "+request.Result.Count()+" packages; Unity "+Application.unityVersion);
        }
        [TestCase("Assets/Scenes/SampleScene.unity")]
        [TestCase("Assets/Settings/Scenes/URP2DSceneTemplate.unity")]
        public void ExistingSceneLoadsWithoutMissingScriptsOrSerializationWarnings(string path)
        {
            var previous=SceneManager.GetActiveScene();
            var existing=SceneManager.GetSceneByPath(path);
            bool alreadyLoaded=existing.IsValid() && existing.isLoaded;
            int missing=0; var messages=new System.Collections.Generic.List<string>();
            Application.LogCallback capture=(message,stack,type)=>
            {
                if(type==LogType.Error || type==LogType.Exception ||
                    (type==LogType.Warning && message.IndexOf("serializ",StringComparison.OrdinalIgnoreCase)>=0))messages.Add(message);
            };
            Application.logMessageReceived+=capture;
            try
            {
                var scene=alreadyLoaded?existing:EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                Assert.IsTrue(scene.isLoaded);Assert.AreEqual(2,scene.rootCount);
                foreach(var root in scene.GetRootGameObjects())
                    foreach(var transform in root.GetComponentsInChildren<Transform>(true))
                        missing+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                Assert.AreEqual(0,missing);Assert.IsEmpty(messages);
            }
            finally
            {
                Application.logMessageReceived-=capture;
                if(previous.IsValid() && previous.isLoaded)SceneManager.SetActiveScene(previous);
                var inspected=SceneManager.GetSceneByPath(path);
                // Headless tests can start without any loaded/active scene. Do not restore an invalid setup.
                if(!alreadyLoaded && inspected.IsValid() && SceneManager.sceneCount>1)
                    EditorSceneManager.CloseScene(inspected,true); // Never save or rewrite team scenes.
            }
        }
        [Test] public void ExistingURPUses2DRenderer()
        {
            var pipeline=GraphicsSettings.defaultRenderPipeline;
            Assert.NotNull(pipeline);Assert.AreEqual("UniversalRenderPipelineAsset",pipeline.GetType().Name);
            var serialized=new SerializedObject(pipeline);
            var renderers=serialized.FindProperty("m_RendererDataList");
            Assert.NotNull(renderers);Assert.Greater(renderers.arraySize,0);
            Assert.AreEqual("Renderer2DData",renderers.GetArrayElementAtIndex(0).objectReferenceValue.GetType().Name);
        }
    }
}

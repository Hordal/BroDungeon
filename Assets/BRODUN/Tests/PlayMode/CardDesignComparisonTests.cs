#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Brodun.Flow;
using Brodun.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace Brodun.Tests.PlayMode
{
    public sealed class CardDesignComparisonTests
    {
        private GameFlow Flow=>GameFlow.Instance;
        private BattleController Controller=>UnityEngine.Object.FindFirstObjectByType<BattleController>();
        private BattleRuntime Runtime=>Flow.Session.Battle.Runtime;
        private string Output=>Environment.GetEnvironmentVariable("BRODUN_CARD_DESIGN_CAPTURE_DIR");
        private static void Pointer(GameObject target)=>ExecuteEvents.Execute(target,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
        private static void Click(string name)
        {var b=UnityEngine.Object.FindObjectsByType<StyledButton>(FindObjectsSortMode.None).Single(x=>x.name==name);Assert.IsTrue(b.IsInteractable(),name);Pointer(b.gameObject);}
        [UnitySetUp] public IEnumerator Boot()
        {
            CardDesignComparison.Reset();if(Flow!=null){UnityEngine.Object.Destroy(Flow.gameObject);yield return null;}
            yield return SceneManager.LoadSceneAsync("BrodunBoot");yield return Ready(FlowScreen.Title);
        }
        [UnityTearDown] public IEnumerator Clean()
        {CardDesignComparison.Reset();if(Flow!=null)UnityEngine.Object.Destroy(Flow.gameObject);yield return null;}
        private IEnumerator Ready(FlowScreen screen)
        {
            float deadline=Time.realtimeSinceStartup+15;
            while((Flow==null||Flow.Session.Screen!=screen||Flow.Session.IsTransitioning)&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.NotNull(Flow);Assert.AreEqual(screen,Flow.Session.Screen);Assert.IsFalse(Flow.Session.IsTransitioning);yield return null;
        }
        private IEnumerator Prepare()
        {
            var defs=CardDesignComparison.Definitions(Flow.Session.Definitions);var seed=CardDesignComparison.ComparisonSeed(defs);
            Assert.IsTrue(Flow.StartEditorPreviewRun(defs,seed));
            var art=CardEffectSummary.PreviewIds.ToDictionary(id=>id,id=>AssetDatabase.LoadAssetAtPath<Sprite>("Assets/BRODUN/Art/QA/CardDesignComparison/"+id+".png"));
            Assert.IsTrue(art.Values.All(s=>s!=null));CardDesignComparison.Enable(Flow.Session,art,CardDesignVariant.Illustration);
            yield return Ready(FlowScreen.CharacterSelect);Click("Character_Warrior");yield return Ready(FlowScreen.DungeonMap);
            string first=DungeonRuntime.Available(Flow.Session.Run).First(id=>DungeonRuntime.Node(Flow.Session.Run,id).Type==RoomType.Battle);
            Click("Node_"+first);yield return Ready(FlowScreen.Battle);yield return new WaitForSecondsRealtime(.5f);
            Assert.AreEqual(5,Runtime.Energy);Assert.AreEqual(5,Runtime.Deck.Hand.Count);Assert.AreEqual(6,Flow.Session.Run.Deck.Count);
            CollectionAssert.AreEquivalent(new[]{"B-W01","W02","W05","W12","W08"},Runtime.Deck.Hand.Select(c=>c.CardId));
            TestContext.WriteLine("CARD_UI_QA seed="+seed+" hand="+string.Join(",",Runtime.Deck.Hand.Select(c=>c.CardId)));
        }
        private IEnumerator Settle()
        {
            float deadline=Time.realtimeSinceStartup+12;
            while(Flow.Session.Battle.Phase==BattlePhase.ResolvingPlayerAction||Flow.Session.Battle.Phase==BattlePhase.EnemyTurn)
            {Assert.Less(Time.realtimeSinceStartup,deadline);yield return null;}
            yield return new WaitForSecondsRealtime(.3f);
        }
        private CardView Card(string id)=>Controller.View.Cards.Single(c=>c.Definition.Id==id);
        private IEnumerator Use(string id){Pointer(Card(id).gameObject);yield return Settle();Assert.AreEqual(id,Runtime.LastCardResult.CardId);}
        private string State()=>string.Join("|",Runtime.Deck.Hand.Select(c=>c.InstanceId))+"/"+string.Join("|",Runtime.Deck.DrawPile.Select(c=>c.InstanceId))+"/"+Runtime.Energy+"/"+Runtime.Block+"/"+Runtime.Run.CurrentHP+"/"+Runtime.Enemy.CurrentHP;
        [UnityTest] public IEnumerator RenderAllDesignsAtBothResolutionsWithHoverGalleryAndBlindSymbols()
        {
            PlayModeWindow.SetCustomRenderingResolution(1920,1080,"BRODUN Card design QA");yield return null;yield return Prepare();
            string state=State();
            foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,720)})
            {
                PlayModeWindow.SetCustomRenderingResolution((uint)size.x,(uint)size.y,"BRODUN Card design "+size.x);yield return null;yield return null;
                foreach(CardDesignVariant variant in Enum.GetValues(typeof(CardDesignVariant)))
                {
                    string letter=variant==CardDesignVariant.Illustration?"A":variant==CardDesignVariant.Symbols?"B":"C";
                    Click("QA_"+letter);yield return new WaitForSecondsRealtime(.5f);Assert.AreEqual(state,State());
                    yield return Capture("Battle_"+letter+"_"+size.x+"x"+size.y);
                    Assert.AreEqual(5,Controller.View.Cards.Count);AssertCards(Controller.View.Cards);
                    if(variant==CardDesignVariant.Illustration)Assert.IsTrue(Controller.View.Cards.All(c=>c.GetComponent<CardDesignFace>()==null));
                    else Assert.IsTrue(Controller.View.Cards.All(c=>c.GetComponent<CardDesignFace>()!=null));
                    var hovered=Card("W12");hovered.OnPointerEnter(new PointerEventData(EventSystem.current));yield return new WaitForSecondsRealtime(.4f);
                    Assert.IsTrue(hovered.Hovered);AssertCards(new[]{hovered});yield return Capture("Hover_"+letter+"_"+size.x+"x"+size.y);
                    hovered.OnPointerExit(new PointerEventData(EventSystem.current));yield return new WaitForSecondsRealtime(.35f);
                    Click("QA_Gallery");yield return null;yield return new WaitForSecondsRealtime(.3f);
                    var gallery=Controller.GetComponent<CardDesignQaToolbar>();Assert.AreEqual(6,gallery.GalleryCards.Count);AssertCards(gallery.GalleryCards);
                    if(size.x==1920)
                    {
                        string name="Gallery_"+letter;yield return Capture(name);SaveRegions(name,gallery.GalleryCards);
                        Click("QA_Blind");yield return null;yield return new WaitForSecondsRealtime(.3f);
                        Assert.IsTrue(gallery.GalleryCards.All(c=>!c.title.gameObject.activeSelf&&!c.description.gameObject.activeSelf&&!c.artwork.gameObject.activeSelf));
                        yield return Capture("Blind_"+letter);SaveRegions("Blind_"+letter,gallery.GalleryCards);
                        Click("QA_Blind");yield return null;
                    }
                    Click("QA_Gallery");yield return null;Assert.AreEqual(state,State());
                }
            }
            PlayModeWindow.SetCustomRenderingResolution(1920,1080,"BRODUN Card symbol legend");yield return null;
            Click("QA_Legend");yield return null;yield return Capture("Effect_Symbol_Legend");
            var glyphs=Controller.View.Root.GetComponentsInChildren<CardEffectGlyph>();
            foreach(CardEffectSymbol s in Enum.GetValues(typeof(CardEffectSymbol)))
            {
                var glyph=glyphs.First(g=>g.Symbol==s);using(var vh=new VertexHelper())
                {
                    typeof(CardEffectGlyph).GetMethod("OnPopulateMesh",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance,
                        null,new[]{typeof(VertexHelper)},null).Invoke(glyph,new object[]{vh});
                    Assert.Greater(vh.currentVertCount,0,s.ToString());
                }
            }
        }
        [UnityTest] public IEnumerator UiSwitchesPreserveRealCostDamageAndDefenseThenANormalRunUsesNormalUi()
        {
            yield return Prepare();CardDesignComparison.SetVariant(CardDesignVariant.Symbols);yield return null;
            yield return Use("W05");Assert.AreEqual(14,Runtime.Block);Assert.AreEqual(3,Runtime.Energy);Assert.AreEqual(4,Runtime.Deck.Hand.Count);
            CardDesignComparison.SetVariant(CardDesignVariant.Hybrid);yield return null;
            yield return Use("W08");Assert.AreEqual(20,Runtime.LastCardResult.Damage);Assert.AreEqual(14,Runtime.Block);Assert.AreEqual(0,Runtime.Energy);
            var unavailable=Card("B-W01");Assert.IsFalse(unavailable.CanPlay);unavailable.OnPointerEnter(new PointerEventData(EventSystem.current));yield return null;Assert.IsTrue(unavailable.Hovered);
            Assert.IsFalse(Controller.PlayCard(unavailable.InstanceId));Assert.AreEqual(0,Runtime.Energy);
            Flow.ReturnToTitle();yield return Ready(FlowScreen.Title);Assert.IsTrue(Flow.StartNewRun(17));yield return Ready(FlowScreen.CharacterSelect);
            Assert.IsFalse(CardDesignComparison.Active);Click("Character_Warrior");yield return Ready(FlowScreen.ThemeSelect);Click("Theme_ruins");yield return Ready(FlowScreen.DungeonMap);Assert.AreEqual(10,Flow.Session.Run.Deck.Count);
            Click("Node_"+DungeonRuntime.Available(Flow.Session.Run).First());yield return Ready(FlowScreen.Battle);
            Assert.IsTrue(Controller.View.Cards.All(c=>c.GetComponent<CardDesignFace>()==null));Assert.IsNull(Controller.GetComponent<CardDesignQaToolbar>());
        }
        [UnityTest] public IEnumerator RepairExistingBlockBranchAndDrawStillUseTheOriginalRules()
        {
            yield return Prepare();CardDesignComparison.SetVariant(CardDesignVariant.Symbols);yield return null;
            yield return Use("W02");Assert.AreEqual(4,Runtime.Block);Assert.AreEqual(4,Runtime.Energy);Assert.AreEqual(5,Runtime.Deck.Hand.Count);
            yield return Use("W05");Assert.AreEqual(13,Runtime.Block);Assert.AreEqual(2,Runtime.Energy);Assert.AreEqual(5,Runtime.Deck.Hand.Count);
        }
        private static void AssertCards(IEnumerable<CardView> cards)
        {
            Canvas.ForceUpdateCanvases();
            foreach(var c in cards)
            {
                Assert.AreEqual(c.Definition.Cost.ToString(),c.cost.text,c.Definition.Id);
                if(c.description.gameObject.activeInHierarchy)Assert.GreaterOrEqual(c.description.cachedTextGenerator.characterCountVisible,c.Definition.Description.Length-1,c.Definition.Id+" full description");
                var corners=new Vector3[4];c.visual.GetWorldCorners(corners);
                foreach(var p in corners){Assert.That(p.x,Is.InRange(-2,Screen.width+2),c.Definition.Id);Assert.That(p.y,Is.InRange(-2,Screen.height+2),c.Definition.Id);}
                var summary=c.GetComponent<CardDesignFace>()?.Summary;
                if(summary!=null)foreach(var t in summary.GetComponentsInChildren<Text>())
                    Assert.GreaterOrEqual(t.cachedTextGenerator.characterCountVisible,t.text.Length-1,c.Definition.Id+" summary "+t.name+" "+t.text);
            }
        }
        [Serializable] private sealed class RegionFile {public string source;public int width,height;public Region[] cards;}
        [Serializable] private sealed class Region {public string id,name;public int x,y,width,height;}
        private void SaveRegions(string name,IReadOnlyList<CardView> cards)
        {
            if(string.IsNullOrEmpty(Output))return;
            var regions=cards.Select(card=>
            {
                var p=new Vector3[4];card.visual.GetWorldCorners(p);int x=Mathf.FloorToInt(p[0].x),y=Mathf.FloorToInt(Screen.height-p[2].y);
                return new Region{id=card.Definition.Id,name=card.Definition.Name,x=x,y=y,width=Mathf.CeilToInt(p[2].x)-x,height=Mathf.CeilToInt(Screen.height-p[0].y)-y};
            }).ToArray();
            File.WriteAllText(Path.Combine(Output,name+".json"),JsonUtility.ToJson(new RegionFile{source=name+".png",width=Screen.width,height=Screen.height,cards=regions},true));
        }
        private IEnumerator Capture(string name)
        {
            if(string.IsNullOrEmpty(Output))yield break;Directory.CreateDirectory(Output);
            yield return new WaitForSecondsRealtime(.25f);Canvas.ForceUpdateCanvases();string path=Path.Combine(Output,name+".png");ScreenCapture.CaptureScreenshot(path);
            yield return null;yield return null;float deadline=Time.realtimeSinceStartup+5;while(!File.Exists(path)&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsTrue(File.Exists(path),name);
        }
    }
}
#endif

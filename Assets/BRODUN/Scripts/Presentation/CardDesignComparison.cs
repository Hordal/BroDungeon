#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Brodun.Flow;
using UnityEngine;
using UnityEngine.UI;
namespace Brodun.Presentation
{
    public enum CardDesignVariant { Illustration, Symbols, Hybrid }
    // Session-scoped Editor QA switch. A normal new run cannot inherit these styles or artworks.
    public static class CardDesignComparison
    {
        private static FlowSession session;
        private static IReadOnlyDictionary<string,Sprite> artworks;
        public static CardDesignVariant Variant {get;private set;}
        public static bool Blind {get;private set;}
        public static bool Active=>session!=null&&GameFlow.Instance!=null&&ReferenceEquals(session,GameFlow.Instance.Session);
        public static GameDefinitions Definitions(GameDefinitions source)
        {
            var heroes=source.Characters.Select(h=>h.Id==CharacterId.Warrior?
                new CharacterDefinition(h.Id,h.Name,h.Description,h.MaxHP,h.Energy,CardEffectSummary.PreviewIds):h);
            return new GameDefinitions(source.AllCards,heroes,source.Enemies,source.Config,source.Reward,source.Elements);
        }
        public static uint ComparisonSeed(GameDefinitions data)
        {
            // Choose a reproducible ordinary shuffle, not an injected hand. The sixth card is in draw.
            var desired=new[]{"B-W01","W02","W05","W12","W08"};
            for(uint seed=0;seed<1000;seed++)
            {
                var map=LayeredMapGenerator.Generate(seed);var room=map.Rooms.First(n=>n.Type==RoomType.Battle&&n.Layer==1);
                var run=new RunState(data,CharacterId.Warrior,seed,dungeon:map);
                var hand=DeckRuntime.Begin(data,run,room).Hand.Select(c=>c.CardId).ToArray();
                if(hand.Length==5&&desired.All(hand.Contains))return seed;
            }
            throw new InvalidOperationException("No reproducible comparison hand found.");
        }
        public static void Enable(FlowSession owner,IReadOnlyDictionary<string,Sprite> images,CardDesignVariant variant)
        {session=owner;artworks=images;Variant=variant;Blind=false;}
        public static void Reset(){session=null;artworks=null;Blind=false;}
        public static void SetVariant(CardDesignVariant variant,bool blind=false)
        {
            var battle=Active?UnityEngine.Object.FindFirstObjectByType<BattleController>():null;
            if(battle!=null&&battle.Context.Phase!=BattlePhase.PlayerTurn)return;
            Variant=variant;Blind=blind;
            if(!Active)return;
            if(battle==null)return;
            battle.View.RefreshHand(card=>battle.PlayCard(card.InstanceId),true);battle.View.Refresh();
            battle.GetComponent<CardDesignQaToolbar>()?.RefreshGallery();
        }
        public static void Apply(CardView card)
        {
            if(!Active||!CardEffectSummary.Supports(card.Definition.Id))return;
            if(artworks!=null&&artworks.TryGetValue(card.Definition.Id,out var sprite))card.artwork.sprite=sprite;
            if(Variant!=CardDesignVariant.Illustration||Blind)
            {
                var face=card.GetComponent<CardDesignFace>()??card.gameObject.AddComponent<CardDesignFace>();face.Configure(card);
            }
            var battle=card.GetComponentInParent<BattleController>();
            if(battle!=null&&battle.GetComponent<CardDesignQaToolbar>()==null)battle.gameObject.AddComponent<CardDesignQaToolbar>().Owner=battle;
        }
        public static void Restyle(CardView card){if(Active)card.GetComponent<CardDesignFace>()?.ApplyLayout();}
        public static Color Tint(CardEffectSymbol symbol)
        {
            switch(symbol)
            {
                case CardEffectSymbol.Damage:return new Color(.94f,.73f,.63f);
                case CardEffectSymbol.Block:return new Color(.56f,.80f,.82f);
                case CardEffectSymbol.Draw:return new Color(.88f,.79f,.57f);
                case CardEffectSymbol.Counter:return new Color(.90f,.65f,.42f);
                case CardEffectSymbol.BlockScale:return new Color(.70f,.83f,.96f);
                case CardEffectSymbol.BlockSpend:return new Color(.76f,.49f,.39f);
                case CardEffectSymbol.BlockRestore:return new Color(.52f,.79f,.66f);
                default:return new Color(.67f,.63f,.51f);
            }
        }
        public static CardEffectGlyph Glyph(Transform parent,string name,CardEffectSymbol symbol,Vector2 min,Vector2 max)
        {var g=BattleUi.Rect(parent,name,min,max).gameObject.AddComponent<CardEffectGlyph>();g.Set(symbol);g.color=Tint(symbol);return g;}
    }

    // Attached only to the six supported QA cards. Existing CardView still owns input and hover.
    public sealed class CardDesignFace:MonoBehaviour
    {
        private CardView view;
        private RectTransform summary;
        private Sprite cropped;
        private readonly List<RectTransform> rows=new List<RectTransform>();
        private readonly Dictionary<Text,int> typeSizes=new Dictionary<Text,int>();
        private readonly Dictionary<Text,Vector3> fittedAreas=new Dictionary<Text,Vector3>();
        private readonly Dictionary<Text,int> fittedSizes=new Dictionary<Text,int>();
        public RectTransform Summary=>summary;
        private static void Slot(RectTransform r,float x0,float y0,float x1,float y1)
        {r.anchorMin=new Vector2(x0,y0);r.anchorMax=new Vector2(x1,y1);r.offsetMin=r.offsetMax=Vector2.zero;}
        public void Configure(CardView card)
        {
            view=card;bool hybrid=CardDesignComparison.Variant==CardDesignVariant.Hybrid;
            if(hybrid&&view.artwork.sprite!=null)
            {
                var source=view.artwork.sprite;var r=source.rect;float h=r.width/4f;
                cropped=Sprite.Create(source.texture,new Rect(r.x,r.center.y-h*.5f,r.width,h),new Vector2(.5f,.5f),source.pixelsPerUnit);
                cropped.name=source.name+" QA band";view.artwork.sprite=cropped;view.artwork.preserveAspect=false;
            }
            if(CardDesignComparison.Variant!=CardDesignVariant.Illustration)
            {
                summary=BattleUi.Rect(view.visual,"QA effect summary",Vector2.zero,Vector2.one);
                var lines=CardEffectSummary.For(card.Definition);
                for(int i=0;i<lines.Count;i++)
                {
                    var line=lines[i];var row=BattleUi.Rect(summary,"Effect row "+i,new Vector2(0,1-(i+1f)/lines.Count),new Vector2(1,1-i/(float)lines.Count));rows.Add(row);
                    var divider=BattleUi.Image(row,"Engraved divider",null,new Color(.44f,.39f,.28f,.43f),new Vector2(0,0),new Vector2(1,.018f));divider.raycastTarget=false;
                    if(line.Conditional)
                    {
                        CardDesignComparison.Glyph(row,"Conditional marker",CardEffectSymbol.Conditional,new Vector2(.01f,.69f),new Vector2(.075f,.98f));
                        if(CardDesignComparison.Blind)
                        {
                            CardDesignComparison.Glyph(row,"Condition shield",CardEffectSymbol.Block,new Vector2(.10f,.72f),new Vector2(.18f,.98f));
                            Label(row,"Condition value",line.BlindCondition,13,new Color(.76f,.72f,.60f),new Vector2(.19f,.70f),new Vector2(.47f,.99f),TextAnchor.MiddleLeft);
                        }
                        else
                        {
                            string condition=hybrid?CompactCondition(line):line.Condition;
                            Label(row,"Condition",condition,hybrid?11:12,new Color(.76f,.72f,.60f),new Vector2(.09f,.69f),new Vector2(.99f,.99f),TextAnchor.MiddleLeft);
                        }
                    }
                    for(int b=0;b<line.Badges.Count;b++)
                    {
                        var badge=line.Badges[b];var cell=BattleUi.Rect(row,"Badge "+badge.Symbol,new Vector2(b/(float)line.Badges.Count,.04f),new Vector2((b+1f)/line.Badges.Count,line.Conditional?.68f:.96f));
                        if(line.Conditional)
                        {
                            bool single=line.Badges.Count==1;
                            CardDesignComparison.Glyph(cell,"Symbol "+badge.Symbol,badge.Symbol,new Vector2(.02f,.08f),new Vector2(single?.20f:.33f,.94f));
                            var value=Label(cell,"Value",badge.Value,hybrid?24:31,CardDesignComparison.Tint(badge.Symbol),new Vector2(single?.23f:.36f,.03f),new Vector2(single?.56f:.98f,.98f),TextAnchor.MiddleLeft);
                            value.fontStyle=FontStyle.Bold;value.resizeTextMinSize=hybrid?16:22;
                            if(single&&!CardDesignComparison.Blind)Label(cell,"Caption",badge.Caption,hybrid?10:12,BattleUi.Pale,new Vector2(.58f,0),Vector2.one,TextAnchor.MiddleLeft);
                            else if(single&&badge.Limit!=null)Label(cell,"Limit",badge.Limit,12,BattleUi.Pale,new Vector2(.58f,0),Vector2.one,TextAnchor.MiddleLeft);
                            continue;
                        }
                        float iconRight=line.Badges.Count==1?.31f:.35f;
                        CardDesignComparison.Glyph(cell,"Symbol "+badge.Symbol,badge.Symbol,new Vector2(.04f,.25f),new Vector2(iconRight,.94f));
                        var number=Label(cell,"Value",badge.Value,hybrid?23:31,CardDesignComparison.Tint(badge.Symbol),new Vector2(iconRight+.02f,.28f),new Vector2(.98f,.99f),TextAnchor.MiddleLeft);
                        number.fontStyle=FontStyle.Bold;number.resizeTextMinSize=hybrid?14:20;
                        if(!CardDesignComparison.Blind&&!(hybrid&&lines.Count>1))Label(cell,"Caption",badge.Caption,hybrid?11:12,new Color(.75f,.78f,.76f),new Vector2(.03f,.015f),new Vector2(.99f,.29f),TextAnchor.MiddleCenter);
                        else if(badge.Limit!=null)Label(cell,"Limit",badge.Limit,12,BattleUi.Pale,new Vector2(.40f,.0f),new Vector2(.97f,.28f),TextAnchor.MiddleLeft);
                    }
                }
                summary.SetAsLastSibling();
            }
            ApplyLayout();
        }
        private Text Label(Transform parent,string name,string text,int size,Color color,Vector2 min,Vector2 max,TextAnchor align)
        {
            var t=BattleUi.Text(parent,name,text,view.description.font,size,color,min,max);t.alignment=align;
            t.resizeTextForBestFit=true;t.resizeTextMinSize=Mathf.Max(9,size-3);t.resizeTextMaxSize=size;t.lineSpacing=1;
            t.raycastTarget=false;BattleUi.Readable(t);typeSizes.Add(t,size);return t;
        }
        private void LateUpdate()
        {
            foreach(var entry in typeSizes)Fit(entry.Key,entry.Value);
            if(view!=null&&CardDesignComparison.Variant!=CardDesignVariant.Illustration)
                Fit(view.description,CardDesignComparison.Variant==CardDesignVariant.Hybrid?18:20);
        }
        private void Fit(Text label,int preferred)
        {
            if(label==null||!label.gameObject.activeInHierarchy)return;
            var r=label.rectTransform.rect;if(r.width<=0||r.height<=0)return;
            var area=new Vector3(r.width,r.height,label.pixelsPerUnit);label.resizeTextForBestFit=false;
            if(fittedAreas.TryGetValue(label,out var previous)&&previous==area)
            {label.fontSize=fittedSizes[label];return;}
            // Measure this actual font instead of assuming that point size equals line height.
            int chosen=preferred;
            for(;chosen>7;chosen--)
            {
                var settings=label.GetGenerationSettings(r.size);settings.resizeTextForBestFit=false;settings.fontSize=chosen;
                float height=label.cachedTextGeneratorForLayout.GetPreferredHeight(label.text,settings)/label.pixelsPerUnit;
                if(height<=r.height-1)break;
            }
            label.fontSize=chosen;fittedAreas[label]=area;fittedSizes[label]=chosen;
        }
        private static string CompactCondition(CardEffectSummaryRow row)
        {
            switch(row.BlindCondition)
            {
                case "=0":return "사용 전 방어 = 0";
                case ">0":return "사용 전 방어 > 0";
                case "→":return "다음 적턴 · 방어 흡수 시";
                case "×":return "방어 비례 · 소모 없음";
                default:return row.Condition;
            }
        }
        public void ApplyLayout()
        {
            if(view==null)return;
            bool hybrid=CardDesignComparison.Variant==CardDesignVariant.Hybrid;
            bool symbols=CardDesignComparison.Variant==CardDesignVariant.Symbols;
            if(symbols||hybrid)
            {
                view.artwork.gameObject.SetActive(hybrid&&!CardDesignComparison.Blind);
                if(hybrid)Slot(view.artwork.rectTransform,.065f,.66f,.935f,.825f);
                if(summary!=null)Slot(summary,.085f,hybrid?.35f:.495f,.915f,hybrid?.65f:.825f);
                Slot(view.description.rectTransform,.085f,hybrid?.09f:.125f,.915f,hybrid?.335f:.475f);
                view.description.fontSize=hybrid?18:20;view.description.resizeTextMaxSize=hybrid?18:20;view.description.resizeTextMinSize=hybrid?10:12;view.description.lineSpacing=1;
                view.description.alignment=TextAnchor.UpperLeft;
                if(hybrid)
                {
                    Slot(view.element.rectTransform,.085f,.02f,.53f,.08f);Slot(view.kind.rectTransform,.53f,.02f,.915f,.08f);
                    view.element.fontSize=view.kind.fontSize=14;
                }
                var bed=view.visual.Find("Obsidian text bed") as RectTransform;if(bed!=null)Slot(bed,.065f,.025f,.935f,.83f);
                // Keep the backing underneath all information, not above the symbol rows.
                if(bed!=null)bed.SetSiblingIndex(view.frame.transform.GetSiblingIndex()+1);
                var rule=view.visual.Find("Element rule") as RectTransform;if(rule!=null)Slot(rule,.075f,hybrid?.345f:.49f,.925f,hybrid?.35f:.495f);
            }
            if(CardDesignComparison.Blind)
            {
                view.title.gameObject.SetActive(false);view.description.gameObject.SetActive(false);view.artwork.gameObject.SetActive(false);
                view.element.gameObject.SetActive(false);view.kind.gameObject.SetActive(false);view.effect.gameObject.SetActive(false);
                var seal=view.visual.Find("Energy seal");if(seal!=null)seal.gameObject.SetActive(false);
            }
        }
        private void OnDestroy(){if(cropped!=null)Destroy(cropped);}
    }

    public sealed class CardDesignQaToolbar:MonoBehaviour
    {
        public BattleController Owner {get;set;}
        private RectTransform toolbar,gallery,legend;
        public IReadOnlyList<CardView> GalleryCards=>gallery!=null?gallery.GetComponentsInChildren<CardView>():Array.Empty<CardView>();
        private void Start()
        {
            if(Owner==null||!CardDesignComparison.Active){Destroy(this);return;}
            toolbar=BattleUi.Rect(Owner.View.Root,"Card design QA toolbar",new Vector2(.26f,.93f),new Vector2(.74f,.985f));
            Button(toolbar,"QA_A","A · 그림",0,.15f,()=>CardDesignComparison.SetVariant(CardDesignVariant.Illustration));
            Button(toolbar,"QA_B","B · 심볼",.16f,.31f,()=>CardDesignComparison.SetVariant(CardDesignVariant.Symbols));
            Button(toolbar,"QA_C","C · 혼합",.32f,.47f,()=>CardDesignComparison.SetVariant(CardDesignVariant.Hybrid));
            Button(toolbar,"QA_Gallery","6장 비교",.48f,.65f,ToggleGallery);
            Button(toolbar,"QA_Legend","범례",.66f,.78f,ToggleLegend);
            Button(toolbar,"QA_Blind","심볼만",.79f,.99f,()=>CardDesignComparison.SetVariant(CardDesignComparison.Variant,!CardDesignComparison.Blind));
        }
        private void Button(Transform parent,string name,string label,float a,float b,Action action)
        {BattleUi.Button(parent,name,label,GameFlow.Instance.Assets,new Vector2(a,0),new Vector2(b,1),action);}
        private void Update(){if(!CardDesignComparison.Active){if(toolbar!=null)Destroy(toolbar.gameObject);if(gallery!=null)Destroy(gallery.gameObject);if(legend!=null)Destroy(legend.gameObject);Destroy(this);}}
        public void ToggleGallery(){if(gallery!=null){Destroy(gallery.gameObject);gallery=null;}else BuildGallery();}
        public void RefreshGallery(){if(gallery!=null){DestroyImmediate(gallery.gameObject);gallery=null;BuildGallery();}}
        private void BuildGallery()
        {
            gallery=BattleUi.Panel(Owner.View.Root,"QA six-card gallery",new Vector2(.045f,.255f),new Vector2(.955f,.855f));
            BattleUi.Text(gallery,"QA gallery heading","QA · "+CardDesignComparison.Variant+"  /  동일 데이터 6장",GameFlow.Instance.Assets.font,24,BattleUi.Pale,new Vector2(.03f,.90f),new Vector2(.85f,.99f));
            BattleUi.Button(gallery,"QA_GalleryClose","닫기",GameFlow.Instance.Assets,new Vector2(.87f,.91f),new Vector2(.98f,.99f),ToggleGallery);
            var hand=BattleUi.Rect(gallery,"Hand",new Vector2(0,.05f),new Vector2(1,.86f));
            var art=Resources.Load<BattlePresentationAssets>("BrodunBattle");
            for(int i=0;i<CardEffectSummary.PreviewIds.Count;i++)
            {
                string id=CardEffectSummary.PreviewIds[i];var card=Instantiate(art.cardPrefab,hand,false);card.name="QA_Card_"+id;
                card.Bind(Owner.Context.Runtime.Data.Card(id),id,art,Owner.Context.Runtime.Data,_=>{});
                var r=(RectTransform)card.transform;r.sizeDelta=new Vector2(HandPresentation.CardWidth,HandPresentation.CardHeight);r.anchoredPosition=new Vector2((i-2.5f)*256,0);
                card.SetHandExposedWidth(HandPresentation.CardWidth);
            }
            toolbar.SetAsLastSibling();
        }
        public void ToggleLegend()
        {
            if(legend!=null){Destroy(legend.gameObject);legend=null;return;}
            legend=BattleUi.Panel(Owner.View.Root,"QA effect legend",new Vector2(.30f,.42f),new Vector2(.70f,.86f));
            var names=new[]{"공격 피해","방어도 획득","카드 드로우","방어 흡수 후 반격","방어도 비례 추가 피해","방어도 소모","방어도 복원","조건 / 분기"};
            for(int i=0;i<names.Length;i++)
            {
                float top=.95f-i*.11f;
                CardDesignComparison.Glyph(legend,"Legend "+i,(CardEffectSymbol)i,new Vector2(.06f,top-.09f),new Vector2(.14f,top));
                BattleUi.Text(legend,"Legend label "+i,names[i],GameFlow.Instance.Assets.font,21,BattleUi.Pale,new Vector2(.20f,top-.09f),new Vector2(.94f,top));
            }
        }
        private void OnDestroy(){if(toolbar!=null)Destroy(toolbar.gameObject);if(gallery!=null)Destroy(gallery.gameObject);if(legend!=null)Destroy(legend.gameObject);}
    }
}
#endif

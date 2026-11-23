using System;
using System.Collections.Generic;
using System.Linq;
namespace Brodun.Presentation
{
    // Read-only presentation metadata. It never participates in combat resolution.
    public enum CardEffectSymbol { Damage, Block, Draw, Counter, BlockScale, BlockSpend, BlockRestore, Conditional }
    public sealed class CardEffectBadge
    {
        public CardEffectSymbol Symbol { get; }
        public string Value { get; }
        public string Caption { get; }
        public string Limit { get; }
        public CardEffectBadge(CardEffectSymbol symbol,string value,string caption,string limit=null){Symbol=symbol;Value=value;Caption=caption;Limit=limit;}
    }
    public sealed class CardEffectSummaryRow
    {
        public string Condition { get; }
        public string BlindCondition { get; }
        public bool Conditional=>Condition!=null;
        public IReadOnlyList<CardEffectBadge> Badges { get; }
        public CardEffectSummaryRow(string condition,string blindCondition,params CardEffectBadge[] badges)
        {Condition=condition;BlindCondition=blindCondition;Badges=Array.AsReadOnly(badges);}
    }
    public static class CardEffectSummary
    {
        public static readonly IReadOnlyList<string> PreviewIds=Array.AsReadOnly(new[]{"B-W01","B-W02","W02","W05","W12","W08"});
        public static bool Supports(string id)=>PreviewIds.Contains(id);
        public static IReadOnlyList<CardEffectSummaryRow> For(CardDefinition card)
        {
            if(!Supports(card.Id))return Array.Empty<CardEffectSummaryRow>();
            CardEffect Find(EffectType type,ConditionType condition=ConditionType.None)=>card.Effects.Single(e=>e.Type==type&&e.Condition==condition);
            CardEffectBadge Fixed(EffectType type,CardEffectSymbol symbol,string caption,ConditionType condition=ConditionType.None)
            {var e=Find(type,condition);return new CardEffectBadge(symbol,(e.Amount*e.Hits).ToString(),caption);}
            switch(card.Id)
            {
                case "B-W01":return new[]{new CardEffectSummaryRow(null,null,Fixed(EffectType.Damage,CardEffectSymbol.Damage,"피해"))};
                case "B-W02":return new[]{new CardEffectSummaryRow(null,null,Fixed(EffectType.Block,CardEffectSymbol.Block,"방어도"))};
                case "W02":return new[]{new CardEffectSummaryRow(null,null,Fixed(EffectType.Block,CardEffectSymbol.Block,"방어도"),Fixed(EffectType.Draw,CardEffectSymbol.Draw,"드로우"))};
                case "W05":return new[]{
                    new CardEffectSummaryRow("사용 전 방어도 0","=0",Fixed(EffectType.Block,CardEffectSymbol.Block,"방어도",ConditionType.NoBlockBefore)),
                    new CardEffectSummaryRow("사용 전 방어도 있음",">0",Fixed(EffectType.Block,CardEffectSymbol.Block,"방어도",ConditionType.BlockBefore),Fixed(EffectType.Draw,CardEffectSymbol.Draw,"드로우",ConditionType.BlockBefore))};
                case "W12":
                    var counter=Find(EffectType.Counter);
                    return new[]{new CardEffectSummaryRow(null,null,Fixed(EffectType.Block,CardEffectSymbol.Block,"방어도")),
                        new CardEffectSummaryRow("다음 적 턴 · 방어로 막으면","→",new CardEffectBadge(CardEffectSymbol.Counter,counter.Amount.ToString(),"반격 · 적당 "+counter.Rule.AttackerLimit+"회"))};
                case "W08":
                    var hit=Find(EffectType.Damage);var percentage=hit.Rule.Numerator*100/hit.Rule.Denominator;
                    return new[]{new CardEffectSummaryRow(null,null,new CardEffectBadge(CardEffectSymbol.Damage,hit.Amount.ToString(),"기본 피해")),
                        new CardEffectSummaryRow("현재 방어도 비례 · 소모 없음","×",new CardEffectBadge(CardEffectSymbol.BlockScale,"+"+percentage+"%","추가 피해 · 최대 +"+hit.Rule.Maximum,"≤+"+hit.Rule.Maximum))};
                default:throw new InvalidOperationException("Missing preview summary "+card.Id);
            }
        }
    }
}

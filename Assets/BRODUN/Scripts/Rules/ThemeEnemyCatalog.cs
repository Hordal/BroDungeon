using System;
using System.Collections.Generic;
namespace Brodun
{
    public sealed class ThemeEnemyEntry
    {
        public string Id { get; }
        public string ThemeId { get; }
        public string Name { get; }
        public EnemyType Rank { get; }
        internal ThemeEnemyEntry(string id, string theme, string name, EnemyType rank)
        { Id = id; ThemeId = theme; Name = name; Rank = rank; }
    }
    // IDs and names from DeckbuilderMonsterCombat/Data/monsters.json, not new monster designs.
    public static class ThemeEnemyCatalog
    {
        public static readonly IReadOnlyList<ThemeEnemyEntry> Entries = Array.AsReadOnly(new[]
        {
            new ThemeEnemyEntry("fire_ember_mite", "fire", "불씨진드기", EnemyType.Normal),
            new ThemeEnemyEntry("fire_ash_hound", "fire", "재갈기 사냥개", EnemyType.Normal),
            new ThemeEnemyEntry("fire_kiln_imp", "fire", "가마 도깨비", EnemyType.Normal),
            new ThemeEnemyEntry("fire_cinder_moth", "fire", "불티나방", EnemyType.Normal),
            new ThemeEnemyEntry("fire_coal_porter", "fire", "석탄 짐꾼", EnemyType.Normal),
            new ThemeEnemyEntry("fire_chain_penitent", "fire", "사슬 참회자", EnemyType.Normal),
            new ThemeEnemyEntry("fire_slag_slime", "fire", "용재 점액체", EnemyType.Normal),
            new ThemeEnemyEntry("fire_bellows_bat", "fire", "풀무박쥐", EnemyType.Normal),
            new ThemeEnemyEntry("fire_brand_guard", "fire", "낙인 보초", EnemyType.Normal),
            new ThemeEnemyEntry("fire_chimney_wraith", "fire", "굴뚝 망령", EnemyType.Normal),
            new ThemeEnemyEntry("fire_glass_scorpion", "fire", "유리전갈", EnemyType.Normal),
            new ThemeEnemyEntry("fire_crucible_monk", "fire", "도가니 수도사", EnemyType.Normal),
            new ThemeEnemyEntry("fire_salamander", "fire", "화맥도롱뇽", EnemyType.Normal),
            new ThemeEnemyEntry("fire_blade_dancer", "fire", "잿칼 무희", EnemyType.Normal),
            new ThemeEnemyEntry("fire_coal_heart", "fire", "탄심 인형", EnemyType.Normal),
            new ThemeEnemyEntry("fire_furnace_warden", "fire", "용광로 간수", EnemyType.Elite),
            new ThemeEnemyEntry("fire_ash_bride", "fire", "잿빛 신부", EnemyType.Elite),
            new ThemeEnemyEntry("fire_anvil_twins", "fire", "쌍두 모루귀", EnemyType.Elite),
            new ThemeEnemyEntry("fire_crowned_dragon", "fire", "왕관을 삼킨 용 아그라스", EnemyType.Boss),
            new ThemeEnemyEntry("frost_rime_tick", "frost", "서리진드기", EnemyType.Normal),
            new ThemeEnemyEntry("frost_snow_stitch", "frost", "눈실 인형", EnemyType.Normal),
            new ThemeEnemyEntry("frost_bell_fox", "frost", "방울꼬리 여우", EnemyType.Normal),
            new ThemeEnemyEntry("frost_icicle_raven", "frost", "고드름 까마귀", EnemyType.Normal),
            new ThemeEnemyEntry("frost_frozen_bearer", "frost", "얼음 관 운반자", EnemyType.Normal),
            new ThemeEnemyEntry("frost_needle_seamstress", "frost", "빙침 재봉사", EnemyType.Normal),
            new ThemeEnemyEntry("frost_frost_clapper", "frost", "서리 종추", EnemyType.Normal),
            new ThemeEnemyEntry("frost_mirror_elk", "frost", "거울뿔 사슴", EnemyType.Normal),
            new ThemeEnemyEntry("frost_snow_mourner", "frost", "눈먼 조문객", EnemyType.Normal),
            new ThemeEnemyEntry("frost_rime_crab", "frost", "서리 등껍질게", EnemyType.Normal),
            new ThemeEnemyEntry("frost_ice_lantern", "frost", "빙등지기", EnemyType.Normal),
            new ThemeEnemyEntry("frost_frozen_swimmer", "frost", "얼어붙은 잠수자", EnemyType.Normal),
            new ThemeEnemyEntry("frost_silk_widow", "frost", "눈실 과부거미", EnemyType.Normal),
            new ThemeEnemyEntry("frost_frost_tuning", "frost", "빙음 악사", EnemyType.Normal),
            new ThemeEnemyEntry("frost_coffin_knight", "frost", "관뚜껑 기사", EnemyType.Normal),
            new ThemeEnemyEntry("frost_bell_pallbearer", "frost", "빙종 운구자", EnemyType.Elite),
            new ThemeEnemyEntry("frost_mirror_marshal", "frost", "거울 근위장", EnemyType.Elite),
            new ThemeEnemyEntry("frost_frost_librarian", "frost", "서리 사서", EnemyType.Elite),
            new ThemeEnemyEntry("frost_funeral_queen", "frost", "영면의 여왕 이스벨", EnemyType.Boss),
            new ThemeEnemyEntry("void_ink_crawler", "void", "먹물 순례충", EnemyType.Normal),
            new ThemeEnemyEntry("void_candle_acolyte", "void", "촛농 시종", EnemyType.Normal),
            new ThemeEnemyEntry("void_hollow_gargoyle", "void", "공동 가고일", EnemyType.Normal),
            new ThemeEnemyEntry("void_star_moth", "void", "별눈나방", EnemyType.Normal),
            new ThemeEnemyEntry("void_mask_worm", "void", "가면벌레", EnemyType.Normal),
            new ThemeEnemyEntry("void_silent_choir", "void", "무음 성가대원", EnemyType.Normal),
            new ThemeEnemyEntry("void_stained_spider", "void", "색유리 거미", EnemyType.Normal),
            new ThemeEnemyEntry("void_orbit_urchin", "void", "궤도 성게", EnemyType.Normal),
            new ThemeEnemyEntry("void_page_eater", "void", "성서 포식자", EnemyType.Normal),
            new ThemeEnemyEntry("void_shadow_censer", "void", "그림자 향로", EnemyType.Normal),
            new ThemeEnemyEntry("void_eclipse_sentinel", "void", "식의 보초", EnemyType.Normal),
            new ThemeEnemyEntry("void_confession_doll", "void", "고해 인형", EnemyType.Normal),
            new ThemeEnemyEntry("void_clock_deacon", "void", "역행 부제", EnemyType.Normal),
            new ThemeEnemyEntry("void_star_gravedigger", "void", "별무덤지기", EnemyType.Normal),
            new ThemeEnemyEntry("void_nightsail_ray", "void", "밤돛 가오리", EnemyType.Normal),
            new ThemeEnemyEntry("void_nameless_confessor", "void", "이름 없는 고해사", EnemyType.Elite),
            new ThemeEnemyEntry("void_orbit_angel", "void", "궤도의 천사", EnemyType.Elite),
            new ThemeEnemyEntry("void_reverse_astrologer", "void", "역행 점성술사", EnemyType.Elite),
            new ThemeEnemyEntry("void_eclipse_prellate", "void", "별을 삼킨 교황 녹틸", EnemyType.Boss),
        });
    }
}

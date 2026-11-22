using UnityEngine;

namespace Brodun.Presentation
{
    // Presentation only. Area identities and progression remain owned by the run.
    public static class BattleBackgrounds
    {
        public const int ThemeCount = 4;
        public const string ResourceRoot = "BRODUN/Backgrounds/";
        private static readonly string[] Keys =
        {
            "01_ruins", "02_waterway", "03_foundry", "04_storm"
        };
        private static readonly string[] Names =
        {
            "잊혀진 회랑", "침잠의 수맥", "소멸의 화로", "폭풍의 첨탑 (시안)"
        };
        public static string ResourcePath(string themeId,bool boss)
        {
            var theme=DungeonThemes.Find(themeId);
            return theme==null?null:ResourceRoot+theme.BackgroundKey+(boss?"_boss":"_normal");
        }
        public static Sprite Resolve(string themeId,bool boss,Sprite fallback)
        {
            string path=ResourcePath(themeId,boss);
            if(path==null)return fallback;
            return Resources.Load<Sprite>(path)??fallback;
        }

        public static string ResourcePath(int area, bool boss)
        {
            if (area < 1 || area > ThemeCount) return null;
            return ResourceRoot + Keys[area - 1] + (boss ? "_boss" : "_normal");
        }

        public static string ThemeName(int area)
        {
            return area >= 1 && area <= ThemeCount ? Names[area - 1] : "구역 " + area;
        }

        public static Sprite Resolve(int area, bool boss, Sprite fallback)
        {
            string path = ResourcePath(area, boss);
            if (path == null) return fallback;
            Sprite sprite = Resources.Load<Sprite>(path);
            return sprite != null ? sprite : fallback;
        }
    }
}

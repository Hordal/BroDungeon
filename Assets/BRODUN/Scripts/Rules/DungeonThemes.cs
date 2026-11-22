using System;
using System.Collections.Generic;
using System.Linq;

namespace Brodun
{
    public sealed class DungeonTheme
    {
        public string Id { get; }
        public string Name { get; }
        public string BackgroundKey { get; }
        public IReadOnlyList<string> EnemyIds { get; }
        internal DungeonTheme(string id, string name, string background, IEnumerable<string> enemies)
        {
            Id = id; Name = name; BackgroundKey = background;
            EnemyIds = Array.AsReadOnly(enemies.ToArray());
        }
    }

    // Theme identity is independent of the run's stage/area number.
    public static class DungeonThemes
    {
        public const string DefaultId = "ruins";
        public static readonly IReadOnlyList<DungeonTheme> All = Array.AsReadOnly(new[]
        {
            new DungeonTheme(DefaultId, "잊혀진 회랑", "01_ruins",
                new[] { "ruin_gargoyle", "moss_slime", "gale_warden", "fallen_colossus" }),
            Theme("fire", "소멸의 화로", "03_foundry"),
            Theme("frost", "서리의 수맥", "02_waterway"),
            Theme("void", "공허의 성좌", "04_storm")
        });

        private static DungeonTheme Theme(string id, string name, string background)
        {
            return new DungeonTheme(id, name, background,
                ThemeEnemyCatalog.Entries.Where(e => e.ThemeId == id).Select(e => e.Id));
        }

        public static DungeonTheme Find(string id) => All.FirstOrDefault(t => t.Id == id);

        public static bool IsAvailable(GameDefinitions data, string id)
        {
            var theme = Find(id);
            return theme != null && Enum.GetValues(typeof(EnemyType)).Cast<EnemyType>().All(rank =>
                data.Enemies.Any(e => theme.EnemyIds.Contains(e.Id) && e.Type == rank));
        }

        public static GameDefinitions WithThemeEnemies(GameDefinitions data)
        {
            var enemies = data.Enemies.ToList();
            foreach (var entry in ThemeEnemyCatalog.Entries)
            {
                if (enemies.Any(e => e.Id == entry.Id)) continue;
                // Start all themes at stage-one tuning. Source role descriptions are not executable skills.
                var template = data.Enemies.First(e => e.Area == 1 && e.Type == entry.Rank && !e.IsLegacy);
                enemies.Add(new EnemyDefinition(entry.Id, entry.Name, entry.Rank, 1,
                    template.BaseHP, 0, entry.Id, template.Pattern));
            }
            return new GameDefinitions(data.Cards, data.Characters, enemies, data.Config, data.Reward, data.Elements);
        }
    }
}

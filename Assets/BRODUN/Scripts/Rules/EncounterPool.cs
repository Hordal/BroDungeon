using System;
using System.Collections.Generic;
using System.Linq;
namespace Brodun
{
    public static class EncounterPool
    {
        public static IReadOnlyList<EnemyDefinition> ForArea(GameDefinitions data,int area,EnemyType rank)=>
            Array.AsReadOnly(data.Enemies.Where(e=>e.Area==area&&e.Type==rank&&!ThemeEnemyCatalog.Contains(e.Id)).OrderBy(e=>e.Id,StringComparer.Ordinal).ToArray());
        public static uint NodeSeed(uint seed,string id,uint salt)
        {
            // Stable FNV-1a, not string.GetHashCode (which varies by runtime/process).
            uint hash=2166136261u;foreach(char c in id)hash=unchecked((hash^c)*16777619u);
            return unchecked(seed+hash+salt);
        }
        public static IReadOnlyList<EnemyDefinition> ForTheme(GameDefinitions data,string themeId,EnemyType rank)
        {
            var theme=DungeonThemes.Find(themeId);
            if(theme==null)return Array.Empty<EnemyDefinition>();
            return Array.AsReadOnly(data.Enemies.Where(e=>e.Type==rank&&theme.EnemyIds.Contains(e.Id))
                .OrderBy(e=>e.Id,StringComparer.Ordinal).ToArray());
        }

        internal static EnemyDefinition OpeningCompanion(GameDefinitions data,RunState run,RoomNode room)
        {
            if(run.GeneratedDungeon?.IsOpeningBattle(room,2)!=true)return null;
            var slime=data.Enemy("moss_slime");
            var companion=new EnemyDefinition(slime.Id,slime.Name,slime.Type,slime.Area,27,0,slime.AssetId,
                new[]{new IntentData(IntentType.Buff,2),new IntentData(IntentType.Attack,11),new IntentData(IntentType.Attack,6)});
            if(!run.IsThemedRun||run.ThemeId==DungeonThemes.DefaultId)return companion;
            var primary=Select(data,run,room);
            var pool=ForTheme(data,run.ThemeId,EnemyType.Normal).Where(e=>e.Id!=primary.Id).ToArray();
            return pool.Length==0?null:OpeningTuning(Pick(pool,run,room,0x270B11u),companion);
        }

        public static EnemyDefinition Select(GameDefinitions data,RunState run,RoomNode room)
        {
            if(!run.IsThemedRun||run.ThemeId==DungeonThemes.DefaultId&&run.CurrentArea==1)
                return SelectBase(data,run,room);
            var rank=room.Type==RoomType.Boss?EnemyType.Boss:room.Encounter;
            var pool=ForTheme(data,run.ThemeId,rank);
            if(pool.Count==0)throw new InvalidOperationException("Missing encounter pool: "+run.ThemeId+" / "+rank);
            var enemy=Pick(pool,run,room,0x719B11u);
            return run.GeneratedDungeon?.IsOpeningBattle(room,2)==true
                ?OpeningTuning(enemy,SelectBase(data,run,room)):enemy;
        }

        private static EnemyDefinition Pick(IReadOnlyList<EnemyDefinition> pool,RunState run,RoomNode room,uint salt)
        {
            var rng=new SeededRandom(NodeSeed(run.AreaSeed,room.Id,salt));
            return pool[(int)(rng.Next()*pool.Count)];
        }

        private static EnemyDefinition OpeningTuning(EnemyDefinition enemy,EnemyDefinition tuning) =>
            new EnemyDefinition(enemy.Id,enemy.Name,enemy.Type,enemy.Area,tuning.BaseHP,tuning.AreaHPStep,enemy.AssetId,tuning.Pattern);

        private static EnemyDefinition SelectBase(GameDefinitions data,RunState run,RoomNode room)
        {
            if(run.GeneratedDungeon?.IsOpeningBattle(room,2)==true)
            {
                // Immediate threat. The left enemy attacks while the right enemy prepares.
                var gargoyle=data.Enemy("ruin_gargoyle");
                return new EnemyDefinition(gargoyle.Id,gargoyle.Name,gargoyle.Type,gargoyle.Area,21,0,gargoyle.AssetId,
                    new[]{new IntentData(IntentType.Attack,8),new IntentData(IntentType.Block,4),new IntentData(IntentType.Attack,10)});
            }
            var rank=room.Type==RoomType.Boss?EnemyType.Boss:room.Encounter;
            var pool=run.IsThemedRun?ForTheme(data,DungeonThemes.DefaultId,rank):ForArea(data,run.CurrentArea,rank);
            if(pool.Count==0)throw new InvalidOperationException("Missing encounter pool: area "+run.CurrentArea+" / "+rank);
            // Keep the tutorial/capture first fight and old web-v1 fixtures identical.
            if(run.GeneratedDungeon==null || room.Layer==1)
            {
                bool legacy=rank==EnemyType.Normal&&run.GeneratedDungeon==null&&room.Id=="r8";
                return pool.Single(e=>e.IsLegacy==legacy);
            }
            var rng=new SeededRandom(NodeSeed(run.Seed,room.Id,0x719B11u));
            return pool[(int)(rng.Next()*pool.Count)];
        }
    }
}

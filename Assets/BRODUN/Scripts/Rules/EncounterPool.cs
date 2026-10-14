using System;
using System.Collections.Generic;
using System.Linq;
namespace Brodun
{
    public static class EncounterPool
    {
        public static IReadOnlyList<EnemyDefinition> ForArea(GameDefinitions data,int area,EnemyType rank)=>
            Array.AsReadOnly(data.Enemies.Where(e=>e.Area==area&&e.Type==rank).OrderBy(e=>e.Id,StringComparer.Ordinal).ToArray());
        public static uint NodeSeed(uint seed,string id,uint salt)
        {
            // Stable FNV-1a, not string.GetHashCode (which varies by runtime/process).
            uint hash=2166136261u;foreach(char c in id)hash=unchecked((hash^c)*16777619u);
            return unchecked(seed+hash+salt);
        }
        public static EnemyDefinition Select(GameDefinitions data,RunState run,RoomNode room)
        {
            var rank=room.Type==RoomType.Boss?EnemyType.Boss:room.Encounter;
            var pool=ForArea(data,run.CurrentArea,rank);
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

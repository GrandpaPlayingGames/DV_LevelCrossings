using Newtonsoft.Json;
using System.IO;
using UnityEngine;

namespace DV_LevelCrossings
{
    public static class CrossingPersistence
    {
        private static string SavePath =>
            Path.Combine(Main.Mod.Path, "Crossings.json");


#if DVLC_AUTHORING
        public static void Save(CrossingDatabase db)
        {
            string json = JsonConvert.SerializeObject(db, Formatting.Indented);
            File.WriteAllText(SavePath, json);

#if DVLC_DEBUG
            Main.Log("[Crossings] Saved to: " + SavePath, true);
#endif
        }
#endif
 
        public static CrossingDatabase Load()
        {
            if (!File.Exists(SavePath))
                return new CrossingDatabase();

            try
            {
                string json = File.ReadAllText(SavePath);
                var db = JsonConvert.DeserializeObject<CrossingDatabase>(json);
                return db ?? new CrossingDatabase();
            }
            catch (System.Exception ex)
            {
#if DVLC_DEBUG
                Main.Log("[Crossings] ERROR loading DB: " + ex.Message, true);
#endif
                return new CrossingDatabase();
            }
        }
    }
}
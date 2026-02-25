using System;
using System.Collections.Generic;

namespace DV_LevelCrossings
{
    
    [Serializable]
    public class CrossingDatabase
    {
        public int version = 1;
        public List<CrossingData> crossings = new List<CrossingData>();
    }

    public class CrossingData
    {
        public string id;
        
        public List<string> barrierPaths = new List<string>();
        
        public List<BarrierData> barriers = new List<BarrierData>();

        public List<TriggerData> triggers = new List<TriggerData>();
    }

    public class TriggerData
    {
        public string group;          // "A" or "B"
        public float posX;
        public float posY;
        public float posZ;
        public float rotY;
    }

    [Serializable]
    public class BarrierData
    {
        public string path;
        public float posX;
        public float posY;
        public float posZ;
    }
}
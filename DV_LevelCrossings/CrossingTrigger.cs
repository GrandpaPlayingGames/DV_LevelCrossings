
using UnityEngine;

namespace DV_LevelCrossings
{
    public class CrossingTrigger : MonoBehaviour
    {
        public enum TriggerGroup
        {
            A,
            B
        }

        public CrossingController controller;
        public TriggerGroup group;

        // ===== NEW: occupancy tracking =====
        private int occupantCount = 0;
        public bool IsOccupied => occupantCount > 0;

        void Start()
        {
            if (controller != null)
                controller.RegisterTrigger(this);

            var col = GetComponent<Collider>();
            if (col is BoxCollider box)
            {
                Main.Log($"[Trigger DEBUG] {group} size={box.size} center={box.center}");
                Main.Log($"[Trigger DEBUG] lossyScale={transform.lossyScale}");
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (controller == null || other == null) return;
            if (!IsTrainCarCollider(other)) return;
            Main.Log($"[Trigger ENTER] {group} by {other.name} @ {other.transform.position}");

            occupantCount++;  

            controller.NotifyTrigger(group);
        }

        private void OnTriggerStay(Collider other)
        {
            if (controller == null || other == null) return;
            if (!IsTrainCarCollider(other)) return;

            controller.NotifyTriggerStay(group);
        }

        private void OnTriggerExit(Collider other)  // NEW
        {
            if (other == null) return;
            if (!IsTrainCarCollider(other)) return;

            occupantCount = Mathf.Max(0, occupantCount - 1);
        }

        void OnDestroy()
        {
            if (controller != null)
                controller.UnregisterTrigger(this);
        }

        private static bool IsTrainCarCollider(Collider other)
        {
            Transform t = other.transform;
            while (t != null)
            {
                var comps = t.GetComponents<Component>();
                for (int i = 0; i < comps.Length; i++)
                {
                    var c = comps[i];
                    if (c == null) continue;
                    if (c.GetType().Name == "TrainCar")
                        return true;
                }
                t = t.parent;
            }
            return false;
        }
    }
}



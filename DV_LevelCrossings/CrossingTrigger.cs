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

        private void OnCollisionEnter(Collision c)
        {
            Main.Log($"[DVLC ERROR] SOLID COLLISION on trigger {name} with {c.collider.name}. isTrigger={GetComponent<Collider>().isTrigger}");
        }

        private void OnTriggerEnter(Collider other)
        {
            if (controller == null || other == null) return;
            if (!IsTrainCarCollider(other)) return;

            TrainCar car = other.GetComponentInParent<TrainCar>();

            string carName = car != null ? car.name : "UNKNOWN_CAR";
            string carId = car != null ? car.ID : "NO_ID";

            Main.Log($"[Trigger ENTER] {group} by {carName}({carId})/{other.name} @ {other.transform.position}");

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
            if (other == null) return false;

            // Check directly
            if (other.GetComponentInParent<TrainCarColliders>() != null)
                return true;

            return false;
        }
    }
}



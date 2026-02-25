using System;
using UnityEngine;
using UnityModManagerNet;

namespace DV_LevelCrossings
{
    [Serializable]
    public class Settings : UnityModManager.ModSettings, IDrawable
    {
        // =========================================================
        // GENERAL
        // =========================================================

        public bool enabled = true;

#if DVLC_DEBUG
        public bool debugLogging = true;
#endif
        public bool barriersAlwaysUp = false;

        public float normalBarrierSpeed = 45f;
        public float slowBarrierSpeed = 25f;
        public bool useSlowBarrierSpeed = false;

        // =========================================================
        // SAVE
        // =========================================================

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }

        public void OnChange()
        {
            // Intentionally empty
        }

        public void Draw(UnityModManager.ModEntry modEntry)
        {
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.gray }
            };

            GUILayout.Space(6);

            // =========================================================
            // GENERAL
            // =========================================================

            BeginGroup("General", titleStyle);

            enabled = GUILayout.Toggle(enabled, "Enabled");

#if DVLC_DEBUG
            debugLogging = GUILayout.Toggle(debugLogging, "Debug Logging");
#endif

            EndGroup();

            // =========================================================
            // CROSSING BEHAVIOUR
            // =========================================================

            BeginGroup("Crossing Behaviour", titleStyle);

            barriersAlwaysUp = GUILayout.Toggle(
                barriersAlwaysUp,
                "Keep Barriers Permanently Raised (Ignore Triggers)"
            );

            EndGroup();

            // =========================================================
            // BARRIER SPEED
            // =========================================================

            BeginGroup("Barrier Speed", titleStyle);

            bool previousGUIState = GUI.enabled;
            GUI.enabled = !barriersAlwaysUp;

            useSlowBarrierSpeed = GUILayout.Toggle(
                useSlowBarrierSpeed,
                $"Use Slow Barrier Speed ({slowBarrierSpeed:F0}°/sec instead of {normalBarrierSpeed:F0}°/sec)"
            );

            GUI.enabled = previousGUIState;

            if (barriersAlwaysUp)
            {
                GUILayout.Space(4);
                GUILayout.Label(
                    "Speed options disabled while barriers are permanently raised.",
                    new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Italic }
                );
            }

            EndGroup();
        }

        // =========================================================
        // UI HELPERS
        // =========================================================

        private void BeginGroup(string title, GUIStyle style)
        {
            GUI.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
            GUILayout.BeginVertical("box");
            GUI.backgroundColor = Color.white;

            GUILayout.Label(title, style);
            GUILayout.Space(6);
        }

        private void EndGroup()
        {
            GUILayout.EndVertical();
            GUILayout.Space(10);
        }
    }
}
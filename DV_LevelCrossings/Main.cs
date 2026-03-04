
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using UnityModManagerNet;

namespace DV_LevelCrossings
{
    public static class Main
    {
        public static UnityModManager.ModEntry Mod;
        public static Settings Settings;
        public static Harmony Harmony;

        private static GameObject _runtimeGO;
        private static bool _loaded;

        private static CharacterControllerProvider _cachedProvider;
        private static bool _gameLoadedFired;
        private static bool _sessionInitialized;

        public static CrossingDatabase _loadedDatabase;
        private static HashSet<string> _instantiatedCrossings = new HashSet<string>();
        private static Dictionary<string, List<Transform>> _pathCache;
        private static List<Transform> _rootTransforms = null;

        private static Vector3 _lastCheckPos;
        private static Vector3 _lastMove;

        private const float MovementThreshold = 800f; // tune this
        private const float CrossingAttachRadius = 1200f;
#if DVLC_AUTHORING
        private static CrossingTriggerAuthoring _cachedAuthoring;
#endif

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            Mod = modEntry;

            Settings = UnityModManager.ModSettings.Load<Settings>(modEntry);

            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;
            modEntry.OnUpdate = Update;

            Harmony = new Harmony(modEntry.Info.Id);
            Harmony.PatchAll();

            _loaded = true;

            Log("Mod loaded successfully.");
            return true;
        }

        private static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            Settings.Draw(modEntry);
        }

        private static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            Settings.Save(modEntry);
        }

        private static void Update(UnityModManager.ModEntry modEntry, float dt)
        {
            if (!_loaded || !Settings.enabled)
                return;

            if (_runtimeGO == null)
                InitializeRuntime();

            CheckGameLoadedOnce();

            if (!_sessionInitialized || _cachedProvider == null)
                return;

            var move = WorldMover.currentMove;
            if (move != _lastMove)
            {
                Vector3 delta = move - _lastMove;
                Log($"[Crossings] WOS detected. delta={delta}");

                if (RootCacheNeedsRefresh())
                {
                    CacheRootTransforms();
                }

                foreach (var ctrl in CrossingController.AllControllers)
                {
                    if (ctrl != null)
                        ctrl.transform.position += delta;
                }
            
                _lastCheckPos += delta;

                _lastMove = move;
            }


            Vector3 playerPos = _cachedProvider.transform.position;

            if (Vector3.Distance(playerPos, _lastCheckPos) > MovementThreshold)
            {
                ReconcileCrossingsNear(playerPos);
                _lastCheckPos = playerPos;
            }        

#if DVLC_AUTHORING
            if (Input.GetKeyDown(KeyCode.F10))
            {
                SaveCrossings();
                Log("Crossings saved.");
            }

            //commented for safety, uncomment if ever need to use

            //if (Input.GetKeyDown(KeyCode.F11))
            //{
            //    CrossingControllerAuthoringExtensions.RaiseAllTriggerHeights(-0.4f);
            //}
#endif
        }

        private static void InitializeRuntime()
        {
            if (_runtimeGO != null)
                return;

            _runtimeGO = new GameObject("DV_LevelCrossings_Runtime");

#if DVLC_AUTHORING
            _runtimeGO.AddComponent<CrossingTriggerAuthoring>();
#endif
        }

        private static void CheckGameLoadedOnce()
        {
            if (_cachedProvider == null)
            {
                _cachedProvider =
                    Object.FindObjectOfType<CharacterControllerProvider>();

                if (_cachedProvider == null)
                    return;
            }

            if (!_cachedProvider.IsGameLoaded)
            {
                _gameLoadedFired = false;
                _sessionInitialized = false;
                return;
            }

            if (_gameLoadedFired)
                return;

            _gameLoadedFired = true;
            OnGameLoaded();
        }

        private static void OnGameLoaded()
        {
            if (_sessionInitialized)
                return;

            LoadCrossings();

            CacheRootTransforms();

            _lastCheckPos = _cachedProvider.transform.position;
            ReconcileCrossingsNear(_lastCheckPos);     

            _lastMove = WorldMover.currentMove;

            _sessionInitialized = true;
        }


        private static void LoadCrossings()
        {
            Log("LoadCrossings called.");

            _loadedDatabase = CrossingPersistence.Load();

            if (_loadedDatabase == null)
            {
                _loadedDatabase = new CrossingDatabase();
                Log("[Crossings] No existing database. Created new.");
            }

            Log($"[Crossings] Loaded {_loadedDatabase.crossings.Count} crossings.");
        }


        private static void ReconcileCrossingsNear(Vector3 playerSessionPos)
        {
            Log("[Crossings] Reconcile triggered.");

            if (_loadedDatabase == null || _loadedDatabase.crossings == null)
                return;

            Vector3 playerCanonical = playerSessionPos - WorldMover.currentMove;

            foreach (var crossing in _loadedDatabase.crossings)
            {
                if (crossing == null || crossing.triggers == null || crossing.triggers.Count == 0)
                    continue;

                var t = crossing.triggers[0];
                Vector3 crossingCanonical = new Vector3(t.posX, t.posY, t.posZ);

                float dist = Vector3.Distance(playerCanonical, crossingCanonical);

                if (dist <= CrossingAttachRadius)
                {
                    EnsureCrossingRuntime(crossing);
                }
            }
        }


        private static void EnsureCrossingRuntime(CrossingData crossing)
        {         
            if (crossing == null) return;
#if DVLC_AUTHORING
           
            if (_cachedAuthoring == null)
                _cachedAuthoring = UnityEngine.Object.FindObjectOfType<CrossingTriggerAuthoring>();

            if (_cachedAuthoring != null && _cachedAuthoring.activeGroup != null)
            {
                _cachedAuthoring.ExitEditMode();
            }

#endif

            CrossingController controller = null;
            foreach (var ctrl in CrossingController.AllControllers)
            {
                if (ctrl != null && ctrl.CrossingID == crossing.id)
                {
                    controller = ctrl;
                    break;
                }
            }

            /*

            if (controller == null)
            {
                Log($"[Crossings] Building runtime crossing {crossing.id}");
                GameObject go = new GameObject("LC_Group_" + crossing.id);
                controller = go.AddComponent<CrossingController>();
                controller.CrossingID = crossing.id;
            }
            else
            {
                Log($"[Crossings] Refreshing runtime crossing {crossing.id}");
            }
            */

            bool newlyCreated = false;

            if (controller == null)
            {
                Log($"[Crossings] Building runtime crossing {crossing.id}");
                GameObject go = new GameObject("LC_Group_" + crossing.id);
                controller = go.AddComponent<CrossingController>();
                controller.CrossingID = crossing.id;

                newlyCreated = true;
            }
            else
            {
                Log($"[Crossings] Refreshing runtime crossing {crossing.id}");
            }

            controller.CleanupNullBarriers();

            for (int i = 0; i < crossing.barrierPaths.Count; i++)
            {
                string path = crossing.barrierPaths[i];
                var barrierData = crossing.barriers[i];               
                Vector3 expectedCanonical = new Vector3(
                    barrierData.posX,
                    barrierData.posY,
                    barrierData.posZ
                );

                Log($"[DEBUG] Checking barrier canonical {expectedCanonical}");
                bool has = controller.HasBarrierWithCanonical(expectedCanonical);
                Log($"[DEBUG] HasBarrierWithCanonical = {has}");

                if (controller.HasBarrierWithCanonical(expectedCanonical))
                    continue;

                var barrier = FindTransformByPath(path, expectedCanonical);
                if (barrier == null) continue;

                if (!controller.IsRegisteredBarrier(barrier))
                    controller.RegisterBarrier(barrier);
                                
                var member = barrier.GetComponent<BarrierMember>();
                if (member == null)
                    member = barrier.gameObject.AddComponent<BarrierMember>();

                member.controller = controller;
                member.barrierRoot = barrier;
            }
     
            Log($"[Crossings] Crossing {crossing.id} has {crossing.triggers.Count} triggers in DB");

            for (int i = 0; i < crossing.triggers.Count; i++)
            {
                var td = crossing.triggers[i];
                if (td == null) continue;

                Vector3 canonical = new Vector3(td.posX, td.posY, td.posZ);
                Vector3 sessionPos = canonical + WorldMover.currentMove;
                
                bool exists = false;

                var runtimeTriggers = controller.GetComponentsInChildren<CrossingTrigger>(true);
                foreach (var rt in runtimeTriggers)
                {
                    if (rt == null) continue;
                    if (rt.controller != controller) continue;

                    if (Vector3.SqrMagnitude(rt.transform.position - sessionPos) < 0.01f)
                    {
                        exists = true;
                        break;
                    }
                }

                if (exists)
                {
                    Log($"[Crossings] Trigger {td.group} already exists for {crossing.id}");
                    continue;
                }
                if (exists)
                    continue;
   
                // ---- create missing trigger ----
                Log($"[Crossings] Creating runtime trigger {td.group} for crossing {crossing.id}");

                var trigGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
                trigGO.transform.SetParent(controller.transform, true);

                trigGO.name = td.group == "A" ? "LC_Trigger_A" : "LC_Trigger_B";
                trigGO.transform.position = sessionPos;
                trigGO.transform.localScale = new Vector3(3f, .25f, 1.5f);

                var col = trigGO.GetComponent<BoxCollider>();
                col.isTrigger = true;

                var trig = trigGO.AddComponent<CrossingTrigger>();
                trig.controller = controller;
                trig.group = (td.group == "A")
                    ? CrossingTrigger.TriggerGroup.A
                    : CrossingTrigger.TriggerGroup.B;

                var mr = trigGO.GetComponent<MeshRenderer>();
                if (mr != null)
                    mr.enabled = false;

                Log($"[Crossings] Runtime trigger {td.group} created at {sessionPos}");
            }

#if DVLC_AUTHORING
            var triggers = controller.GetComponentsInChildren<CrossingTrigger>(true);
            foreach (var rt in triggers)
            {
                if (rt == null) continue;

                var r = rt.GetComponent<Renderer>();
                if (r != null) r.enabled = false;
            }

            controller.SetAuthoringVisualsVisible(false);
#endif
            //controller.ForceUp();
            //controller.ResetRuntimeState();
            if (newlyCreated)
            {
                controller.ForceUp();
                controller.ResetRuntimeState();
            }
        }      
        private static Transform FindTransformByPath(string path, Vector3 expectedCanonical)
        {
            if (string.IsNullOrEmpty(path))
            {
                Main.Log("[FindTransformByPath] NULL or empty path requested.");
                return null;
            }

            Main.Log($"[FindTransformByPath] Requested: {path}");           

            var matches = FindAllTransformsByPath(path);

            if (matches.Count == 0)
                return null;

            if (matches.Count == 1)
                return matches[0];

            if (matches.Count > 1)
            {
                Main.Log($"[FindTransformByPath] AMBIGUOUS: {matches.Count} matches for {path}");
            }

            foreach (var t in matches)
            {
                if (t == null) continue;

                Vector3 canonical = t.position - WorldMover.currentMove;

                if (Vector3.SqrMagnitude(canonical - expectedCanonical) < 0.01f)
                {
                    Main.Log("[FindTransformByPath] Resolved via canonical match.");
                    return t;
                }
            }

            Main.Log(
                $"[FindTransformByPath] ERROR: No canonical match found. Expected: {expectedCanonical}"
            );
            return null;
        }

        private static List<Transform> FindAllTransformsByPath(string path)
        {
            var results = new List<Transform>();
            if (string.IsNullOrEmpty(path)) return results;

            var parts = path.Split('/');
            if (parts.Length == 0) return results;

            if (_rootTransforms == null)
            {
                CacheRootTransforms();
            }

            var rootCandidates = new List<Transform>();

            for (int i = 0; i < _rootTransforms.Count; i++)
            {
                var root = _rootTransforms[i];
                if (root == null) continue;

                if (root.name == parts[0])
                    rootCandidates.Add(root);
            }

            foreach (var root in rootCandidates)
            {
                FindAllFromRoot(root, parts, 1, results);
            }

            return results;
        }
   
        private static void FindAllFromRoot(Transform current, string[] parts, int index, List<Transform> results)
        {
            if (current == null) return;

            if (index >= parts.Length)
            {
                results.Add(current);
                return;
            }

            string want = parts[index];
            
            int childCount = current.childCount;
            for (int i = 0; i < childCount; i++)
            {
                var ch = current.GetChild(i);
                if (ch == null) continue;
                if (ch.name != want) continue;

                FindAllFromRoot(ch, parts, index + 1, results);
            }
        }

        private static void CacheRootTransforms()
        {
            _rootTransforms = new List<Transform>();

            foreach (var t in UnityEngine.Object.FindObjectsOfType<Transform>())
            {
                if (t == null) continue;
                if (t.parent == null)
                    _rootTransforms.Add(t);
            }

            Main.Log($"[Crossings] Cached {_rootTransforms.Count} root transforms.");
        }

        private static bool RootCacheNeedsRefresh()
        {
            foreach (var r in _rootTransforms)
            {
                if (r == null)
                    return true;
            }
            return false;
        }

        // =========================================================
        // LOGGER
        // =========================================================

        public static void Log(string message, bool force = false)
        {
#if DVLC_DEBUG
            if (!force && (Settings == null || !Settings.debugLogging))
                return;

            Mod?.Logger?.Log(message);
#endif
        }


#if DVLC_AUTHORING
        // =========================================================
        // AUTHORING SAVE
        // =========================================================
        public static void SaveCrossings()
        {
            if (_loadedDatabase == null)
                return;

            CrossingPersistence.Save(_loadedDatabase);

#if DVLC_DEBUG
            Log($"[Crossings] Saved {_loadedDatabase.crossings.Count} crossings.");
#endif
        }
#endif
    }
}



using DV_LevelCrossings;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class CrossingController : MonoBehaviour
{   
    public string CrossingID = Guid.NewGuid().ToString();

    private readonly List<BarrierArm> arms = new List<BarrierArm>();
    private readonly HashSet<Transform> registeredBarrierRoots = new HashSet<Transform>();
    public IEnumerable<Transform> RegisteredBarrierRoots
    {
        get { return registeredBarrierRoots; }
    }
    public int BarrierCount => registeredBarrierRoots.Count;
    public static readonly List<CrossingController> AllControllers = new List<CrossingController>();

    private readonly List<CrossingTrigger> registeredTriggers =
    new List<CrossingTrigger>();
    public int TriggerCount => registeredTriggers.Count;

    // ===== STATE =====
    private bool isDown;

    private bool isBlocked;
    CrossingTrigger.TriggerGroup blockingGroup;
    CrossingTrigger.TriggerGroup clearingGroup;

    private bool clearingPrimed;
    private float lastClearingHitTime = -1f;

    private float raiseTimerEnd = -1f;

    // Clearance behavior
    public float quietWindowSeconds = 5f;   // no hits for this long => train has cleared
    public float raiseDelaySeconds = 2f;    // after quiet window, wait this long then raise
    private float blockStartTime = -1f;
    public float reverseFailSafeSeconds = 60f;

    // ===== AUDIO =====
    private readonly List<AudioSource> bellSources = new List<AudioSource>();
    private AudioClip bellClip;

    // ===== LIGHTS =====
    private readonly List<Renderer> leftRenderers = new List<Renderer>();
    private readonly List<Renderer> rightRenderers = new List<Renderer>();
    private float flashTimer;
    private bool leftActive;
    public float flashInterval = 0.5f;   

    private void Awake()
    {
        AllControllers.Add(this);
    }

    private void OnDestroy()
    {
        AllControllers.Remove(this);
    }   

    private void ResetLogicState()
    {
        isBlocked = false;
        blockingGroup = CrossingTrigger.TriggerGroup.A;
        clearingGroup = CrossingTrigger.TriggerGroup.B;

        clearingPrimed = false;
        lastClearingHitTime = -1f;

        raiseTimerEnd = -1f;
    }

    public bool RegisterBarrier(Transform barrierRoot, bool addMarker = true)
    {
        if (barrierRoot == null) return false;

        if (barrierRoot.name == null || !barrierRoot.name.StartsWith("RailwayCrossingBarrier"))
        {
            Main.Log("[Crossing] Not a barrier root: " + barrierRoot.name);
            return false;
        }

        Vector3 expectedCanonical = barrierRoot.position - WorldMover.currentMove;

        foreach (var existing in registeredBarrierRoots)
        {
            if (existing == null) continue;

            Vector3 canon = existing.position - WorldMover.currentMove;

            if (Vector3.SqrMagnitude(canon - expectedCanonical) < 0.01f)
            {
                Main.Log("[Crossing] Canonical barrier already registered: " + barrierRoot.name);
                return false;
            }
        }

        var member = barrierRoot.GetComponent<BarrierMember>();
        if (member == null)
            member = barrierRoot.gameObject.AddComponent<BarrierMember>();

        member.controller = this;
        member.barrierRoot = barrierRoot;

        arms.Add(new BarrierArm(barrierRoot));
        registeredBarrierRoots.Add(barrierRoot);

        SetupBell(barrierRoot);
        SetupLights(barrierRoot);

#if DVLC_AUTHORING
        if (addMarker)
            SpawnBarrierMarker(barrierRoot);
#endif

        Main.Log("[Crossing] Registered barrier: " + barrierRoot.name +
                  " @ " + barrierRoot.position +
                  " (arms=" + arms.Count + ")");

        return true;
    }

    public void UnregisterBarrier(Transform barrierRoot)
    {
        if (!registeredBarrierRoots.Contains(barrierRoot))
        {
            Main.Log("[Crossing] Barrier not registered: " + barrierRoot.name);
            return;
        }

        arms.RemoveAll(a => a.RootTransform == barrierRoot);

        var signal = barrierRoot.Find("RailwayCrossingSignal");
        if (signal != null)
        {
            var audio = signal.GetComponent<AudioSource>();
            if (audio != null)
            {
                bellSources.Remove(audio);
                Destroy(audio);
            }
        }
        
        var markers = barrierRoot.GetComponentsInChildren<Transform>();
        foreach (var t in markers)
        {
            if (t.name == "LC_BarrierMarker" ||
                t.name == "LC_Light_Left" ||
                t.name == "LC_Light_Right")
            {
                Destroy(t.gameObject);
            }
        }

        registeredBarrierRoots.Remove(barrierRoot);

        Main.Log("[Crossing] Barrier unregistered. arms=" + arms.Count);
    }

#if DVLC_AUTHORING
    private void SpawnBarrierMarker(Transform barrierRoot)
    {
        GameObject m = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        m.name = "LC_BarrierMarker";
        Destroy(m.GetComponent<Collider>());

        m.transform.SetParent(barrierRoot, false);
        m.transform.localPosition = new Vector3(0f, 3.0f, 0f);
        m.transform.localScale = Vector3.one * 0.25f;

        var r = m.GetComponent<Renderer>();
        if (r != null)
        {
            var mat = new Material(Shader.Find("Standard"));
            mat.color = Color.cyan;
            r.material = mat;
        }
    }

    
#endif

    // =========================================================
    // TRIGGERS: BLOCK SIGNALLING A/B
    // =========================================================

    public void RegisterTrigger(CrossingTrigger trigger)
    {
        if (trigger == null)
            return;

        if (!registeredTriggers.Contains(trigger))
            registeredTriggers.Add(trigger);
    }

    public void UnregisterTrigger(CrossingTrigger trigger)
    {
        if (trigger == null)
            return;

        registeredTriggers.Remove(trigger);
    }

    public void NotifyTrigger(CrossingTrigger.TriggerGroup group)
    {
        if (Main.Settings.barriersAlwaysUp)
            return;

        if (arms.Count == 0) return;

        if (!isBlocked)
        {
            isBlocked = true;
            blockStartTime = Time.time;
            blockingGroup = group;
            clearingGroup = (group == CrossingTrigger.TriggerGroup.A) ? CrossingTrigger.TriggerGroup.B : CrossingTrigger.TriggerGroup.A;

            clearingPrimed = false;
            lastClearingHitTime = -1f;
            raiseTimerEnd = -1f;

            ForceDown();            
            return;
        }

        if (group == clearingGroup)
        {
            lastClearingHitTime = Time.time;
            clearingPrimed = true;

            if (raiseTimerEnd > 0f)
                raiseTimerEnd = -1f;  
        }
        else
        {
            // hits on blocking side while already blocked are ignored
        }
    }

    public void NotifyTriggerStay(CrossingTrigger.TriggerGroup group)
    {
        if (Main.Settings.barriersAlwaysUp)
            return;

        if (arms.Count == 0) return;

        if (!isBlocked) return;
        if (group != clearingGroup) return;

        lastClearingHitTime = Time.time;
        clearingPrimed = true;

        if (raiseTimerEnd > 0f)
            raiseTimerEnd = -1f;
    }


    public void ForceDown()
    {
        Main.Log("ForceDown called. isDown=" + isDown);

        if (isDown) return;
        isDown = true;

        for (int i = 0; i < arms.Count; i++)
            arms[i].SetDown();

        for (int i = 0; i < bellSources.Count; i++)
        {
            if (bellSources[i] != null && bellClip != null && !bellSources[i].isPlaying)
                bellSources[i].Play();
        }
    }

    public void ForceUp()
    {
        Main.Log("ForceUp called. isDown=" + isDown);

        isDown = false;
        blockStartTime = -1f;

        for (int i = 0; i < arms.Count; i++)
            arms[i].SetUp();

        for (int i = 0; i < bellSources.Count; i++)
        {
            if (bellSources[i] != null && bellSources[i].isPlaying)
                bellSources[i].Stop();
        }

        ResetLogicState();
    }


    private void Update()
    {
        

        // animate arms
        for (int i = 0; i < arms.Count; i++)
            arms[i].Tick();

        // flashing lights while down
        if (isDown)
        {
            flashTimer += Time.deltaTime;
            if (flashTimer >= flashInterval)
            {
                flashTimer = 0f;
                leftActive = !leftActive;
            }
            UpdateLights();
        }
        else
        {
            SetLightsOff();
        }

        // ===== CLEARING: quiet window => start raise delay =====
        if (isDown && isBlocked)
        {
            if (clearingPrimed && lastClearingHitTime > 0f)
            {
                float sinceLast = Time.time - lastClearingHitTime;

                if (raiseTimerEnd < 0f && sinceLast >= quietWindowSeconds)
                {
                    raiseTimerEnd = Time.time + raiseDelaySeconds;
                    clearingPrimed = false;

                    Main.Log("[Crossing] " + CrossingID + " CLEAR quiet for " + quietWindowSeconds +
                              "s; starting raise delay " + raiseDelaySeconds + "s");
                }
            }

            // ===== Raise when delay elapsed =====
            if (raiseTimerEnd > 0f && Time.time >= raiseTimerEnd)
            {
                Main.Log("[Crossing] " + CrossingID + " Raise delay elapsed: UP");
                raiseTimerEnd = -1f;
                ForceUp();
            }
        }

        if (isDown && isBlocked && blockStartTime > 0f)
        {
            float blockedDuration = Time.time - blockStartTime;

            if (blockedDuration >= reverseFailSafeSeconds)
            {
                bool anyOccupied = false;

                for (int i = 0; i < registeredTriggers.Count; i++)
                {
                    if (registeredTriggers[i] != null && registeredTriggers[i].IsOccupied)
                    {
                        anyOccupied = true;
                        break;
                    }
                }

                if (!anyOccupied)
                {
                    Main.Log("[Crossing] " + CrossingID + " FAIL-SAFE timeout: forcing UP");
                    ForceUp();
                }
            }
        }

        if (Main.Settings.barriersAlwaysUp && isDown)
        {
            ForceUp();
            isBlocked = false;
            clearingPrimed = false;
            raiseTimerEnd = -1f;
        }
    }

    private void SetupBell(Transform barrierRoot)
    {
        var signal = barrierRoot.Find("RailwayCrossingSignal");
        if (signal == null)
        {
            Main.Log("[Crossing] RailwayCrossingSignal not found for bell on: " + barrierRoot.name);
            return;
        }

        var bellSource = signal.GetComponent<AudioSource>();
        if (bellSource == null)
            bellSource = signal.gameObject.AddComponent<AudioSource>();

        bellSource.playOnAwake = false;
        bellSource.loop = true;
        bellSource.spatialBlend = 1f;
        bellSource.dopplerLevel = 0f;

        bellSource.rolloffMode = AudioRolloffMode.Linear;
        bellSource.minDistance = 10f;
        bellSource.maxDistance = 60f;
        bellSource.volume = 0.9f;

        if (!bellSources.Contains(bellSource))
            bellSources.Add(bellSource);

        if (bellClip == null)
        {
            string path = Path.Combine(Main.Mod.Path, "Assets", "Sounds", "bell_loop.wav");
            StartCoroutine(LoadBell(path));
        }
        else
        {
            bellSource.clip = bellClip;
        }
    }

    private IEnumerator LoadBell(string path)
    {
        if (!File.Exists(path))
        {
            Main.Log("[Crossing] Bell file not found: " + path);
            yield break;
        }

        string url = "file://" + path;

        UnityWebRequest www = UnityWebRequest.Get(url);
        www.downloadHandler = new DownloadHandlerAudioClip(url, AudioType.WAV);

        yield return www.SendWebRequest();

        if (www.isNetworkError || www.isHttpError)
        {
            Main.Log("[Crossing] Failed to load bell: " + www.error);
        }
        else
        {
            bellClip = ((DownloadHandlerAudioClip)www.downloadHandler).audioClip;

            for (int i = 0; i < bellSources.Count; i++)
                if (bellSources[i] != null) bellSources[i].clip = bellClip;

            Main.Log("[Crossing] Bell loaded successfully.");
        }
    }

    private void SetupLights(Transform barrierRoot)
    {
        var signal = barrierRoot.Find("RailwayCrossingSignal");
        if (signal == null)
        {
            Main.Log("[Crossing] RailwayCrossingSignal not found for lights on: " + barrierRoot.name);
            return;
        }

        GameObject left = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        GameObject right = GameObject.CreatePrimitive(PrimitiveType.Sphere);

        left.name = "LC_Light_Left";
        right.name = "LC_Light_Right";

        left.transform.SetParent(signal, false);
        right.transform.SetParent(signal, false);

        Destroy(left.GetComponent<Collider>());
        Destroy(right.GetComponent<Collider>());

 
        left.transform.localPosition = new Vector3(-0.15f, 2.57f, 0.13f);
        right.transform.localPosition = new Vector3(0.15f, 2.57f, 0.13f);

        left.transform.localRotation = Quaternion.identity;
        right.transform.localRotation = Quaternion.identity;


        left.transform.localScale = new Vector3(0.12f, 0.12f, 0.04f);
        right.transform.localScale = new Vector3(0.12f, 0.12f, 0.04f);

        Renderer leftR = left.GetComponent<Renderer>();
        Renderer rightR = right.GetComponent<Renderer>();

        var baseMat = new Material(Shader.Find("Standard"));
        baseMat.EnableKeyword("_EMISSION");
        baseMat.SetFloat("_Metallic", 0f);
        baseMat.SetFloat("_Glossiness", 0f);
        baseMat.SetColor("_Color", Color.black);
        baseMat.SetColor("_EmissionColor", Color.red * 0f);

        leftR.material = baseMat;
        rightR.material = new Material(baseMat);

        leftRenderers.Add(leftR);
        rightRenderers.Add(rightR);
    }

    private void UpdateLights()
    {
        for (int i = 0; i < leftRenderers.Count; i++)
        {
            if (leftRenderers[i] == null || rightRenderers[i] == null) continue;

            leftRenderers[i].material.SetColor("_EmissionColor",
                leftActive ? Color.red * 3f : Color.red * 0f);

            rightRenderers[i].material.SetColor("_EmissionColor",
                leftActive ? Color.red * 0f : Color.red * 3f);
        }
    }

    private void SetLightsOff()
    {
        for (int i = 0; i < leftRenderers.Count; i++)
        {
            if (leftRenderers[i] == null || rightRenderers[i] == null) continue;

            leftRenderers[i].material.SetColor("_EmissionColor", Color.red * 0f);
            rightRenderers[i].material.SetColor("_EmissionColor", Color.red * 0f);
        }
    }

    public CrossingData ToData()
    {
        var data = new CrossingData
        {
            id = CrossingID
        };


        foreach (var root in registeredBarrierRoots)
        {
            string path = GetTransformPath(root);
            data.barrierPaths.Add(path);
        }

        foreach (var trig in registeredTriggers)
        {
            if (trig == null)
                continue;

            Transform t = trig.transform;
            Vector3 canonical = t.position - WorldMover.currentMove;

            data.triggers.Add(new TriggerData
            {
                group = trig.group.ToString(),
                posX = canonical.x,
                posY = canonical.y,
                posZ = canonical.z,
                rotY = t.eulerAngles.y
            });
        }
        return data;
    }

    private static string GetTransformPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }

    public bool IsRegisteredBarrier(Transform t)
    {
        return registeredBarrierRoots.Contains(t);
    }

    public void CleanupNullBarriers()
    {
        if (registeredBarrierRoots == null)
            return;

        List<Transform> toRemove = null;

        foreach (var root in registeredBarrierRoots)
        {
            if (root == null)
            {
                if (toRemove == null)
                    toRemove = new List<Transform>();

                toRemove.Add(root);
            }
        }

        if (toRemove != null)
        {
            foreach (var dead in toRemove)
                registeredBarrierRoots.Remove(dead);
        }

        arms.RemoveAll(a => a == null || a.RootTransform == null);
        registeredBarrierRoots.RemoveWhere(t => t == null);
    }

    public void ResetRuntimeState()
    {
        isBlocked = false;
        clearingPrimed = false;

        blockingGroup = default(CrossingTrigger.TriggerGroup);
        clearingGroup = default(CrossingTrigger.TriggerGroup);

        lastClearingHitTime = -1f;
        raiseTimerEnd = -1f;
    }

    public bool HasBarrierWithCanonical(Vector3 expectedCanonical)
    {
        foreach (var existing in registeredBarrierRoots)
        {
            if (existing == null) continue;

            Vector3 canon = existing.position - WorldMover.currentMove;

            if (Vector3.SqrMagnitude(canon - expectedCanonical) < 0.01f)
                return true;
        }

        return false;
    }

    public int RegisteredTriggerCountForDebug()
    {
        return registeredTriggers != null ? registeredTriggers.Count : 0;
    }

    public int Debug_GetArmsCount()
    {
        return arms != null ? arms.Count : 0;
    }
}

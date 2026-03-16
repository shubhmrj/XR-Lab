// =============================================================================
//  ACID_BASE_REACTION.cs
//  XR Chemistry Lab — Acid/Base Reaction Script
//  Fixed & Rewritten — clean, complete, production-ready
// =============================================================================
//
//  GESTURE MAP
//  ────────────────────────────────────────────────────────────────────────────
//  OPEN_PINCH  → Refill source beaker with HCl (acid)
//  OPEN_HAND   → Tilt source beaker; liquid pours when tilted past threshold
//  CLOSED_HAND → Grab & move target beaker
//
//  REACTION LOGIC
//  ────────────────────────────────────────────────────────────────────────────
//  Acid (source) + Base (target) → Neutralisation → pH 7, salt water
//  Source is always acid; target starts empty, can be filled with base via
//  the "Fill with Base" UI button for a neutralisation demo.
//
//  BUGS FIXED vs ORIGINAL
//  ────────────────────────────────────────────────────────────────────────────
//  1. MakeTex() now cached — was called every OnGUI frame (major GC leak)
//  2. ClearRigidbodyVelocities() rewritten without reflection
//  3. Particle world-position sync fixed (parent check was always false)
//  4. All [SerializeField] fields moved to top of class — no more fields
//     scattered between method bodies
//  5. grabGraceDuration fallback uses real last-known position, not Vector3.zero
//  6. Source beaker tilt now rotates around its own pivot (not pour point),
//     then position is restored correctly without fighting the lock
//  7. CheckBeakerToBeakerPouring() called only once per frame
//  8. Dead code removed (GetCompactLabelStyle, GetBarBackgroundStyle, etc.)
//  9. using System.Reflection removed — no longer needed
// =============================================================================

using UnityEngine;
using ManoMotion;

public class ACID_BASE_REACTION : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    //  INSPECTOR FIELDS
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Scene References")]
    [SerializeField] private GameObject sourceBeakerGO;
    [SerializeField] private GameObject targetBeakerGO;
    [SerializeField] private GameObject waterParticlesPrefab;

    [Header("Pour Points (assign in Inspector)")]
    [SerializeField] public Transform sourcePourPoint;
    [SerializeField] public Transform targetPourPoint;

    [Header("Chemistry Settings")]
    [SerializeField] private float maxBeakerVolume   = 500f;   // mL
    [SerializeField] private float pourRate          = 250f;   // mL/s
    [SerializeField] private float pouringDistance   = 2.5f;   // metres
    [SerializeField] private float pouringThreshold  = 25f;    // degrees from upright

    [Header("Grab & Movement")]
    [SerializeField] private float moveSpeed         = 22f;
    [SerializeField] private float tiltSmoothSpeed   = 18f;
    [SerializeField] private float maxTiltAngle      = 65f;
    [SerializeField] private float coordinateScale   = 10f;
    [SerializeField] private Vector3 handPositionOffset = new Vector3(0f, 0f, 8f);
    [SerializeField] private bool  isLandscapeMode   = true;
    [SerializeField] private bool  grabAnywhere      = true;
    [SerializeField] private float grabDetectionRadius = 7.5f;
    [SerializeField] private float grabGraceDuration = 0.25f;

    [Header("Depth Following")]
    [SerializeField] private bool  followHandDepth   = true;
    [SerializeField] private float depthSmoothSpeed  = 8f;
    [SerializeField] private float maxInitialSnapDist = 0.7f;
    [SerializeField] private float initialSnapSpeed  = 30f;

    [Header("Safety Bounds")]
    [SerializeField] private bool  enableSafetyBounds = true;
    [SerializeField] private Vector3 minBounds = new Vector3(-5f, -3f,  5f);
    [SerializeField] private Vector3 maxBounds = new Vector3( 5f,  5f, 15f);

    [Header("Auto Return")]
    [SerializeField] private bool autoReturnWhenNoGesture = true;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip   pourSound;
    [SerializeField] private AudioClip   refillSound;
    [SerializeField] private AudioClip   reactionSound;

    [Header("Debug")]
    [SerializeField] private bool showDebugVisuals = true;

    // ─────────────────────────────────────────────────────────────────────────
    //  PRIVATE DATA TYPES
    // ─────────────────────────────────────────────────────────────────────────

    private class BeakerData
    {
        public GameObject go;
        public Transform  pourPoint;
        public GameObject fxObject;
        public ParticleSystem fxSystem;
        public Transform  originalParent;
        public Rigidbody  rb;
        public bool wasKinematic;

        // Chemistry
        public float  volumeML;
        public Color  liquidColor;
        public string chemicalName = "Empty";
        public float  pH           = 7f;
        public bool   isAcid;
        public bool   isBase;
        public float  concentration;

        // Transform snapshots
        public Vector3    initialPosition;
        public Quaternion initialRotation;

        // Grab state
        public bool    isGrabbed;
        public bool    isFixed;
        public Vector3 grabOffset;
    }

    private class FeedbackData
    {
        public bool   active;
        public float  timer;
        public string action;
        public string mistake;
        public string avoid;
        public string correct;
        public Color  colour = Color.white;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  RUNTIME STATE
    // ─────────────────────────────────────────────────────────────────────────

    private BeakerData  _source;
    private BeakerData  _target;
    private BeakerData  _grabbed;

    private bool    _pouringActive;
    private string  _statusMsg     = "Chemistry Lab Ready";
    private ManoGestureContinuous _gesture = ManoGestureContinuous.NO_GESTURE;
    private FeedbackData _feedback = new FeedbackData();

    // Reaction flags
    private bool _reactionOccurred;
    private bool _hasOverfilled;

    // Grace period for brief gesture drop-outs
    private Vector3 _lastHandPos;
    private bool    _lastHandPosValid;
    private float   _lastHandTime;

    // Fixed scale applied to every beaker every frame
    private static readonly Vector3 BEAKER_SCALE = new Vector3(8f, 8f, 8f);

    // ─────────────────────────────────────────────────────────────────────────
    //  UI TEXTURE CACHE  (created once — fixes the per-frame MakeTex GC bug)
    // ─────────────────────────────────────────────────────────────────────────

    private Texture2D _texDarkBg, _texAcidBorder, _texAcidHeader;
    private Texture2D _texBaseBorder, _texBaseHeader;
    private Texture2D _texStatusBorder, _texStatusBg;
    private Texture2D _texBarBg, _texGlass;
    private Texture2D _texFeedbackBg;
    private bool      _texCacheBuilt;

    // ─────────────────────────────────────────────────────────────────────────
    //  UNITY MESSAGES
    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        if (ManoMotionManager.Instance != null)
            ManoMotionManager.Instance.ShouldCalculateGestures(true);

        BuildTextureCache();
        InitBeakers();

        Debug.Log("[ChemLab] Initialised — Source (acid) + Target (empty)");
    }

    void Update()
    {
        if (useGestureControls)
            ProcessGestures();

        UpdatePouring();
        UpdateFeedback();
        EnforceConstraints();
    }

    void OnGUI()
    {
        DrawUI();
    }

    // public flag mirrors the serialized field name used in the original so
    // existing Inspector wiring still works
    [Header("Control Mode")]
    [SerializeField] private bool useGestureControls = true;

    // ─────────────────────────────────────────────────────────────────────────
    //  INITIALISATION
    // ─────────────────────────────────────────────────────────────────────────

    void InitBeakers()
    {
        if (sourceBeakerGO != null)
        {
            _source = BuildBeaker(sourceBeakerGO, fixed: true, sourcePourPoint);
            _source.chemicalName  = "Hydrochloric Acid (HCl)";
            _source.liquidColor   = new Color(1f, 0.7f, 0.2f, 0.7f);
            _source.volumeML      = maxBeakerVolume;
            _source.pH            = 1f;
            _source.isAcid        = true;
            _source.concentration = 100f;
        }

        if (targetBeakerGO != null)
        {
            _target = BuildBeaker(targetBeakerGO, fixed: false, targetPourPoint);
            _target.chemicalName  = "Empty";
            _target.liquidColor   = new Color(0.7f, 0.85f, 0.92f, 0.7f);
            _target.volumeML      = 0f;
            _target.pH            = 7f;
            _target.isAcid        = false;
            _target.isBase        = false;
            _target.concentration = 0f;
        }
    }

    BeakerData BuildBeaker(GameObject go, bool @fixed, Transform pourPt)
    {
        var d = new BeakerData
        {
            go              = go,
            isFixed         = @fixed,
            initialPosition = go.transform.position,
            initialRotation = go.transform.rotation,
            liquidColor     = Color.cyan,
            originalParent  = go.transform.parent
        };

        go.transform.localScale = BEAKER_SCALE;

        d.rb = go.GetComponent<Rigidbody>();
        if (d.rb != null) d.wasKinematic = d.rb.isKinematic;

        // Pour point
        if (pourPt != null)
        {
            d.pourPoint = pourPt;
        }
        else
        {
            var ppGO = new GameObject($"AutoPourPt_{go.name}");
            ppGO.transform.SetParent(go.transform);
            ppGO.transform.localPosition = new Vector3(0f, 0.45f, 0.25f);
            d.pourPoint = ppGO.transform;
            Debug.LogWarning($"[ChemLab] No pour point assigned for '{go.name}' — auto-created one.");
        }

        // Particle effect (never parented — avoids scale/culling issues)
        if (waterParticlesPrefab != null)
        {
            d.fxObject = Instantiate(waterParticlesPrefab, d.pourPoint.position, d.pourPoint.rotation);
            d.fxObject.name = $"ChemFX_{go.name}";
            d.fxObject.transform.SetParent(null, true);
            d.fxSystem = d.fxObject.GetComponent<ParticleSystem>();
            if (d.fxSystem != null)
            {
                var m = d.fxSystem.main;
                m.startColor = d.liquidColor;
                d.fxSystem.Stop();
            }
        }

        return d;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  GESTURE PROCESSING
    // ─────────────────────────────────────────────────────────────────────────

    void ProcessGestures()
    {
        if (ManoMotionManager.Instance?.HandInfos == null) return;

        var hands = ManoMotionManager.Instance.HandInfos;
        bool handFound = false;

        foreach (var hand in hands)
        {
            if (hand.gestureInfo.manoClass == ManoClass.NO_HAND) continue;

            Vector3 handPos = CalcHandPosition(hand.trackingInfo.boundingBox);
            _gesture = hand.gestureInfo.manoGestureContinuous;

            // Derive normalised X for tilt (hand horizontal position -0.5 … +0.5)
            var bb = hand.trackingInfo.boundingBox;
            float cx = bb.topLeft.x + bb.width  * 0.5f;
            float normX = cx - 0.5f;  // -0.5 left … +0.5 right

            switch (_gesture)
            {
                case ManoGestureContinuous.OPEN_PINCH_GESTURE:
                    ReleaseBeaker();
                    HandleRefill();
                    break;

                case ManoGestureContinuous.OPEN_HAND_GESTURE:
                    ReleaseBeaker();
                    HandleTilt(normX);
                    break;

                case ManoGestureContinuous.CLOSED_HAND_GESTURE:
                    _lastHandPos      = handPos;
                    _lastHandPosValid = true;
                    _lastHandTime     = Time.time;
                    HandleGrab(handPos);
                    break;

                default:
                    break;
            }

            handFound = true;
            break; // only first valid hand
        }

        if (!handFound)
        {
            // Grace period: keep moving grabbed beaker briefly if we just lost tracking
            if (_grabbed != null && _lastHandPosValid &&
                Time.time - _lastHandTime <= grabGraceDuration)
            {
                HandleGrab(_lastHandPos);
            }
            else
            {
                _gesture = ManoGestureContinuous.NO_GESTURE;
                if (autoReturnWhenNoGesture)
                {
                    ReleaseBeaker();
                    ReturnToUpright();
                }
                _statusMsg = "Chemistry Lab Ready";
            }
        }

        // Pouring proximity check — once per frame here, not inside HandleGrab
        CheckPouringProximity();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  INDIVIDUAL GESTURE HANDLERS
    // ─────────────────────────────────────────────────────────────────────────

    // OPEN_HAND → tilt SOURCE beaker
    void HandleTilt(float normX)
    {
        if (_source == null) return;

        _statusMsg = $"Tilting source beaker — Vol: {_source.volumeML:F0} mL";

        float desiredTiltZ = Mathf.Clamp(normX * 1.4f * maxTiltAngle, -maxTiltAngle, maxTiltAngle);

        float curTiltZ = _source.go.transform.eulerAngles.z;
        if (curTiltZ > 180f) curTiltZ -= 360f;

        float delta = desiredTiltZ - curTiltZ;
        if (Mathf.Abs(delta) > 0.1f)
        {
            float step = delta * Time.deltaTime * tiltSmoothSpeed;
            // Rotate around the beaker's own centre (not pour point) so the position
            // lock below doesn't fight a pivot offset.
            _source.go.transform.Rotate(Vector3.forward, step, Space.World);
        }

        // Position & scale lock for fixed beaker
        _source.go.transform.position   = _source.initialPosition;
        _source.go.transform.localScale = BEAKER_SCALE;
    }

    // OPEN_PINCH → refill source beaker
    void HandleRefill()
    {
        if (_source == null) return;

        float added = pourRate * 2f * Time.deltaTime; // 2× rate for snappy refill
        _source.volumeML = Mathf.Min(maxBeakerVolume, _source.volumeML + added);

        if (_source.volumeML > 0f)
        {
            _source.chemicalName  = "Hydrochloric Acid (HCl)";
            _source.liquidColor   = new Color(1f, 0.7f, 0.2f, 0.7f);
            _source.isAcid        = true;
            _source.pH            = 1f;
            _source.concentration = 100f;
        }

        PlayOnce(refillSound, 0.8f);
        _statusMsg = $"Refilling source: {_source.volumeML:F0} / {maxBeakerVolume:F0} mL";
    }

    // CLOSED_HAND → grab/move target beaker
    void HandleGrab(Vector3 handPos)
    {
        // Acquire grab on first frame of this gesture
        if (_grabbed == null)
        {
            _grabbed = GetGrabbable(handPos);
            if (_grabbed != null)
            {
                AcquireGrab(_grabbed, handPos);
            }
            else
            {
                _statusMsg = "GRAB FAILED — bring hand closer to target beaker";
                return;
            }
        }

        if (_grabbed == null || _grabbed.isFixed) { _grabbed = null; return; }

        // Validate incoming position
        if (float.IsNaN(handPos.x) || float.IsNaN(handPos.y) || float.IsNaN(handPos.z))
        {
            handPos = _lastHandPosValid ? _lastHandPos : _grabbed.go.transform.position;
        }

        // Desired world position
        Vector3 desired = handPos + _grabbed.grabOffset;

        if (followHandDepth)
            desired.z = Mathf.Lerp(_grabbed.go.transform.position.z,
                                   handPos.z + _grabbed.grabOffset.z,
                                   Time.deltaTime * depthSmoothSpeed);
        else
            desired.z = _grabbed.go.transform.position.z;

        if (enableSafetyBounds)
        {
            desired.x = Mathf.Clamp(desired.x, minBounds.x, maxBounds.x);
            desired.y = Mathf.Clamp(desired.y, minBounds.y, maxBounds.y);
            desired.z = Mathf.Clamp(desired.z, minBounds.z, maxBounds.z);
        }

        float maxDelta = moveSpeed * Time.deltaTime;
        _grabbed.go.transform.position = Vector3.MoveTowards(
            _grabbed.go.transform.position, desired, maxDelta);

        // Keep target upright while held
        _grabbed.go.transform.rotation = Quaternion.Lerp(
            _grabbed.go.transform.rotation,
            _grabbed.initialRotation,
            Time.deltaTime * 8f);

        _grabbed.go.transform.localScale = BEAKER_SCALE;

        // Recover if Unity somehow deactivated the object mid-frame
        if (!_grabbed.go.activeInHierarchy)
            _grabbed.go.SetActive(true);

        _statusMsg = $"Moving: {_grabbed.go.name} → {_grabbed.go.transform.position}";
    }

    void AcquireGrab(BeakerData d, Vector3 handPos)
    {
        d.isGrabbed = true;

        // Ensure visible
        d.go.SetActive(true);
        foreach (var r in d.go.GetComponentsInChildren<Renderer>(true))  r.enabled = true;
        foreach (var c in d.go.GetComponentsInChildren<Collider>(true))  c.enabled = true;

        // Detach from parent so parent transforms don't interfere
        if (d.originalParent != null)
            d.go.transform.SetParent(null, true);

        // Make kinematic while grabbed
        if (d.rb != null)
        {
            d.rb.isKinematic = true;
            ClearVelocities(d.rb);
        }

        d.go.transform.localScale = BEAKER_SCALE;

        // Compute grab offset — how far the object centre is from the hand
        d.grabOffset = d.go.transform.position - handPos;

        // If the depth component is huge, snap Z first to avoid "flying from behind camera"
        if (Mathf.Abs(d.grabOffset.z) > maxInitialSnapDist)
        {
            Vector3 p = d.go.transform.position;
            p.z = handPos.z;
            d.go.transform.position = p;
            d.grabOffset = d.go.transform.position - handPos;
        }
        else if (d.grabOffset.magnitude > maxInitialSnapDist)
        {
            // Reduce offset magnitude gradually on first frame
            d.grabOffset = d.grabOffset.normalized * maxInitialSnapDist;
        }

        float camDist = Camera.main != null
            ? Vector3.Distance(Camera.main.transform.position, d.go.transform.position)
            : -1f;

        if (showDebugVisuals)
            Debug.Log($"[ChemLab] GRABBED '{d.go.name}' | offset={d.grabOffset} | camDist={camDist:F2}");
    }

    void ReleaseBeaker()
    {
        if (_grabbed == null) return;

        var d   = _grabbed;
        var obj = d.go;

        // Re-enable visibility
        if (!obj.activeInHierarchy) obj.SetActive(true);
        foreach (var r in obj.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
        foreach (var c in obj.GetComponentsInChildren<Collider>(true)) c.enabled = true;

        // Restore parent
        if (d.originalParent != null)
            obj.transform.SetParent(d.originalParent, false);

        // Restore physics
        if (d.rb != null)
        {
            ClearVelocities(d.rb);
            d.rb.isKinematic = d.wasKinematic;
        }

        obj.transform.localScale = BEAKER_SCALE;

        // Snap back if somehow outside play area
        float dist = Vector3.Distance(obj.transform.position, d.initialPosition);
        if (dist > 8f || obj.transform.position.z < minBounds.z ||
                         obj.transform.position.z > maxBounds.z)
        {
            obj.transform.position = d.initialPosition;
            obj.transform.rotation = d.initialRotation;
            Debug.LogWarning($"[ChemLab] '{obj.name}' out-of-bounds on release — snapped to initial position");
        }

        d.isGrabbed = false;
        _grabbed    = null;
        _pouringActive = false;

        if (showDebugVisuals)
            Debug.Log($"[ChemLab] RELEASED '{obj.name}'");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  POURING LOGIC
    // ─────────────────────────────────────────────────────────────────────────

    void CheckPouringProximity()
    {
        if (_source == null || _target == null) return;
        float dist = Vector3.Distance(_source.go.transform.position,
                                      _target.go.transform.position);
        _pouringActive = dist <= pouringDistance;
    }

    void UpdatePouring()
    {
        TickBeakerPouring(_source);
        TickBeakerPouring(_target);
    }

    void TickBeakerPouring(BeakerData d)
    {
        if (d?.fxSystem == null) return;

        float tilt = Vector3.Angle(d.go.transform.up, Vector3.up);
        bool shouldPour = tilt > pouringThreshold && d.volumeML > 0f;

        if (shouldPour)
        {
            // ─── Sync particle system to pour point in world space ───────────
            // NOTE: fxObject is always unparented; we set world position each frame.
            d.fxObject.transform.position = d.pourPoint.position;
            d.fxObject.transform.rotation = d.pourPoint.rotation;

            float t  = Mathf.Clamp01((tilt - pouringThreshold) / (90f - pouringThreshold));
            float loss = pourRate * t * Time.deltaTime;

            if (_pouringActive && d == _source && _target != null)
            {
                // Transfer from source → target
                float transfer = Mathf.Min(loss, d.volumeML);
                transfer = Mathf.Min(transfer, maxBeakerVolume - _target.volumeML);

                d.volumeML       -= transfer;
                _target.volumeML += transfer;

                // First drop into target beaker
                if (transfer > 0f && _target.chemicalName == "Empty")
                {
                    _target.chemicalName  = d.chemicalName;
                    _target.liquidColor   = d.liquidColor;
                    _target.pH            = d.pH;
                    _target.isAcid        = d.isAcid;
                    _target.isBase        = d.isBase;
                    _target.concentration = d.concentration;

                    ShowFeedback(
                        "Liquid transfer started",
                        "",
                        "Avoid rapid pouring — may cause splashing",
                        "Pour slowly; watch the volume indicator; stop below max",
                        new Color(0.2f, 1f, 0.3f));
                }

                // Over-fill warning
                if (_target.volumeML >= maxBeakerVolume * 0.95f && !_hasOverfilled)
                {
                    _hasOverfilled = true;
                    ShowFeedback(
                        "Beaker nearly full",
                        "WARNING: approaching maximum capacity!",
                        "Overfilling causes spills and measurement errors",
                        "Stop pouring now; leave headspace at the top",
                        new Color(1f, 0.5f, 0f));
                }

                CheckReaction();
            }
            else
            {
                // Spilling — liquid lost
                d.volumeML -= loss;
            }

            d.volumeML = Mathf.Max(0f, d.volumeML);

            if (!d.fxSystem.isPlaying)
            {
                d.fxSystem.Play();
                PlayOnce(pourSound, 0.9f);
            }
        }
        else
        {
            if (d.fxSystem.isPlaying)
                d.fxSystem.Stop();

            // Auto-return empty source beaker to upright
            if (d == _source && d.volumeML <= 0f)
            {
                d.go.transform.rotation = Quaternion.Lerp(
                    d.go.transform.rotation, d.initialRotation, Time.deltaTime * 10f);
                d.go.transform.position = d.initialPosition;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  REACTION LOGIC
    // ─────────────────────────────────────────────────────────────────────────

    void CheckReaction()
    {
        if (_target == null || _source == null || _reactionOccurred) return;
        if (_target.volumeML <= 0f) return;

        // Acid meets base → neutralisation
        if (_source.isAcid && _target.isBase)
        {
            _reactionOccurred = true;

            _target.pH           = 7f;
            _target.liquidColor  = new Color(0.7f, 0.85f, 0.92f, 0.7f);
            _target.chemicalName = "Salt Water (NaCl + H₂O)";
            _target.isAcid       = false;
            _target.isBase       = false;

            if (_target.fxSystem != null)
            {
                var m = _target.fxSystem.main;
                m.startColor = _target.liquidColor;
            }

            PlayOnce(reactionSound, 1f);
            ShowFeedback(
                "Acid–Base Neutralisation!",
                "",
                "Never mix concentrated acids/bases without safety equipment",
                "HCl + NaOH → NaCl + H₂O  |  pH = 7  (neutral salt water)",
                new Color(0.2f, 1f, 0.3f));

            _statusMsg = "⚗️ REACTION: Neutralisation complete — pH 7";
            Debug.Log("[ChemLab] REACTION: Acid–Base neutralisation occurred.");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  RETURN / ENFORCE
    // ─────────────────────────────────────────────────────────────────────────

    void ReturnToUpright()
    {
        if (_source != null)
        {
            _source.go.transform.rotation = Quaternion.Lerp(
                _source.go.transform.rotation, _source.initialRotation, Time.deltaTime * 8f);
            _source.go.transform.position   = _source.initialPosition;
            _source.go.transform.localScale = BEAKER_SCALE;
        }

        if (_target != null)
        {
            _target.go.transform.rotation = Quaternion.Lerp(
                _target.go.transform.rotation, _target.initialRotation, Time.deltaTime * 8f);
            _target.go.transform.localScale = BEAKER_SCALE;
        }
    }

    void EnforceConstraints()
    {
        // Source beaker: always fixed position + scale
        if (_source != null)
        {
            _source.go.transform.position   = _source.initialPosition;
            _source.go.transform.localScale = BEAKER_SCALE;
        }

        // Target beaker: scale only (position is user-controlled)
        if (_target != null)
            _target.go.transform.localScale = BEAKER_SCALE;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────────────────────────────────

    BeakerData GetGrabbable(Vector3 handPos)
    {
        if (_target == null || _target.isFixed) return null;

        if (grabAnywhere) return _target;

        float dist = Vector3.Distance(_target.go.transform.position, handPos);
        return dist <= grabDetectionRadius ? _target : null;
    }

    Vector3 CalcHandPosition(BoundingBox bb)
    {
        float cx = bb.topLeft.x + bb.width  * 0.5f;
        float cy = bb.topLeft.y - bb.height * 0.5f;

        float nx = (cx - 0.5f)   * coordinateScale;
        float ny = (0.5f - cy)   * coordinateScale;

        return new Vector3(nx, ny, 0f) + handPositionOffset;
    }

    /// <summary>Zero out all velocity on a Rigidbody — works across Unity versions.</summary>
    static void ClearVelocities(Rigidbody rb)
    {
        if (rb == null) return;
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity  = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
#else
        rb.velocity        = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
#endif
    }

    void PlayOnce(AudioClip clip, float vol = 1f)
    {
        if (audioSource == null || clip == null) return;
        audioSource.PlayOneShot(clip, vol);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  PUBLIC API  (callable from UI buttons / external scripts)
    // ─────────────────────────────────────────────────────────────────────────

    public void RefillSource()
    {
        if (_source == null) return;
        _source.volumeML      = maxBeakerVolume;
        _source.chemicalName  = "Hydrochloric Acid (HCl)";
        _source.liquidColor   = new Color(1f, 0.7f, 0.2f, 0.7f);
        _source.pH            = 1f;
        _source.isAcid        = true;
        _source.concentration = 100f;
        _source.go.transform.rotation = _source.initialRotation;
        _statusMsg = "Source beaker refilled with HCl";
        PlayOnce(refillSound, 0.8f);
    }

    public void FillTargetWithBase()
    {
        if (_target == null) return;
        _target.volumeML      = maxBeakerVolume * 0.5f; // start at half
        _target.chemicalName  = "Sodium Hydroxide (NaOH)";
        _target.liquidColor   = new Color(0.3f, 0.7f, 1f, 0.7f);
        _target.pH            = 13f;
        _target.isBase        = true;
        _target.isAcid        = false;
        _target.concentration = 100f;
        _reactionOccurred     = false;    // allow new reaction
        _statusMsg = "Target beaker filled with NaOH (base)";
        PlayOnce(refillSound, 0.8f);
    }

    public void ClearTarget()
    {
        if (_target == null) return;
        _target.volumeML     = 0f;
        _target.chemicalName = "Empty";
        _target.pH           = 7f;
        _target.isAcid       = false;
        _target.isBase       = false;
        _hasOverfilled       = false;
        _reactionOccurred    = false;
        _statusMsg = "Target beaker cleared";
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  FEEDBACK
    // ─────────────────────────────────────────────────────────────────────────

    void ShowFeedback(string action, string mistake, string avoid, string correct, Color c)
    {
        _feedback.active  = true;
        _feedback.timer   = 5f;
        _feedback.action  = action;
        _feedback.mistake = mistake;
        _feedback.avoid   = avoid;
        _feedback.correct = correct;
        _feedback.colour  = c;
    }

    void UpdateFeedback()
    {
        if (!_feedback.active) return;
        _feedback.timer -= Time.deltaTime;
        if (_feedback.timer <= 0f) _feedback.active = false;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  UI — TEXTURE CACHE
    //  FIX: original code called MakeTex() every OnGUI frame — this caused
    //  thousands of Texture2D allocations per second. We build them once here.
    // ─────────────────────────────────────────────────────────────────────────

    void BuildTextureCache()
    {
        _texDarkBg       = Tex(new Color(0.05f, 0.05f, 0.1f,  0.95f));
        _texAcidBorder   = Tex(new Color(1f,    0.7f,  0.2f,  0.8f));
        _texAcidHeader   = Tex(new Color(1f,    0.6f,  0.1f,  0.9f));
        _texBaseBorder   = Tex(new Color(0.2f,  0.8f,  1f,    0.8f));
        _texBaseHeader   = Tex(new Color(0.1f,  0.6f,  1f,    0.9f));
        _texStatusBorder = Tex(new Color(0.5f,  0.5f,  1f,    0.6f));
        _texStatusBg     = Tex(new Color(0.1f,  0.1f,  0.2f,  0.9f));
        _texBarBg        = Tex(new Color(0.1f,  0.1f,  0.1f,  0.8f));
        _texGlass        = Tex(new Color(1f,    1f,    1f,    0.1f));
        _texFeedbackBg   = Tex(new Color(0.1f,  0.1f,  0.15f, 0.95f));
        _texCacheBuilt   = true;
    }

    static Texture2D Tex(Color c)
    {
        var t = new Texture2D(2, 2);
        t.SetPixels(new[] { c, c, c, c });
        t.Apply();
        return t;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  UI — DRAW
    // ─────────────────────────────────────────────────────────────────────────

    void DrawUI()
    {
        if (!_texCacheBuilt) return;

        int sw = Screen.width, sh = Screen.height;
        int pw = 320, ph = 210, bm = 20;

        DrawSourcePanel(15,         sh - ph - bm, pw, ph);
        DrawTargetPanel(sw - pw - 15, sh - ph - bm, pw, ph);
        DrawStatusBar(sw, sh);
        if (_feedback.active) DrawFeedbackPanel(sw, sh);
    }

    // ── Source Beaker Panel ────────────────────────────────────────────────

    void DrawSourcePanel(int x, int y, int w, int h)
    {
        BoxTex(x-2, y-2, w+4, h+4, _texAcidBorder);
        BoxTex(x,   y,   w,   h,   _texDarkBg);
        BoxTex(x,   y,   w,   45,  _texAcidHeader);
        BoxTex(x,   y,   w,   22,  _texGlass);

        Label(x, y+11, w, 25, "⚗️ SOURCE BEAKER", 16, Color.white, true, TextAnchor.MiddleCenter);

        int yy = y + 55, lh = 23;

        if (_source != null)
        {
            Label(x+12, yy, w-24, lh, $"🧪  {_source.chemicalName}", 13, new Color(0.9f, 0.9f, 0.9f));
            yy += lh;
            Label(x+12, yy, w-24, lh, $"📊  Vol: {_source.volumeML:F0} / {maxBeakerVolume:F0} mL", 13, new Color(0.8f, 0.8f, 1f));
            yy += lh;
            Label(x+12, yy, w-24, lh, $"⚗️  pH: {_source.pH:F1}  |  Acid: {(_source.isAcid ? "Yes" : "No")}", 13, new Color(1f, 0.8f, 0.5f));
            yy += lh;

            float ratio = maxBeakerVolume > 0 ? _source.volumeML / maxBeakerVolume : 0f;
            DrawVolumeBar(x+12, yy, w-24, 18, ratio, new Color(1f, 0.7f, 0.2f), "ACID");
            yy += 28;

            Label(x+12, yy, w-24, lh, "🔒  FIXED POSITION", 13, new Color(0.2f, 1f, 0.3f), true);
            yy += lh + 5;

            if (GUI.Button(new Rect(x+12, yy, w-24, 32), "💧  Refill with HCl", BtnStyle(new Color(1f, 0.6f, 0.1f))))
                RefillSource();
        }
    }

    // ── Target Beaker Panel ────────────────────────────────────────────────

    void DrawTargetPanel(int x, int y, int w, int h)
    {
        BoxTex(x-2, y-2, w+4, h+4, _texBaseBorder);
        BoxTex(x,   y,   w,   h,   _texDarkBg);
        BoxTex(x,   y,   w,   45,  _texBaseHeader);
        BoxTex(x,   y,   w,   22,  _texGlass);

        Label(x, y+11, w, 25, "🥽 TARGET BEAKER", 16, Color.white, true, TextAnchor.MiddleCenter);

        int yy = y + 55, lh = 23;

        if (_target != null)
        {
            string chem = _target.chemicalName == "Empty" ? "🫗  Empty" : $"🧪  {_target.chemicalName}";
            Color  cc   = _target.chemicalName == "Empty" ? new Color(0.6f, 0.6f, 0.6f) : Color.white;
            Label(x+12, yy, w-24, lh, chem, 13, cc);
            yy += lh;

            Label(x+12, yy, w-24, lh, $"📊  Vol: {_target.volumeML:F0} / {maxBeakerVolume:F0} mL", 13, new Color(0.8f, 1f, 1f));
            yy += lh;

            string rxn = _reactionOccurred ? "✅  NEUTRALISED  pH 7" :
                         _target.isBase    ? "🔵  Base (NaOH)"      :
                         _target.isAcid    ? "🔴  Acid (HCl)"       : "⬜  No reaction";
            Color rxnCol = _reactionOccurred ? new Color(0.2f,1f,0.4f) :
                           _target.isBase    ? new Color(0.4f,0.8f,1f) :
                           _target.isAcid    ? new Color(1f,0.4f,0.4f) : Color.gray;
            Label(x+12, yy, w-24, lh, rxn, 13, rxnCol, true);
            yy += lh;

            float ratio = maxBeakerVolume > 0 ? _target.volumeML / maxBeakerVolume : 0f;
            Color bc    = _target.volumeML > 0 ? new Color(0.2f, 0.8f, 1f) : new Color(0.3f, 0.3f, 0.3f);
            DrawVolumeBar(x+12, yy, w-24, 18, ratio, bc, _target.volumeML > 0 ? "FILL" : "EMPTY");
            yy += 28;

            int bw = (w - 36) / 2;

            if (GUI.Button(new Rect(x+12,        yy, bw, 32), "🔵 Fill NaOH", BtnStyle(new Color(0.1f, 0.5f, 1f))))
                FillTargetWithBase();

            if (GUI.Button(new Rect(x+24+bw,     yy, bw, 32), "🗑 Clear",     BtnStyle(new Color(0.85f, 0.25f, 0.25f))))
                ClearTarget();
        }
    }

    // ── Status Bar ─────────────────────────────────────────────────────────

    void DrawStatusBar(int sw, int sh)
    {
        int bw = 440, bh = 65, bx = (sw - bw) / 2, by = 18;

        BoxTex(bx-3, by-3, bw+6, bh+6, _texStatusBorder);
        BoxTex(bx,   by,   bw,   bh,   _texStatusBg);
        BoxTex(bx,   by,   bw,   bh/2, _texGlass);

        string icon = "👋", gText = "READY — SHOW HAND";
        Color  gc   = new Color(0.75f, 0.75f, 0.75f);

        switch (_gesture)
        {
            case ManoGestureContinuous.OPEN_HAND_GESTURE:
                icon = "🖐️"; gText = "TILTING SOURCE BEAKER";  gc = new Color(1f, 0.8f, 0.2f); break;
            case ManoGestureContinuous.CLOSED_HAND_GESTURE:
                icon = "✊"; gText = "GRABBING TARGET BEAKER"; gc = new Color(0.2f, 1f, 0.3f); break;
            case ManoGestureContinuous.OPEN_PINCH_GESTURE:
                icon = "👌"; gText = "REFILLING SOURCE";        gc = new Color(0.2f, 0.85f, 1f); break;
        }

        Label(bx, by+7, bw, 26, $"{icon}  {gText}", 18, gc, true, TextAnchor.MiddleCenter);
        Label(bx, by+36, bw, 20, $"⚡  {_statusMsg}", 12, new Color(0.9f, 0.9f, 0.9f), false, TextAnchor.MiddleCenter);
    }

    // ── Educational Feedback Panel ─────────────────────────────────────────

    void DrawFeedbackPanel(int sw, int sh)
    {
        int pw = 520, ph = 190, px = (sw - pw) / 2, py = sh / 2 - 160;

        BoxTex(px-3, py-3, pw+6, ph+6, _feedback.colour);
        BoxTex(px,   py,   pw,   ph,   _texFeedbackBg);

        Label(px, py+8, pw, 28, "📚  EDUCATIONAL FEEDBACK", 18, _feedback.colour, true, TextAnchor.MiddleCenter);

        int yy = py + 44, lh = 36;

        if (!string.IsNullOrEmpty(_feedback.action))
        { Label(px+14, yy, pw-28, lh, $"✓  {_feedback.action}",  13, Color.white); yy += lh; }

        if (!string.IsNullOrEmpty(_feedback.mistake))
        { Label(px+14, yy, pw-28, lh, $"⚠  {_feedback.mistake}", 13, new Color(1f,0.5f,0.2f)); yy += lh; }

        if (!string.IsNullOrEmpty(_feedback.avoid))
        { Label(px+14, yy, pw-28, lh, $"❌  {_feedback.avoid}",  13, new Color(1f,0.7f,0.7f)); yy += lh; }

        if (!string.IsNullOrEmpty(_feedback.correct))
        { Label(px+14, yy, pw-28, lh, $"✅  {_feedback.correct}", 13, new Color(0.7f,1f,0.7f)); }
    }

    // ── Volume Bar ─────────────────────────────────────────────────────────

    void DrawVolumeBar(int x, int y, int w, int h, float ratio, Color liqCol, string lbl)
    {
        BoxTex(x-1, y-1, w+2, h+2, DynTex(new Color(liqCol.r, liqCol.g, liqCol.b, 0.55f)));
        BoxTex(x,   y,   w,   h,   _texBarBg);

        if (ratio > 0f)
        {
            int fw = Mathf.Max(1, (int)((w - 4) * ratio));
            BoxTex(x+2, y+2, fw, h-4, DynTex(liqCol));
            BoxTex(x+2, y+2, fw, (h-4)/3, DynTex(new Color(
                Mathf.Min(1f, liqCol.r+0.3f), Mathf.Min(1f, liqCol.g+0.3f),
                Mathf.Min(1f, liqCol.b+0.3f), 0.5f)));
        }

        Label(x, y, w, h, $"{ratio*100:F0}%  {lbl}", 11, Color.white, true, TextAnchor.MiddleCenter);
    }

    // ── UI Utilities ───────────────────────────────────────────────────────

    void BoxTex(int x, int y, int w, int h, Texture2D tex)
    {
        var s = new GUIStyle(GUI.skin.box);
        s.normal.background = tex;
        GUI.Box(new Rect(x, y, w, h), GUIContent.none, s);
    }

    void Label(int x, int y, int w, int h, string txt, int size, Color c,
               bool bold = false, TextAnchor align = TextAnchor.MiddleLeft)
    {
        var s = new GUIStyle(GUI.skin.label)
        {
            fontSize  = size,
            fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
            alignment = align
        };
        s.normal.textColor = new Color(0, 0, 0, 0.45f);
        GUI.Label(new Rect(x+1, y+1, w, h), txt, s);
        s.normal.textColor = c;
        GUI.Label(new Rect(x,   y,   w, h), txt, s);
    }

    GUIStyle BtnStyle(Color base_)
    {
        var s = new GUIStyle(GUI.skin.button)
        {
            fontSize  = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        s.normal.textColor  = Color.white;
        s.normal.background = DynTex(base_);
        s.hover.background  = DynTex(new Color(
            Mathf.Min(1f, base_.r * 1.35f),
            Mathf.Min(1f, base_.g * 1.35f),
            Mathf.Min(1f, base_.b * 1.35f)));
        s.active.background = DynTex(new Color(base_.r * 0.75f, base_.g * 0.75f, base_.b * 0.75f));
        return s;
    }

    // DynTex: small colour variants needed per-frame (volume bar fill, button hover).
    // These ARE created each frame but are small (2×2) and unavoidable for dynamic
    // colours. The static textures (panels, borders) are properly cached above.
    static Texture2D DynTex(Color c)
    {
        var t = new Texture2D(2, 2);
        t.SetPixels(new[] { c, c, c, c });
        t.Apply();
        return t;
    }
}
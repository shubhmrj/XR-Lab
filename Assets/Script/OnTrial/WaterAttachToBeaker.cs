using UnityEngine;
using ManoMotion;
using System.Collections.Generic;

public class WaterAttachToBeaker : MonoBehaviour
{
    [Header("Chemistry Lab Setup")]
    [SerializeField] private GameObject sourceBeaker; // Fixed beaker (source)
    [SerializeField] private GameObject targetBeaker; // Movable beaker (target)
    [SerializeField] private GameObject waterParticlesPrefab;
    [SerializeField] private float grabDetectionRadius = 7.5f; // INCREASED: Better grab detection from further away
    
    [Header("Pour Points - Assign These in Unity Inspector")]
    [SerializeField] public Transform sourcePourPoint; // ASSIGN THIS in Unity Inspector
    [SerializeField] public Transform targetPourPoint; // ASSIGN THIS in Unity Inspector
    
    [Header("Chemistry Settings")]
    [SerializeField] private float maxBeakerVolume = 500f; // mL
    [SerializeField] private float pourRate = 250f; // mL per second (250mL precision)
    [SerializeField] private float pouringDistance = 2.0f; // Distance for beaker-to-beaker pouring

    [Header("Pouring Settings")]
    [SerializeField] private float pouringThresholdAngle = 25f;
    [SerializeField] private float maxPourRate = 100.0f;
    [SerializeField] public Color waterColor = new Color(0.7f, 0.85f, 0.92f, 0.7f);

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 22f; // Increased for better responsiveness (tuned for snappier grabs)
    [SerializeField] private float tiltSmoothSpeed = 20f; // Faster tilt response
    [SerializeField] private float maxTiltAngle = 60f;
    [SerializeField] private Vector3 handPositionOffset = new Vector3(0, 0f, 8f);
    [SerializeField] private float coordinateScale = 10f;  // INCREASED: Better hand position mapping (was 4f)
    [SerializeField] private bool isLandscapeMode = true;

    [Tooltip("If enabled, the grabbed beaker will follow the hand depth (Z). Disable to keep fixed depth.)")]
    [SerializeField] private bool followHandDepth = true;
    [Tooltip("Smoothing speed used when following hand depth to reduce jitter")]
    [SerializeField] private float depthSmoothSpeed = 8f; // reasonable default (higher = snappier) 

    [Tooltip("Max distance considered a realistic initial offset on grab — larger distances will snap the beaker to the hand to avoid jumps")]
    [SerializeField] private float maxInitialSnapDistance = 0.7f;
    [Tooltip("How quickly the beaker snaps to the hand on first grab (higher = faster)")]
    [SerializeField] private float initialSnapSpeed = 30f;

    [Header("Control Mode")]
    [SerializeField] private bool useGestureControls = true; // Allow disabling ManoMotion gating for XR/controller input
    [SerializeField] private bool autoReturnWhenNoGesture = true; // Optional upright snap when gestures disappear
    [Tooltip("Allow grabbing the target beaker anywhere in the XR scene (no proximity check). Useful for open-world gesture control.")]
    [SerializeField] private bool grabAnywhere = true;
    
    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip pourSound;
    [SerializeField] private AudioClip refillSound;
    [SerializeField] private AudioClip reactionSound;

    // Chemistry beaker data structure
    private class ChemistryBeaker
    {
        public GameObject beakerObject;
        public Transform pourPoint;
        public GameObject waterEffectObj;
        public ParticleSystem waterEffect;
        public ParticleSystem splashEffect;
        // Original parent saved so we can detach during grab and restore on release
        public Transform originalParent;
        public float volumeML = 500f;
        public Vector3 initialPosition;
        public Quaternion initialRotation;
        public Vector3 lastEmitPosition;
        public Vector3 grabOffset; // offset between hand and object to avoid jumps on acquire
        public bool isGrabbed = false;
        public bool isFixed = false;
        public Color liquidColor;
        public Rigidbody rb; // cached Rigidbody if present
        public bool wasKinematic = false; // original kinematic state
        public string chemicalName = "Water";
        public float concentration = 100f;
        public float pH = 7.0f;
        public bool isAcid = false;
        public bool isBase = false;
    }

    // Educational feedback system
    private class EducationalFeedback
    {
        public string actionPerformed = "";
        public string mistakeMade = "";
        public string whatToAvoid = "";
        public string correctProcedure = "";
        public bool showFeedback = false;
        public float feedbackTimer = 0f;
        public Color feedbackColor = Color.white;
    }

    // Reaction tracking
    private class ReactionData
    {
        public bool reactionOccurred = false;
        public string reactionType = "None";
        public float resultingPH = 7.0f;
        public string productName = "";
        public Color productColor = Color.clear;
        public bool isNeutralized = false;
    }

    private ChemistryBeaker sourceBeakerData = null;
    private ChemistryBeaker targetBeakerData = null;
    private ChemistryBeaker currentlyGrabbedBeaker = null;
    private bool isPouringBetweenBeakers = false;
    private Vector3 FIXED_BEAKER_SCALE = new Vector3(8f, 8f, 8f); // INCREASED SIZE for better visibility
    
    // Current gesture tracking
    private ManoGestureContinuous currentGesture = ManoGestureContinuous.NO_GESTURE;
    private string systemStatus = "Chemistry Lab Ready";
    
    // Educational and reaction systems
    private EducationalFeedback feedback = new EducationalFeedback();
    private ReactionData reactionData = new ReactionData();
    private float lastTransferAmount = 0f;
    private bool hasOverfilled = false;
    private bool hasSpilled = false;
    // Grace period to handle brief gesture flicker so grabbed object continues moving
    private Vector3 lastHandPosition = Vector3.zero;
    private float lastHandTime = 0f;
    [SerializeField] private float grabGraceDuration = 0.25f; // seconds
    
    [SerializeField] private bool showDebugVisuals = true;

    void Start()
    {
        if (ManoMotionManager.Instance != null)
        {
            ManoMotionManager.Instance.ShouldCalculateGestures(true);
        }
        InitializeBeakers();
        Debug.Log("Chemistry Lab initialized with Source and Target beakers");
    }

    void InitializeBeakers()
    {
        // Initialize Source Beaker (Fixed)
        if (sourceBeaker != null)
        {
            sourceBeakerData = CreateChemistryBeaker(sourceBeaker, true);
            sourceBeakerData.chemicalName = "Hydrochloric Acid";
            sourceBeakerData.liquidColor = new Color(1f, 0.7f, 0.2f, 0.7f);
            sourceBeakerData.concentration = 100f;
            sourceBeakerData.volumeML = maxBeakerVolume;
            sourceBeakerData.pH = 1.0f;
            sourceBeakerData.isAcid = true;
        }

        // Initialize Target Beaker (Movable)
        if (targetBeaker != null)
        {
            targetBeakerData = CreateChemistryBeaker(targetBeaker, false);
            targetBeakerData.chemicalName = "Empty";
            targetBeakerData.liquidColor = new Color(0.7f, 0.85f, 0.92f, 0.7f);
            targetBeakerData.concentration = 0f;
            targetBeakerData.volumeML = 0f;
            targetBeakerData.pH = 7.0f;
            targetBeakerData.isAcid = false;
            targetBeakerData.isBase = false;
        }
    }

    ChemistryBeaker CreateChemistryBeaker(GameObject beakerObj, bool isFixed)
    {
        ChemistryBeaker data = new ChemistryBeaker
        {
            beakerObject = beakerObj,
            initialPosition = beakerObj.transform.position,
            initialRotation = beakerObj.transform.rotation,
            isFixed = isFixed,
            liquidColor = waterColor,
            originalParent = beakerObj.transform.parent
        };

        beakerObj.transform.localScale = FIXED_BEAKER_SCALE;

        // Cache Rigidbody (if present) so we can control physics during grabs
        data.rb = beakerObj.GetComponent<Rigidbody>();
        if (data.rb != null)
        {
            data.wasKinematic = data.rb.isKinematic;
        }

        // Use assigned pour points from Inspector
        if (beakerObj == sourceBeaker && sourcePourPoint != null)
        {
            data.pourPoint = sourcePourPoint;
            Debug.Log("Using assigned SOURCE pour point from Inspector");
        }
        else if (beakerObj == targetBeaker && targetPourPoint != null)
        {
            data.pourPoint = targetPourPoint;
            Debug.Log("Using assigned TARGET pour point from Inspector");
        }
        else
        {
            // Fallback: Create pour point if not assigned
            GameObject pourPointObj = new GameObject($"PourPoint_{beakerObj.name}");
            pourPointObj.transform.parent = beakerObj.transform;
            pourPointObj.transform.localPosition = new Vector3(0, 0.45f, 0.25f);
            data.pourPoint = pourPointObj.transform;
            Debug.LogWarning($"Pour point not assigned in Inspector for {beakerObj.name}! Using auto-created pour point.");
        }

        // Create water particles and position them at pour point (DO NOT parent to pourPoint — parenting caused culling/visibility issues)
        if (waterParticlesPrefab != null && data.pourPoint != null)
        {
            data.waterEffectObj = Instantiate(waterParticlesPrefab, data.pourPoint.position, data.pourPoint.rotation);
            data.waterEffectObj.name = $"ChemicalEffect_{beakerObj.name}";
            // ensure transform aligns with pour point in world space
            data.waterEffectObj.transform.position = data.pourPoint.position;
            data.waterEffectObj.transform.rotation = data.pourPoint.rotation;
            // ensure it is not parented to avoid scale/visibility inheritance
            data.waterEffectObj.transform.SetParent(null);
            data.waterEffect = data.waterEffectObj.GetComponent<ParticleSystem>();
            if (data.waterEffect != null)
            {
                var main = data.waterEffect.main;
                main.startColor = data.liquidColor;
                data.waterEffect.Stop();
            }
        }
        return data;
    }

    void Update()
    {
        // Gesture path (can be disabled for XR/controller use)
        if (useGestureControls && ManoMotionManager.Instance != null && ManoMotionManager.Instance.HandInfos != null)
        {
            HandInfo[] handInfos = ManoMotionManager.Instance.HandInfos;
            bool handledHand = false;

            if (handInfos != null && handInfos.Length > 0)
            {
                // Process only the first valid detected hand to avoid conflicting state
                foreach (var handInfo in handInfos)
                {
                    if (handInfo.gestureInfo.manoClass == ManoClass.NO_HAND) continue;

                    Vector3 handPosition = CalculateHandPosition(handInfo.trackingInfo.boundingBox);
                    ManoGestureContinuous gesture = handInfo.gestureInfo.manoGestureContinuous;
                    currentGesture = gesture;

                    BoundingBox boundingBox = handInfo.trackingInfo.boundingBox;
                    float centerX = boundingBox.topLeft.x + boundingBox.width / 2f;
                    float centerY = boundingBox.topLeft.y - boundingBox.height / 2f;
                    float normalizedX = (centerX - 0.5f);

                    switch (gesture)
                    {
                        case ManoGestureContinuous.OPEN_HAND_GESTURE:
                            // Open hand releases any grabbed beaker so user can place it and then tilt to pour
                            ReleaseAllBeakers();
                            HandleTiltGesture(normalizedX);
                            break;
                        case ManoGestureContinuous.OPEN_PINCH_GESTURE:
                            HandleRefillGesture();
                            break;
                                case ManoGestureContinuous.CLOSED_HAND_GESTURE:
                                    // Update last hand data to prevent immediate drop on brief detection loss
                                    lastHandPosition = handPosition;
                                    lastHandTime = Time.time;
                                    HandleGrabGesture(handPosition);
                                    break;
                        default:
                            // ignore other gestures for the moment
                            break;
                    }

                    handledHand = true;
                    break; // only handle the primary hand
                }
            }

            // If no valid hand processed, consider grab-grace fallback before releasing
            if (!handledHand)
            {
                // If we recently had a hand for grab, continue moving the beaker for a short grace period
                if (currentlyGrabbedBeaker != null && Time.time - lastHandTime <= grabGraceDuration)
                {
                    // continue moving based on last known hand position
                    HandleGrabGesture(lastHandPosition);
                }
                else
                {
                    currentGesture = ManoGestureContinuous.NO_GESTURE;
                    if (autoReturnWhenNoGesture)
                    {
                        ReleaseAllBeakers();
                        ReturnBeakersToInitialPosition();
                    }
                    systemStatus = "Chemistry Lab Ready";
                }
            }

            // Always update pouring check after possible movement
            CheckBeakerToBeakerPouring();
        }

        UpdateWaterPouring(); // Always keep pouring logic alive, even without gestures
        UpdateEducationalFeedback(); // Update feedback timer
        EnforceScaleLock(); // Enforce scale lock once at end of frame
    }

    void EnforceScaleLock()
    {
        if (sourceBeakerData?.beakerObject != null)
        {
            sourceBeakerData.beakerObject.transform.localScale = FIXED_BEAKER_SCALE;
            // CRITICAL: LOCK SOURCE BEAKER POSITION - it should never move from initial position
            sourceBeakerData.beakerObject.transform.position = sourceBeakerData.initialPosition;
            
            // Debug info to track source beaker behavior
            if (showDebugVisuals && Vector3.Distance(sourceBeakerData.beakerObject.transform.position, sourceBeakerData.initialPosition) > 0.01f)
            {
                Debug.LogError($"SOURCE BEAKER MOVED! Resetting to {sourceBeakerData.initialPosition}");
            }
        }
        if (targetBeakerData?.beakerObject != null)
        {
            targetBeakerData.beakerObject.transform.localScale = FIXED_BEAKER_SCALE;
        }
    }

    ChemistryBeaker GetNearestGrabbableBeaker(Vector3 handPosition)
    {
        // ONLY TARGET BEAKER CAN BE GRABBED - SOURCE IS ALWAYS FIXED
        if (targetBeakerData?.beakerObject != null && !targetBeakerData.isFixed)
        {
            // Global grab mode: ignore distance and return target directly
            if (grabAnywhere)
            {
                if (showDebugVisuals) Debug.Log($"[GRAB_ANYWHERE] {targetBeakerData.beakerObject.name} grabbed (global mode)");
                return targetBeakerData;
            }

            Vector3 beakerPos = targetBeakerData.beakerObject.transform.position;
            float distance = Vector3.Distance(beakerPos, handPosition);
            
            if (showDebugVisuals && Time.frameCount % 30 == 0)
            {
                Debug.Log($"GRAB_CHECK: Hand@{handPosition}, Beaker@{beakerPos}, Dist={distance:F2}, Radius={grabDetectionRadius}");
            }
            
            if (distance <= grabDetectionRadius)
            {
                if (showDebugVisuals) Debug.Log($"✓ GRAB_SUCCESS: {targetBeakerData.beakerObject.name} (dist: {distance:F2}m)");
                return targetBeakerData;
            }
            else
            {
                if (showDebugVisuals && Time.frameCount % 60 == 0) 
                    Debug.LogWarning($"✗ GRAB_OUT_OF_REACH: dist={distance:F2}m vs threshold={grabDetectionRadius}m");
            }
        }
        else
        {
            if (showDebugVisuals && Time.frameCount % 60 == 0) 
                Debug.LogWarning("✗ GRAB_FAILED: Target beaker missing or marked as FIXED");
        }

        // NEVER return source beaker - it should always be fixed
        return null;
    }

    void HandleGrabGesture(Vector3 handPosition)
    {
        if (currentlyGrabbedBeaker == null)
        {
            currentlyGrabbedBeaker = GetNearestGrabbableBeaker(handPosition);
            if (currentlyGrabbedBeaker != null)
            {
                currentlyGrabbedBeaker.isGrabbed = true;
                systemStatus = $"GRABBED: {currentlyGrabbedBeaker.beakerObject.name}";

                // AUTO-RECOVERY: Ensure the beaker GameObject and its renderers/colliders are enabled
                GameObject bobj = currentlyGrabbedBeaker.beakerObject;
                bool wasActive = bobj.activeInHierarchy;
                if (!wasActive) bobj.SetActive(true);

                var renderers = bobj.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers) r.enabled = true;

                var colliders = bobj.GetComponentsInChildren<Collider>(true);
                foreach (var c in colliders) c.enabled = true;

                // Detach from any parent to avoid parent-based culling or deactivation while grabbed
                if (currentlyGrabbedBeaker.originalParent != null)
                {
                    bobj.transform.SetParent(null, true);
                    if (showDebugVisuals) Debug.Log($"[GRAB_DIAG] Detached {bobj.name} from parent {currentlyGrabbedBeaker.originalParent.name} while grabbed");
                }

                // Diagnostic logging to help track invisibility in XR: position, scale, previous parent, camera distance
                float camDist = Camera.main != null ? Vector3.Distance(Camera.main.transform.position, bobj.transform.position) : -1f;
                if (showDebugVisuals)
                {
                    Debug.Log($">>> GRAB_ACQUIRED: {bobj.name} <<< (wasActive={wasActive}, pos={bobj.transform.position}, scale={bobj.transform.localScale}, previousParent={(currentlyGrabbedBeaker.originalParent!=null?currentlyGrabbedBeaker.originalParent.name:"null")}, camDist={camDist:F2})");
                    if (renderers.Length == 0) Debug.LogWarning($"[GRAB_DIAG] No Renderer found on {bobj.name} - it may be invisible");
                }

                // Compute & store offset between beaker and hand to avoid sudden jumps on first-frame grab
                currentlyGrabbedBeaker.grabOffset = bobj.transform.position - handPosition;

                // If Z offset is large, snap the object Z to the hand immediately and update the offset.
                if (Mathf.Abs(currentlyGrabbedBeaker.grabOffset.z) > maxInitialSnapDistance)
                {
                    Vector3 snapped = bobj.transform.position;
                    snapped.z = handPosition.z; // align depth with hand to avoid being stuck behind
                    bobj.transform.position = snapped;
                    // recompute offset after snapping
                    currentlyGrabbedBeaker.grabOffset = bobj.transform.position - handPosition;
                    if (showDebugVisuals) Debug.Log($"[GRAB_SNAP_DEPTH] Snapped Z from offset -> newPos={bobj.transform.position}, newOffset.z={currentlyGrabbedBeaker.grabOffset.z:F2}");
                }
                else if (currentlyGrabbedBeaker.grabOffset.magnitude > maxInitialSnapDistance)
                {
                    Vector3 targetPos = handPosition + currentlyGrabbedBeaker.grabOffset.normalized * maxInitialSnapDistance;
                    bobj.transform.position = Vector3.Lerp(bobj.transform.position, targetPos, Mathf.Clamp(Time.deltaTime * initialSnapSpeed, 0f, 1f));
                    // update offset after the snap attempt so future frames use smaller offset
                    currentlyGrabbedBeaker.grabOffset = bobj.transform.position - handPosition;
                    if (showDebugVisuals) Debug.Log($"[GRAB_SNAP] large offset {currentlyGrabbedBeaker.grabOffset.magnitude:F2}m — snapping towards hand (newPos={bobj.transform.position})");
                }

                // Make Rigidbody kinematic while grabbed so transforms are authoritative
                if (currentlyGrabbedBeaker.rb != null)
                {
                    currentlyGrabbedBeaker.rb.isKinematic = true;
                    // stop residual physics velocities so the object doesn't fight the hand
                    ClearRigidbodyVelocities(currentlyGrabbedBeaker.rb);
                    if (showDebugVisuals) Debug.Log("[GRAB_PHYSICS] Rigidbody found, set isKinematic=true and cleared velocities");
                }

                // Enforce fixed scale to avoid parent-induced scaling issues
                bobj.transform.localScale = FIXED_BEAKER_SCALE;
                if (showDebugVisuals) Debug.Log($"[GRAB_STATE] Forced scale {bobj.name} => {FIXED_BEAKER_SCALE}");
            }
            else
            {
                systemStatus = "GRAB FAILED - Check hand position and beaker proximity";
                if (showDebugVisuals) Debug.LogWarning($">>> GRAB_FAILED: Cannot reach target beaker <<<");
            }
        }

        // Move the grabbed beaker (only target beaker can be moved)
        if (currentlyGrabbedBeaker != null && !currentlyGrabbedBeaker.isFixed)
        {
            // Validate hand position is not NaN or Infinity
            if (float.IsNaN(handPosition.x) || float.IsNaN(handPosition.y) || float.IsNaN(handPosition.z))
            {
                if (showDebugVisuals) Debug.LogError("!!! INVALID_HAND: NaN detected! Using last position !!!");
                handPosition = lastHandPosition;
            }
            
            // Use stored grabOffset so the object follows the hand without sudden leaps
            Vector3 desiredPos = handPosition + currentlyGrabbedBeaker.grabOffset;

            // Depth handling: apply smoothed Z if followHandDepth is enabled
            if (followHandDepth)
            {
                float smoothedZ = Mathf.Lerp(currentlyGrabbedBeaker.beakerObject.transform.position.z, handPosition.z + currentlyGrabbedBeaker.grabOffset.z, Time.deltaTime * depthSmoothSpeed);
                desiredPos.z = smoothedZ;
            }
            else
            {
                desiredPos.z = currentlyGrabbedBeaker.beakerObject.transform.position.z; // keep previous Z
            }

            // Diagnostic: large depth discrepancy between hand and object
            if (showDebugVisuals && Mathf.Abs(desiredPos.z - handPosition.z) > 0.5f)
            {
                Debug.LogWarning($"[GRAB_DIAG] Depth delta large: desiredZ={desiredPos.z:F2}, handZ={handPosition.z:F2}");
            }

            // Apply safety bounds if enabled
            if (enableSafetyBounds)
            {
                desiredPos.x = Mathf.Clamp(desiredPos.x, minBounds.x, maxBounds.x);
                desiredPos.y = Mathf.Clamp(desiredPos.y, minBounds.y, maxBounds.y);
                desiredPos.z = Mathf.Clamp(desiredPos.z, minBounds.z, maxBounds.z);
            }

            // Move towards the desired position with a max delta per frame for snappy, stable following
            Vector3 currentPos = currentlyGrabbedBeaker.beakerObject.transform.position;
            float maxDelta = moveSpeed * Time.deltaTime;
            Vector3 newPos = Vector3.MoveTowards(currentPos, desiredPos, maxDelta);
            currentlyGrabbedBeaker.beakerObject.transform.position = newPos;
            
            // Keep beaker upright while grabbing
            currentlyGrabbedBeaker.beakerObject.transform.rotation = Quaternion.Lerp(
                currentlyGrabbedBeaker.beakerObject.transform.rotation,
                currentlyGrabbedBeaker.initialRotation,
                Time.deltaTime * 8f
            );
            
            // Force scale after movement
            currentlyGrabbedBeaker.beakerObject.transform.localScale = FIXED_BEAKER_SCALE;

            // If beaker accidentally became inactive or renderers disabled during movement, recover it
            GameObject moveObj = currentlyGrabbedBeaker.beakerObject;
            if (!moveObj.activeInHierarchy)
            {
                moveObj.SetActive(true);
                if (showDebugVisuals) Debug.LogWarning($"[GRAB_DIAG] Reactivated {moveObj.name} during movement (was inactive)");
            }

            var moveRenderers = moveObj.GetComponentsInChildren<Renderer>(true);
            bool anyDisabled = false;
            foreach (var r in moveRenderers) if (!r.enabled) { anyDisabled = true; r.enabled = true; }
            if (anyDisabled && showDebugVisuals) Debug.LogWarning($"[GRAB_DIAG] Re-enabled renderers for {moveObj.name} during movement");

            // Record last hand position/time so we can survive short detection flicker
            lastHandPosition = handPosition;
            lastHandTime = Time.time;

            CheckBeakerToBeakerPouring();
            
            if (showDebugVisuals && Time.frameCount % 15 == 0) 
            {
                Debug.Log($">> MOVING: {currentlyGrabbedBeaker.beakerObject.name} | Pos={currentlyGrabbedBeaker.beakerObject.transform.position}");
            }
        }
        else if (currentlyGrabbedBeaker != null && currentlyGrabbedBeaker.isFixed)
        {
            if (showDebugVisuals) Debug.LogError("!!! ERROR: Grabbed beaker is FIXED! Releasing !!!");
            currentlyGrabbedBeaker = null;
        }
    }

    void HandleTiltGesture(float normalizedX)
    {
        // FIXED: ONLY tilt the source beaker (fixed beaker) - never tilt target beaker
        ChemistryBeaker beakerToTilt = sourceBeakerData;

        if (beakerToTilt != null)
        {
            systemStatus = $"Tilting: {beakerToTilt.beakerObject.name} - Volume: {beakerToTilt.volumeML:F0}mL";
            
            // Enhanced tilt input with better control
            float tiltInput = normalizedX * 1.5f; // Reduced multiplier for better control
            float desiredTiltZ = Mathf.Clamp(tiltInput * maxTiltAngle, -maxTiltAngle, maxTiltAngle);
            
            float currentTiltZ = beakerToTilt.beakerObject.transform.eulerAngles.z;
            if (currentTiltZ > 180f) currentTiltZ -= 360f;
            
            float angleDiff = desiredTiltZ - currentTiltZ;
            if (Mathf.Abs(angleDiff) > 0.1f) // More sensitive response
            {
                float rotateAmount = angleDiff * Time.deltaTime * tiltSmoothSpeed;
                
                // Use pour point if available, otherwise use beaker center
                Vector3 rotationPoint = beakerToTilt.pourPoint != null ? 
                    beakerToTilt.pourPoint.position : 
                    beakerToTilt.beakerObject.transform.position;
                    
                beakerToTilt.beakerObject.transform.RotateAround(
                    rotationPoint,
                    Vector3.forward,
                    rotateAmount
                );
                
                Debug.Log($"TILT: {beakerToTilt.beakerObject.name} angle: {currentTiltZ:F1}° → {desiredTiltZ:F1}°");
            }
            
            // Maintain position lock for source beaker (don't let it move)
            if (beakerToTilt == sourceBeakerData)
            {
                beakerToTilt.beakerObject.transform.position = beakerToTilt.initialPosition;
            }
            
            // Force scale after rotation
            beakerToTilt.beakerObject.transform.localScale = FIXED_BEAKER_SCALE;
        }
        else
        {
            systemStatus = "No beaker available for tilting";
        }
    }
    
    void ReturnBeakersToInitialPosition()
    {
        // FAST return to upright position for source beaker
        if (sourceBeakerData != null)
        {
            // Faster rotation return with better smoothing
            sourceBeakerData.beakerObject.transform.rotation = Quaternion.Lerp(
                sourceBeakerData.beakerObject.transform.rotation,
                sourceBeakerData.initialRotation,
                Time.deltaTime * 8f // Much faster return
            );
            // Lock position - source beaker should never move
            sourceBeakerData.beakerObject.transform.position = sourceBeakerData.initialPosition;
            sourceBeakerData.beakerObject.transform.localScale = FIXED_BEAKER_SCALE;
            
            // Check if we're close to upright position
            float currentAngle = Mathf.Abs(sourceBeakerData.beakerObject.transform.eulerAngles.z);
            if (currentAngle > 180f) currentAngle = 360f - currentAngle;
            
            if (currentAngle < 2f) // Close to upright
            {
                // Snap to exact upright position
                sourceBeakerData.beakerObject.transform.rotation = sourceBeakerData.initialRotation;
                Debug.Log("SOURCE BEAKER: Returned to upright position");
            }
        }
        
        // Return target beaker to upright position (but keep its current position)
        if (targetBeakerData != null)
        {
            targetBeakerData.beakerObject.transform.rotation = Quaternion.Lerp(
                targetBeakerData.beakerObject.transform.rotation,
                targetBeakerData.initialRotation,
                Time.deltaTime * 8f // Much faster return
            );
            targetBeakerData.beakerObject.transform.localScale = FIXED_BEAKER_SCALE;
        }
    }

    void HandleRefillGesture()
    {
        // Pinch should ONLY refill the source beaker (main working beaker) - NO DISTANCE CHECK
        if (sourceBeakerData == null)
        {
            Debug.Log($"REFILL FAILED: Source beaker not available");
            systemStatus = "Source beaker not available for refilling";
            return;
        }

        ChemistryBeaker beakerToRefill = sourceBeakerData;
        string beakerName = "Source";

        float currentVolume = beakerToRefill.volumeML;
        float refillAmount = pourRate * Time.deltaTime * 2f; // Faster refill: 500mL per second
        beakerToRefill.volumeML = Mathf.Min(maxBeakerVolume, beakerToRefill.volumeML + refillAmount);
        
        if (beakerToRefill.volumeML > 0)
        {
            beakerToRefill.chemicalName = "Hydrochloric Acid";
            beakerToRefill.liquidColor = new Color(1f, 0.7f, 0.2f, 0.7f); // Orange for acid
        }
        
        // Play refill sound
        if (audioSource != null && refillSound != null)
        {
            try
            {
                audioSource.PlayOneShot(refillSound, 0.8f);
                if (showDebugVisuals) Debug.Log("[AUDIO_SUCCESS] Refill sound played");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[AUDIO_ERROR] Failed to play refill sound: {ex.Message}");
            }
        }
        
        systemStatus = $"Refilling {beakerName}: {beakerToRefill.volumeML:F0}mL / {maxBeakerVolume:F0}mL";
        Debug.Log($"REFILL SUCCESS: {beakerName} beaker now has {beakerToRefill.volumeML:F0}mL (was {currentVolume:F0}mL)");
    }

    void ReleaseAllBeakers()
    {
        if (currentlyGrabbedBeaker != null)
        {
            // Ensure the beaker is left visible and interactive after release
            ChemistryBeaker temp = currentlyGrabbedBeaker;
            GameObject rObj = temp.beakerObject;
            if (rObj != null)
            {
                if (!rObj.activeInHierarchy) rObj.SetActive(true);
                var rens = rObj.GetComponentsInChildren<Renderer>(true);
                foreach (var r in rens) r.enabled = true;
                var cols = rObj.GetComponentsInChildren<Collider>(true);
                foreach (var c in cols) c.enabled = true;

                // Restore original parent if we detached it
                if (temp.originalParent != null)
                {
                    // Use worldPositionStays=false to avoid inheriting weird world transforms; we'll reapply world position if needed
                    rObj.transform.SetParent(temp.originalParent, false);
                    if (showDebugVisuals) Debug.Log($"[GRAB_RELEASED] Restored parent {temp.originalParent.name} for {rObj.name}");
                }

                // Restore physics state if we changed it during grab
                if (temp.rb != null)
                {
                    // clear any velocities and restore kinematic flag
                    ClearRigidbodyVelocities(temp.rb);
                    temp.rb.isKinematic = temp.wasKinematic;
                    if (showDebugVisuals) Debug.Log("[GRAB_PHYSICS] Restored Rigidbody kinematic state and cleared velocities on release");
                }

                // Enforce fixed scale after reparenting to avoid parent scale propagation
                rObj.transform.localScale = FIXED_BEAKER_SCALE;

                // If the object ended up far outside expected play area, snap it back to initial safe position
                float distFromInit = Vector3.Distance(rObj.transform.position, temp.initialPosition);
                if (distFromInit > 5f || rObj.transform.position.z < minBounds.z || rObj.transform.position.z > maxBounds.z)
                {
                    rObj.transform.position = temp.initialPosition;
                    rObj.transform.rotation = temp.initialRotation;
                    if (showDebugVisuals) Debug.LogWarning($"[GRAB_RELEASED] {rObj.name} was out-of-bounds (dist={distFromInit:F2}), snapped back to initial position");
                }

                if (showDebugVisuals) Debug.Log($"[GRAB_RELEASED] {rObj.name} released and ensured visible");
            }

            temp.isGrabbed = false;
            currentlyGrabbedBeaker = null;
        }
        isPouringBetweenBeakers = false;
        if (currentGesture == ManoGestureContinuous.NO_GESTURE)
            systemStatus = "Chemistry Lab Ready";
    }

    // Safety bounds
    [Header("Safety Settings")]
    [SerializeField] private bool enableSafetyBounds = true;
    [SerializeField] private Vector3 minBounds = new Vector3(-5f, -3f, 5f);
    [SerializeField] private Vector3 maxBounds = new Vector3(5f, 5f, 15f);

    // Chemistry lab control methods
    public void RefillAllBeakers()
    {
        RefillSourceBeaker();
        RefillTargetBeaker();
    }

    // Methods are properly defined below in the user-added section

    // Add these methods before the closing brace:

void CheckBeakerToBeakerPouring()
{
    if (sourceBeakerData != null && targetBeakerData != null)
    {
        float distance = Vector3.Distance(sourceBeakerData.beakerObject.transform.position,
                                        targetBeakerData.beakerObject.transform.position);
        isPouringBetweenBeakers = distance <= pouringDistance;
    }
}

void UpdateWaterPouring()
{
    UpdateBeakerPouring(sourceBeakerData);
    UpdateBeakerPouring(targetBeakerData);
}

// Utility: clear rigidbody velocities in a forward-compatible way using reflection
private void ClearRigidbodyVelocities(Rigidbody rb)
{
    if (rb == null) return;
    var t = rb.GetType();
    // Try property 'linearVelocity' (newer APIs)
    var linearProp = t.GetProperty("linearVelocity");
    if (linearProp != null && linearProp.CanWrite)
    {
        linearProp.SetValue(rb, Vector3.zero, null);
    }
    else
    {
        // Try 'velocity' via reflection as a fallback
        var velProp = t.GetProperty("velocity");
        if (velProp != null && velProp.CanWrite)
        {
            velProp.SetValue(rb, Vector3.zero, null);
        }
    }

    // angular velocity
    var angularProp = t.GetProperty("angularVelocity");
    if (angularProp != null && angularProp.CanWrite)
    {
        angularProp.SetValue(rb, Vector3.zero, null);
    }
    else
    {
        var angPropAlt = t.GetProperty("angularVelocity");
        if (angPropAlt != null && angPropAlt.CanWrite)
        {
            angPropAlt.SetValue(rb, Vector3.zero, null);
        }
    }
}

// The remaining methods are properly defined below

void UpdateBeakerPouring(ChemistryBeaker beakerData)
{
    if (beakerData?.waterEffect == null) return;
    
    Vector3 beakerUp = beakerData.beakerObject.transform.up;
    float tiltAngle = Vector3.Angle(beakerUp, Vector3.up);
    
    // DEBUG: Log pouring state for source beaker
    if (showDebugVisuals && beakerData == sourceBeakerData && Time.frameCount % 30 == 0)
    {
        Debug.Log($"SOURCE_TILT: Angle={tiltAngle:F1}° | Threshold={pouringThresholdAngle:F1}° | Volume={beakerData.volumeML:F0}mL | Pouring={beakerData.waterEffect.isPlaying}");
    }
    
    // FIXED: Check if beaker has liquid AND is tilted enough to pour
    if (tiltAngle > pouringThresholdAngle && beakerData.volumeML > 0)
    {
        // Use the assigned pour point from Inspector (no need to calculate position)
        if (beakerData.pourPoint != null && beakerData.waterEffectObj != null)
        {
            // CRITICAL FIX: Keep particles aligned with pour point in world space
            Vector3 pourWorldPos = beakerData.pourPoint.position;
            Quaternion pourWorldRot = beakerData.pourPoint.rotation;
            
            // If the particle object is parented to the pourPoint, use local coordinates
            if (beakerData.waterEffectObj.transform.parent == beakerData.pourPoint)
            {
                beakerData.waterEffectObj.transform.localPosition = Vector3.zero;
                beakerData.waterEffectObj.transform.localRotation = Quaternion.identity;
            }
            else
            {
                // Fallback: set world position/rotation to match pour point exactly
                beakerData.waterEffectObj.transform.position = pourWorldPos;
                beakerData.waterEffectObj.transform.rotation = pourWorldRot;
            }
            
            // Additional: Adjust particle emission to follow pour direction (downward)
            var particleMain = beakerData.waterEffect.main;
            particleMain.startRotation = new ParticleSystem.MinMaxCurve(0);

            if (showDebugVisuals && Time.frameCount % 60 == 0) 
            {
                Debug.Log($"POUR_POS: {beakerData.beakerObject.name} | Point={pourWorldPos} | Tilt={tiltAngle:F1}°");
            }
        }
        
        float pourRateMultiplier = Mathf.Clamp01((tiltAngle - pouringThresholdAngle) / (90f - pouringThresholdAngle));
        float volumeLoss = pourRate * pourRateMultiplier * Time.deltaTime;
        
        if (isPouringBetweenBeakers && beakerData == sourceBeakerData && targetBeakerData != null)
        {
            float transferAmount = Mathf.Min(volumeLoss, beakerData.volumeML);
            transferAmount = Mathf.Min(transferAmount, maxBeakerVolume - targetBeakerData.volumeML);
            
            beakerData.volumeML -= transferAmount;
            targetBeakerData.volumeML += transferAmount;
            lastTransferAmount = transferAmount;
            
            // Update target beaker chemical name when receiving liquid
            if (transferAmount > 0 && targetBeakerData.chemicalName == "Empty")
            {
                targetBeakerData.chemicalName = beakerData.chemicalName;
                targetBeakerData.liquidColor = beakerData.liquidColor;
                targetBeakerData.pH = beakerData.pH;
                targetBeakerData.isAcid = beakerData.isAcid;
                
                // Educational feedback for first transfer
                ShowEducationalFeedback(
                    "Liquid Transfer Started",
                    "",
                    "Avoid pouring too quickly or overfilling the beaker",
                    "Pour slowly and watch the volume indicator. Stop before reaching maximum capacity.",
                    new Color(0.2f, 1f, 0.3f)
                );
            }
            
            // Check for overfilling mistake
            if (targetBeakerData.volumeML >= maxBeakerVolume * 0.95f && !hasOverfilled)
            {
                hasOverfilled = true;
                ShowEducationalFeedback(
                    "Beaker Nearly Full",
                    "WARNING: Beaker is almost at maximum capacity!",
                    "Do not overfill - this can cause spills and inaccurate measurements",
                    "Stop pouring now. Leave some space at the top of the beaker.",
                    new Color(1f, 0.5f, 0f)
                );
            }
            
            // Trigger reaction if mixing acid with base (future enhancement)
            CheckForChemicalReaction();
        }
        else
        {
            beakerData.volumeML -= volumeLoss;
        }
        
        beakerData.volumeML = Mathf.Max(0, beakerData.volumeML);
        
        // Start particle effect only if there's liquid to pour
        if (beakerData.volumeML > 0 && !beakerData.waterEffect.isPlaying)
        {
            beakerData.waterEffect.Play();
            
            // Play pour sound
            if (audioSource != null && pourSound != null)
            {
                try
                {
                    audioSource.PlayOneShot(pourSound, 0.9f);
                    if (showDebugVisuals) Debug.Log("[AUDIO_SUCCESS] Pour sound played");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[AUDIO_ERROR] Failed to play pour sound: {ex.Message}");
                }
            }
        }
    }
    
    // FIXED: Stop pouring immediately when beaker is empty OR not tilted enough
    if (beakerData.volumeML <= 0 || tiltAngle <= pouringThresholdAngle)
    {
        if (beakerData.waterEffect.isPlaying)
        {
            beakerData.waterEffect.Stop();
            Debug.Log($"STOPPED POURING: {beakerData.beakerObject.name} - Volume: {beakerData.volumeML:F0}mL, Tilt: {tiltAngle:F1}°");
        }
        
        // SPECIAL: If source beaker is empty, automatically return it to upright position
        if (beakerData == sourceBeakerData && beakerData.volumeML <= 0)
        {
            beakerData.beakerObject.transform.rotation = Quaternion.Lerp(
                beakerData.beakerObject.transform.rotation,
                beakerData.initialRotation,
                Time.deltaTime * 10f // Very fast return when empty
            );
            beakerData.beakerObject.transform.position = beakerData.initialPosition;
            
            // Check if close to upright and snap
            float currentAngle = Mathf.Abs(beakerData.beakerObject.transform.eulerAngles.z);
            if (currentAngle > 180f) currentAngle = 360f - currentAngle;
            if (currentAngle < 5f)
            {
                beakerData.beakerObject.transform.rotation = beakerData.initialRotation;
                systemStatus = "Source beaker empty - returned to upright position";
            }
        }
    }
}

Vector3 CalculateHandPosition(BoundingBox boundingBox)
{
    float centerX = boundingBox.topLeft.x + boundingBox.width / 2f;
    float centerY = boundingBox.topLeft.y - boundingBox.height / 2f;
    
    if (isLandscapeMode)
    {
        // FIXED COORDINATE MAPPING: Direct X-Y mapping (was swapped before)
        float normalizedX = (centerX - 0.5f) * coordinateScale;  // Horizontal movement
        float normalizedY = (0.5f - centerY) * coordinateScale;  // Vertical movement (inverted Y)
        Vector3 handPos = new Vector3(normalizedX, normalizedY, 0) + handPositionOffset;
        
        if (showDebugVisuals && Time.frameCount % 30 == 0)
        {
            Debug.Log($"HAND: BBox=({centerX:F3},{centerY:F3}) → Norm=({normalizedX:F3},{normalizedY:F3}) → World={handPos}");
        }
        return handPos;
    }
    return new Vector3(centerX, centerY, 0);
}

// Professional UI methods
void OnGUI()
{
    DrawProfessionalChemistryUI();
}

void DrawProfessionalChemistryUI()
{
    int screenWidth = Screen.width;
    int screenHeight = Screen.height;
    
    // Enhanced panels at bottom corners
    int panelWidth = 320;
    int panelHeight = 200;
    int bottomMargin = 20;
    
    // Bottom-left: Source Beaker Panel
    DrawEnhancedSourcePanel(15, screenHeight - panelHeight - bottomMargin, panelWidth, panelHeight);
    
    // Bottom-right: Target Beaker Panel  
    DrawEnhancedTargetPanel(screenWidth - panelWidth - 15, screenHeight - panelHeight - bottomMargin, panelWidth, panelHeight);
    
    // Top-center: Enhanced Status Bar
    DrawEnhancedStatusBar(screenWidth, screenHeight);
    
    // Center: Educational Feedback Panel (when active)
    DrawEducationalFeedbackPanel(screenWidth, screenHeight);
}

void DrawEnhancedSourcePanel(int x, int y, int width, int height)
{
    // Modern gradient background with rounded corners effect
    GUIStyle panelBg = new GUIStyle(GUI.skin.box);
    panelBg.normal.background = MakeTex(2, 2, new Color(0.05f, 0.05f, 0.1f, 0.95f));
    GUI.Box(new Rect(x, y, width, height), "", panelBg);
    
    // Glowing border effect
    GUIStyle borderStyle = new GUIStyle(GUI.skin.box);
    borderStyle.normal.background = MakeTex(2, 2, new Color(1f, 0.7f, 0.2f, 0.8f));
    GUI.Box(new Rect(x - 2, y - 2, width + 4, height + 4), "", borderStyle);
    GUI.Box(new Rect(x, y, width, height), "", panelBg);
    
    // Premium header with gradient
    GUIStyle headerGradient = new GUIStyle(GUI.skin.box);
    headerGradient.normal.background = MakeTex(2, 2, new Color(1f, 0.6f, 0.1f, 0.9f));
    GUI.Box(new Rect(x, y, width, 45), "", headerGradient);
    
    // Glass effect overlay
    GUIStyle glassEffect = new GUIStyle(GUI.skin.box);
    glassEffect.normal.background = MakeTex(2, 2, new Color(1f, 1f, 1f, 0.1f));
    GUI.Box(new Rect(x, y, width, 22), "", glassEffect);
    
    // Enhanced title with shadow effect
    GUIStyle titleShadow = new GUIStyle(GUI.skin.label);
    titleShadow.fontSize = 16;
    titleShadow.fontStyle = FontStyle.Bold;
    titleShadow.normal.textColor = new Color(0, 0, 0, 0.5f);
    titleShadow.alignment = TextAnchor.MiddleCenter;
    GUI.Label(new Rect(x + 1, y + 12, width, 25), "⚗️ SOURCE BEAKER", titleShadow);
    
    GUIStyle titleMain = new GUIStyle(GUI.skin.label);
    titleMain.fontSize = 16;
    titleMain.fontStyle = FontStyle.Bold;
    titleMain.normal.textColor = Color.white;
    titleMain.alignment = TextAnchor.MiddleCenter;
    GUI.Label(new Rect(x, y + 11, width, 25), "⚗️ SOURCE BEAKER", titleMain);
    
    int yPos = y + 55;
    int lineHeight = 22;
    
    if (sourceBeakerData != null)
    {
        // Chemical name with icon
        GUI.Label(new Rect(x + 15, yPos, width - 30, lineHeight), 
                 $"🧪 Chemical: {sourceBeakerData.chemicalName}", GetEnhancedLabelStyle(14, new Color(0.9f, 0.9f, 0.9f)));
        yPos += lineHeight;
        
        // Volume with enhanced styling
        GUI.Label(new Rect(x + 15, yPos, width - 30, lineHeight), 
                 $"📊 Volume: {sourceBeakerData.volumeML:F0}mL / {maxBeakerVolume:F0}mL", GetEnhancedLabelStyle(13, new Color(0.8f, 0.8f, 1f)));
        yPos += lineHeight;
        
        // Enhanced volume bar with glow
        float volumeRatio = sourceBeakerData.volumeML / maxBeakerVolume;
        DrawEnhancedVolumeBar(x + 15, yPos, width - 30, 20, volumeRatio, new Color(1f, 0.7f, 0.2f), "ACID");
        yPos += 30;
        
        // Status with icon
        GUI.Label(new Rect(x + 15, yPos, width - 30, lineHeight), 
                 "🔒 Status: FIXED POSITION", GetEnhancedStatusStyle(new Color(0.2f, 1f, 0.3f)));
        yPos += lineHeight + 10;
        
        // Enhanced refill button
        if (GUI.Button(new Rect(x + 15, yPos, width - 30, 35), "💧 REFILL ACID", GetEnhancedButtonStyle(new Color(1f, 0.6f, 0.1f))))
        {
            RefillSourceBeaker();
        }
    }
}

// All remaining UI methods and helper functions are properly defined below

void DrawEnhancedTargetPanel(int x, int y, int width, int height)
{
    // Modern gradient background
    GUIStyle panelBg = new GUIStyle(GUI.skin.box);
    panelBg.normal.background = MakeTex(2, 2, new Color(0.05f, 0.1f, 0.15f, 0.95f));
    GUI.Box(new Rect(x, y, width, height), "", panelBg);
    
    // Cyan glowing border for target beaker
    GUIStyle borderStyle = new GUIStyle(GUI.skin.box);
    borderStyle.normal.background = MakeTex(2, 2, new Color(0.2f, 0.8f, 1f, 0.8f));
    GUI.Box(new Rect(x - 2, y - 2, width + 4, height + 4), "", borderStyle);
    GUI.Box(new Rect(x, y, width, height), "", panelBg);
    
    // Premium header with cyan gradient
    GUIStyle headerGradient = new GUIStyle(GUI.skin.box);
    headerGradient.normal.background = MakeTex(2, 2, new Color(0.1f, 0.6f, 1f, 0.9f));
    GUI.Box(new Rect(x, y, width, 45), "", headerGradient);
    
    // Glass effect overlay
    GUIStyle glassEffect = new GUIStyle(GUI.skin.box);
    glassEffect.normal.background = MakeTex(2, 2, new Color(1f, 1f, 1f, 0.1f));
    GUI.Box(new Rect(x, y, width, 22), "", glassEffect);
    
    // Enhanced title with shadow effect
    GUIStyle titleShadow = new GUIStyle(GUI.skin.label);
    titleShadow.fontSize = 16;
    titleShadow.fontStyle = FontStyle.Bold;
    titleShadow.normal.textColor = new Color(0, 0, 0, 0.5f);
    titleShadow.alignment = TextAnchor.MiddleCenter;
    GUI.Label(new Rect(x + 1, y + 12, width, 25), "🥽 TARGET BEAKER", titleShadow);
    
    GUIStyle titleMain = new GUIStyle(GUI.skin.label);
    titleMain.fontSize = 16;
    titleMain.fontStyle = FontStyle.Bold;
    titleMain.normal.textColor = Color.white;
    titleMain.alignment = TextAnchor.MiddleCenter;
    GUI.Label(new Rect(x, y + 11, width, 25), "🥽 TARGET BEAKER", titleMain);
    
    int yPos = y + 55;
    int lineHeight = 22;
    
    if (targetBeakerData != null)
    {
        // Chemical name with icon
        string chemicalDisplay = targetBeakerData.chemicalName == "Empty" ? "🫗 Empty" : $"🧪 {targetBeakerData.chemicalName}";
        Color chemicalColor = targetBeakerData.chemicalName == "Empty" ? new Color(0.7f, 0.7f, 0.7f) : new Color(0.9f, 0.9f, 0.9f);
        GUI.Label(new Rect(x + 15, yPos, width - 30, lineHeight), 
                 $"Chemical: {chemicalDisplay}", GetEnhancedLabelStyle(14, chemicalColor));
        yPos += lineHeight;
        
        // Volume with enhanced styling
        GUI.Label(new Rect(x + 15, yPos, width - 30, lineHeight), 
                 $"📊 Volume: {targetBeakerData.volumeML:F0}mL / {maxBeakerVolume:F0}mL", GetEnhancedLabelStyle(13, new Color(0.8f, 1f, 1f)));
        yPos += lineHeight;
        
        // Enhanced volume bar
        float volumeRatio = targetBeakerData.volumeML / maxBeakerVolume;
        Color barColor = targetBeakerData.volumeML > 0 ? new Color(0.2f, 0.8f, 1f) : new Color(0.3f, 0.3f, 0.3f);
        string barLabel = targetBeakerData.volumeML > 0 ? "MIXED" : "EMPTY";
        DrawEnhancedVolumeBar(x + 15, yPos, width - 30, 20, volumeRatio, barColor, barLabel);
        yPos += 30;
        
        // Status with dynamic icon and color
        string statusIcon = targetBeakerData.isGrabbed ? "✋" : "🎯";
        string statusText = targetBeakerData.isGrabbed ? "GRABBED - MOVABLE" : "READY TO GRAB";
        Color statusColor = targetBeakerData.isGrabbed ? new Color(1f, 1f, 0.2f) : new Color(0.2f, 1f, 1f);
        GUI.Label(new Rect(x + 15, yPos, width - 30, lineHeight), 
                 $"{statusIcon} Status: {statusText}", GetEnhancedStatusStyle(statusColor));
        yPos += lineHeight + 10;
        
        // Enhanced dual buttons
        int buttonWidth = (width - 45) / 2;
        
        if (GUI.Button(new Rect(x + 15, yPos, buttonWidth, 35), "💧 REFILL\n250mL", GetEnhancedButtonStyle(new Color(0.1f, 0.6f, 1f))))
        {
            RefillTargetBeaker();
        }
        
        if (GUI.Button(new Rect(x + 30 + buttonWidth, yPos, buttonWidth, 35), "🗑 CLEAR\nEMPTY", GetEnhancedButtonStyle(new Color(1f, 0.3f, 0.3f))))
        {
            ClearTargetBeaker();
        }
    }
}

void DrawEnhancedStatusBar(int screenWidth, int screenHeight)
{
    int statusWidth = 400;
    int statusHeight = 60;
    int statusX = (screenWidth - statusWidth) / 2;
    int statusY = 20;
    
    // Modern status bar with gradient and glow
    GUIStyle statusBorder = new GUIStyle(GUI.skin.box);
    statusBorder.normal.background = MakeTex(2, 2, new Color(0.5f, 0.5f, 1f, 0.6f));
    GUI.Box(new Rect(statusX - 3, statusY - 3, statusWidth + 6, statusHeight + 6), "", statusBorder);
    
    GUIStyle statusBg = new GUIStyle(GUI.skin.box);
    statusBg.normal.background = MakeTex(2, 2, new Color(0.1f, 0.1f, 0.2f, 0.9f));
    GUI.Box(new Rect(statusX, statusY, statusWidth, statusHeight), "", statusBg);
    
    // Glass effect
    GUIStyle glassEffect = new GUIStyle(GUI.skin.box);
    glassEffect.normal.background = MakeTex(2, 2, new Color(1f, 1f, 1f, 0.1f));
    GUI.Box(new Rect(statusX, statusY, statusWidth, statusHeight / 2), "", glassEffect);
    
    // Enhanced gesture display with icons and colors
    string gestureIcon = "👋";
    string gestureText = "READY - SHOW HAND";
    Color gestureColor = new Color(0.8f, 0.8f, 0.8f);
    
    switch (currentGesture)
    {
        case ManoGestureContinuous.OPEN_HAND_GESTURE:
            gestureIcon = "🖐️";
            gestureText = "TILTING BEAKER";
            gestureColor = new Color(1f, 0.8f, 0.2f);
            break;
        case ManoGestureContinuous.CLOSED_HAND_GESTURE:
            gestureIcon = "✊";
            gestureText = "GRABBING BEAKER";
            gestureColor = new Color(0.2f, 1f, 0.3f);
            break;
        case ManoGestureContinuous.OPEN_PINCH_GESTURE:
            gestureIcon = "👌";
            gestureText = "REFILLING BEAKER";
            gestureColor = new Color(0.2f, 0.8f, 1f);
            break;
    }
    
    // Main gesture text with shadow
    GUIStyle gestureTextShadow = new GUIStyle(GUI.skin.label);
    gestureTextShadow.fontSize = 18;
    gestureTextShadow.fontStyle = FontStyle.Bold;
    gestureTextShadow.normal.textColor = new Color(0, 0, 0, 0.5f);
    gestureTextShadow.alignment = TextAnchor.MiddleCenter;
    GUI.Label(new Rect(statusX + 1, statusY + 9, statusWidth, 25), $"{gestureIcon} {gestureText}", gestureTextShadow);
    
    GUIStyle gestureTextMain = new GUIStyle(GUI.skin.label);
    gestureTextMain.fontSize = 18;
    gestureTextMain.fontStyle = FontStyle.Bold;
    gestureTextMain.normal.textColor = gestureColor;
    gestureTextMain.alignment = TextAnchor.MiddleCenter;
    GUI.Label(new Rect(statusX, statusY + 8, statusWidth, 25), $"{gestureIcon} {gestureText}", gestureTextMain);
    
    // System status with enhanced styling
    GUIStyle systemStatusStyle = new GUIStyle(GUI.skin.label);
    systemStatusStyle.fontSize = 12;
    systemStatusStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
    systemStatusStyle.alignment = TextAnchor.MiddleCenter;
    GUI.Label(new Rect(statusX, statusY + 35, statusWidth, 20), $"⚡ System: {systemStatus}", systemStatusStyle);
}

void DrawEnhancedVolumeBar(int x, int y, int width, int height, float ratio, Color liquidColor, string label)
{
    GUIStyle bgStyle = new GUIStyle(GUI.skin.box);
    bgStyle.normal.background = MakeTex(2, 2, new Color(0.1f, 0.1f, 0.1f, 0.8f));
    GUI.Box(new Rect(x, y, width, height), "", bgStyle);
    
    GUIStyle borderStyle = new GUIStyle(GUI.skin.box);
    borderStyle.normal.background = MakeTex(2, 2, new Color(liquidColor.r, liquidColor.g, liquidColor.b, 0.6f));
    GUI.Box(new Rect(x - 1, y - 1, width + 2, height + 2), "", borderStyle);
    GUI.Box(new Rect(x, y, width, height), "", bgStyle);
    
    if (ratio > 0)
    {
        GUIStyle fillStyle = new GUIStyle(GUI.skin.box);
        fillStyle.normal.background = MakeTex(2, 2, liquidColor);
        GUI.Box(new Rect(x + 2, y + 2, (width - 4) * ratio, height - 4), "", fillStyle);
        
        GUIStyle highlightStyle = new GUIStyle(GUI.skin.box);
        highlightStyle.normal.background = MakeTex(2, 2, new Color(liquidColor.r + 0.3f, liquidColor.g + 0.3f, liquidColor.b + 0.3f, 0.5f));
        GUI.Box(new Rect(x + 2, y + 2, (width - 4) * ratio, (height - 4) / 3), "", highlightStyle);
    }
    
    GUIStyle textShadow = new GUIStyle(GUI.skin.label);
    textShadow.fontSize = 11;
    textShadow.fontStyle = FontStyle.Bold;
    textShadow.normal.textColor = new Color(0, 0, 0, 0.8f);
    textShadow.alignment = TextAnchor.MiddleCenter;
    GUI.Label(new Rect(x + 1, y + 1, width, height), $"{ratio * 100:F0}% {label}", textShadow);
    
    GUIStyle textMain = new GUIStyle(GUI.skin.label);
    textMain.fontSize = 11;
    textMain.fontStyle = FontStyle.Bold;
    textMain.normal.textColor = Color.white;
    textMain.alignment = TextAnchor.MiddleCenter;
    GUI.Label(new Rect(x, y, width, height), $"{ratio * 100:F0}% {label}", textMain);
}

void DrawEducationalFeedbackPanel(int screenWidth, int screenHeight)
{
    if (!feedback.showFeedback) return;
    
    int panelWidth = 500;
    int panelHeight = 200;
    int panelX = (screenWidth - panelWidth) / 2;
    int panelY = screenHeight / 2 - 150;
    
    GUIStyle borderStyle = new GUIStyle(GUI.skin.box);
    borderStyle.normal.background = MakeTex(2, 2, feedback.feedbackColor);
    GUI.Box(new Rect(panelX - 3, panelY - 3, panelWidth + 6, panelHeight + 6), "", borderStyle);
    
    GUIStyle panelBg = new GUIStyle(GUI.skin.box);
    panelBg.normal.background = MakeTex(2, 2, new Color(0.1f, 0.1f, 0.15f, 0.95f));
    GUI.Box(new Rect(panelX, panelY, panelWidth, panelHeight), "", panelBg);
    
    GUIStyle headerStyle = new GUIStyle(GUI.skin.label);
    headerStyle.fontSize = 18;
    headerStyle.fontStyle = FontStyle.Bold;
    headerStyle.normal.textColor = feedback.feedbackColor;
    headerStyle.alignment = TextAnchor.MiddleCenter;
    GUI.Label(new Rect(panelX, panelY + 10, panelWidth, 30), "📚 EDUCATIONAL FEEDBACK", headerStyle);
    
    int yPos = panelY + 50;
    int lineHeight = 35;
    
    if (!string.IsNullOrEmpty(feedback.actionPerformed))
    {
        GUI.Label(new Rect(panelX + 15, yPos, panelWidth - 30, lineHeight), 
                 $"✓ Action: {feedback.actionPerformed}", GetEnhancedLabelStyle(13, Color.white));
        yPos += lineHeight;
    }
    
    if (!string.IsNullOrEmpty(feedback.mistakeMade))
    {
        GUI.Label(new Rect(panelX + 15, yPos, panelWidth - 30, lineHeight), 
                 $"⚠ Mistake: {feedback.mistakeMade}", GetEnhancedLabelStyle(13, new Color(1f, 0.5f, 0.2f)));
        yPos += lineHeight;
    }
    
    if (!string.IsNullOrEmpty(feedback.whatToAvoid))
    {
        GUI.Label(new Rect(panelX + 15, yPos, panelWidth - 30, lineHeight), 
                 $"❌ Avoid: {feedback.whatToAvoid}", GetEnhancedLabelStyle(12, new Color(1f, 0.7f, 0.7f)));
        yPos += lineHeight;
    }
    
    if (!string.IsNullOrEmpty(feedback.correctProcedure))
    {
        GUI.Label(new Rect(panelX + 15, yPos, panelWidth - 30, lineHeight), 
                 $"✓ Correct: {feedback.correctProcedure}", GetEnhancedLabelStyle(12, new Color(0.7f, 1f, 0.7f)));
    }
}

void UpdateEducationalFeedback()
{
    if (feedback.showFeedback)
    {
        feedback.feedbackTimer -= Time.deltaTime;
        if (feedback.feedbackTimer <= 0f)
        {
            feedback.showFeedback = false;
        }
    }
}

void ShowEducationalFeedback(string action, string mistake, string avoid, string correct, Color color)
{
    feedback.actionPerformed = action;
    feedback.mistakeMade = mistake;
    feedback.whatToAvoid = avoid;
    feedback.correctProcedure = correct;
    feedback.showFeedback = true;
    feedback.feedbackTimer = 5f;
    feedback.feedbackColor = color;
}

void CheckForChemicalReaction()
{
    if (targetBeakerData == null || sourceBeakerData == null) return;
    
    // Check for acid-base neutralization
    if (targetBeakerData.volumeML > 0)
    {
        if (targetBeakerData.isAcid && sourceBeakerData.isBase)
        {
            // Neutralization reaction
            reactionData.reactionOccurred = true;
            reactionData.reactionType = "Acid-Base Neutralization";
            reactionData.resultingPH = 7.0f;
            reactionData.productName = "Salt + Water";
            reactionData.productColor = new Color(0.7f, 0.85f, 0.92f, 0.7f);
            reactionData.isNeutralized = true;
            
            // Update target beaker properties
            targetBeakerData.pH = 7.0f;
            targetBeakerData.liquidColor = reactionData.productColor;
            targetBeakerData.isAcid = false;
            targetBeakerData.isBase = false;
            
            // Play reaction sound
            if (audioSource != null && reactionSound != null)
            {
                audioSource.PlayOneShot(reactionSound);
            }
            
            // Show educational feedback
            ShowEducationalFeedback(
                "Neutralization Reaction Occurred!",
                "",
                "Mixing acids and bases can produce heat and gas",
                "The acid and base have neutralized each other, forming salt and water (pH = 7)",
                new Color(0.2f, 1f, 0.3f)
            );
        }
        else if (targetBeakerData.isAcid)
        {
            reactionData.reactionOccurred = true;
            reactionData.reactionType = "Acid Present";
            reactionData.resultingPH = targetBeakerData.pH;
            reactionData.productName = targetBeakerData.chemicalName;
            reactionData.productColor = targetBeakerData.liquidColor;
        }
    }
}

GUIStyle GetEnhancedLabelStyle(int fontSize, Color color)
{
    GUIStyle style = new GUIStyle(GUI.skin.label);
    style.fontSize = fontSize;
    style.normal.textColor = color;
    style.fontStyle = FontStyle.Normal;
    return style;
}

GUIStyle GetEnhancedStatusStyle(Color color)
{
    GUIStyle style = new GUIStyle(GUI.skin.label);
    style.fontSize = 13;
    style.fontStyle = FontStyle.Bold;
    style.normal.textColor = color;
    return style;
}

GUIStyle GetEnhancedButtonStyle(Color baseColor)
{
    GUIStyle style = new GUIStyle(GUI.skin.button);
    style.fontSize = 12;
    style.fontStyle = FontStyle.Bold;
    style.normal.textColor = Color.white;
    style.alignment = TextAnchor.MiddleCenter;
    
    // Enhanced button with gradient and glow
    style.normal.background = MakeTex(2, 2, baseColor);
    style.hover.background = MakeTex(2, 2, new Color(baseColor.r * 1.3f, baseColor.g * 1.3f, baseColor.b * 1.3f, 1f));
    style.active.background = MakeTex(2, 2, new Color(baseColor.r * 0.8f, baseColor.g * 0.8f, baseColor.b * 0.8f, 1f));
    
    return style;
}

GUIStyle GetCompactLabelStyle()
{
    GUIStyle style = new GUIStyle(GUI.skin.label);
    style.fontSize = 11;
    style.normal.textColor = Color.white;
    return style;
}

GUIStyle GetCompactStatusStyle(Color color)
{
    GUIStyle style = new GUIStyle(GUI.skin.label);
    style.fontSize = 11;
    style.fontStyle = FontStyle.Bold;
    style.normal.textColor = color;
    return style;
}

GUIStyle GetCompactButtonStyle(Color baseColor)
{
    GUIStyle style = new GUIStyle(GUI.skin.button);
    style.fontSize = 11;
    style.fontStyle = FontStyle.Bold;
    style.normal.textColor = Color.white;
    style.normal.background = MakeTex(2, 2, baseColor);
    style.hover.background = MakeTex(2, 2, new Color(baseColor.r * 1.2f, baseColor.g * 1.2f, baseColor.b * 1.2f, 1f));
    style.alignment = TextAnchor.MiddleCenter;
    return style;
}

GUIStyle GetBarBackgroundStyle()
{
    GUIStyle style = new GUIStyle(GUI.skin.box);
    style.normal.background = MakeTex(2, 2, new Color(0.2f, 0.2f, 0.2f, 0.8f));
    return style;
}

private Texture2D MakeTex(int width, int height, Color col)
{
    Color[] pix = new Color[width * height];
    for (int i = 0; i < pix.Length; i++)
        pix[i] = col;
    Texture2D result = new Texture2D(width, height);
    result.SetPixels(pix);
    result.Apply();
    return result;
}

public void RefillSourceBeaker()
{
    if (sourceBeakerData != null)
    {
        sourceBeakerData.volumeML = maxBeakerVolume;
        systemStatus = "Source Beaker Refilled";
    }
}

public void RefillTargetBeaker()
{
    if (targetBeakerData != null)
    {
        targetBeakerData.volumeML = maxBeakerVolume;
        targetBeakerData.chemicalName = "Sodium Hydroxide";
        targetBeakerData.liquidColor = new Color(0.3f, 0.7f, 1f, 0.7f);
        targetBeakerData.pH = 13.0f;
        targetBeakerData.isBase = true;
        targetBeakerData.isAcid = false;
        targetBeakerData.concentration = 100f;
        systemStatus = "Target Beaker Refilled with Base";
        
        // Play refill sound
        if (audioSource != null && refillSound != null)
        {
            try
            {
                audioSource.PlayOneShot(refillSound, 0.8f);
                if (showDebugVisuals) Debug.Log("[AUDIO_SUCCESS] Refill (target) sound played");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[AUDIO_ERROR] Failed to play refill (target) sound: {ex.Message}");
            }
        }
    }
}

public void ClearTargetBeaker()
{
    if (targetBeakerData != null)
    {
        targetBeakerData.volumeML = 0f;
        targetBeakerData.chemicalName = "Empty";
        targetBeakerData.pH = 7.0f;
        targetBeakerData.isAcid = false;
        targetBeakerData.isBase = false;
        hasOverfilled = false;
        systemStatus = "Target Beaker Cleared";
    }
}
}
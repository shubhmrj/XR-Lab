using UnityEngine;
using ManoMotion;
using System.Collections.Generic;

public class WaterAttachToBeaker : MonoBehaviour
{
    [Header("Chemistry Lab Setup")]
    [SerializeField] private GameObject sourceBeaker; // Fixed beaker (source)
    [SerializeField] private GameObject targetBeaker; // Movable beaker (target)
    [SerializeField] private GameObject waterParticlesPrefab;
    [SerializeField] private float grabDetectionRadius = 7.5f;
    
    [Header("Pour Points - Assign These in Unity Inspector")]
    [SerializeField] public Transform sourcePourPoint;
    [SerializeField] public Transform targetPourPoint;
    
    [Header("Chemistry Settings")]
    [SerializeField] private float maxBeakerVolume = 500f;
    [SerializeField] private float pourRate = 250f;
    [SerializeField] private float pouringDistance = 2.0f;

    [Header("Pouring Settings")]
    [SerializeField] private float pouringThresholdAngle = 30f; // BUG FIX #6: Increased from 25f
    [SerializeField] private float maxPourRate = 100.0f;
    [SerializeField] public Color waterColor = new Color(0.7f, 0.85f, 0.92f, 0.7f);

    // BUG FIX #1 & #2: Moved Safety Bounds to top (was at line 550)
    [Header("Safety Settings")]
    [SerializeField] private bool enableSafetyBounds = true;
    [SerializeField] private Vector3 minBounds = new Vector3(-5f, -3f, 5f);
    [SerializeField] private Vector3 maxBounds = new Vector3(5f, 5f, 15f);

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 22f;
    [SerializeField] private float tiltSmoothSpeed = 20f;
    [SerializeField] private float maxTiltAngle = 60f;
    [SerializeField] private Vector3 handPositionOffset = new Vector3(0, 0f, 8f);
    [SerializeField] private float coordinateScale = 10f;
    [SerializeField] private bool isLandscapeMode = true;

    [Tooltip("If enabled, the grabbed beaker will follow the hand depth (Z). Disable to keep fixed depth.)")]
    [SerializeField] private bool followHandDepth = true;
    [Tooltip("Smoothing speed used when following hand depth to reduce jitter")]
    [SerializeField] private float depthSmoothSpeed = 8f;

    [Tooltip("Max distance considered a realistic initial offset on grab")]
    [SerializeField] private float maxInitialSnapDistance = 0.7f;
    [Tooltip("How quickly the beaker snaps to the hand on first grab")]
    [SerializeField] private float initialSnapSpeed = 30f;

    [Header("Control Mode")]
    [SerializeField] private bool useGestureControls = true;
    [SerializeField] private bool autoReturnWhenNoGesture = true;
    [Tooltip("Allow grabbing the target beaker anywhere in the XR scene")]
    [SerializeField] private bool grabAnywhere = true;
    
    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip pourSound;
    [SerializeField] private AudioClip refillSound;
    [SerializeField] private AudioClip reactionSound;

    private class ChemistryBeaker
    {
        public GameObject beakerObject;
        public Transform pourPoint;
        public GameObject waterEffectObj;
        public ParticleSystem waterEffect;
        public ParticleSystem splashEffect;
        public Transform originalParent;
        public float volumeML = 500f;
        public Vector3 initialPosition;
        public Quaternion initialRotation;
        public Vector3 lastEmitPosition;
        public Vector3 grabOffset;
        public bool isGrabbed = false;
        public bool isFixed = false;
        public Color liquidColor;
        public Rigidbody rb;
        public bool wasKinematic = false;
        public string chemicalName = "Water";
        public float concentration = 100f;
        public float pH = 7.0f;
        public bool isAcid = false;
        public bool isBase = false;
    }

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
    private Vector3 FIXED_BEAKER_SCALE = new Vector3(8f, 8f, 8f);
    
    private ManoGestureContinuous currentGesture = ManoGestureContinuous.NO_GESTURE;
    private string systemStatus = "Chemistry Lab Ready";
    
    private EducationalFeedback feedback = new EducationalFeedback();
    private ReactionData reactionData = new ReactionData();
    private float lastTransferAmount = 0f;
    private bool hasOverfilled = false;
    private bool hasSpilled = false;
    private Vector3 lastHandPosition = Vector3.zero;
    private float lastHandTime = 0f;
    [SerializeField] private float grabGraceDuration = 0.25f;
    
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

        data.rb = beakerObj.GetComponent<Rigidbody>();
        if (data.rb != null)
        {
            data.wasKinematic = data.rb.isKinematic;
        }

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
            GameObject pourPointObj = new GameObject($"PourPoint_{beakerObj.name}");
            pourPointObj.transform.parent = beakerObj.transform;
            pourPointObj.transform.localPosition = new Vector3(0, 0.45f, 0.25f);
            data.pourPoint = pourPointObj.transform;
            Debug.LogWarning($"Pour point not assigned in Inspector for {beakerObj.name}! Using auto-created pour point.");
        }

        // BUG FIX #3: Add null check warning for water effect
        if (waterParticlesPrefab != null && data.pourPoint != null)
        {
            data.waterEffectObj = Instantiate(waterParticlesPrefab, data.pourPoint.position, data.pourPoint.rotation);
            data.waterEffectObj.name = $"ChemicalEffect_{beakerObj.name}";
            data.waterEffectObj.transform.position = data.pourPoint.position;
            data.waterEffectObj.transform.rotation = data.pourPoint.rotation;
            data.waterEffectObj.transform.SetParent(null);
            data.waterEffect = data.waterEffectObj.GetComponent<ParticleSystem>();
            if (data.waterEffect != null)
            {
                var main = data.waterEffect.main;
                main.startColor = data.liquidColor;
                data.waterEffect.Stop();
            }
        }
        else if (waterParticlesPrefab == null)
        {
            Debug.LogWarning($"Water Particles Prefab not assigned! Beaker {beakerObj.name} will not show pouring effects.");
        }
        
        return data;
    }

    void Update()
    {
        if (useGestureControls && ManoMotionManager.Instance != null && ManoMotionManager.Instance.HandInfos != null)
        {
            HandInfo[] handInfos = ManoMotionManager.Instance.HandInfos;
            bool handledHand = false;

            if (handInfos != null && handInfos.Length > 0)
            {
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
                            ReleaseAllBeakers();
                            HandleTiltGesture(normalizedX);
                            break;
                        case ManoGestureContinuous.OPEN_PINCH_GESTURE:
                            HandleRefillGesture();
                            break;
                        case ManoGestureContinuous.CLOSED_HAND_GESTURE:
                            lastHandPosition = handPosition;
                            lastHandTime = Time.time;
                            HandleGrabGesture(handPosition);
                            break;
                        case ManoGestureContinuous.NO_GESTURE: // BUG FIX #10: Explicit NO_GESTURE case
                            break;
                        default:
                            if (showDebugVisuals && Time.frameCount % 60 == 0)
                                Debug.LogWarning($"Unknown gesture detected: {currentGesture}");
                            break;
                    }

                    handledHand = true;
                    break;
                }
            }

            if (!handledHand)
            {
                if (currentlyGrabbedBeaker != null && Time.time - lastHandTime <= grabGraceDuration)
                {
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

            CheckBeakerToBeakerPouring();
        }

        UpdateWaterPouring();
        UpdateEducationalFeedback();
        EnforceScaleLock();
    }

    // BUG FIX #9: Optimized scale lock - only reset if changed
    void EnforceScaleLock()
    {
        if (sourceBeakerData?.beakerObject != null)
        {
            float posDistance = Vector3.Distance(sourceBeakerData.beakerObject.transform.position, sourceBeakerData.initialPosition);
            if (posDistance > 0.01f)
            {
                sourceBeakerData.beakerObject.transform.position = sourceBeakerData.initialPosition;
                if (showDebugVisuals) Debug.LogError($"SOURCE BEAKER MOVED! Resetting to {sourceBeakerData.initialPosition}");
            }
            
            if (sourceBeakerData.beakerObject.transform.localScale != FIXED_BEAKER_SCALE)
            {
                sourceBeakerData.beakerObject.transform.localScale = FIXED_BEAKER_SCALE;
            }
        }
        if (targetBeakerData?.beakerObject != null)
        {
            if (targetBeakerData.beakerObject.transform.localScale != FIXED_BEAKER_SCALE)
            {
                targetBeakerData.beakerObject.transform.localScale = FIXED_BEAKER_SCALE;
            }
        }
    }

    ChemistryBeaker GetNearestGrabbableBeaker(Vector3 handPosition)
    {
        if (targetBeakerData?.beakerObject != null && !targetBeakerData.isFixed)
        {
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

        return null;
    }

    void HandleGrabGesture(Vector3 handPosition)
    {
        // BUG FIX #5: SAFETY - Never allow source beaker to be grabbed
        if (currentlyGrabbedBeaker == sourceBeakerData)
        {
            if (showDebugVisuals) Debug.LogError("!!! SAFETY VIOLATION: Source beaker was grabbed! Releasing immediately !!!");
            ReleaseAllBeakers();
            return;
        }
        
        if (currentlyGrabbedBeaker == null)
        {
            currentlyGrabbedBeaker = GetNearestGrabbableBeaker(handPosition);
            if (currentlyGrabbedBeaker != null)
            {
                currentlyGrabbedBeaker.isGrabbed = true;
                systemStatus = $"GRABBED: {currentlyGrabbedBeaker.beakerObject.name}";

                GameObject bobj = currentlyGrabbedBeaker.beakerObject;
                bool wasActive = bobj.activeInHierarchy;
                if (!wasActive) bobj.SetActive(true);

                var renderers = bobj.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers) r.enabled = true;

                var colliders = bobj.GetComponentsInChildren<Collider>(true);
                foreach (var c in colliders) c.enabled = true;

                if (currentlyGrabbedBeaker.originalParent != null)
                {
                    bobj.transform.SetParent(null, true);
                    if (showDebugVisuals) Debug.Log($"[GRAB_DIAG] Detached {bobj.name} from parent {currentlyGrabbedBeaker.originalParent.name} while grabbed");
                }

                float camDist = Camera.main != null ? Vector3.Distance(Camera.main.transform.position, bobj.transform.position) : -1f;
                if (showDebugVisuals)
                {
                    Debug.Log($">>> GRAB_ACQUIRED: {bobj.name} <<< (wasActive={wasActive}, pos={bobj.transform.position}, scale={bobj.transform.localScale}, previousParent={(currentlyGrabbedBeaker.originalParent!=null?currentlyGrabbedBeaker.originalParent.name:"null")}, camDist={camDist:F2})");
                    if (renderers.Length == 0) Debug.LogWarning($"[GRAB_DIAG] No Renderer found on {bobj.name} - it may be invisible");
                }

                currentlyGrabbedBeaker.grabOffset = bobj.transform.position - handPosition;

                if (Mathf.Abs(currentlyGrabbedBeaker.grabOffset.z) > maxInitialSnapDistance)
                {
                    Vector3 snapped = bobj.transform.position;
                    snapped.z = handPosition.z;
                    bobj.transform.position = snapped;
                    currentlyGrabbedBeaker.grabOffset = bobj.transform.position - handPosition;
                    if (showDebugVisuals) Debug.Log($"[GRAB_SNAP_DEPTH] Snapped Z from offset -> newPos={bobj.transform.position}, newOffset.z={currentlyGrabbedBeaker.grabOffset.z:F2}");
                }
                else if (currentlyGrabbedBeaker.grabOffset.magnitude > maxInitialSnapDistance)
                {
                    Vector3 targetPos = handPosition + currentlyGrabbedBeaker.grabOffset.normalized * maxInitialSnapDistance;
                    bobj.transform.position = Vector3.Lerp(bobj.transform.position, targetPos, Mathf.Clamp(Time.deltaTime * initialSnapSpeed, 0f, 1f));
                    currentlyGrabbedBeaker.grabOffset = bobj.transform.position - handPosition;
                    if (showDebugVisuals) Debug.Log($"[GRAB_SNAP] large offset {currentlyGrabbedBeaker.grabOffset.magnitude:F2}m — snapping towards hand (newPos={bobj.transform.position})");
                }

                if (currentlyGrabbedBeaker.rb != null)
                {
                    currentlyGrabbedBeaker.rb.isKinematic = true;
                    ClearRigidbodyVelocities(currentlyGrabbedBeaker.rb);
                    if (showDebugVisuals) Debug.Log("[GRAB_PHYSICS] Rigidbody found, set isKinematic=true and cleared velocities");
                }

                bobj.transform.localScale = FIXED_BEAKER_SCALE;
                if (showDebugVisuals) Debug.Log($"[GRAB_STATE] Forced scale {bobj.name} => {FIXED_BEAKER_SCALE}");
            }
            else
            {
                systemStatus = "GRAB FAILED - Check hand position and beaker proximity";
                if (showDebugVisuals) Debug.LogWarning($">>> GRAB_FAILED: Cannot reach target beaker <<<");
            }
        }

        if (currentlyGrabbedBeaker != null && !currentlyGrabbedBeaker.isFixed)
        {
            if (float.IsNaN(handPosition.x) || float.IsNaN(handPosition.y) || float.IsNaN(handPosition.z))
            {
                if (showDebugVisuals) Debug.LogError("!!! INVALID_HAND: NaN detected! Using last position !!!");
                handPosition = lastHandPosition;
            }
            
            Vector3 desiredPos = handPosition + currentlyGrabbedBeaker.grabOffset;

            if (followHandDepth)
            {
                float smoothedZ = Mathf.Lerp(currentlyGrabbedBeaker.beakerObject.transform.position.z, handPosition.z + currentlyGrabbedBeaker.grabOffset.z, Time.deltaTime * depthSmoothSpeed);
                desiredPos.z = smoothedZ;
            }
            else
            {
                desiredPos.z = currentlyGrabbedBeaker.beakerObject.transform.position.z;
            }

            if (showDebugVisuals && Mathf.Abs(desiredPos.z - handPosition.z) > 0.5f)
            {
                Debug.LogWarning($"[GRAB_DIAG] Depth delta large: desiredZ={desiredPos.z:F2}, handZ={handPosition.z:F2}");
            }

            if (enableSafetyBounds)
            {
                desiredPos.x = Mathf.Clamp(desiredPos.x, minBounds.x, maxBounds.x);
                desiredPos.y = Mathf.Clamp(desiredPos.y, minBounds.y, maxBounds.y);
                desiredPos.z = Mathf.Clamp(desiredPos.z, minBounds.z, maxBounds.z);
            }

            Vector3 currentPos = currentlyGrabbedBeaker.beakerObject.transform.position;
            float maxDelta = moveSpeed * Time.deltaTime;
            Vector3 newPos = Vector3.MoveTowards(currentPos, desiredPos, maxDelta);
            currentlyGrabbedBeaker.beakerObject.transform.position = newPos;
            
            currentlyGrabbedBeaker.beakerObject.transform.rotation = Quaternion.Lerp(
                currentlyGrabbedBeaker.beakerObject.transform.rotation,
                currentlyGrabbedBeaker.initialRotation,
                Time.deltaTime * 8f
            );
            
            currentlyGrabbedBeaker.beakerObject.transform.localScale = FIXED_BEAKER_SCALE;

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
        ChemistryBeaker beakerToTilt = sourceBeakerData;

        if (beakerToTilt != null)
        {
            systemStatus = $"Tilting: {beakerToTilt.beakerObject.name} - Volume: {beakerToTilt.volumeML:F0}mL";
            
            float tiltInput = normalizedX * 1.5f;
            float desiredTiltZ = Mathf.Clamp(tiltInput * maxTiltAngle, -maxTiltAngle, maxTiltAngle);
            
            float currentTiltZ = beakerToTilt.beakerObject.transform.eulerAngles.z;
            if (currentTiltZ > 180f) currentTiltZ -= 360f;
            
            float angleDiff = desiredTiltZ - currentTiltZ;
            if (Mathf.Abs(angleDiff) > 0.1f)
            {
                float rotateAmount = angleDiff * Time.deltaTime * tiltSmoothSpeed;
                
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
            
            if (beakerToTilt == sourceBeakerData)
            {
                beakerToTilt.beakerObject.transform.position = beakerToTilt.initialPosition;
            }
            
            beakerToTilt.beakerObject.transform.localScale = FIXED_BEAKER_SCALE;
        }
        else
        {
            systemStatus = "No beaker available for tilting";
        }
    }
    
    void ReturnBeakersToInitialPosition()
    {
        if (sourceBeakerData != null)
        {
            sourceBeakerData.beakerObject.transform.rotation = Quaternion.Lerp(
                sourceBeakerData.beakerObject.transform.rotation,
                sourceBeakerData.initialRotation,
                Time.deltaTime * 8f
            );
            sourceBeakerData.beakerObject.transform.position = sourceBeakerData.initialPosition;
            sourceBeakerData.beakerObject.transform.localScale = FIXED_BEAKER_SCALE;
            
            float currentAngle = Mathf.Abs(sourceBeakerData.beakerObject.transform.eulerAngles.z);
            if (currentAngle > 180f) currentAngle = 360f - currentAngle;
            
            if (currentAngle < 2f)
            {
                sourceBeakerData.beakerObject.transform.rotation = sourceBeakerData.initialRotation;
                Debug.Log("SOURCE BEAKER: Returned to upright position");
            }
        }
        
        if (targetBeakerData != null)
        {
            targetBeakerData.beakerObject.transform.rotation = Quaternion.Lerp(
                targetBeakerData.beakerObject.transform.rotation,
                targetBeakerData.initialRotation,
                Time.deltaTime * 8f
            );
            targetBeakerData.beakerObject.transform.localScale = FIXED_BEAKER_SCALE;
        }
    }

    void HandleRefillGesture()
    {
        if (sourceBeakerData == null)
        {
            Debug.Log($"REFILL FAILED: Source beaker not available");
            systemStatus = "Source beaker not available for refilling";
            return;
        }

        ChemistryBeaker beakerToRefill = sourceBeakerData;
        string beakerName = "Source";

        float currentVolume = beakerToRefill.volumeML;
        float refillAmount = pourRate * Time.deltaTime * 2f;
        beakerToRefill.volumeML = Mathf.Min(maxBeakerVolume, beakerToRefill.volumeML + refillAmount);
        
        if (beakerToRefill.volumeML > 0)
        {
            beakerToRefill.chemicalName = "Hydrochloric Acid";
            beakerToRefill.liquidColor = new Color(1f, 0.7f, 0.2f, 0.7f);
        }
        
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
            ChemistryBeaker temp = currentlyGrabbedBeaker;
            GameObject rObj = temp.beakerObject;
            if (rObj != null)
            {
                if (!rObj.activeInHierarchy) rObj.SetActive(true);
                var rens = rObj.GetComponentsInChildren<Renderer>(true);
                foreach (var r in rens) r.enabled = true;
                var cols = rObj.GetComponentsInChildren<Collider>(true);
                foreach (var c in cols) c.enabled = true;

                if (temp.originalParent != null)
                {
                    rObj.transform.SetParent(temp.originalParent, false);
                    if (showDebugVisuals) Debug.Log($"[GRAB_RELEASED] Restored parent {temp.originalParent.name} for {rObj.name}");
                }

                if (temp.rb != null)
                {
                    ClearRigidbodyVelocities(temp.rb);
                    temp.rb.isKinematic = temp.wasKinematic;
                    if (showDebugVisuals) Debug.Log("[GRAB_PHYSICS] Restored Rigidbody kinematic state and cleared velocities on release");
                }

                rObj.transform.localScale = FIXED_BEAKER_SCALE;

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

    public void RefillAllBeakers()
    {
        RefillSourceBeaker();
        RefillTargetBeaker();
    }

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

    // BUG FIX #4: Replace reflection with direct property assignment
    private void ClearRigidbodyVelocities(Rigidbody rb)
    {
        if (rb == null) return;
        
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        
        if (showDebugVisuals) Debug.Log("[PHYSICS] Rigidbody velocities cleared");
    }

    void UpdateBeakerPouring(ChemistryBeaker beakerData)
    {
        if (beakerData?.waterEffect == null) return;
        
        // BUG FIX #3: Add null check for water effect object
        if (beakerData?.waterEffectObj == null)
        {
            if (showDebugVisuals && Time.frameCount % 300 == 0)
                Debug.LogWarning($"Water effect object is null for {beakerData.beakerObject.name}! Particles won't emit.");
            return;
        }
        
        Vector3 beakerUp = beakerData.beakerObject.transform.up;
        float tiltAngle = Vector3.Angle(beakerUp, Vector3.up);
        
        if (showDebugVisuals && beakerData == sourceBeakerData && Time.frameCount % 30 == 0)
        {
            Debug.Log($"SOURCE_TILT: Angle={tiltAngle:F1}° | Threshold={pouringThresholdAngle:F1}° | Volume={beakerData.volumeML:F0}mL | Pouring={beakerData.waterEffect.isPlaying}");
        }
        
        if (tiltAngle > pouringThresholdAngle && beakerData.volumeML > 0)
        {
            if (beakerData.pourPoint != null && beakerData.waterEffectObj != null)
            {
                Vector3 pourWorldPos = beakerData.pourPoint.position;
                Quaternion pourWorldRot = beakerData.pourPoint.rotation;
                
                if (beakerData.waterEffectObj.transform.parent == beakerData.pourPoint)
                {
                    beakerData.waterEffectObj.transform.localPosition = Vector3.zero;
                    beakerData.waterEffectObj.transform.localRotation = Quaternion.identity;
                }
                else
                {
                    beakerData.waterEffectObj.transform.position = pourWorldPos;
                    beakerData.waterEffectObj.transform.rotation = pourWorldRot;
                }
                
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
                
                if (transferAmount > 0 && targetBeakerData.chemicalName == "Empty")
                {
                    targetBeakerData.chemicalName = beakerData.chemicalName;
                    targetBeakerData.liquidColor = beakerData.liquidColor;
                    targetBeakerData.pH = beakerData.pH;
                    targetBeakerData.isAcid = beakerData.isAcid;
                    
                    ShowEducationalFeedback(
                        "Liquid Transfer Started",
                        "",
                        "Avoid pouring too quickly or overfilling the beaker",
                        "Pour slowly and watch the volume indicator. Stop before reaching maximum capacity.",
                        new Color(0.2f, 1f, 0.3f)
                    );
                }
                
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
                
                CheckForChemicalReaction();
            }
            else
            {
                beakerData.volumeML -= volumeLoss;
            }
            
            beakerData.volumeML = Mathf.Max(0, beakerData.volumeML);
            
            if (beakerData.volumeML > 0 && !beakerData.waterEffect.isPlaying)
            {
                beakerData.waterEffect.Play();
                
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
        
        if (beakerData.volumeML <= 0 || tiltAngle <= pouringThresholdAngle)
        {
            if (beakerData.waterEffect.isPlaying)
            {
                beakerData.waterEffect.Stop();
                Debug.Log($"STOPPED POURING: {beakerData.beakerObject.name} - Volume: {beakerData.volumeML:F0}mL, Tilt: {tiltAngle:F1}°");
            }
            
            if (beakerData == sourceBeakerData && beakerData.volumeML <= 0)
            {
                beakerData.beakerObject.transform.rotation = Quaternion.Lerp(
                    beakerData.beakerObject.transform.rotation,
                    beakerData.initialRotation,
                    Time.deltaTime * 10f
                );
                beakerData.beakerObject.transform.position = beakerData.initialPosition;
                
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
            float normalizedX = (centerX - 0.5f) * coordinateScale;
            float normalizedY = (0.5f - centerY) * coordinateScale;
            Vector3 handPos = new Vector3(normalizedX, normalizedY, 0) + handPositionOffset;
            
            if (showDebugVisuals && Time.frameCount % 30 == 0)
            {
                Debug.Log($"HAND: BBox=({centerX:F3},{centerY:F3}) → Norm=({normalizedX:F3},{normalizedY:F3}) → World={handPos}");
            }
            return handPos;
        }
        return new Vector3(centerX, centerY, 0);
    }

    void OnGUI()
    {
        DrawProfessionalChemistryUI();
    }

    void DrawProfessionalChemistryUI()
    {
        int screenWidth = Screen.width;
        int screenHeight = Screen.height;
        
        int panelWidth = 320;
        int panelHeight = 200;
        int bottomMargin = 20;
        
        DrawEnhancedSourcePanel(15, screenHeight - panelHeight - bottomMargin, panelWidth, panelHeight);
        DrawEnhancedTargetPanel(screenWidth - panelWidth - 15, screenHeight - panelHeight - bottomMargin, panelWidth, panelHeight);
        DrawEnhancedStatusBar(screenWidth, screenHeight);
        DrawEducationalFeedbackPanel(screenWidth, screenHeight);
    }

    void DrawEnhancedSourcePanel(int x, int y, int width, int height)
    {
        GUIStyle panelBg = new GUIStyle(GUI.skin.box);
        panelBg.normal.background = MakeTex(2, 2, new Color(0.05f, 0.05f, 0.1f, 0.95f));
        GUI.Box(new Rect(x, y, width, height), "", panelBg);
        
        GUIStyle borderStyle = new GUIStyle(GUI.skin.box);
        borderStyle.normal.background = MakeTex(2, 2, new Color(1f, 0.7f, 0.2f, 0.8f));
        GUI.Box(new Rect(x - 2, y - 2, width + 4, height + 4), "", borderStyle);
        GUI.Box(new Rect(x, y, width, height), "", panelBg);
        
        GUIStyle headerGradient = new GUIStyle(GUI.skin.box);
        headerGradient.normal.background = MakeTex(2, 2, new Color(1f, 0.6f, 0.1f, 0.9f));
        GUI.Box(new Rect(x, y, width, 45), "", headerGradient);
        
        GUIStyle glassEffect = new GUIStyle(GUI.skin.box);
        glassEffect.normal.background = MakeTex(2, 2, new Color(1f, 1f, 1f, 0.1f));
        GUI.Box(new Rect(x, y, width, 22), "", glassEffect);
        
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
            GUI.Label(new Rect(x + 15, yPos, width - 30, lineHeight), 
                     $"🧪 Chemical: {sourceBeakerData.chemicalName}", GetEnhancedLabelStyle(14, new Color(0.9f, 0.9f, 0.9f)));
            yPos += lineHeight;
            
            GUI.Label(new Rect(x + 15, yPos, width - 30, lineHeight), 
                     $"📊 Volume: {sourceBeakerData.volumeML:F0}mL / {maxBeakerVolume:F0}mL", GetEnhancedLabelStyle(13, new Color(0.8f, 0.8f, 1f)));
            yPos += lineHeight;
            
            float volumeRatio = sourceBeakerData.volumeML / maxBeakerVolume;
            DrawEnhancedVolumeBar(x + 15, yPos, width - 30, 20, volumeRatio, new Color(1f, 0.7f, 0.2f), "ACID");
            yPos += 30;
            
            GUI.Label(new Rect(x + 15, yPos, width - 30, lineHeight), 
                     "🔒 Status: FIXED POSITION", GetEnhancedStatusStyle(new Color(0.2f, 1f, 0.3f)));
            yPos += lineHeight + 10;
            
            if (GUI.Button(new Rect(x + 15, yPos, width - 30, 35), "💧 REFILL ACID", GetEnhancedButtonStyle(new Color(1f, 0.6f, 0.1f))))
            {
                RefillSourceBeaker();
            }
        }
    }

    void DrawEnhancedTargetPanel(int x, int y, int width, int height)
    {
        GUIStyle panelBg = new GUIStyle(GUI.skin.box);
        panelBg.normal.background = MakeTex(2, 2, new Color(0.05f, 0.1f, 0.15f, 0.95f));
        GUI.Box(new Rect(x, y, width, height), "", panelBg);
        
        GUIStyle borderStyle = new GUIStyle(GUI.skin.box);
        borderStyle.normal.background = MakeTex(2, 2, new Color(0.2f, 0.8f, 1f, 0.8f));
        GUI.Box(new Rect(x - 2, y - 2, width + 4, height + 4), "", borderStyle);
        GUI.Box(new Rect(x, y, width, height), "", panelBg);
        
        GUIStyle headerGradient = new GUIStyle(GUI.skin.box);
        headerGradient.normal.background = MakeTex(2, 2, new Color(0.1f, 0.6f, 1f, 0.9f));
        GUI.Box(new Rect(x, y, width, 45), "", headerGradient);
        
        GUIStyle glassEffect = new GUIStyle(GUI.skin.box);
        glassEffect.normal.background = MakeTex(2, 2, new Color(1f, 1f, 1f, 0.1f));
        GUI.Box(new Rect(x, y, width, 22), "", glassEffect);
        
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
            string chemicalDisplay = targetBeakerData.chemicalName == "Empty" ? "🫗 Empty" : $"🧪 {targetBeakerData.chemicalName}";
            Color chemicalColor = targetBeakerData.chemicalName == "Empty" ? new Color(0.7f, 0.7f, 0.7f) : new Color(0.9f, 0.9f, 0.9f);
            GUI.Label(new Rect(x + 15, yPos, width - 30, lineHeight), 
                     $"Chemical: {chemicalDisplay}", GetEnhancedLabelStyle(14, chemicalColor));
            yPos += lineHeight;
            
            GUI.Label(new Rect(x + 15, yPos, width - 30, lineHeight), 
                     $"📊 Volume: {targetBeakerData.volumeML:F0}mL / {maxBeakerVolume:F0}mL", GetEnhancedLabelStyle(13, new Color(0.8f, 1f, 1f)));
            yPos += lineHeight;
            
            float volumeRatio = targetBeakerData.volumeML / maxBeakerVolume;
            Color barColor = targetBeakerData.volumeML > 0 ? new Color(0.2f, 0.8f, 1f) : new Color(0.3f, 0.3f, 0.3f);
            string barLabel = targetBeakerData.volumeML > 0 ? "MIXED" : "EMPTY";
            DrawEnhancedVolumeBar(x + 15, yPos, width - 30, 20, volumeRatio, barColor, barLabel);
            yPos += 30;
            
            string statusIcon = targetBeakerData.isGrabbed ? "✋" : "🎯";
            string statusText = targetBeakerData.isGrabbed ? "GRABBED - MOVABLE" : "READY TO GRAB";
            Color statusColor = targetBeakerData.isGrabbed ? new Color(1f, 1f, 0.2f) : new Color(0.2f, 1f, 1f);
            GUI.Label(new Rect(x + 15, yPos, width - 30, lineHeight), 
                     $"{statusIcon} Status: {statusText}", GetEnhancedStatusStyle(statusColor));
            yPos += lineHeight + 10;
            
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
        
        GUIStyle statusBorder = new GUIStyle(GUI.skin.box);
        statusBorder.normal.background = MakeTex(2, 2, new Color(0.5f, 0.5f, 1f, 0.6f));
        GUI.Box(new Rect(statusX - 3, statusY - 3, statusWidth + 6, statusHeight + 6), "", statusBorder);
        
        GUIStyle statusBg = new GUIStyle(GUI.skin.box);
        statusBg.normal.background = MakeTex(2, 2, new Color(0.1f, 0.1f, 0.2f, 0.9f));
        GUI.Box(new Rect(statusX, statusY, statusWidth, statusHeight), "", statusBg);
        
        GUIStyle glassEffect = new GUIStyle(GUI.skin.box);
        glassEffect.normal.background = MakeTex(2, 2, new Color(1f, 1f, 1f, 0.1f));
        GUI.Box(new Rect(statusX, statusY, statusWidth, statusHeight / 2), "", glassEffect);
        
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

    // BUG FIX #7: Prevent overlapping feedback
    void ShowEducationalFeedback(string action, string mistake, string avoid, string correct, Color color)
    {
        // Only show new feedback if current feedback has expired
        if (feedback.showFeedback && feedback.feedbackTimer > 0.5f)
        {
            if (showDebugVisuals) Debug.Log("[FEEDBACK] Ignoring new feedback - current feedback still active");
            return;
        }
        
        feedback.actionPerformed = action;
        feedback.mistakeMade = mistake;
        feedback.whatToAvoid = avoid;
        feedback.correctProcedure = correct;
        feedback.showFeedback = true;
        feedback.feedbackTimer = 5f;
        feedback.feedbackColor = color;
        
        if (showDebugVisuals) Debug.Log($"[FEEDBACK] Showing: {action}");
    }

    void CheckForChemicalReaction()
    {
        if (targetBeakerData == null || sourceBeakerData == null) return;
        
        if (targetBeakerData.volumeML > 0)
        {
            if (targetBeakerData.isAcid && sourceBeakerData.isBase)
            {
                reactionData.reactionOccurred = true;
                reactionData.reactionType = "Acid-Base Neutralization";
                reactionData.resultingPH = 7.0f;
                reactionData.productName = "Salt + Water";
                reactionData.productColor = new Color(0.7f, 0.85f, 0.92f, 0.7f);
                reactionData.isNeutralized = true;
                
                targetBeakerData.pH = 7.0f;
                targetBeakerData.liquidColor = reactionData.productColor;
                targetBeakerData.isAcid = false;
                targetBeakerData.isBase = false;
                
                if (audioSource != null && reactionSound != null)
                {
                    audioSource.PlayOneShot(reactionSound);
                }
                
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
        
        style.normal.background = MakeTex(2, 2, baseColor);
        style.hover.background = MakeTex(2, 2, new Color(baseColor.r * 1.3f, baseColor.g * 1.3f, baseColor.b * 1.3f, 1f));
        style.active.background = MakeTex(2, 2, new Color(baseColor.r * 0.8f, baseColor.g * 0.8f, baseColor.b * 0.8f, 1f));
        
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
            
            // BUG FIX #8: Ensure all audio calls have proper null checks
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
            else
            {
                if (showDebugVisuals) Debug.LogWarning("[AUDIO_WARNING] AudioSource or refillSound not assigned");
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

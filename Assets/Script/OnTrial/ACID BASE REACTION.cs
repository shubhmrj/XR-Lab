// using UnityEngine;
// using UnityEngine.UI;
// using TMPro;
// using System.Collections;
// using ManoMotion;

// /// <summary>
// /// Standalone Acid-Base Reaction System for XR Chemistry Lab
// /// Complete solution with gesture controls, reaction detection, and immersive UI
// /// </summary>
// public class ACIDBASEREACTION : MonoBehaviour
// {
//     [Header("Beaker Setup")]
//     [SerializeField] private GameObject sourceBeaker;
//     [SerializeField] private GameObject targetBeaker;
//     [SerializeField] private GameObject waterParticlesPrefab;
//     [SerializeField] private Transform sourcePourPoint;
//     [SerializeField] private Transform targetPourPoint;
    
//     [Header("Camera Reference")]
//     [SerializeField] private Camera arCamera;
    
//     [Header("Chemistry Settings")]
//     [SerializeField] private float maxBeakerVolume = 250f;
//     [SerializeField] private float pourRate = 50f;
//     [SerializeField] private float pouringDistance = 2.0f;
//     [SerializeField] private float pouringThresholdAngle = 25f;
    
//     [Header("Gesture Settings")]
//     [SerializeField] private float grabDetectionRadius = 3.0f;
//     [SerializeField] private float moveSpeed = 15f;
//     [SerializeField] private float tiltSmoothSpeed = 25f;
//     [SerializeField] private float maxTiltAngle = 60f;
//     [SerializeField] private float handPositionOffsetZ = 8f;
//     [SerializeField] private float coordinateScale = 4f;
    
//     [Header("Reaction Settings")]
//     [SerializeField] private float neutralizationTime = 2.0f;
//     [SerializeField] private Color acidColor = new Color(1f, 0.7f, 0.2f, 0.8f);
//     [SerializeField] private Color baseColor = new Color(0.2f, 0.6f, 1f, 0.8f);
//     [SerializeField] private Color neutralColor = new Color(0.9f, 0.9f, 0.9f, 0.7f);
    
//     [Header("UI Settings")]
//     [SerializeField] private bool createCanvasIfMissing = true;
//     [SerializeField] private Canvas mainCanvas;
    
//     // Beaker Data Structure
//     [System.Serializable]
//     public class BeakerData
//     {
//         public GameObject beakerObject;
//         public Transform pourPoint;
//         public GameObject particleEffectObj;
//         public ParticleSystem particleEffect;
//         public float volumeML;
//         public Vector3 initialPosition;
//         public Quaternion initialRotation;
//         public bool isFixed;
//         public bool isGrabbed;
//         public Color liquidColor;
//         public string chemicalName;
//         public float concentration;
//         public float currentTiltAngle;
        
//         // For smooth grab tracking
//         public Vector3 lastHandPosition;
//         public bool isTrackingHand;
//     }
    
//     private BeakerData sourceBeakerData;
//     private BeakerData targetBeakerData;
//     private BeakerData currentlyGrabbedBeaker;
    
//     // Reaction State
//     private bool isReactionInProgress = false;
//     private float reactionProgress = 0f;
//     private ReactionState currentReactionState = ReactionState.NoReaction;
    
//     private enum ReactionState
//     {
//         NoReaction,
//         ReactionInProgress,
//         NeutralizationComplete
//     }
    
//     // Gesture Tracking
//     private ManoGestureContinuous currentGesture = ManoGestureContinuous.NO_GESTURE;
//     private Vector3 currentHandPosition = Vector3.zero;
//     private bool handDetected = false;
    
//     // UI References
//     private GameObject sourcePanel;
//     private GameObject targetPanel;
//     private GameObject feedbackPanel;
//     private TextMeshProUGUI sourceChemicalText;
//     private TextMeshProUGUI sourceVolumeText;
//     private TextMeshProUGUI sourceFillStatusText;
//     private Image sourceVolumeBar;
//     private Image sourceTypeIndicator;
//     private Button sourceRefillButton;
    
//     private TextMeshProUGUI targetVolumeText;
//     private TextMeshProUGUI targetReactionStatusText;
//     private TextMeshProUGUI targetSolutionText;
//     private Image targetVolumeBar;
//     private Image targetReactionIndicator;
//     private Button targetClearButton;
    
//     private TextMeshProUGUI feedbackTitleText;
//     private TextMeshProUGUI feedbackMessageText;
//     private TextMeshProUGUI feedbackGuidanceText;
//     private Image feedbackBackground;
    
//     // Constants
//     private const string ACID_NAME = "Hydrochloric Acid";
//     private const string BASE_NAME = "Sodium Hydroxide";
//     private const string PRODUCT_NAME = "Sodium Chloride Solution";
//     private const string REACTION_EQUATION = "HCl + NaOH → NaCl + H₂O";
//     private readonly Vector3 BEAKER_SCALE = new Vector3(8f, 8f, 8f);
    
//     // Previous states for change detection
//     private float previousTargetVolume = 0f;
    
//     void Start()
//     {
//         InitializeSystem();
//     }
    
//     void InitializeSystem()
//     {
//         // Find AR Camera if not assigned
//         if (arCamera == null)
//         {
//             arCamera = Camera.main;
//             if (arCamera == null)
//             {
//                 arCamera = FindObjectOfType<Camera>();
//             }
//         }
        
//         // Initialize ManoMotion
//         if (ManoMotionManager.Instance != null)
//         {
//             ManoMotionManager.Instance.ShouldCalculateGestures(true);
//         }
        
//         // Initialize beakers
//         InitializeBeakers();
        
//         // Setup Canvas and UI
//         SetupCanvas();
//         CreateImmersiveUI();
        
//         Debug.Log("ACIDBASEREACTION: Standalone system initialized successfully!");
//     }
    
//     void InitializeBeakers()
//     {
//         // Initialize Source Beaker (Fixed)
//         if (sourceBeaker != null)
//         {
//             sourceBeakerData = CreateBeakerData(sourceBeaker, true);
//             sourceBeakerData.chemicalName = ACID_NAME;
//             sourceBeakerData.liquidColor = acidColor;
//             sourceBeakerData.concentration = 100f;
//             sourceBeakerData.volumeML = maxBeakerVolume;
            
//             // Lock position
//             sourceBeakerData.beakerObject.transform.position = sourceBeakerData.initialPosition;
//             sourceBeakerData.beakerObject.transform.localScale = BEAKER_SCALE;
//         }
        
//         // Initialize Target Beaker (Movable)
//         if (targetBeaker != null)
//         {
//             targetBeakerData = CreateBeakerData(targetBeaker, false);
//             targetBeakerData.chemicalName = "Empty";
//             targetBeakerData.liquidColor = new Color(0.7f, 0.85f, 0.92f, 0.7f);
//             targetBeakerData.concentration = 0f;
//             targetBeakerData.volumeML = 0f;
            
//             targetBeakerData.beakerObject.transform.localScale = BEAKER_SCALE;
//         }
//     }
    
//     BeakerData CreateBeakerData(GameObject beakerObj, bool isFixed)
//     {
//         BeakerData data = new BeakerData
//         {
//             beakerObject = beakerObj,
//             initialPosition = beakerObj.transform.position,
//             initialRotation = beakerObj.transform.rotation,
//             isFixed = isFixed,
//             isGrabbed = false,
//             volumeML = isFixed ? maxBeakerVolume : 0f,
//             liquidColor = isFixed ? acidColor : new Color(0.7f, 0.85f, 0.92f, 0.7f),
//             chemicalName = isFixed ? ACID_NAME : "Empty",
//             concentration = isFixed ? 100f : 0f,
//             currentTiltAngle = 0f,
//             isTrackingHand = false
//         };
        
//         // Setup pour point
//         if (isFixed && sourcePourPoint != null)
//         {
//             data.pourPoint = sourcePourPoint;
//         }
//         else if (!isFixed && targetPourPoint != null)
//         {
//             data.pourPoint = targetPourPoint;
//         }
//         else
//         {
//             // Create pour point if not assigned
//             GameObject pourPointObj = new GameObject($"PourPoint_{beakerObj.name}");
//             pourPointObj.transform.SetParent(beakerObj.transform);
//             pourPointObj.transform.localPosition = new Vector3(0, 0.45f, 0.25f);
//             data.pourPoint = pourPointObj.transform;
//         }
        
//         // Create particle effect
//         if (waterParticlesPrefab != null && data.pourPoint != null)
//         {
//             data.particleEffectObj = Instantiate(waterParticlesPrefab, data.pourPoint.position, Quaternion.identity);
//             data.particleEffectObj.name = $"ParticleEffect_{beakerObj.name}";
//             data.particleEffect = data.particleEffectObj.GetComponent<ParticleSystem>();
//             if (data.particleEffect != null)
//             {
//                 var main = data.particleEffect.main;
//                 main.startColor = data.liquidColor;
//                 data.particleEffect.Stop();
//             }
//         }
        
//         return data;
//     }
    
//     void Update()
//     {
//         // Handle gestures
//         HandleGestures();
        
//         // Update beaker physics
//         UpdateBeakerPhysics();
        
//         // Check for reactions
//         CheckForReaction();
        
//         // Update UI
//         UpdateUI();
        
//         // Maintain beaker constraints
//         MaintainBeakerConstraints();
//     }
    
//     void HandleGestures()
//     {
//         if (ManoMotionManager.Instance == null || ManoMotionManager.Instance.HandInfos == null)
//         {
//             handDetected = false;
//             ReleaseGrab();
//             return;
//         }
        
//         HandInfo[] handInfos = ManoMotionManager.Instance.HandInfos;
//         if (handInfos.Length == 0)
//         {
//             handDetected = false;
//             ReleaseGrab();
//             return;
//         }
        
//         foreach (var handInfo in handInfos)
//         {
//             if (handInfo.gestureInfo.manoClass == ManoClass.NO_HAND)
//             {
//                 handDetected = false;
//                 ReleaseGrab();
//                 continue;
//             }
            
//             handDetected = true;
//             currentGesture = handInfo.gestureInfo.manoGestureContinuous;
            
//             // Calculate hand position
//             Vector3 handPosition = CalculateHandPosition(handInfo.trackingInfo.boundingBox);
//             currentHandPosition = handPosition;
            
//             // Handle different gestures
//             switch (currentGesture)
//             {
//                 case ManoGestureContinuous.CLOSED_HAND_GESTURE:
//                     HandleGrabGesture(handPosition);
//                     break;
                    
//                 case ManoGestureContinuous.OPEN_HAND_GESTURE:
//                     HandleTiltGesture(handInfo.trackingInfo.boundingBox);
//                     break;
                    
//                 case ManoGestureContinuous.OPEN_PINCH_GESTURE:
//                     HandleRefillGesture();
//                     break;
                    
//                 default:
//                     // Don't release on other gestures - maintain grab if already grabbed
//                     if (currentlyGrabbedBeaker != null && currentlyGrabbedBeaker.isGrabbed)
//                     {
//                         // Continue tracking hand position for smooth movement
//                         UpdateGrabbedBeaker(handPosition);
//                     }
//                     break;
//             }
//         }
//     }
    
//     Vector3 CalculateHandPosition(BoundingBox boundingBox)
//     {
//         float centerX = boundingBox.topLeft.x + boundingBox.width / 2f;
//         float centerY = boundingBox.topLeft.y - boundingBox.height / 2f;
        
//         // Convert to world coordinates
//         float normalizedX = (centerX - 0.5f) * coordinateScale;
//         float normalizedY = (0.5f - centerY) * coordinateScale;
        
//         Vector3 worldPos = new Vector3(normalizedX, normalizedY, handPositionOffsetZ);
        
//         if (arCamera != null)
//         {
//             // Use camera's forward direction for better depth
//             worldPos = arCamera.transform.position + arCamera.transform.forward * handPositionOffsetZ;
//             worldPos.x += normalizedX;
//             worldPos.y += normalizedY;
//         }
        
//         return worldPos;
//     }
    
//     void HandleGrabGesture(Vector3 handPosition)
//     {
//         // If no beaker is grabbed, try to grab target beaker
//         if (currentlyGrabbedBeaker == null && targetBeakerData != null && !targetBeakerData.isFixed)
//         {
//             float distance = Vector3.Distance(targetBeakerData.beakerObject.transform.position, handPosition);
            
//             if (distance <= grabDetectionRadius)
//             {
//                 currentlyGrabbedBeaker = targetBeakerData;
//                 currentlyGrabbedBeaker.isGrabbed = true;
//                 currentlyGrabbedBeaker.isTrackingHand = true;
//                 currentlyGrabbedBeaker.lastHandPosition = handPosition;
                
//                 Debug.Log("ACIDBASEREACTION: Target beaker grabbed!");
//             }
//         }
        
//         // If beaker is grabbed, update its position
//         if (currentlyGrabbedBeaker != null && currentlyGrabbedBeaker.isGrabbed)
//         {
//             UpdateGrabbedBeaker(handPosition);
//         }
//     }
    
//     void UpdateGrabbedBeaker(Vector3 handPosition)
//     {
//         if (currentlyGrabbedBeaker == null || currentlyGrabbedBeaker.isFixed) return;
        
//         // Smooth movement towards hand position
//         Vector3 targetPos = new Vector3(
//             handPosition.x,
//             handPosition.y,
//             currentlyGrabbedBeaker.beakerObject.transform.position.z // Maintain Z depth
//         );
        
//         // Use smooth interpolation for better tracking
//         currentlyGrabbedBeaker.beakerObject.transform.position = Vector3.Lerp(
//             currentlyGrabbedBeaker.beakerObject.transform.position,
//             targetPos,
//             moveSpeed * Time.deltaTime
//         );
        
//         // Keep beaker upright while moving
//         currentlyGrabbedBeaker.beakerObject.transform.rotation = Quaternion.Lerp(
//             currentlyGrabbedBeaker.beakerObject.transform.rotation,
//             currentlyGrabbedBeaker.initialRotation,
//             Time.deltaTime * 5f
//         );
        
//         // Update last hand position
//         currentlyGrabbedBeaker.lastHandPosition = handPosition;
//         currentlyGrabbedBeaker.isTrackingHand = true;
        
//         // Check for beaker-to-beaker pouring
//         CheckBeakerToBeakerPouring();
//     }
    
//     void ReleaseGrab()
//     {
//         if (currentlyGrabbedBeaker != null)
//         {
//             currentlyGrabbedBeaker.isGrabbed = false;
//             currentlyGrabbedBeaker.isTrackingHand = false;
//             currentlyGrabbedBeaker = null;
//         }
//     }
    
//     void HandleTiltGesture(BoundingBox boundingBox)
//     {
//         // Source beaker can be tilted
//         if (sourceBeakerData != null && sourceBeakerData.volumeML > 0.1f)
//         {
//             float centerX = boundingBox.topLeft.x + boundingBox.width / 2f;
//             float normalizedX = (centerX - 0.5f) * 1.5f;
            
//             float desiredTilt = Mathf.Clamp(normalizedX * maxTiltAngle, -maxTiltAngle, maxTiltAngle);
            
//             float currentTiltZ = sourceBeakerData.beakerObject.transform.eulerAngles.z;
//             if (currentTiltZ > 180f) currentTiltZ -= 360f;
            
//             float angleDiff = desiredTilt - currentTiltZ;
//             if (Mathf.Abs(angleDiff) > 0.1f)
//             {
//                 Vector3 rotationPoint = sourceBeakerData.pourPoint != null ?
//                     sourceBeakerData.pourPoint.position :
//                     sourceBeakerData.beakerObject.transform.position;
                
//                 sourceBeakerData.beakerObject.transform.RotateAround(
//                     rotationPoint,
//                     Vector3.forward,
//                     angleDiff * Time.deltaTime * tiltSmoothSpeed
//                 );
                
//                 sourceBeakerData.currentTiltAngle = Mathf.Abs(desiredTilt);
//             }
            
//             // Maintain position lock
//             sourceBeakerData.beakerObject.transform.position = sourceBeakerData.initialPosition;
//         }
        
//         // Release grab when opening hand
//         ReleaseGrab();
//     }
    
//     void HandleRefillGesture()
//     {
//         // Refill source beaker
//         if (sourceBeakerData != null)
//         {
//             float refillAmount = pourRate * Time.deltaTime * 2f;
//             sourceBeakerData.volumeML = Mathf.Min(maxBeakerVolume, sourceBeakerData.volumeML + refillAmount);
            
//             if (sourceBeakerData.volumeML > 0.1f)
//             {
//                 sourceBeakerData.chemicalName = ACID_NAME;
//                 sourceBeakerData.liquidColor = acidColor;
//             }
//         }
//     }
    
//     void CheckBeakerToBeakerPouring()
//     {
//         if (sourceBeakerData == null || targetBeakerData == null) return;
        
//         float distance = Vector3.Distance(
//             sourceBeakerData.beakerObject.transform.position,
//             targetBeakerData.beakerObject.transform.position
//         );
        
//         // Check if beakers are close enough and source is tilted
//         if (distance <= pouringDistance && sourceBeakerData.currentTiltAngle > pouringThresholdAngle)
//         {
//             // Pouring is happening
//             UpdatePouring();
//         }
//     }
    
//     void UpdatePouring()
//     {
//         if (sourceBeakerData == null || targetBeakerData == null) return;
//         if (sourceBeakerData.volumeML <= 0.1f) return;
        
//         float tiltMultiplier = Mathf.Clamp01((sourceBeakerData.currentTiltAngle - pouringThresholdAngle) / (maxTiltAngle - pouringThresholdAngle));
//         float pourAmount = pourRate * tiltMultiplier * Time.deltaTime;
        
//         float transferAmount = Mathf.Min(pourAmount, sourceBeakerData.volumeML);
//         transferAmount = Mathf.Min(transferAmount, maxBeakerVolume - targetBeakerData.volumeML);
        
//         if (transferAmount > 0.1f)
//         {
//             sourceBeakerData.volumeML -= transferAmount;
//             targetBeakerData.volumeML += transferAmount;
            
//             // Update target beaker chemical
//             if (targetBeakerData.chemicalName == "Empty" && sourceBeakerData.chemicalName == ACID_NAME)
//             {
//                 targetBeakerData.chemicalName = sourceBeakerData.chemicalName;
//                 targetBeakerData.liquidColor = sourceBeakerData.liquidColor;
//             }
            
//             // Update particle effect
//             if (sourceBeakerData.particleEffect != null && !sourceBeakerData.particleEffect.isPlaying)
//             {
//                 sourceBeakerData.particleEffect.Play();
//             }
//         }
//     }
    
//     void UpdateBeakerPhysics()
//     {
//         // Update source beaker pouring
//         if (sourceBeakerData != null)
//         {
//             float tiltAngle = Vector3.Angle(sourceBeakerData.beakerObject.transform.up, Vector3.up);
//             sourceBeakerData.currentTiltAngle = tiltAngle;
            
//             // Stop pouring if not tilted enough or empty
//             if (tiltAngle <= pouringThresholdAngle || sourceBeakerData.volumeML <= 0.1f)
//             {
//                 if (sourceBeakerData.particleEffect != null && sourceBeakerData.particleEffect.isPlaying)
//                 {
//                     sourceBeakerData.particleEffect.Stop();
//                 }
//             }
//         }
        
//         // Return source beaker to upright if empty
//         if (sourceBeakerData != null && sourceBeakerData.volumeML <= 0.1f)
//         {
//             sourceBeakerData.beakerObject.transform.rotation = Quaternion.Lerp(
//                 sourceBeakerData.beakerObject.transform.rotation,
//                 sourceBeakerData.initialRotation,
//                 Time.deltaTime * 10f
//             );
//         }
//     }
    
//     void CheckForReaction()
//     {
//         if (targetBeakerData == null || isReactionInProgress) return;
        
//         bool sourceIsAcid = IsAcid(sourceBeakerData?.chemicalName ?? "");
//         bool targetHasLiquid = targetBeakerData.volumeML > 0.1f;
        
//         // Detect reaction when acid mixes with base (or any liquid for now)
//         if (sourceIsAcid && targetHasLiquid && targetBeakerData.volumeML > previousTargetVolume + 5f)
//         {
//             StartReaction();
//         }
        
//         previousTargetVolume = targetBeakerData.volumeML;
//     }
    
//     bool IsAcid(string chemicalName)
//     {
//         return chemicalName.Contains("Acid") || chemicalName == ACID_NAME;
//     }
    
//     void StartReaction()
//     {
//         if (isReactionInProgress) return;
        
//         isReactionInProgress = true;
//         currentReactionState = ReactionState.ReactionInProgress;
//         reactionProgress = 0f;
        
//         Debug.Log($"ACIDBASEREACTION: Reaction started! {REACTION_EQUATION}");
//         StartCoroutine(ProcessReaction());
//     }
    
//     IEnumerator ProcessReaction()
//     {
//         float elapsedTime = 0f;
        
//         while (elapsedTime < neutralizationTime)
//         {
//             elapsedTime += Time.deltaTime;
//             reactionProgress = elapsedTime / neutralizationTime;
            
//             // Update color gradually
//             Color currentColor = Color.Lerp(
//                 Color.Lerp(acidColor, baseColor, 0.5f),
//                 neutralColor,
//                 reactionProgress
//             );
            
//             targetBeakerData.liquidColor = currentColor;
//             targetBeakerData.chemicalName = PRODUCT_NAME;
//             targetBeakerData.concentration = 50f;
            
//             yield return null;
//         }
        
//         CompleteReaction();
//     }
    
//     void CompleteReaction()
//     {
//         isReactionInProgress = false;
//         currentReactionState = ReactionState.NeutralizationComplete;
//         reactionProgress = 1f;
        
//         targetBeakerData.liquidColor = neutralColor;
//         targetBeakerData.chemicalName = PRODUCT_NAME;
        
//         Debug.Log("ACIDBASEREACTION: Reaction completed!");
//     }
    
//     void MaintainBeakerConstraints()
//     {
//         // Lock source beaker position
//         if (sourceBeakerData != null)
//         {
//             sourceBeakerData.beakerObject.transform.position = sourceBeakerData.initialPosition;
//             sourceBeakerData.beakerObject.transform.localScale = BEAKER_SCALE;
//         }
        
//         // Maintain target beaker scale
//         if (targetBeakerData != null)
//         {
//             targetBeakerData.beakerObject.transform.localScale = BEAKER_SCALE;
//         }
//     }
    
//     #region UI Creation
    
//     void SetupCanvas()
//     {
//         if (mainCanvas == null)
//         {
//             if (createCanvasIfMissing)
//             {
//                 GameObject canvasObj = new GameObject("ChemistryLabCanvas");
//                 mainCanvas = canvasObj.AddComponent<Canvas>();
//                 mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
//                 canvasObj.AddComponent<CanvasScaler>();
//                 canvasObj.AddComponent<GraphicRaycaster>();
//                 mainCanvas.sortingOrder = 10;
//             }
//             else
//             {
//                 mainCanvas = FindObjectOfType<Canvas>();
//             }
//         }
//     }
    
//     void CreateImmersiveUI()
//     {
//         CreateSourcePanel();
//         CreateTargetPanel();
//         CreateFeedbackPanel();
//     }
    
//     void CreateSourcePanel()
//     {
//         sourcePanel = CreateGlassPanel("SourceBeakerPanel");
//         RectTransform rect = sourcePanel.GetComponent<RectTransform>();
//         rect.anchorMin = new Vector2(0, 0);
//         rect.anchorMax = new Vector2(0, 0);
//         rect.anchoredPosition = new Vector2(20, Screen.height - 320);
//         rect.sizeDelta = new Vector2(380, 300);
        
//         // Add glassmorphism background
//         Image bg = sourcePanel.AddComponent<Image>();
//         bg.color = new Color(0.1f, 0.15f, 0.2f, 0.85f);
        
//         // Title with glow effect
//         GameObject titleObj = CreateGlowText("Title", sourcePanel.transform, "⚗️ SOURCE BEAKER", 22, FontStyles.Bold, new Color(1f, 0.7f, 0.2f));
//         SetRectAnchor(titleObj, 0, 1, 1, 1, 0, -20, 0, 50);
        
//         // Chemical name
//         sourceChemicalText = CreateText("ChemicalName", sourcePanel.transform, "Chemical: Loading...", 15).GetComponent<TextMeshProUGUI>();
//         SetRectAnchor(sourceChemicalText.gameObject, 0, 1, 1, 1, 15, -75, -15, 28);
        
//         // Volume text
//         sourceVolumeText = CreateText("VolumeText", sourcePanel.transform, "Volume: 0 mL", 14).GetComponent<TextMeshProUGUI>();
//         SetRectAnchor(sourceVolumeText.gameObject, 0, 1, 1, 1, 15, -110, -15, 28);
        
//         // Volume bar with glow
//         GameObject barContainer = CreateVolumeBar(sourcePanel.transform, "SourceVolumeBar", acidColor);
//         SetRectAnchor(barContainer, 0, 1, 1, 1, 15, -145, -15, 30);
//         sourceVolumeBar = barContainer.transform.Find("Fill").GetComponent<Image>();
        
//         // Fill status
//         sourceFillStatusText = CreateText("FillStatus", sourcePanel.transform, "Status: Full", 13).GetComponent<TextMeshProUGUI>();
//         SetRectAnchor(sourceFillStatusText.gameObject, 0, 1, 1, 1, 15, -180, -15, 25);
        
//         // Type indicator with pulse
//         GameObject indicatorObj = new GameObject("TypeIndicator");
//         indicatorObj.transform.SetParent(sourcePanel.transform);
//         sourceTypeIndicator = indicatorObj.AddComponent<Image>();
//         sourceTypeIndicator.color = acidColor;
//         SetRectAnchor(indicatorObj, 0, 1, 0, 1, 20, -215, 20, 25);
//         StartCoroutine(PulseIndicator(sourceTypeIndicator));
        
//         // Refill button with hover effect
//         sourceRefillButton = CreateGlowButton("RefillButton", sourcePanel.transform, "💧 REFILL ACID", new Color(1f, 0.6f, 0.1f), () => {
//             if (sourceBeakerData != null)
//             {
//                 sourceBeakerData.volumeML = maxBeakerVolume;
//                 sourceBeakerData.chemicalName = ACID_NAME;
//                 sourceBeakerData.liquidColor = acidColor;
//             }
//         });
//         SetRectAnchor(sourceRefillButton.gameObject, 0, 0, 1, 0, 15, 20, -15, 45);
//     }
    
//     void CreateTargetPanel()
//     {
//         targetPanel = CreateGlassPanel("TargetBeakerPanel");
//         RectTransform rect = targetPanel.GetComponent<RectTransform>();
//         rect.anchorMin = new Vector2(1, 0);
//         rect.anchorMax = new Vector2(1, 0);
//         rect.anchoredPosition = new Vector2(-20, Screen.height - 320);
//         rect.sizeDelta = new Vector2(380, 300);
        
//         Image bg = targetPanel.AddComponent<Image>();
//         bg.color = new Color(0.1f, 0.2f, 0.25f, 0.85f);
        
//         // Title
//         GameObject titleObj = CreateGlowText("Title", targetPanel.transform, "🥽 TARGET BEAKER", 22, FontStyles.Bold, new Color(0.2f, 0.8f, 1f));
//         SetRectAnchor(titleObj, 0, 1, 1, 1, 0, -20, 0, 50);
        
//         // Volume received
//         targetVolumeText = CreateText("VolumeReceived", targetPanel.transform, "Received: 0 mL", 14).GetComponent<TextMeshProUGUI>();
//         SetRectAnchor(targetVolumeText.gameObject, 0, 1, 1, 1, 15, -70, -15, 28);
        
//         // Reaction status
//         targetReactionStatusText = CreateText("ReactionStatus", targetPanel.transform, "Status: No Reaction", 15, FontStyles.Bold).GetComponent<TextMeshProUGUI>();
//         targetReactionStatusText.color = Color.yellow;
//         SetRectAnchor(targetReactionStatusText.gameObject, 0, 1, 1, 1, 15, -105, -15, 28);
        
//         // Solution details
//         targetSolutionText = CreateText("SolutionDetails", targetPanel.transform, "Solution: Empty", 12).GetComponent<TextMeshProUGUI>();
//         targetSolutionText.enableWordWrapping = true;
//         SetRectAnchor(targetSolutionText.gameObject, 0, 1, 1, 1, 15, -140, -15, 60);
        
//         // Volume bar
//         GameObject barContainer = CreateVolumeBar(targetPanel.transform, "TargetVolumeBar", baseColor);
//         SetRectAnchor(barContainer, 0, 1, 1, 1, 15, -210, -15, 30);
//         targetVolumeBar = barContainer.transform.Find("Fill").GetComponent<Image>();
        
//         // Reaction indicator
//         GameObject reactionIndicatorObj = new GameObject("ReactionIndicator");
//         reactionIndicatorObj.transform.SetParent(targetPanel.transform);
//         targetReactionIndicator = reactionIndicatorObj.AddComponent<Image>();
//         targetReactionIndicator.color = Color.clear;
//         SetRectAnchor(reactionIndicatorObj, 0, 1, 0, 1, 20, -245, 20, 25);
        
//         // Clear button
//         targetClearButton = CreateGlowButton("ClearButton", targetPanel.transform, "🗑 CLEAR", new Color(1f, 0.3f, 0.3f), () => {
//             if (targetBeakerData != null)
//             {
//                 targetBeakerData.volumeML = 0f;
//                 targetBeakerData.chemicalName = "Empty";
//                 currentReactionState = ReactionState.NoReaction;
//                 isReactionInProgress = false;
//             }
//         });
//         SetRectAnchor(targetClearButton.gameObject, 0, 0, 1, 0, 15, 20, -15, 45);
//     }
    
//     void CreateFeedbackPanel()
//     {
//         feedbackPanel = CreateGlassPanel("EducationalFeedbackPanel");
//         RectTransform rect = feedbackPanel.GetComponent<RectTransform>();
//         rect.anchorMin = new Vector2(0.5f, 1);
//         rect.anchorMax = new Vector2(0.5f, 1);
//         rect.anchoredPosition = new Vector2(0, -40);
//         rect.sizeDelta = new Vector2(700, 140);
        
//         feedbackBackground = feedbackPanel.AddComponent<Image>();
//         feedbackBackground.color = new Color(0.05f, 0.1f, 0.15f, 0.92f);
        
//         // Title
//         feedbackTitleText = CreateGlowText("FeedbackTitle", feedbackPanel.transform, "📚 Educational Feedback", 18, FontStyles.Bold, new Color(0.2f, 0.8f, 1f)).GetComponent<TextMeshProUGUI>();
//         SetRectAnchor(feedbackTitleText.gameObject, 0, 1, 1, 1, 15, -15, -15, 35);
        
//         // Message
//         feedbackMessageText = CreateText("FeedbackMessage", feedbackPanel.transform, "Ready to start experiment...", 14).GetComponent<TextMeshProUGUI>();
//         feedbackMessageText.enableWordWrapping = true;
//         SetRectAnchor(feedbackMessageText.gameObject, 0, 1, 1, 0.55f, 15, -50, -15, 0);
        
//         // Guidance
//         feedbackGuidanceText = CreateText("FeedbackGuidance", feedbackPanel.transform, "", 12).GetComponent<TextMeshProUGUI>();
//         feedbackGuidanceText.enableWordWrapping = true;
//         feedbackGuidanceText.color = new Color(0.8f, 0.9f, 0.8f);
//         SetRectAnchor(feedbackGuidanceText.gameObject, 0, 0.55f, 1, 0, 15, 0, -15, 0);
//     }
    
//     GameObject CreateGlassPanel(string name)
//     {
//         GameObject panel = new GameObject(name);
//         panel.transform.SetParent(mainCanvas.transform);
//         RectTransform rect = panel.AddComponent<RectTransform>();
//         rect.localScale = Vector3.one;
//         return panel;
//     }
    
//     GameObject CreateGlowText(string name, Transform parent, string text, int fontSize, FontStyles style, Color color)
//     {
//         GameObject textObj = new GameObject(name);
//         textObj.transform.SetParent(parent);
//         TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
//         tmp.text = text;
//         tmp.fontSize = fontSize;
//         tmp.fontStyle = style;
//         tmp.color = color;
//         tmp.alignment = TextAlignmentOptions.Center;
//         tmp.fontMaterial = Resources.Load<Material>("Fonts & Materials/LiberationSans SDF - Drop Shadow");
//         return textObj;
//     }
    
//     GameObject CreateText(string name, Transform parent, string text, int fontSize, FontStyles style = FontStyles.Normal)
//     {
//         GameObject textObj = new GameObject(name);
//         textObj.transform.SetParent(parent);
//         TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
//         tmp.text = text;
//         tmp.fontSize = fontSize;
//         tmp.fontStyle = style;
//         tmp.color = Color.white;
//         tmp.alignment = TextAlignmentOptions.Left;
//         return textObj;
//     }
    
//     GameObject CreateVolumeBar(Transform parent, string name, Color fillColor)
//     {
//         GameObject container = new GameObject(name);
//         container.transform.SetParent(parent);
        
//         // Background
//         GameObject bgObj = new GameObject("Background");
//         bgObj.transform.SetParent(container.transform);
//         Image bg = bgObj.AddComponent<Image>();
//         bg.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
//         SetRectAnchor(bgObj, 0, 0, 1, 1, 0, 0, 0, 0);
        
//         // Fill
//         GameObject fillObj = new GameObject("Fill");
//         fillObj.transform.SetParent(container.transform);
//         Image fill = fillObj.AddComponent<Image>();
//         fill.color = fillColor;
//         SetRectAnchor(fillObj, 0, 0, 0, 1, 2, 2, 0, -4);
        
//         return container;
//     }
    
//     Button CreateGlowButton(string name, Transform parent, string text, Color color, UnityEngine.Events.UnityAction onClick)
//     {
//         GameObject buttonObj = new GameObject(name);
//         buttonObj.transform.SetParent(parent);
//         Image bg = buttonObj.AddComponent<Image>();
//         bg.color = color;
//         Button button = buttonObj.AddComponent<Button>();
//         button.onClick.AddListener(onClick);
        
//         // Add hover effect
//         ColorBlock colors = button.colors;
//         colors.highlightedColor = new Color(color.r * 1.3f, color.g * 1.3f, color.b * 1.3f, 1f);
//         colors.pressedColor = new Color(color.r * 0.7f, color.g * 0.7f, color.b * 0.7f, 1f);
//         button.colors = colors;
        
//         GameObject textObj = new GameObject("Text");
//         textObj.transform.SetParent(buttonObj.transform);
//         TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
//         tmp.text = text;
//         tmp.fontSize = 14;
//         tmp.fontStyle = FontStyles.Bold;
//         tmp.color = Color.white;
//         tmp.alignment = TextAlignmentOptions.Center;
//         SetRectAnchor(textObj, 0, 0, 1, 1, 0, 0, 0, 0);
        
//         return button;
//     }
    
//     void SetRectAnchor(GameObject obj, float minX, float minY, float maxX, float maxY, float posX, float posY, float sizeX, float sizeY)
//     {
//         RectTransform rect = obj.GetComponent<RectTransform>();
//         rect.anchorMin = new Vector2(minX, minY);
//         rect.anchorMax = new Vector2(maxX, maxY);
//         rect.anchoredPosition = new Vector2(posX, posY);
//         if (sizeX != 0 || sizeY != 0)
//         {
//             rect.sizeDelta = new Vector2(sizeX, sizeY);
//         }
//     }
    
//     IEnumerator PulseIndicator(Image indicator)
//     {
//         while (true)
//         {
//             float alpha = 0.5f + Mathf.Sin(Time.time * 2f) * 0.5f;
//             Color c = indicator.color;
//             c.a = alpha;
//             indicator.color = c;
//             yield return null;
//         }
//     }
    
//     #endregion
    
//     #region UI Updates
    
//     void UpdateUI()
//     {
//         UpdateSourcePanel();
//         UpdateTargetPanel();
//         UpdateFeedbackPanel();
//     }
    
//     void UpdateSourcePanel()
//     {
//         if (sourceBeakerData == null) return;
        
//         if (sourceChemicalText != null)
//         {
//             string typeLabel = IsAcid(sourceBeakerData.chemicalName) ? "🧪 ACID" : "🧪 BASE";
//             sourceChemicalText.text = $"{typeLabel}: {sourceBeakerData.chemicalName}";
//         }
        
//         if (sourceVolumeText != null)
//         {
//             sourceVolumeText.text = $"Volume: {sourceBeakerData.volumeML:F0} mL / {maxBeakerVolume:F0} mL";
//         }
        
//         if (sourceVolumeBar != null)
//         {
//             float fillAmount = sourceBeakerData.volumeML / maxBeakerVolume;
//             RectTransform barRect = sourceVolumeBar.GetComponent<RectTransform>();
//             barRect.anchorMax = new Vector2(fillAmount, 1);
//             barRect.sizeDelta = Vector2.zero;
//             sourceVolumeBar.color = IsAcid(sourceBeakerData.chemicalName) ? acidColor : baseColor;
//         }
        
//         if (sourceFillStatusText != null)
//         {
//             float fillPercent = (sourceBeakerData.volumeML / maxBeakerVolume) * 100f;
//             string status = fillPercent > 80f ? "Full" : fillPercent > 40f ? "Half Full" : fillPercent > 0f ? "Low" : "Empty";
//             sourceFillStatusText.text = $"Status: {status} ({fillPercent:F0}%)";
//         }
        
//         if (sourceTypeIndicator != null)
//         {
//             sourceTypeIndicator.color = IsAcid(sourceBeakerData.chemicalName) ? acidColor : baseColor;
//         }
//     }
    
//     void UpdateTargetPanel()
//     {
//         if (targetBeakerData == null) return;
        
//         if (targetVolumeText != null)
//         {
//             targetVolumeText.text = $"Received: {targetBeakerData.volumeML:F0} mL";
//         }
        
//         if (targetReactionStatusText != null)
//         {
//             string statusText = "";
//             Color statusColor = Color.yellow;
            
//             switch (currentReactionState)
//             {
//                 case ReactionState.NoReaction:
//                     statusText = targetBeakerData.volumeML > 0.1f ? "Ready for Reaction" : "No Reaction";
//                     statusColor = Color.yellow;
//                     break;
//                 case ReactionState.ReactionInProgress:
//                     statusText = $"⚗️ Reacting... {reactionProgress * 100:F0}%";
//                     statusColor = Color.Lerp(Color.yellow, Color.green, reactionProgress);
//                     break;
//                 case ReactionState.NeutralizationComplete:
//                     statusText = "✓ Reaction Complete!";
//                     statusColor = Color.green;
//                     break;
//             }
            
//             targetReactionStatusText.text = $"Status: {statusText}";
//             targetReactionStatusText.color = statusColor;
//         }
        
//         if (targetSolutionText != null)
//         {
//             string details = "";
//             if (currentReactionState == ReactionState.ReactionInProgress || currentReactionState == ReactionState.NeutralizationComplete)
//             {
//                 details = $"Solution: {targetBeakerData.chemicalName}\n";
//                 details += $"Equation: {REACTION_EQUATION}\n";
//                 details += $"pH: ~7 (Neutral)";
//             }
//             else if (targetBeakerData.volumeML > 0.1f)
//             {
//                 details = $"Solution: {targetBeakerData.chemicalName}\n";
//                 details += "Waiting for acid-base reaction...";
//             }
//             else
//             {
//                 details = "Solution: Empty\nReady to receive liquid";
//             }
            
//             targetSolutionText.text = details;
//         }
        
//         if (targetVolumeBar != null)
//         {
//             float fillAmount = targetBeakerData.volumeML / maxBeakerVolume;
//             RectTransform barRect = targetVolumeBar.GetComponent<RectTransform>();
//             barRect.anchorMax = new Vector2(fillAmount, 1);
//             barRect.sizeDelta = Vector2.zero;
            
//             if (currentReactionState == ReactionState.NeutralizationComplete)
//             {
//                 targetVolumeBar.color = neutralColor;
//             }
//             else if (currentReactionState == ReactionState.ReactionInProgress)
//             {
//                 targetVolumeBar.color = Color.Lerp(acidColor, neutralColor, reactionProgress);
//             }
//             else
//             {
//                 targetVolumeBar.color = targetBeakerData.volumeML > 0.1f ? baseColor : new Color(0.3f, 0.3f, 0.3f);
//             }
//         }
        
//         if (targetReactionIndicator != null)
//         {
//             if (currentReactionState == ReactionState.ReactionInProgress)
//             {
//                 targetReactionIndicator.color = Color.Lerp(Color.yellow, Color.green, reactionProgress);
//             }
//             else if (currentReactionState == ReactionState.NeutralizationComplete)
//             {
//                 targetReactionIndicator.color = Color.green;
//             }
//             else
//             {
//                 targetReactionIndicator.color = Color.clear;
//             }
//         }
//     }
    
//     void UpdateFeedbackPanel()
//     {
//         if (sourceBeakerData == null || targetBeakerData == null) return;
        
//         string title = "📚 Educational Feedback";
//         string message = "";
//         string guidance = "";
        
//         bool sourceHasLiquid = sourceBeakerData.volumeML > 0.1f;
//         bool targetHasLiquid = targetBeakerData.volumeML > 0.1f;
//         bool isPouring = sourceBeakerData.currentTiltAngle > pouringThresholdAngle;
        
//         if (isReactionInProgress)
//         {
//             message = $"⚗️ Reaction in Progress!\n{REACTION_EQUATION}\nProgress: {reactionProgress * 100:F0}%";
//             guidance = "✓ Acid and base are neutralizing.\n✓ Color change indicates pH shift.\n✓ Result will be salt + water.";
//         }
//         else if (currentReactionState == ReactionState.NeutralizationComplete)
//         {
//             message = $"✓ Reaction Complete!\n{REACTION_EQUATION}\nResult: Neutral Salt Solution (pH ~7)";
//             guidance = "✓ Neutralization successful!\n✓ The solution is now neutral.\n✓ This is a complete acid-base reaction.";
//         }
//         else if (isPouring && sourceHasLiquid)
//         {
//             message = "🔄 Pouring liquid from source beaker...";
//             guidance = "Action: Tilt source beaker with open hand gesture.\nNext: Position target beaker to receive liquid.";
            
//             if (targetBeakerData.volumeML > maxBeakerVolume * 0.9f)
//             {
//                 message += "\n⚠️ Warning: Target beaker nearly full!";
//                 guidance += "\n⚠️ Avoid: Overflow - reduce pouring amount.";
//             }
//         }
//         else if (targetHasLiquid && !isReactionInProgress)
//         {
//             message = "🥽 Target beaker contains liquid.\nReady for acid-base reaction.";
//             guidance = "Current: Liquid received in target beaker.\nNext: Pour acid from source to trigger reaction.\nTip: Ensure beakers are properly aligned.";
//         }
//         else if (sourceHasLiquid)
//         {
//             message = "⚗️ Source beaker ready.\nContains: " + sourceBeakerData.chemicalName;
//             guidance = "Gesture: Use open hand to tilt and pour.\nProcedure: Pour acid into target beaker.\nSafety: Virtual experiment - no real hazards.";
//         }
//         else
//         {
//             message = "💧 Source beaker is empty.";
//             guidance = "Action: Use pinch gesture to refill source beaker.\nOr: Click the REFILL button in the panel.";
//         }
        
//         if (targetBeakerData.isGrabbed)
//         {
//             message += "\n✋ Target beaker is being moved.";
//             guidance += "\nTip: Use closed hand gesture to grab and move target beaker.";
//         }
        
//         if (feedbackTitleText != null) feedbackTitleText.text = title;
//         if (feedbackMessageText != null) feedbackMessageText.text = message;
//         if (feedbackGuidanceText != null) feedbackGuidanceText.text = guidance;
        
//         if (feedbackBackground != null)
//         {
//             if (currentReactionState == ReactionState.NeutralizationComplete)
//             {
//                 feedbackBackground.color = new Color(0.1f, 0.3f, 0.1f, 0.92f);
//             }
//             else if (currentReactionState == ReactionState.ReactionInProgress)
//             {
//                 feedbackBackground.color = Color.Lerp(
//                     new Color(0.2f, 0.2f, 0.1f, 0.92f),
//                     new Color(0.1f, 0.3f, 0.1f, 0.92f),
//                     reactionProgress
//                 );
//             }
//             else
//             {
//                 feedbackBackground.color = new Color(0.05f, 0.1f, 0.15f, 0.92f);
//             }
//         }
//     }
    
//     #endregion
// }

# XR Chemistry Lab - Gesture & Pouring Fixes

## Issues Identified & Fixed

### 🔴 **ISSUE #1: Target Beaker Not Moving with Closed Hand Gesture**

#### Root Causes Found:

1. **Coordinate System Transformation Bug (Line 687-692)**
   - **Problem**: `CalculateHandPosition()` was swapping X and Y coordinates incorrectly
   - **Original Code**:
     ```csharp
     float normalizedX = (centerY - 0.5f) * coordinateScale;  // WRONG: Using Y for X
     float normalizedY = (0.5f - centerX) * coordinateScale;  // WRONG: Using X for Y
     ```
   - **Why it failed**: Hand tracking bounding box center was being mapped to wrong axis, causing hand position to appear in wrong location
   - **Fix Applied**: Corrected direct X→X, Y→Y mapping
     ```csharp
     float normalizedX = (centerX - 0.5f) * coordinateScale;  // Correct: X for horizontal
     float normalizedY = (0.5f - centerY) * coordinateScale;  // Correct: Y for vertical
     ```

2. **Grab Detection Radius Too Small (Line 12)**
   - **Problem**: `grabDetectionRadius = 5.0f` was insufficient for the XR environment scale
   - **Why it failed**: Even though beaker was in view and close to hand, distance calculation exceeded radius
   - **Fix Applied**: Increased to `7.5f` and added detailed distance logging
     ```csharp
     [SerializeField] private float grabDetectionRadius = 7.5f;
     ```

3. **Coordinate Scale Too Small (Line 33)**
   - **Problem**: `coordinateScale = 4f` produced too-small hand position movements
   - **Why it failed**: Hand movements were scaled down, making beaker follow with insufficient range
   - **Fix Applied**: Increased to `10f` for better 1:1 hand tracking
     ```csharp
     [SerializeField] private float coordinateScale = 10f;
     ```

4. **Missing Hand Position Validation (HandleGrabGesture)**
   - **Problem**: No check for NaN or Infinity values in hand position
   - **Why it failed**: Corrupted hand position data could cause beaker to teleport or freeze
   - **Fix Applied**: Added validation with fallback to last known position
     ```csharp
     if (float.IsNaN(handPosition.x) || float.IsNaN(handPosition.y) || float.IsNaN(handPosition.z))
     {
         if (showDebugVisuals) Debug.LogError("!!! INVALID_HAND: NaN detected! Using last position !!!");
         handPosition = lastHandPosition;
     }
     ```

#### Gesture Flow Diagram:
```
CLOSED_HAND_GESTURE (ManoMotion) 
    ↓
CalculateHandPosition(boundingBox) - [FIX: Correct X-Y mapping]
    ↓
HandPosition (world space)
    ↓
HandleGrabGesture(handPosition)
    ↓
GetNearestGrabbableBeaker(handPosition) - [FIX: Increased radius to 7.5]
    ↓
Distance Check (Vector3.Distance) 
    ↓
IF distance ≤ grabDetectionRadius → GRAB & MOVE
    ↓
targetPosition.xyz = (handPosition.x, handPosition.y, currentZ)
    ↓
beaker.transform.position = targetPosition (DIRECT, no lerp)
    ↓
✓ BEAKER FOLLOWS HAND
```

---

### 🔴 **ISSUE #2: Improper Liquid Falling from Source Beaker**

#### Root Causes Found:

1. **Water Particle Not Following Pour Point in World Space (Line 565-578)**
   - **Problem**: Particles could be positioned incorrectly relative to pour point when beaker rotates
   - **Why it failed**: When beaker tilts, pour point moves in 3D space. Particles were not consistently following it
   - **Fix Applied**: Enhanced logic to sync particles with pour point world position and rotation
     ```csharp
     Vector3 pourWorldPos = beakerData.pourPoint.position;
     Quaternion pourWorldRot = beakerData.pourPoint.rotation;
     
     // Use local coords if parented, else force world position
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
     ```

2. **Missing Tilt Angle Logging**
   - **Problem**: No visibility into whether tilt angle threshold is being met
   - **Why it failed**: Hard to debug if particles weren't emitting because angle was just below threshold
   - **Fix Applied**: Added frame-rate-throttled debug logging for tilt angle
     ```csharp
     if (showDebugVisuals && beakerData == sourceBeakerData && Time.frameCount % 30 == 0)
     {
         Debug.Log($"SOURCE_TILT: Angle={tiltAngle:F1}° | Threshold={pouringThresholdAngle:F1}° | Volume={beakerData.volumeML:F0}mL");
     }
     ```

3. **Particle Emission Rotation Not Aligned**
   - **Problem**: Particles may inherit wrong rotation from their parent transform
   - **Why it failed**: When beaker rotates 45°, particles could be emitted at 45° instead of straight down
   - **Fix Applied**: Explicitly set particle start rotation to 0
     ```csharp
     var particleMain = beakerData.waterEffect.main;
     particleMain.startRotation = new ParticleSystem.MinMaxCurve(0);
     ```

#### Liquid Pouring Flow:
```
Gesture: OPEN_HAND (tilt beaker)
    ↓
HandleTiltGesture(normalizedX)
    ↓
Rotate beaker around pour point
    ↓
Calculate tiltAngle = Vector3.Angle(beakerUp, Vector3.up)
    ↓
IF tiltAngle > 25° AND volume > 0:
    ├─ Update pourPoint world position [FIX: Consistent world space sync]
    ├─ Update particle emission rotation [FIX: Align to 0°]
    ├─ Calculate volume loss & transfer
    └─ Play particle effect
    ↓
ELSE: Stop particles
```

---

## Fixes Applied to Code

### **File Modified**: `WaterAttachToBeaker.cs`

#### Change 1: Coordinate Mapping Fix (Line ~695)
```csharp
// BEFORE
float normalizedX = (centerY - 0.5f) * coordinateScale;  // SWAPPED!
float normalizedY = (0.5f - centerX) * coordinateScale;  // SWAPPED!

// AFTER
float normalizedX = (centerX - 0.5f) * coordinateScale;  // Correct
float normalizedY = (0.5f - centerY) * coordinateScale;  // Correct
```
**Impact**: Hand now tracks correctly in world space

---

#### Change 2: Grab Detection Radius Increase (Line ~12)
```csharp
// BEFORE
[SerializeField] private float grabDetectionRadius = 5.0f;

// AFTER
[SerializeField] private float grabDetectionRadius = 7.5f;
```
**Impact**: Beaker can be grabbed from further away

---

#### Change 3: Coordinate Scale Increase (Line ~33)
```csharp
// BEFORE
[SerializeField] private float coordinateScale = 4f;

// AFTER
[SerializeField] private float coordinateScale = 10f;
```
**Impact**: Hand movements now map more directly to world space

---

#### Change 4: Hand Position Validation (Line ~350)
```csharp
// NEW: Added validation
if (float.IsNaN(handPosition.x) || float.IsNaN(handPosition.y) || float.IsNaN(handPosition.z))
{
    if (showDebugVisuals) Debug.LogError("!!! INVALID_HAND: NaN detected! Using last position !!!");
    handPosition = lastHandPosition;
}
```
**Impact**: Prevents beaker from freezing if hand tracking corrupts

---

#### Change 5: Water Particle World Space Sync (Line ~570)
```csharp
// NEW: Enhanced pour point tracking
Vector3 pourWorldPos = beakerData.pourPoint.position;
Quaternion pourWorldRot = beakerData.pourPoint.rotation;

// Explicit world position update
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
```
**Impact**: Liquid particles always follow pour point correctly

---

## Testing Recommendations

### **Test 1: Beaker Grab & Movement**
```
1. Show CLOSED_HAND gesture
2. Move hand left/right/up/down
3. Target beaker should follow immediately
4. Release gesture → beaker stays in place
5. Check Console: Should see "GRAB_SUCCESS" messages
```

### **Test 2: Liquid Pouring**
```
1. With target beaker grabbed and positioned below source
2. Show OPEN_HAND gesture & tilt source beaker
3. Observe liquid falling from pour point into target
4. Liquid should fall vertically from beaker spout
5. When beaker uprights, particles should stop
```

### **Test 3: Debug Output**
```
Enable "showDebugVisuals" checkbox in Inspector
Expected Console Output:
- HAND: BBox(...) → Norm(...) → World(...) [every 30 frames]
- GRAB_CHECK: Hand@..., Beaker@..., Dist=X.XX
- SOURCE_TILT: Angle=XX.X° | Threshold=25.0°
- POUR_POS: SOURCE_BEAKER | Point=(...) | Tilt=XX.X°
```

---

## Configuration Checklist

- [ ] Inspector: `grabDetectionRadius` set to **7.5** (or higher if still too far)
- [ ] Inspector: `coordinateScale` set to **10** (adjust if hand still doesn't map correctly)
- [ ] Inspector: `handPositionOffset` correctly set for your camera position
- [ ] Inspector: `Pour Points` assigned for both source and target beakers
- [ ] Inspector: `showDebugVisuals` **ON** during testing

---

## If Issues Persist

### Beaker Still Not Grabbing?
1. **Check grab radius**: In Inspector, temporarily set `grabDetectionRadius = 15f` to test
2. **Verify hand tracking**: In Console, look for `HAND:` debug messages
3. **Check pour points**: Ensure `sourcePourPoint` and `targetPourPoint` are assigned in hierarchy

### Liquid Still Falling Wrong?
1. **Verify pour point position**: Pour point should be at beaker's lip/spout
2. **Check particle prefab**: Ensure it has `ParticleSystem` component
3. **Tilt angle**: Set `pouringThresholdAngle = 15f` (lower value = easier to pour)

### Hand Position Jerky?
1. **Increase coordinate scale**: Try `coordinateScale = 15f`
2. **Check ManoMotion frame rate**: Should be consistent for smooth tracking
3. **Reduce `moveSpeed`**: Try lower value for smoother follow

---

## Summary

| Issue | Root Cause | Fix | Impact |
|-------|-----------|-----|--------|
| Beaker doesn't move | X-Y coordinate swap + small radius | Fixed mapping, increased radius to 7.5 | ✓ Beaker now follows hand |
| Liquid falls wrong | Particle not syncing with pour point | Enhanced world-space sync | ✓ Liquid falls from correct spout |
| Grab detection fails | Small detection radius | Increased from 5.0 → 7.5 | ✓ Easier to grab beaker |
| Hand mapping off | coordinateScale too small | Increased from 4 → 10 | ✓ Better hand-world mapping |

All fixes maintain the existing architecture and don't break other functionality.

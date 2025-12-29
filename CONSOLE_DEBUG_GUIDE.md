# Visual Debugging Guide - Console Output Reference

## Expected Console Output After Fixes

### 🎯 Successful Grab Sequence

```
Frame 60:
  HAND: BBox=(0.45,0.32) → Norm=(0.1,-0.1) → World=(0.5,-0.5,8.0)

Frame 90:
  GRAB_CHECK: Hand@(0.5,-0.5,8.0), Beaker@(-2.1,0.8,8.0), Dist=2.87, Radius=7.5
  ✓ GRAB_SUCCESS: Target Beaker (dist: 2.87m)
  >>> GRAB_ACQUIRED: Target Beaker <<<

Frame 105:
  >> MOVING: Target Beaker | Pos=(0.5,-0.5,8.0)

Frame 120:
  >> MOVING: Target Beaker | Pos=(1.2,-0.3,8.0)

Frame 135:
  >> MOVING: Target Beaker | Pos=(2.0,0.5,8.0)

Frame 150:
  >> MOVING: Target Beaker | Pos=(1.5,1.2,8.0)
```

✅ **What This Means:**
- Hand is being tracked correctly
- Grab detection is working
- Beaker is following hand smoothly
- Position updates every frame with debug output every 15 frames

---

### 🍶 Successful Pouring Sequence

```
Frame 30:
  SOURCE_TILT: Angle=0.0° | Threshold=25.0° | Volume=500mL | Pouring=False

Frame 60:
  SOURCE_TILT: Angle=12.5° | Threshold=25.0° | Volume=500mL | Pouring=False

Frame 90:
  SOURCE_TILT: Angle=25.2° | Threshold=25.0° | Volume=497mL | Pouring=True
  POUR_POS: SOURCE_BEAKER | Point=(-4.2,0.5,8.0) | Tilt=25.2°

Frame 120:
  SOURCE_TILT: Angle=35.1° | Threshold=25.0° | Volume=480mL | Pouring=True
  POUR_POS: SOURCE_BEAKER | Point=(-4.3,0.2,8.0) | Tilt=35.1°

Frame 150:
  SOURCE_TILT: Angle=45.0° | Threshold=25.0° | Volume=450mL | Pouring=True
  POUR_POS: SOURCE_BEAKER | Point=(-4.5,-0.1,8.0) | Tilt=45.0°

Frame 180:
  SOURCE_TILT: Angle=10.0° | Threshold=25.0° | Volume=450mL | Pouring=False

Frame 210:
  SOURCE_TILT: Angle=2.0° | Threshold=25.0° | Volume=450mL | Pouring=False
```

✅ **What This Means:**
- Tilt angle is being calculated correctly
- Pour point position updates as beaker rotates
- Volume decreases as pouring continues
- Particles stop when angle goes below 25°

---

## Common Issues & Console Signs

### ❌ Issue: Beaker Not Grabbing

**Console Output:**
```
GRAB_CHECK: Hand@(0.5,-0.5,8.0), Beaker@(-8.0,3.5,8.0), Dist=9.2, Radius=7.5
✗ GRAB_OUT_OF_REACH: dist=9.2m vs threshold=7.5m
```

**Diagnosis:**
- Beaker is 9.2m away but threshold is 7.5m
- Either beaker position is wrong (coordinate system issue)
- Or hand detection is giving wrong position

**Solution:**
1. Increase `grabDetectionRadius = 12` in Inspector
2. Check if `HAND:` coordinates make sense
3. Verify pour points are assigned

---

### ❌ Issue: Hand Position Way Off

**Console Output:**
```
HAND: BBox=(0.45,0.32) → Norm=(3.5,2.1) → World=(3.5,2.1,8.0)
GRAB_CHECK: Hand@(3.5,2.1,8.0), Beaker@(-2.1,0.8,8.0), Dist=6.3, Radius=7.5
```

**Diagnosis:**
- BBox values (0.45, 0.32) are normalized screen coordinates ✓
- But normalized values (3.5, 2.1) are too large!
- With coordinateScale=10, should be max ±5
- Something is wrong with coordinate calculation

**Solution:**
1. Check if `centerX` and `centerY` calculations are correct
2. Verify `coordinateScale = 10`
3. Check `handPositionOffset = (0, 0, 8)` is reasonable

---

### ❌ Issue: Liquid Falling from Wrong Spot

**Console Output:**
```
Frame 90:
  SOURCE_TILT: Angle=25.2° | Volume=497mL | Pouring=True
  POUR_POS: SOURCE_BEAKER | Point=(-4.0,0.5,8.0) | Tilt=25.2°

Frame 120:
  SOURCE_TILT: Angle=35.1° | Volume=480mL | Pouring=True
  POUR_POS: SOURCE_BEAKER | Point=(-4.0,0.5,8.0) | Tilt=35.1°  ← SAME POS!

Frame 150:
  SOURCE_TILT: Angle=45.0° | Volume=450mL | Pouring=True
  POUR_POS: SOURCE_BEAKER | Point=(-4.0,0.5,8.0) | Tilt=45.0°  ← STALE!
```

**Diagnosis:**
- Pour point position is NOT updating as beaker tilts
- It's stuck at (-4.0, 0.5, 8.0) the entire time
- This is the BUG: particles not synced with pour point

**Solution:**
- Already fixed in the code! This shouldn't happen anymore.
- If it does, verify the pour point fix was applied to line ~595

---

### ❌ Issue: NaN Error (Beaker Freezes)

**Console Output:**
```
!!! INVALID_HAND: NaN detected! Using last position !!!
>> MOVING: Target Beaker | Pos=NaN
```

**Diagnosis:**
- Hand tracking returned invalid (NaN) values
- This causes beaker to freeze or disappear
- Our fix now catches this and uses last known position

**Solution:**
- Already fixed! The validation code prevents NaN from breaking beaker
- If still seeing this, ManoMotion hand tracking may need recalibration

---

## Debug Output Throttling

All debug logs are throttled to prevent console spam:

| Log Type | Frequency | When |
|----------|-----------|------|
| HAND | Every 30 frames | 60 FPS = Every 0.5 sec |
| GRAB_CHECK | Every 30 frames | When checking grab |
| GRAB_SUCCESS | Always | Only on successful grab |
| GRAB_FAILED | Every 60 frames | When grab fails |
| >> MOVING | Every 15 frames | Every frame moving |
| SOURCE_TILT | Every 30 frames | When pouring |
| POUR_POS | Every 60 frames | When particles emit |

---

## Step-by-Step Debugging Process

### Step 1: Check Hand Tracking
```
Enable showDebugVisuals = ON
Look for: HAND: BBox=(X,Y) → Norm=(X,Y) → World=(X,Y,Z)

✓ If coordinates change as hand moves → Hand tracking OK
✗ If coordinates stay same → Check ManoMotion setup
```

### Step 2: Check Grab Detection
```
Look for: GRAB_CHECK: Hand@(...), Beaker@(...), Dist=X.XX

✓ If Dist < grabDetectionRadius → Should grab
✗ If Dist > grabDetectionRadius → Increase radius or fix hand tracking
```

### Step 3: Check Beaker Movement
```
Look for: >> MOVING: Target Beaker | Pos=(X,Y,Z)

✓ If position changes each frame → Beaker following hand
✗ If position doesn't change → Check for NaN error
```

### Step 4: Check Tilt Angle
```
Look for: SOURCE_TILT: Angle=XX.X° | Threshold=25.0°

✓ If Angle > 25° → Should start pouring
✗ If Angle stays low → Check hand/controller tilt gesture
```

### Step 5: Check Pour Position
```
Look for: POUR_POS: SOURCE_BEAKER | Point=(X,Y,Z) | Tilt=XX.X°

✓ If Point coordinates change as Tilt changes → Synced correctly
✗ If Point coordinates stay same → Particles not updating (bug still present)
```

---

## Console Filter Tips

### To See Only Grab Logs:
Console → Search `GRAB`

### To See Only Pouring Logs:
Console → Search `TILT` or `POUR`

### To See Only Movement Logs:
Console → Search `MOVING`

### To See Only Hand Tracking:
Console → Search `HAND`

### To See Only Errors:
Console → Filter to Errors only

---

## Performance Considerations

With the debug logging throttled:
- **No performance impact** during gameplay
- Console updates every 0.5-1 second for hand/pour updates
- Beaker movement logs every 0.25 seconds
- All logs frame-throttled with `Time.frameCount % N == 0`

---

## Quick Status Check

Open Console and look for:

| What to See | Status |
|------------|--------|
| `HAND: BBox=...` repeating | ✓ Hand tracking working |
| `GRAB_CHECK: ... Dist=<7.5` | ✓ In grab range |
| `GRAB_SUCCESS` message | ✓ Beaker grabbed |
| `>> MOVING: ... Pos=(X,Y,Z)` | ✓ Beaker following |
| `SOURCE_TILT: Angle>25°` | ✓ Can pour |
| `POUR_POS: ... Point=(...) Tilt=` | ✓ Pouring synced |

If all these show up as expected → **All fixes working!** ✅

---

## Saving Console Output for Analysis

If issues occur:
1. Reproduce the problem
2. Right-click Console → Select All
3. Copy and paste to text file
4. Share with debug reports

This helps identify exactly what's going wrong!

---

All fixes are monitored and logged for easy verification! 🎯

# Acid-Base Reaction System - Setup Guide

## Overview
The Acid-Base Reaction System is a comprehensive XR chemistry experiment module that integrates with your existing `WaterAttachToBeaker` system. It provides interactive Canvas-based UI panels, real-time reaction detection, and educational feedback.

## Features Implemented

### ✅ Core Functionality
- **Reaction Detection**: Automatically detects when HCl (acid) mixes with base in target beaker
- **HCl + NaOH Reaction**: Simulates the complete neutralization reaction
- **Real-time Updates**: All UI panels update dynamically based on beaker states
- **Visual Feedback**: Color transitions during reactions, progress indicators

### ✅ UI Panels
1. **Source Beaker Panel** (Bottom Left)
   - Current liquid level with progress bar
   - Chemical type indicator (Acid/Base)
   - Fill status (Empty/Half/Full)
   - Interactive refill button

2. **Target Beaker Panel** (Bottom Right)
   - Volume received display
   - Reaction status (No Reaction/Reacting/Complete)
   - Solution details with chemical equation
   - Clear button

3. **Educational Feedback Panel** (Top Center)
   - Current action description
   - Mistake detection and warnings
   - Step-by-step procedure guidance
   - Real-time experiment feedback

## Setup Instructions

### Step 1: Add Component to Scene
1. In your Unity scene, select the GameObject that has the `WaterAttachToBeaker` component
2. Add the `ACIDBASEREACTION` component to the same GameObject (or a child GameObject)
3. The system will automatically find `WaterAttachToBeaker` if not assigned

### Step 2: Configure in Inspector
The component has these configurable settings:

#### System References
- **Beaker Controller**: Automatically finds `WaterAttachToBeaker` if left empty

#### UI Canvas Setup
- **Main Canvas**: Leave empty to auto-create, or assign existing Canvas
- **Create Canvas If Missing**: ✓ Enabled by default

#### Reaction Settings
- **Reaction Detection Interval**: 0.1s (how often to check for reactions)
- **Neutralization Time**: 2.0s (time for reaction to complete)
- **Acid Color**: Orange/Yellow (for visual representation)
- **Base Color**: Blue (for visual representation)
- **Neutral Color**: Light Gray (for neutralized solution)

#### UI Panel Settings
- Panel positions and sizes (automatically positioned for optimal viewing)

### Step 3: Verify Integration
1. Ensure `WaterAttachToBeaker` is properly set up with:
   - Source beaker assigned
   - Target beaker assigned
   - Pour points configured

2. Run the scene and verify:
   - Canvas is created automatically
   - Three UI panels appear (Source, Target, Feedback)
   - Panels update when beakers are manipulated

## Usage Guide

### Experiment Workflow

1. **Initial State**
   - Source beaker contains Hydrochloric Acid (HCl)
   - Target beaker is empty
   - UI panels show current status

2. **Fill Source Beaker** (if empty)
   - Use **Pinch Gesture** near source beaker
   - OR click **"💧 REFILL ACID"** button in Source Panel
   - Panel updates to show "Full" status

3. **Pour Acid into Target**
   - Use **Open Hand Gesture** to tilt source beaker
   - Position target beaker to receive liquid (using Closed Hand to grab and move)
   - Liquid transfers from source to target
   - Target panel shows "Received: X mL"

4. **Reaction Detection**
   - When acid mixes with base in target beaker:
     - Reaction automatically starts
     - Status changes to "Reacting... X%"
     - Color transitions from mixed to neutral
     - Educational feedback explains the process

5. **Reaction Complete**
   - After 2 seconds, reaction completes
   - Status: "✓ Reaction Complete!"
   - Solution: "Sodium Chloride Solution (pH ~7)"
   - Feedback shows success message

6. **Reset Experiment**
   - Click **"🗑 CLEAR"** button in Target Panel
   - OR use gesture controls to empty beakers
   - System resets for next experiment

### Gesture Controls (from WaterAttachToBeaker)
- **✊ Closed Hand**: Grab and move target beaker
- **✋ Open Hand**: Tilt source beaker to pour
- **👌 Pinch**: Refill source beaker

## UI Panel Details

### Source Beaker Panel
- **Location**: Bottom-left corner
- **Shows**:
  - Chemical name and type (ACID indicator)
  - Current volume (mL) with progress bar
  - Fill status percentage
  - Type indicator (colored dot)
  - Refill button

### Target Beaker Panel
- **Location**: Bottom-right corner
- **Shows**:
  - Volume received
  - Reaction status with progress
  - Solution details and chemical equation
  - Volume bar (color changes during reaction)
  - Reaction indicator (pulsing during reaction)
  - Clear button

### Educational Feedback Panel
- **Location**: Top-center
- **Shows**:
  - Current action being performed
  - Warnings for mistakes (overflow, wrong timing)
  - Step-by-step procedure guidance
  - Real-time experiment feedback
  - Background color changes based on state

## Reaction Chemistry

### HCl + NaOH Reaction
```
Hydrochloric Acid + Sodium Hydroxide → Sodium Chloride + Water
HCl + NaOH → NaCl + H₂O
```

### Visual Indicators
- **Acid Color**: Orange/Yellow (pH < 7)
- **Base Color**: Blue (pH > 7)
- **Neutral Color**: Light Gray (pH ~7)
- **Transition**: Smooth color interpolation during reaction

## Code Structure

### Modular Design
The system is organized into clear sections:
1. **Reaction Detection**: Monitors beaker states and triggers reactions
2. **UI Creation**: Programmatically creates Canvas panels
3. **UI Updates**: Real-time updates based on beaker data
4. **Educational Feedback**: Generates contextual guidance

### Extensibility
Easy to extend for:
- Additional acid-base reactions
- More complex chemical equations
- Additional UI elements
- Enhanced validation logic

## Troubleshooting

### Canvas Not Appearing
- Check that `createCanvasIfMissing` is enabled
- Verify no Canvas conflicts in scene
- Check Canvas sorting order

### Panels Not Updating
- Ensure `WaterAttachToBeaker` is properly assigned
- Verify beakers are initialized
- Check console for errors

### Reaction Not Triggering
- Ensure source beaker contains acid
- Verify target beaker has liquid
- Check that beakers are close enough (pouring distance)
- Verify `IsPouringBetweenBeakers()` returns true

### UI Elements Missing
- TextMeshPro package must be installed
- Check that all UI components are created in `Start()`
- Verify Canvas is properly set up

## Integration with Existing System

The `ACIDBASEREACTION` system:
- ✅ Works alongside `WaterAttachToBeaker` (doesn't modify it)
- ✅ Uses public accessor methods to get beaker data
- ✅ Updates beaker properties through public methods
- ✅ Doesn't interfere with gesture controls
- ✅ Adds educational layer on top of existing system

## Next Steps

To extend the system:
1. Add more reactions (edit `StartReaction()` method)
2. Customize UI appearance (modify panel creation methods)
3. Add more validation rules (extend `UpdateFeedbackPanel()`)
4. Integrate with other chemistry experiments

## Support

For issues or questions:
- Check console logs for error messages
- Verify all components are properly assigned
- Ensure TextMeshPro is installed
- Review `WaterAttachToBeaker` setup

---

**System Status**: ✅ Fully Implemented and Ready to Use

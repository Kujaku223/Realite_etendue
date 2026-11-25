# Current Revision — Red Tutorial Cube / Green Start Cube
This revision supersedes the original single-green-cube implementation described below. The remaining original plan records the baseline architecture and verification requirements; wherever it assigns tutorial grabbing/resizing/recovery to the green cube or suggests resizing the green cube, use the red cube instead.

## Requested behavior
- Use the existing **Red Interaction Cube** scene instance for tutorial grab and resize completion, and lost tutorial-cube recovery.
- Movement and turning remain player locomotion checks, not cube motion checks; preserve current action bindings and thresholds.
- Use **Green Warp Cube** exclusively for the existing start-game/warp gesture after tutorial completion. Keep its optional tutorial gate and fresh post-completion grab requirement.
- Preserve repeated tutorials on scene visits, current game destination/transition behavior, fixed instruction-board placement, its shadow backing, and the user's current gaze configuration.

## Current inspected context
- `Assets/Scenes/EmptyPlains.unity` contains `Red Interaction Cube`, an instance of `Assets/Prefabs/Red Interaction Cube.prefab`, with Rigidbody, BoxCollider, VRHitPointGrabInteractable, VRGrabObject, VRTwoHandScale, and VRObjectFeedback. It has no GreenCubeWarp component.
- EmptyPlainsTutorial currently assigns both `m_Cube` and `m_Scaler` to Green Warp Cube. These references can be reassigned directly without redesigning the state machine.
- The green instance currently has a scene-only VRTwoHandScale, and its GreenCubeWarp references EmptyPlains Tutorial. Remove only that scene-added scaler; preserve the prefab and warp gate.
- Both existing scalers use 0.25x–4x limits. Reuse the red cube's current configuration.
- TutorialInstructionDisplay contains green-specific grab text in both serialized strings and Refresh's progress message. Ready text currently suggests shrinking the green cube; that advice must be removed once green resizing is removed.
- AssetDatabase dependency inspection confirms the red prefab is already referenced by EmptyPlains. No shared prefabs, input assets, lighting/materials, or generic grab/scale scripts need modification.

## UI wording
Keep the existing fixed world-space layout:
```text
1 / 3 MOVE & TURN  -> existing movement instructions
2 / 3 GRAB THE RED CUBE
Point at or touch the RED cube. Hold either GRIP to grab.
3 / 3 RESIZE THE RED CUBE
Release GRIP. Point BOTH controllers at the RED cube.
Hold BOTH TRIGGERS; move hands apart/together; release to finish.
TUTORIAL COMPLETE
Release the triggers, then make a NEW grip grab on the GREEN cube.
Pull it to your hand to enter Environnement.
```
Update both script defaults/hardcoded progress strings and the scene's serialized instruction overrides so saved strings cannot retain obsolete green-tutorial instructions. No new panels or fonts.

## Revision implementation steps
1. Recheck exact red/green scene identities, their component connections, prefab override ownership, and dependency/reverse-dependency impact before edits. Dependency: approval of this revision. Use the available dependency inspection tool where available; otherwise use AssetDatabase read-only dependency inspection.
2. In `Assets/Scenes/EmptyPlains.unity`, assign EmptyPlainsTutorial.m_Cube to Red Interaction Cube's VRHitPointGrabInteractable and m_Scaler to its existing VRTwoHandScale. Preserve Origin, Move, Turn, simulator-testing setting, thresholds, and all red prefab settings. Dependency: Step 1.
3. Verify only the green scene-added VRTwoHandScale is removed from EmptyPlains, leaving VRGrabObject, interactable, Rigidbody, feedback, and GreenCubeWarp untouched. Keep GreenCubeWarp.m_Tutorial assigned. Red must have no warp behavior. Do not apply changes to shared prefabs. Dependency: Steps 1–2.
4. In `Assets/Scripts/TutorialInstructionDisplay.cs`, change grab/resize instructions and titles/progress to explicitly identify the red cube, and completion instructions to identify only the green cube. Apply matching scene string overrides. Preserve text sizing, fixed Transform, Follow Head=false, panel visibility, and shadow caster hierarchy. Dependency: Step 2.
5. Update relevant tutorial documentation in `Assets/Scripts/VRInteractionSetup.md` to describe red practice versus green start and scene-only wiring. Avoid rewriting unrelated documentation. Dependency: Steps 2–4.
6. Compile, inspect saved scene references/overrides, and run available functional checks. No new testing package or permanent harness. Save EmptyPlains only after verifying wiring. Dependency: Steps 2–5.

## Revision verification & testing
- Compilation succeeds; mandatory tutorial references are non-null and red grab/scaler references point to the same object.
- Move/turn still advance the first step using existing controls; neither cube can bypass ordering.
- Grabbing GREEN during tutorial does not advance Grab or warp; grabbing RED with a valid player controller during Grab advances to Resize.
- Genuine RED resizing by the existing 20% threshold followed by trigger release completes Resize. RED never starts a scene transition.
- GREEN has no active resizing component in EmptyPlains. Its fresh grip/contact gesture works only after the tutorial is complete, preserving single-load guard and destination behavior.
- Pre-existing GREEN selection does not automatically start the game when RED resizing completes; release and a new green grab are required.
- Throw/drop RED below the recovery threshold or outside recovery distance: existing tutorial recovery operates on RED only, without marking progress; held/scaling red cube is not repositioned.
- Start-game instructions no longer advise resizing GREEN. Existing fixed board and cast shadow remain unchanged and readable; text does not overflow.
- Revisit EmptyPlains: fresh three-step tutorial uses RED again. Other scenes' green warp cubes and red interaction cubes remain unchanged.
- Report compilation/reference checks separately from actual simulator/headset acceptance testing.

# Project Overview
- **Game Title:** Existing VR project; title not supplied. This plan concerns only its EmptyPlains tutorial, not a new game.
- **High-Level Concept:** Teach the existing VR controls in three ordered, action-verified steps: movement/turning, grabbing the red tutorial cube, and resizing that same red cube. Only after completion may the separate green cube gesture warp the player to Environnement.
- **Players:** Existing single-player XR experience.
- **Inspiration / Reference Games:** Existing project interaction conventions; no external reference requested.
- **Tone / Art Direction:** Preserve the gray plains, red doorway frame, green cube, and amber interaction feedback. Clear, unobtrusive instructional text, without new environment art.
- **Target Platform:** Current Windows Standalone XR target, Unity 6000.3.23f1.
- **Screen Orientation / Resolution:** Stereo headset view; instructions sized in world metres rather than a fixed desktop resolution.
- **Render Pipeline:** Existing Performance URP configuration; unchanged.

## Confirmed user decisions
- Use **one cube**: the existing Green Warp Cube is both the tutorial object and eventual warp trigger.
- Run the tutorial on **every EmptyPlains visit**, including level timeouts and returns. No completion persistence or skip feature.
- Preserve the cube-based departure gesture after completion, rather than automatically loading immediately when resizing succeeds.

# Game Mechanics
## Core Gameplay Loop
Introduce one task, let the player demonstrate it, show clear success/progress, then advance. Action-based completion reinforces competence; delaying the warp until a deliberate fresh grab preserves player agency.

Use an explicit scene-local state machine: `Movement -> Grab -> Resize -> ReadyToWarp`. Later actions cannot complete earlier steps or bank future completion.

### Step 1 — Movement and turning
- Instruction: “Use the LEFT thumbstick to move. Push the RIGHT thumbstick left or right to turn. Move a little and try turning.”
- Display independent movement and turn progress.
- Proposed Inspector-tunable defaults: accumulate **0.75 m of horizontal XR Origin travel** while the configured move action is active, and **30 degrees of absolute XR Origin yaw change** while the configured turn action is active.
- Both conditions are required; movement and turning may happen in either order.
- Subscribe to the existing Input System action callbacks, using the action asset already enabled by InputActionManager. Do not enable unrelated actions or take ownership of disabling the shared asset.
- Sample actual XR Origin position/yaw during active input. Head tracking, room-scale head motion, gravity, and startup spawn alignment must not count.
- Wait until entry fade and XRDoorwaySpawn alignment have settled before setting baselines.

### Step 2 — Grab the cube
- Instruction: “Point at or touch the GREEN cube. Hold either GRIP button to grab it. Release GRIP to put it down.”
- Complete on a valid `selectEntered` event for this cube from the player's left or right controller, after entering this step.
- If already selected when Step 2 begins, require release and a new selection; earlier actions do not pre-complete this step.
- Warp remains locked, even at controller/collider contact. Grabbing, feedback, and release physics remain usable.

### Step 3 — Resize the cube
- Instruction: “Release GRIP so both hands are free. Point BOTH controllers at the cube and hold BOTH TRIGGERS. Move your hands apart to grow it, or together to shrink it. Release a trigger when finished.”
- Add VRTwoHandScale to the **EmptyPlains cube instance only**. Keep the shared green-cube prefab grabbing-only elsewhere.
- Use the existing scaler's tracking, two-trigger lock, collision protection, and size limits; do not replace these mechanics.
- Capture the world-scale baseline when entering this step. Require a genuine scale change during `isScaling`: proposed tunable default **20% growth or shrinkage** relative to that baseline. Either direction is sufficient.
- Record qualifying size change while the scale lock is active; transition to ReadyToWarp only after the lock ends. A tiny jitter, one trigger, or merely beginning a scale lock does not complete the tutorial.
- Preserve ordinary scale limits (0.25x–4x startup size). If the cube became too large to grab, the ready instruction must explain shrinking it back below headset height.

### Ready to warp
- Instruction: “Tutorial complete! Release the triggers, then hold GRIP to pull the green cube to your hand and enter Environnement. If it is too large to grab, shrink it first.”
- Require a **fresh cube selection after readiness** before accepting the existing held-at-controller contact gesture. This avoids accidental departure from a pre-existing grip/selection.
- Preserve the existing destination (`Assets/Scenes/Environnement.unity`), 0.6-second fades, loading hourglass, selection cancellation, duplicate-load guard, and post-load grip-release protection.
- Progress is instance-owned and resets when EmptyPlains is newly loaded. No static or saved tutorial completion flags.

## Controls and Input Methods
Verified New Input System (`activeInputHandler = 1`) and XR Interaction Toolkit Starter Assets 3.5.1. Existing left ControllerInputActionManager enables smooth motion; right enables smooth turning. Grab uses grip/select; resizing uses both triggers/activate with free hands. Preserve the shared XR rig's bindings, locomotion speeds, comfort vignette, interaction layers, and provider setup. Do not introduce legacy Input polling or keyboard-only controls.

# UI
Use a scene-owned, noninteractive instruction panel visible in the headset at a comfortable distance (approximately 1.5–2 m), above the cube's usual floor position. Use standalone world-space TextMeshPro text and a simple background mesh, existing LiberationSans SDF font, high contrast, fixed text sizes, and no colliders/raycaster. Reposition gently only when needed to remain accessible after locomotion; avoid rigid head-locked text and per-frame text rebuilds. Hide instructions during fades.

Example layout:
```text
+--------------------------------------------------+
| TUTORIAL                                  1 / 3  |
| Movement                                         |
| LEFT thumbstick: move                            |
| RIGHT thumbstick left/right: turn                |
| Move: [pending/done]   Turn: [pending/done]        |
+--------------------------------------------------+
```
Steps 2 and 3 replace the title/body; ReadyToWarp shows a completion title and departure instructions. Retain existing controller callouts and cube audio/outline feedback. UI strings are editable English text; no localization system is added.

# Key Asset & Context
## Modify
- `Assets/Scenes/EmptyPlains.unity`: add the tutorial controller and instruction hierarchy; assign XR Origin, configured action references, existing cube, grab interactable, scaler, and warp component. Add VRTwoHandScale only as a scene-instance override. Preserve floor, frame, spawn anchor, manager, and rig.
- `Assets/Scripts/GreenCubeWarp.cs`: add an optional serialized reference to the tutorial controller. Gate only departure eligibility and fresh-selection arming; do **not** gate ordinary grab selection through IXRSelectFilter while learning. Check permission in LateUpdate and recheck before StartTransition in the delayed Warp coroutine.
- `Assets/Scripts/VRInteractionSetup.md`: document steps, thresholds, scene-only resize setup, repeated tutorial behavior, and updated EmptyPlains return flow.

## Create
- `Assets/Scripts/EmptyPlainsTutorial.cs`: state machine, serialized settings/references, action callback subscriptions, actual-motion verification, cube-select listener, genuine-resize tracking, UI state updates, and read-only completion/warp-eligibility API.
- `Assets/Scripts/TutorialInstructionDisplay.cs`: world-space panel text and restrained horizontal follow/visibility behavior. Keep gameplay completion logic out of the display.
- Unity-generated `.meta` files for new scripts.

## Reuse without modifying
- `Assets/Prefabs/Green Warp Cube.prefab`: existing geometry/material/grab configuration; no global resize or tutorial requirement.
- `Assets/Scripts/VRGrabObject.cs`, `VRTwoHandScale.cs`, `VRHitPointGrabInteractable.cs`, `VRObjectFeedback.cs`: existing mechanics; observe their events/public state.
- `Assets/Scripts/XRSceneTransition.cs`, `XRDoorwaySpawn.cs`: existing fade and arrival pipeline.
- `Assets/VRTemplateAssets/Prefabs/Setup/Complete XR Origin Set Up Variant.prefab` and `Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/XRI Default Input Actions.inputactions`: existing movement and turning actions.
- `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset`.

## Dependency / impact findings
AssetDatabase dependency and reverse-dependency inspection confirms EmptyPlains uses the shared rig and warp prefab. GreenCubeWarp is transitively used in Environnement, all three gyms, PorteRouge, and Red Door Frame. Therefore the new optional gate defaults to **no tutorial requirement** for all existing instances, and is assigned only on the EmptyPlains scene instance. C# references also exist in timer/door routing code; preserve their existing public methods and transition semantics. VRTwoHandScale has broad house/puzzle dependents and is not changed.

# Implementation Steps
1. **Verify references before editing.** Recheck EmptyPlains dependency/impact graph and relevant C# callers; inspect direct object references via SerializedObject, since JSON output alone can show nonpersistent references as zero. Confirm runtime action enablement, startup ordering, and available fonts. Dependency: approved plan.
2. **Implement tutorial state logic in EmptyPlainsTutorial.cs.** Add explicit states and validated serialized references; wire callback-based action activity, motion thresholds, cube selection, and scale lock/size-change qualification. Wait for fade/spawn settling. Unsubscribe listeners on disable. Missing mandatory references fail closed with one actionable error, not an unlocked warp or per-frame logging. Dependency: Step 1.
3. **Add the optional warp gate in GreenCubeWarp.cs.** Leave ordinary selection available during the tutorial; suppress only scene departure until complete. Track fresh post-completion selection and recheck gate in Warp before transition commitment. Null tutorial reference preserves current behavior elsewhere. Dependency: Step 2.
4. **Implement TutorialInstructionDisplay.cs and panel hierarchy.** Provide the mockup layout, readable world-space font sizes, step/progress updates only when changed, noninteractive rendering, and visibility during transitions. Dependency: Step 2.
5. **Wire EmptyPlains.unity.** Add controller/display and scene-instance scaler, assign exact left move/right turn actions and cube references, preserve RestartLevel mode and fixed Environnement fallback. Set editable default thresholds. Place panel away from the cube's targeting space. Dependency: Steps 2–4.
6. **Check cube recoverability.** Verify the floor cube can be targeted after ordinary throwing/dropping. If an unheld, non-scaling cube is lost below the floor or beyond practical targeting distance, implement scene-local recovery to a safe reachable floor location without marking progress or changing its size. Never reposition a held/scaling cube. Dependency: Step 5.
7. **Document and validate.** Update VRInteractionSetup.md, compile, inspect saved references and overrides, run available runtime checks, and perform headset/simulator acceptance checks. Dependency: Steps 1–6.

# Verification & Testing
- Unity compilation succeeds; no missing scripts/references or new gameplay exceptions. Verify any errors were not pre-existing before attributing them to the change.
- Direct EmptyPlains launch begins Step 1 only after alignment; startup repositioning and head rotation do not complete it.
- Move without turning and turn without moving: Step 1 stays incomplete until both demonstrated. Test subthreshold input and input against obstacles.
- Grab cube during Step 1: no warp and no pre-completed Grab step. Once Step 2 begins, fresh left/right selection completes it.
- During Grab/Resize steps, controller touching the held cube never initiates a transition.
- Resize while holding grip, using one trigger, or without both controllers targeting the cube: no completion. Genuine growth and genuine shrinkage each satisfy the threshold; jitter and limits with no size change do not.
- Final scale qualification waits for trigger release; grip already held or an earlier selection does not trigger departure. New grip selection plus actual collider contact loads Environnement exactly once.
- Verify oversize-cube instructions and shrinking to regain grab eligibility; player collision protection remains functional.
- Release grip during the pull: remain in EmptyPlains. Invalid destination/load failure: remain recoverable with no stuck transition guard.
- Verify both-eye panel readability, comfort while moving/turning, no interception of controller rays, original amber outline/audio, and fade/hourglass coverage.
- Environnement timer timeout and green-cube return both reload EmptyPlains at Step 1; complete the tutorial again and confirm a fresh Environnement starts normally.
- Regression-check door-spawned green cubes and gym warps: no tutorial requirement or resize behavior outside EmptyPlains. Preserve post-load grip-release safety.
- Automated state checks may use the project's available testing tooling; do not add a testing package or permanent test harness solely for this feature. Compilation/reference inspection is not proof of headset behavior; report interactive checks separately.

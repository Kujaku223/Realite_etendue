# VR grabbing and resizing

For Unity 6 and XR Interaction Toolkit 3.5.1.

## Add the components to an object

1. Add `VRTwoHandScale` to the object's root for grabbing and resizing. Unity
   also adds `VRGrabObject`, `VRHitPointGrabInteractable`, `Rigidbody`, and
   `XRGeneralGrabTransformer`, and `VRObjectFeedback`. For grabbing only, add
   `VRGrabObject` instead.
2. Provide an enabled, non-trigger collider on the root or its children. Use a
   Box, Sphere, Capsule, or convex Mesh Collider for a moving object. Make sure
   the VR Hit Point Grab Interactable's Colliders list includes it (an empty list is
   populated automatically at startup).
   The custom interactable excludes colliders belonging to another Rigidbody or
   nested interactable before XR registration, so Maison cannot claim its items'
   colliders and block their targeting.
3. VR Hit Point Grab Interactable defaults its Interaction Layer Mask to
   `grabbedAndScaled` when added or reset in the editor. The Inspector's setting
   is preserved at runtime. Set **Pull Duration** on `VRGrabObject` (default 0.4 seconds).
   Grabbing automatically uses the selecting player's current headset height as
   the maximum object height.
4. Use a parent with uniform scale. Maison can move and resize with its settled
   contents attached. Resizing is anchored to the root's
   transform pivot; it preserves world position and rotation, not the bottom face
   of a mesh whose pivot is at its centre.

`VRHitPointGrabInteractable` extends Unity's `XRGrabInteractable` and uses its
dynamic attachment hook to store the ray hit as an object-local anchor. It reads
the existing Near-Far or XR Ray Interactor's physics hit instead of casting an
independent ray. For a close-range grab without a ray hit on the object, it uses
the closest point on the object's colliders. The initial controller rotation is
saved as the attachment rotation, preserving the object's initial rotation offset.

`VRGrabObject` configures single selection, a dynamic near attachment, kinematic carrying,
position/rotation tracking, attachment easing, gravity after release, and throwing. It turns off
built-in grab scaling. Its runtime configuration takes precedence over those
settings on VR Hit Point Grab Interactable. Leave the Rigidbody dynamic with gravity for
normal use. It is temporarily made kinematic during carrying and resizing.

On an existing object with a standard XR Grab Interactable, replace that component
with VR Hit Point Grab Interactable before adding these scripts; Unity permits
only one grab interactable on the object. Preserve its collider list, interaction
layers, and event assignments when doing so. Existing project objects using
`VRGrabObject` have already been migrated with their component references retained.

## House contents

`VRHouseInteractionLock` uses Maison's existing interior zones and hierarchy.
It tracks original dynamic contents and adopts unheld objects using
`VRHitPointGrabInteractable` when their pivots enter a zone. No extra component
is needed on an item beyond its existing grab/resize setup. House roots are
excluded. In overlapping houses, the current owner is retained while the item
is inside; otherwise the containing house with the closest pivot owns it.

- **Falling inside:** the item is parented to Maison with normal physics. It is
  woken once on entry or release, including initial contents, so it can fall.
- **Settled inside:** when `Rigidbody.IsSleeping()` becomes true, the item becomes
  kinematic with interpolation disabled. Its local position, rotation, and scale
  stay fixed, so moving, rotating, inverting, or resizing Maison carries it along.
- **Temporarily paused:** if Maison is grabbed or resized while an item is still
  falling, the item becomes kinematic until that manipulation finishes. It then
  resumes falling from rest. It must settle before becoming permanently attached.
- **Individually manipulated or outside:** grabbing or resizing an item releases
  its house attachment and restores its physics before the interaction captures
  that state. XRI finishes normal throwing before house ownership is reconsidered.
  Outside items detach; releasing or bringing one inside adopts it again.

Parent changes preserve world pose and size. Original contents reuse their
existing parent when possible; newly adopted objects use the house root.
Frozen items keep their colliders enabled for XR targeting. Only collision pairs
between that item's Rigidbody and Maison's own Rigidbody are temporarily ignored,
so frozen contents cannot push their house. Previously ignored pairs are left
alone. Physics settings and pairs changed by this feature are restored on pickup,
exit, disable, or destruction. Disabling the house lock leaves the hierarchy in
place and restores the contents' physics; enabling it restarts fall/settle handling.

Resize limits use the object's world scale at startup while scale changes are
applied locally. Entering or leaving Maison therefore does not reset its size
limits. When attached contents inherit Maison's resizing, their size-limit baseline
follows that inherited change, so individually resizing them does not snap their
size. Keep parents uniformly scaled to preserve proportions during reparenting.

The Maison prefab has a 1,000 kg dynamic Rigidbody. Its entrance and bathroom
doors use enabled, convex, non-trigger panel colliders attached to the existing
house Rigidbody, without additional door bodies. Their animations update in
FixedUpdate so visible panels and collision shapes move together. `PorteVR`
opens the exterior door automatically when the player fits, regardless of distance.
The bathroom door retains its balance-puzzle lock. Held-house collision suppression
also covers these panels because they belong to Maison's Rigidbody.

## Grab height limit

Objects taller than the headset's current height above the XR Origin's floor
cannot start a new grab. `VRGrabObject` reads the camera from the selecting
controller's XR Origin and compares that height against the combined world-space
bounds of the object's active non-trigger colliders. This accounts for scaling,
parent scale, rotation, and changes in player height while crouching or standing.
An object the same height as the headset is still allowed.

The height rule filters grip selection only. Two-trigger resizing remains available,
so shrinking an object to headset height or less makes it grabbable again.
An already-held object stays held until grip release. A controller without an
XR Origin, camera, or positive headset height cannot start a grab. This height
check is independent of the fixed 2 m capsule used for player overlap protection.

## Maison interior interaction lock

`Assets/Environnement/Prefabs/Maison.prefab` has `VRHouseInteractionLock` on
its root, alongside its reusable grab/resize/feedback components and colliders.
Environnement and fresh Maison instances inherit this setup. HouseNoDoor has not
been given this lock.

Environnement preserves the house's initial **0.1x** scale. Its instance uses a
**Maximum Scale Multiplier of 20**, allowing the miniature to grow to human scale;
the reusable component's ordinary default remains 4.

When the headset enters any **Interior Volumes** zone, the house root's XR
Interaction Layer Mask switches to **Default**, blocking grabbing and resizing.
An active grab or resize ends on entry without throwing the house. Leaving all
interior zones restores the original **grabbedAndScaled** mask and throwing.
The existing headset-height restriction still applies when starting a grab.
The outside mask is taken from the Inspector. Runtime grab setup preserves it,
and the house switches it only when entering or leaving the interior.

The four zones follow the stepped house footprint in the root's local space,
so they move, rotate, and resize with the house. Select Maison to see their cyan
outlines in the Scene view; adjust **Interior Volumes** on the lock component
if the layout changes. The test uses headset position, so a controller reaching
through a window alone does not count as entering the house. This changes XR
interaction layers, not the GameObject physics layer.

## Controller setup

Use an XR Interaction Manager and the project's XR Origin with left and right
Near-Far Interactors. Both need compatible interaction layers (the cube uses
`grabbedAndScaled`), with Handedness set to Left and Right respectively. Their Select input
must use the grip action with **Select Action Trigger = State Change**, and their
Activate input must use the trigger action. The project's Starter Assets already
have these bindings. Keep the input action asset enabled through the rig's
Input Action Manager.

The project's `Complete XR Origin Set Up Variant` prefab sets both Near-Far
Interactors' Interaction Layer Masks to `grabbedAndScaled` only. Other objects
must include that layer in their XR interactable's Interaction Layer Mask to
interact with these hands. This applies to scenes using this shared rig prefab.
Keep the objects' GameObject physics layer included in the casters' physics
masks; the example cube remains on the Default physics layer.

## Player contact during resizing

The shared XR Origin prefab includes a **Player Scale Capsule** child with a
fixed trigger Capsule Collider: **Height = 2 m**, **Radius = 0.25 m**, and
**Center = (0, 1, 0)**. It stays at the XR Origin's local position zero and moves
with that rig. It uses the Ignore Raycast physics layer and leaves locomotion
to the rig's existing Character Controller. No capsule-tracking script is needed.

While resizing, overlapping the player capsule blocks further growth. Proposed
growth is also checked before it is accepted, so a large size change stops at
contact. Shrinking remains available immediately. Blocked hand movement is
discarded, so moving away from the object does not cause a sudden size jump.
Move clear of the object and spread your hands again to resume growing it.

Scalable objects automatically find the Capsule Collider on the child named
**Player Scale Capsule** through the selecting controller's XR Origin. For
another rig, add a child with that exact name at the XR Origin's local position
zero and add a trigger Capsule Collider with the dimensions above.
Use primitive or convex colliders on scalable moving objects.

Resizing reads the Activate input directly, so **Allow Hovered Activate** does
not need to be enabled. It uses tracked controller status; disconnected or
untracked controllers end a resize. The XR Interaction Simulator can supply
tracked simulated controllers for desktop checks.

## Controls

- **Carry:** point at or touch the object, hold grip, move or rotate the
  controller, then release grip to drop or throw. The selected surface point
  eases to the moving hand over **0.4 seconds**, rather than pulling the object's
  centre to the hand. Controller rotation preserves the initial orientation
  offset. Releasing grip during the approach immediately ends the grab.
  Detection reach is controlled
  by the Near-Far Interactor's casters; there is no additional script distance
  limit. Carrying continues until grip release.
- **Resize:** with both hands free, point both controllers at the same object
  and hold **both triggers**. The object stays at its current world
  position and rotation. Move hands apart to grow it or together to shrink it.
- The resize remains locked if the rays drift away. Release either trigger to
  finish. Grabbing is blocked for the locked controllers until resizing finishes.
- Size is retained across locks, grabs, and releases. Limits default to
  **0.25x–4x** the object's size at startup and are editable on `VRTwoHandScale`.
  Hands must start at least 1 cm apart.
- Resizing temporarily suspends physics. Finishing restores the previous
  Rigidbody settings with zero velocity; gravity may then move the object.
  Disabling the component or losing a controller also restores physics.

## Gym Interaction example

Open `Assets/Scenes/Gyms/Gym Interaction.unity` and find **Red Interaction Cube**
at the scene root. It is a 0.4 m cube at world position `(-0.75, 0.2, -20.97)`,
on the ground in front of the XR Origin's starting pose. It uses the existing
`M_Porte_Rouge` material and has both custom components already attached.

## Target feedback, sounds, and wrist countdown

The shared XR Origin uses **7.5-degree far cone casts** on both hands, increased
from 6 degrees to make small objects easier to aim at. Reach remains 10 m;
near detection radius, visible beam width, and interaction layers are unchanged.
Both near-overlap and far-cast physics masks include **HouseFurniture**, in
addition to their existing layers. This is distinct from the grabbedAndScaled
XR interaction layer. Both controller rays use **#FFB000** in idle, hover,
selection, and UI states, preserving their existing transparency gradients.

`VRObjectFeedback` is added automatically with `VRGrabObject`. Existing objects
have been migrated. It gives hovered, held, or resize-locked objects an amber (#FFB000)
**2-pixel outline**. Original materials and LOD distances are preserved; separate
child interactables are excluded from their parent's outline. The outline uses
depth testing and does not add colliders, shadows, or targeting surfaces.

Select **Assets/Resources/VRFeedbackDefaults.asset** to adjust outline settings,
clips, and volumes for all default objects. Assign your own Audio Clips to
**Thud Clip**, **Grab Clip**, **Resize Loop Clip**, and **Timer Beep Clip**.
The current clips in **Assets/Audio/VRFeedback/** are generated placeholders.
For different feedback on one object, create another profile and assign it to
that object's VR Object Feedback **Profile** field.

The grab cue plays once on successful selection. A spatial resize loop plays
only while size actually changes, stopping within 0.1 seconds when hands stop
or a limit blocks resizing. Solid impacts produce spatial thuds above 0.35 m/s,
with a 0.2-second per-object cooldown and volume increasing with impact speed.
These object sounds use no Doppler effect and a 1–15 m distance range. The mask,
shell, and clear material references belong to the outline effect; retain them
when replacing audio clips.

**Environnement** contains an **Interaction Level Timer**, defaulting to
**300 seconds**. It creates a 12 × 6 cm display above the left controller, with
white `MM:SS` text on a dark background. It has no collider or UI raycaster and
does not intercept targeting. Counting starts after an entry fade finishes.
At **00:00**, the timer reserves the same transition guard used by the cubes,
plays three 2D beeps 0.5 seconds apart, then fades to EmptyPlains. Other warp
requests cannot interrupt that sequence. EmptyPlains has no countdown. Complete
its tutorial, then make a new grip grab of the return cube to reload the level
with a fresh **05:00** timer and reset puzzles.

For a short manual check, temporarily set **Duration Seconds** to 3 before
entering Play Mode, verify the beeps and fade, then restore the saved value to
300. Check wrist readability, both-eye outlines, LOD changes, walls occluding
outlines, impact cooldown, resize limits, and simultaneous timer/cube attempts
with the headset. No test scripts are required.

## EmptyPlains tutorial

Every arrival in EmptyPlains starts a new three-step tutorial using the existing
**Red Interaction Cube** for grabbing and resizing. **Green Warp Cube** is only
for starting the game after completion. `EmptyPlains Tutorial` owns scene-local
progress; no progress is saved between visits. Its instruction board uses
noninteractive world-space TextMeshPro with `Assets/Materials/TutorialPanel.mat`,
and does not intercept rays. Keep **Follow Head** disabled for fixed scene placement;
the shadow-only backing casts the board's ground shadow without a collider.

1. **Move and turn:** use the left thumbstick to move and the right thumbstick
   left/right to turn. Both 0.75 m of horizontal rig travel and 30 degrees of
   rig turning are required. Only actual rig motion during the corresponding
   configured action counts; startup alignment and head tracking do not.
   **Editor simulator exception:** EmptyPlains has **Editor Simulator Pose
   Progress** enabled on its tutorial component for Meta XR Simulator keyboard/
   Xbox view navigation. In this mode, 0.75 m of horizontal camera travel and
   30 degrees of camera yaw count without requiring XR thumbstick actions. The
   board explicitly labels this as an Editor simulator test. Initialization
   alignment is still excluded. Disable this option when validating actual
   headset thumbstick controls in the Editor; it is always ignored in player
   builds. Grab, resize, and warp requirements are not bypassed.
2. **Grab RED:** make a new grip selection of the red cube with either controller.
   Grabbing during movement does not pre-complete this step. Grabbing green cannot
   advance this step or start the game while learning. Release grip for resizing.
3. **Resize RED:** point both controllers at the red cube, hold both triggers,
   then move hands apart or together. Grow or shrink by 20% relative to the size
   when this step began; release a trigger to finish. Jitter or a lock without
   size change does not qualify. The usual 0.25x–4x limits and player-contact
   protection remain active. Completion thresholds are editable on EmptyPlainsTutorial.

Once complete, make a **new grip grab of GREEN**, pulling it to the controller
 to start a fresh Environnement. The ordinary contact check, fades, hourglass,
and post-load grip-release protection are preserved. A green grab made before
completion cannot automatically start the game when red resizing finishes.
Invalid setup keeps the tutorial gate closed. Red never performs a warp.

EmptyPlainsTutorial's **Cube** and **Scaler** references both point to the red
scene instance. Red keeps its existing VRTwoHandScale; the scene-added scaler has
been removed from green. GreenCubeWarp's optional **Tutorial** reference remains
assigned only in EmptyPlains. Shared prefabs, gyms, and door-spawned warp cubes
are unchanged. A lost, unheld, non-scaling RED tutorial cube returns to the floor
two metres ahead if it falls below -2 m or travels more than 12 m horizontally
from the headset; its size is preserved.

Check both red practice steps with either hand, green grabs before readiness,
one-trigger attempts, growth/shrink completion, a fresh green grab after
finishing, and repeated arrivals following Environnement timeout. Check board
readability and stereo fades with the headset; automated state checks do not
substitute for tracked input tests.

The original single-cube tutorial had synthetic runtime state checks; those are
not proof of the revised two-cube input/contact behavior. Physical headset
controls, both-eye comfort, actual contact-to-load departure, and repeated
 timeout/return cycles still require the manual checks above.
No permanent test harness or screenshots are retained.

## Green cube warp

Unlocking PorteRouge invokes`PadlockUnlock.onUnlocked` immediately, while the
door begins opening. `DoorWarpSpawner.SpawnOnce()` creates one **Green Warp Cube**
at the door's **Warp Cube Spawn Point**, initially local `(0, 0.2, -1.5)`.
Edit that child transform to adjust its position. The shared door prefab and
Gym Interaction's replacement padlock are both wired to the spawner.

The cube uses `VRGrabObject` without two-hand resizing and the `grabbedAndScaled`
interaction layer. Hold grip to pull it toward the controller. **Selecting it
with a distant ray does not warp immediately.** `GreenCubeWarp` waits until the
tracked controller position reaches the cube's solid collider, within the
editable **Contact Tolerance** (default 1 cm). Releasing grip before contact
cancels the approach and does not change scenes. Touch without a grab does nothing.

After contact, hand selections are cancelled and the headset fades to black over
**0.6 seconds**. `EmptyPlains` loads in Single mode while the view stays black,
then fades back in over **0.6 seconds** once the new rig and return cube initialize.
Adjust **Fade Out Duration** and **Fade In Duration** on the Green Warp Cube prefab.
`XRSceneTransition` uses a camera-space UI overlay for both headset eyes and
unscaled time. Its temporary runner survives the load, binds to the new camera,
and removes itself after revealing the scene; it does not preserve the old rig.
The plains contains a 500 m gray floor, sky, light, the shared XR Origin, one XR
Interaction Manager, and one green return cube. The return cube is positioned
on the floor two metres in front of the arriving headset. Each hand must release
grip before it can select a cube after a warp, preventing an automatic round trip.

The return cube's **Restart Level** mode has **Return To Departing Scene** disabled
in EmptyPlains, and **Fallback Level** is Environnement. It always starts a fresh
Environnement, including after leaving a gym: player spawn, object poses,
door/lock state, puzzles, and the five-minute timer reset. Other cubes retain the
option to return to the remembered departing scene. Both destinations must be
enabled in the build scene list; invalid routes leave the player in place.

**EmptyPlains is the build startup scene**, followed by Environnement. Gym
Interaction, Gym Perspective, and Gym Balances remain available. Each has a scene-owned XR
Interaction Manager so unloading a level removes its manager with it.

The plains uses Environnement's XR Origin spawn pose and contains a **Red Door
Frame** prefab variant at the same world pose and scale as the level's red door.
It contains only the base, posts, lintel, and solid colliders. There is no panel,
padlock, door script, or countdown in the plains. The return cube remains two
metres ahead of the arriving headset.

## Maison puzzles

Maison nests the latest **TablePlaques** setup from Gym Balances, with one manager,
three plates, their assigned props, and the bathroom door. Correct targets remain
10, 13, and 5, with the existing tolerances and hold times. Every plate notifies the
manager on both solved and unsolved transitions, opening or relocking that door.
The older Environnement scene-only manager and placeholder setup are removed.

**Scale Affects Weight** makes each assigned prop's effective weight proportional
to its resized volume, using its authored Rigidbody mass as the starting weight.
The reference frame is Maison (or the table for standalone use), so resizing the
whole house does not change the puzzle targets. This changes the measured puzzle
weight without changing the house's Rigidbody mass.

The complete **Cube à Perspective** puzzle is nested in the interior beside the
table. Its viewing point is approximately 1.6 m above the floor at 1x house scale.
The projector refreshes when its camera/ghost transforms change and caches its
textures while stationary. Interior receiver-material variants preserve source
materials. The spawned **CubeBleu** inherits the grab/resize/feedback setup and
starts parented to Maison, then follows its normal contents/interaction rules.
The projector uses **Renderer Index 1**, a dedicated **Projector Renderer** in both
URP quality configurations. Its **Anamorphic Depth Capture** feature copies raw
depth into a floating-point color texture with a depth attachment, avoiding the
depth-only target conflict with XR Composition Layers. The headset stays on
renderer 0. Projector rendering has XR, MSAA, and post-processing disabled.

The active OpenXR configuration is **Assets/XR/Settings/OpenXRPackageSettings.asset**
(without spaces), restoring the pre-merge project reference. It contains the
configured Oculus Touch/Touch Plus controller profiles. The similarly named
**OpenXR Package Settings.asset** has disabled controller profiles and is not the
active configuration. Keep the existing shared rig input bindings.

Maison receiver variants use the original **TexturePalie** shader with
**Projection anamorphique** enabled. Texture tiling, tint/paling, masks, lighting,
and double-sided wall culling remain intact. Original materials default this
option off. If a capture fails, the projector hides its ghost and disables itself
with one warning; re-enable it after correcting the renderer configuration.
The balance dial/label quads have no Mesh Colliders; physical pads and trigger
sensors retain their colliders, so decorative faces cannot cause concave-mesh
errors under the dynamic house.
Gym Perspective keeps its original standalone demonstration; its inherited
Maison perspective puzzle is disabled there to avoid two projectors competing
for the shared projection shader state.

The **Complete XR Origin Set Up Variant** prefab owns the control settings and
controller hints previously overridden in Gym Interaction: right-hand smooth
turning, disabled 180-degree turn-around, teleport settings, disabled jump/climb,
the Resize callouts, and the removed teleport hint. Gym Interaction keeps its
spawn pose and name; EmptyPlains inherits the same controls from the prefab.

Check the complete flow in Play Mode/headset: unlock and confirm exactly one
cube appears; grab at a distance and release before contact; then hold until
contact and confirm the warp. Repeat with either hand and hold grip during the
load to verify no immediate return. Grab the return cube, confirm a fresh level,
and repeat the full cycle. The plains must contain no previous level objects.
Verify the fade covers both eyes through loading, restores the view on a failed
load, and that movement, turning, grip/trigger bindings, and hints match after
each trip. Check a direct Play launch of EmptyPlains as well.

## Manual checks

### Doorways, walking, and loading

- Environnement and EmptyPlains have a **Player Spawn** anchor under the red
  doorway/frame. `XRDoorwaySpawn` aligns the tracked headset horizontally and
  faces it toward the frame once on arrival, preserving the player's real height.
  The rig's saved spawn matches this anchor. The plains return cube is placed
  two metres ahead after alignment. Moving the anchor changes the arrival view.
- The shared XR Origin's Character Controller has **Step Offset = 0**. Ordinary
  Maison furniture uses the **HouseFurniture** physics layer (9), ignored only
  by the player's **Ignore Raycast** physics layer (2). Props still collide with
  furniture and XR rays still target it. Floors, carpets, structural walls, and
  the balance table retain player collision. The table blocks walking without
  automatic stepping onto its top; jump and climb remain disabled.
- Maison's **Entrance Walkway** contains two invisible convex ramps and a solid
  threshold bridge. They join the outside landing to the house floor with no
  vertical collision lip and follow the house's transform. Ramp slopes are at
  most 20 degrees at uniform scale. They add no renderers or runtime scripts.
- Maison's exterior `PorteVR` opens only if its scaled passage fits both the
  tracked headset/body height and controller-body width, with 5 cm clearance.
  **Largeur Passage** and **Hauteur Passage** are parent-local opening dimensions;
  the prefab uses the measured clear space between the frame and walkway.
  Growing the house sufficiently opens it from any distance; it remains open
  until the player no longer fits. Proximity and controller inputs do not toggle it.
- Grabbing disables the held Rigidbody's **Detect Collisions** immediately after
  the grab anchor is initialized, including the pull toward the hand. The saved
  flag returns after XRI finishes its release/throw, or on interactable disable.
  Collider components stay enabled for geometric targeting. The padlock checks
  held key shapes explicitly so unlocking still works without physical contacts.
- Exterior, bathroom, and red padlock doors use the shared profile's **Door Creak
  Clip** and **Door Creak Volume**. Replace `DoorCreak_Placeholder.wav` or assign
  another clip in `Resources/VRFeedbackDefaults`; audio is spatial, without Doppler.
- The shared scene fade shows `Resources/LoadingHourglass.png` centered on black
  in both directions, for at least 0.35 seconds before fading into the new scene.
  It does not intercept UI or targeting.

Check both arrivals while physically offset/turned in the tracking space. Walk
through chairs, sofas, and cabinets; confirm the balance table blocks walking
and furniture cannot support the player. Resize Maison below/above doorway fit,
check held-key unlocking, throw after release, and listen for each door's creak.
Confirm the hourglass appears in both eyes on both trips.

Files for the previous doorway/loading update (24 paths, including four new `.meta` files):

- Scenes: `Assets/Scenes/Environnement.unity` and `Assets/Scenes/EmptyPlains.unity`.
  Both rigs now start at `(-1.6992354, 0.02, -20.214226)`, yaw `154` degrees,
  with a scene-owned Player Spawn anchor and XRDoorwaySpawn component. EmptyPlains'
  saved return cube moved to `(-0.8224932, 0.2, -22.011814)`. Frame poses are unchanged.
- Shared prefabs: `Assets/Environnement/Prefabs/Maison.prefab` (612 furniture/prop
  GameObjects assigned layer 9; exterior passage measured at 1.164103 by 2.601978
  parent-local metres), `Assets/Perspective/CubeBleu.prefab` (physics layer 9), and
  `Assets/VRTemplateAssets/Prefabs/Setup/Complete XR Origin Set Up Variant.prefab`
  (only the Character Controller Step Offset override added).
- Physics configuration: `ProjectSettings/TagManager.asset` (HouseFurniture at 9)
  and `ProjectSettings/DynamicsManager.asset` (only the 2/9 collision pair changed).
- Updated scripts: `GreenCubeWarp.cs`, `PadlockUnlock.cs`, `PorteVR_Ouverture.cs`,
  `PorteVR_Balances.cs`, `VRHitPointGrabInteractable.cs`, `VRFeedbackProfile.cs`,
  and `XRSceneTransition.cs`, all under `Assets/Scripts`, plus this setup guide.
- New scripts: `Assets/Scripts/XRDoorwaySpawn.cs` and `Assets/Scripts/VRDoorAudio.cs`,
  each with its `.meta`.
- Feedback: `Assets/Resources/VRFeedbackDefaults.asset`,
  `Assets/Audio/VRFeedback/DoorCreak_Placeholder.wav` and its `.meta`, and
  `Assets/Resources/LoadingHourglass.png` and its `.meta`.

Validation: Unity import/compilation and saved-reference inspection passed. A
graphics-enabled Environnement Play Mode startup produced no gameplay or Render
Graph exceptions. This does not verify headset input, stereo appearance, walking,
contact behavior, or audible playback. No test scripts are included.

### Targeting, solid-door, and threshold repair

This repair changes ten paths, including the new mesh asset's `.meta`:

- `Assets/VRTemplateAssets/Prefabs/Setup/Complete XR Origin Set Up Variant.prefab`:
  both near masks add layer 9 (`0x201`); both far masks add it while preserving
  their other bits (`0x80000221`). All five ray-state gradients use #FFB000;
  their transparency, widths, 10 m reach, 7.5-degree targeting, input actions,
  grabbedAndScaled interaction filter, and zero Step Offset are preserved.
- `Assets/Resources/VRFeedbackDefaults.asset` and `Assets/Scripts/VRFeedbackProfile.cs`:
  shared and new-profile outlines default to #FFB000; width stays at 2 pixels.
- `Assets/Environnement/Prefabs/TablePlaques.prefab`: enable the existing convex,
  non-trigger bathroom door panel collider. The Maison instance inherits this.
- `Assets/Environnement/Prefabs/Maison.prefab`: enable the exterior panel collider
  and add Entrance Walkway with exterior/interior convex ramps and a solid bridge,
  without renderers, extra Rigidbodies, or runtime components. Walkway width is
  1.116168 m at 1x scale. Its base stays at the existing floor bottom (-0.15 m),
  crest is 0.052 m, exterior run is 0.569496 m, and interior run is 0.3 m. The
  measured clear opening becomes 1.156168 by 2.559427 parent-local metres.
- `Assets/Environnement/Meshes/House Entrance Ramps.asset` and its `.meta`:
  the two authored collision-only ramp meshes, each with eight vertices.
- `Assets/Scripts/PorteVR_Ouverture.cs`: automatic size-only opening in FixedUpdate;
  no proximity condition or manual toggle. Keep 5 cm clearance and creak audio.
- `Assets/Scripts/PorteVR_Balances.cs`: retain input/event puzzle logic and move
  only panel animation to FixedUpdate. The red door's existing solid panel and
  padlock logic remain unchanged.
- `Assets/Scripts/VRInteractionSetup.md`: these setup notes and validation steps.

No scene file is directly edited by this repair. Environnement, EmptyPlains, and
the gyms inherit their shared prefab changes; existing scene overrides remain.
In the headset, check near/far grabs and resizing with both hands, repeat releases
and re-entry, walk across the entrance in both directions at several house scales,
and confirm each closed door blocks the player. Grow/shrink Maison while far away
to check size-only opening. Check amber feedback in both eyes. Compilation and
saved-reference checks do not verify those interactive behaviors.

Repair validation passed: Unity compilation and saved-reference inspection;
closed ramp geometry with slopes of 19.3507 and 0.382 degrees; graphics-enabled
Play Mode startup in EmptyPlains and Environnement without gameplay, collider
cooking, or Render Graph errors. Headset input, door blocking, walking, and
stereo color/outline appearance still require the manual checks above.

In Play Mode with a headset or XR Interaction Simulator:

1. Aim at different faces and corners and grab with either hand, including beyond
   2 m when the interactor detects the object. Confirm the aimed-at surface point
   eases to the hand over the configured Pull Duration. Move and rotate the hand
   during the approach; confirm there is no initial rotation snap and that the
   object follows the moving hand. Test close-range grabs too.
2. Release halfway through the approach, then test normal release and throwing.
   Confirm the object stops following the hand and gravity and collisions resume.
3. Aim both rays at the cube and hold both triggers. Confirm only its size changes
   and that a single trigger or controllers targeting different objects do nothing.
4. Grow and shrink, reach both size limits, move rays away, release either trigger,
   and repeat. Confirm size persists and the next lock starts without a size jump.
   Grab the resized object at different points and verify the new anchor is correct.
5. While locked, try grabbing this or another configured object. Confirm it is
   blocked. Disable the scaler or disconnect a controller and confirm physics
   resumes and subsequent grabs work.
6. Resize beside the player capsule: growth must stop at contact, including a
   large outward hand movement. Walk into an already scaled object and confirm
   further growth is blocked while shrinking responds immediately. Step away
   and spread the hands to resume growth without a jump. Repeat after moving
   the XR Origin through locomotion.
7. Check objects just below, at, and above the headset's height above the rig
   floor. Taller objects must be ungrabbable but still resizable; shrink them to
   headset height or less and grab again. Crouch and stand to confirm the cutoff
   updates. Also check rotated objects, scaled parents, and child colliders.
8. Walk into Maison and confirm its root Interaction Layer Mask becomes Default
   and grip/two-trigger resizing both stop. Leave through the doorway and confirm
   grabbedAndScaled returns. Repeat after moving, rotating, or scaling the house;
   check the exterior cutouts remain outside and that the door still works.
9. Drop the key inside Maison: it must fall normally, sleep, and then become
   kinematic with interpolation off. Shake, rotate, invert, and resize Maison;
   settled contents must keep their local pose and scale proportionally. Inspect
   initial contents too: they must get a chance to fall before settling.
10. Grab or resize Maison while the key is still falling. Confirm the key pauses
    relative to the house and resumes falling from rest after release. Repeat
    with both grip and two-trigger resizing.
11. Grab a settled key and throw it outside. Confirm normal gravity, throwing,
    and house collisions return and the key stays independent when Maison moves.
    Bring the red cube inside and release it: it must be adopted, fall, settle,
    and follow the house. Repeat entry/exit and individual resizing, including
    differently sized houses; reparenting must preserve world size and size limits.
12. Disable/re-enable the house lock, item interactable, scaler, and whole objects
    in each state. Confirm physics and collision exclusions are restored, later
    interactions still work, and settled contents do not push the house. Test
    overlapping houses if used: retain the current owner until exiting its zones.

Headset interaction, visual placement, and throwing need interactive verification;
a successful script compilation alone does not verify these behaviors.

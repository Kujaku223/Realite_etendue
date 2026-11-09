# VR grabbing and resizing

For Unity 6 and XR Interaction Toolkit 3.5.1.

## Add the components to an object

1. Add `VRTwoHandScale` to the object's root for grabbing and resizing. Unity
   also adds `VRGrabObject`, `VRHitPointGrabInteractable`, `Rigidbody`, and
   `XRGeneralGrabTransformer`. For grabbing only, add `VRGrabObject` instead.
2. Provide an enabled, non-trigger collider on the root or its children. Use a
   Box, Sphere, Capsule, or convex Mesh Collider for a moving object. Make sure
   the VR Hit Point Grab Interactable's Colliders list includes it (an empty list is
   populated automatically at startup).
3. VR Hit Point Grab Interactable defaults its Interaction Layer Mask to
   `grabbedAndScaled` when added or reset in the editor. The Inspector's setting
   is preserved at runtime. Set **Pull Duration** on `VRGrabObject` (default 0.4 seconds).
   Grabbing automatically uses the selecting player's current headset height as
   the maximum object height.
4. Use a stationary parent with uniform scale. Resizing is anchored to the root's
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

`VRHouseInteractionLock` on Maison remembers its child XR Grab Interactables
whose transform pivots start inside its interior zones. When an item's pivot
leaves those zones, it detaches from the house while preserving its world pose
and scale. This also disables XRI's Retain Transform Parent for that item, so
dropping a held item outside cannot attach it back to Maison. Bringing it back
inside restores its original house parent and parent-retention setting. A held
item continues following the hand and reattaches when released inside. Parenting
changes preserve world position, rotation, and scale. The existing key is handled
automatically; only items that started as house contents are tracked.

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
its root. The Maison instance in Gym Interaction already supplies the root
grab and resize components; the lock finds those automatically. The prefab's
door and geometry are preserved. HouseNoDoor has not been given this lock.

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

## Manual checks

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
9. Grab the key and release it inside Maison: it should return to its house
   parent. Then carry it outside and release it; move, rotate, and scale Maison
   and confirm the key stays independent. Repeat by throwing an item outside,
   then bring it back inside and release it. Confirm it returns to its original
   house parent without jumping and follows subsequent house movement and scale.
   Repeat several inside/outside trips, including picking it up outside and
   throwing it back inside.

Headset interaction, visual placement, and throwing need interactive verification;
a successful script compilation alone does not verify these behaviors.

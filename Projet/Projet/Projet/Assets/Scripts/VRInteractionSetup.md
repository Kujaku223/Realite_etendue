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
limits. Keep parents uniformly scaled to preserve proportions during reparenting.

Maison in Gym Interaction has a 1,000 kg dynamic Rigidbody. Its animated door's
collider is disabled directly in the Maison prefab and its unused Rigidbody is
removed. `PorteVR` handles proximity opening without collision-filtering code.
The door cannot push or block objects and cannot be targeted by XR rays.
The house's floor and walls continue to collide normally with objects.

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

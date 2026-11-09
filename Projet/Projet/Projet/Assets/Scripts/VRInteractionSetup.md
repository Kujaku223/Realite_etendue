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
3. Set the VR Hit Point Grab Interactable's Interaction Layer Mask to
   `grabbedAndScaled`. Set **Pull Duration** on `VRGrabObject` (default 0.4 seconds).
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

The shared XR Origin prefab includes a **Player Scale Capsule** child, with a
trigger Capsule Collider and `VRPlayerScaleCapsule`. It follows the headset's
horizontal position and adjusts its height from the rig floor to the headset,
including room-scale movement and crouching. Its default radius is **0.25 m**;
adjust **Radius** on `VRPlayerScaleCapsule` to change the player body width.
The sensor uses the Ignore Raycast physics layer and leaves locomotion to the
rig's existing Character Controller.

While resizing, overlapping the player capsule blocks further growth. Proposed
growth is also checked before it is accepted, so a large size change stops at
contact. Shrinking remains available immediately. Blocked hand movement is
discarded, so moving away from the object does not cause a sudden size jump.
Move clear of the object and spread your hands again to resume growing it.

Scalable objects automatically find this capsule through the selecting
controller's XR Origin. For another rig, add a **Player Scale Capsule** child
at the XR Origin's local position zero and add `VRPlayerScaleCapsule` to it.
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
   and spread the hands to resume growth without a jump. Repeat while crouching
   and moving around within the tracked room.

Headset interaction, visual placement, and throwing need interactive verification;
a successful script compilation alone does not verify these behaviors.

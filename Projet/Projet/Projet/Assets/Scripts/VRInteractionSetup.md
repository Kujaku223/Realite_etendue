# VR grabbing and resizing

For Unity 6 and XR Interaction Toolkit 3.5.1.

## Add the components to an object

1. Add `VRGrabObject` to the object's root. Unity also adds the required
   `XRGrabInteractable`, `Rigidbody`, and `XRGeneralGrabTransformer`.
2. Provide an enabled, non-trigger collider on the root or its children. Use a
   Box, Sphere, Capsule, or convex Mesh Collider for a moving object. Make sure
   the XR Grab Interactable's Colliders list includes it (an empty list is
   populated automatically at startup).
3. Add `VRTwoHandScale` to the same root if the object should also resize.
4. Use a stationary parent with uniform scale. Resizing is anchored to the root's
   transform pivot; it preserves world position and rotation, not the bottom face
   of a mesh whose pivot is at its centre.

`VRGrabObject` configures single selection, a near attachment, kinematic carrying,
position/rotation tracking, gravity after release, and throwing. It turns off
built-in grab scaling. Its runtime configuration takes precedence over those
settings on XR Grab Interactable. Leave the Rigidbody dynamic with gravity for
normal use. It is temporarily made kinematic during carrying and resizing.

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

Resizing reads the Activate input directly, so **Allow Hovered Activate** does
not need to be enabled. It uses tracked controller status; disconnected or
untracked controllers end a resize. The XR Interaction Simulator can supply
tracked simulated controllers for desktop checks.

## Controls

- **Carry:** point at or touch the object, hold grip, move or rotate the
  controller, then release grip to drop or throw. Detection reach is controlled
  by the Near-Far Interactor's casters; there is no additional script distance
  limit. Carrying continues until grip release.
- **Resize:** with both hands free, point both controllers at the same object
  and hold **both triggers**. The object stays at its current world
  position and rotation. Move hands apart to grow it or together to shrink it.
- The resize remains locked if the rays drift away. Release either trigger to
  finish. Grabbing is blocked for
  the locked controllers until resizing finishes.
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

1. Grab with either hand, including beyond 2 m when the interactor detects the
   object. Continue carrying, rotate, release, and throw; confirm gravity and collisions.
2. Aim both rays at the cube and hold both triggers. Confirm only its size changes
   and that a single trigger or controllers targeting different objects do nothing.
3. Grow and shrink, reach both size limits, move rays away, release either trigger,
   and repeat. Confirm size persists and the next lock starts without a size jump.
4. While locked, try grabbing this or another configured object. Confirm it is
   blocked. Disable the scaler or disconnect a controller and confirm physics
   resumes and subsequent grabs work.

Headset interaction, visual placement, and throwing need interactive verification;
a successful script compilation alone does not verify these behaviors.

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

/// <summary>Uses the selected collider hit as the object's dynamic grab anchor.</summary>
[AddComponentMenu("XR/Interactables/VR Hit Point Grab Interactable")]
public sealed class VRHitPointGrabInteractable : XRGrabInteractable
{
    bool m_FinishHouseRelease;

    protected override void OnEnable()
    {
        // XRI auto-fills an empty collider list with every descendant collider.
        // Exclude independent contents before registering, regardless of enable order.
        var body = GetComponent<Rigidbody>();
        colliders.RemoveAll(collider => collider == null ||
            collider.attachedRigidbody != body ||
            collider.GetComponentInParent<XRBaseInteractable>(true) != this);
        base.OnEnable();
        VRHouseInteractionLock.RegisterItem(this);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        m_FinishHouseRelease = false;
        VRHouseInteractionLock.UnregisterItem(this);
    }

    protected override void OnSelectEntering(SelectEnterEventArgs args)
    {
        // Restore dynamic physics before XRI saves the Rigidbody's grab state.
        m_FinishHouseRelease = false;
        VRHouseInteractionLock.PrepareForInteraction(this);
        base.OnSelectEntering(args);
    }

    protected override void OnSelectExiting(SelectExitEventArgs args)
    {
        VRHouseInteractionLock.MarkReleasePending(this);
        base.OnSelectExiting(args);
    }

    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        base.OnSelectExited(args);
        m_FinishHouseRelease = !isSelected;
    }

    public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
    {
        base.ProcessInteractable(updatePhase);
        if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Late)
            return;

        // XRI applies its deferred detach/throw in the base Late processing.
        if (m_FinishHouseRelease && !isSelected)
        {
            m_FinishHouseRelease = false;
            VRHouseInteractionLock.FinishInteraction(this);
        }
        if (TryGetComponent<VRHouseInteractionLock>(out var house))
            house.SyncFrozenContents();
    }

    protected override void Reset()
    {
        base.Reset();
        interactionLayers = InteractionLayerMask.GetMask("grabbedAndScaled");
    }

    protected override void InitializeDynamicAttachPose(
        IXRSelectInteractor interactor, Transform dynamicAttachTransform)
    {
        Transform hand = interactor.GetAttachTransform(this);
        if (!TryGetRayHitPoint(interactor, out Vector3 worldHitPoint))
            worldHitPoint = GetNearestColliderPoint(hand.position);

        // The toolkit parents this anchor to the object before initializing it.
        // Setting its world pose therefore saves the hit in the object's local space.
        // Matching the initial hand rotation preserves the object's rotation offset.
        dynamicAttachTransform.SetPositionAndRotation(worldHitPoint, hand.rotation);
    }

    bool TryGetRayHitPoint(IXRSelectInteractor interactor, out Vector3 point)
    {
        if (interactor is NearFarInteractor nearFar &&
            nearFar.TryGetCurveEndPoint(out point) == EndPointType.ValidCastHit &&
            IsOwnHitTransform(((IXRRayProvider)nearFar).rayEndTransform))
        {
            return true;
        }

        if (interactor is XRRayInteractor ray &&
            ray.TryGetCurrent3DRaycastHit(out RaycastHit hit) &&
            colliders.Contains(hit.collider))
        {
            point = hit.point;
            return true;
        }

        point = default;
        return false;
    }

    bool IsOwnHitTransform(Transform hitTransform)
    {
        if (hitTransform == null)
            return false;

        foreach (var collider in colliders)
        {
            if (!IsUsable(collider))
                continue;

            // RaycastHit.transform can be the attached Rigidbody's transform,
            // including when the actual hit collider is on a child object.
            if (collider.transform == hitTransform ||
                (collider.attachedRigidbody != null &&
                 collider.attachedRigidbody.transform == hitTransform))
            {
                return true;
            }
        }

        return false;
    }

    Vector3 GetNearestColliderPoint(Vector3 handPosition)
    {
        Vector3 nearestPoint = transform.position;
        float nearestDistanceSquared = float.PositiveInfinity;
        foreach (var collider in colliders)
        {
            if (!IsUsable(collider))
                continue;

            Vector3 point = collider.ClosestPoint(handPosition);
            float distanceSquared = (point - handPosition).sqrMagnitude;
            if (distanceSquared < nearestDistanceSquared)
            {
                nearestDistanceSquared = distanceSquared;
                nearestPoint = point;
            }
        }

        return nearestPoint;
    }

    static bool IsUsable(Collider collider)
    {
        return collider != null && collider.enabled && !collider.isTrigger &&
            collider.gameObject.activeInHierarchy;
    }
}

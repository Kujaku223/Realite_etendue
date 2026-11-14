using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

/// <summary>Grip to carry an object, subject to resizing and size restrictions.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(VRHitPointGrabInteractable), typeof(Rigidbody), typeof(XRGeneralGrabTransformer))]
[RequireComponent(typeof(VRObjectFeedback))]
public sealed class VRGrabObject : MonoBehaviour, IXRSelectFilter
{
    [SerializeField, Min(0.01f)]
    [Tooltip("Time in seconds for the grabbed hit point to ease toward the moving hand.")]
    float m_PullDuration = 0.4f;

    XRGrabInteractable m_Interactable;
    VRTwoHandScale m_Scaler;
    bool m_ThrowOnDetachBeforeBlock;

    public XRGrabInteractable interactable => m_Interactable;
    public bool canProcess => isActiveAndEnabled;
    public bool interactionBlocked { get; private set; }

    void Awake()
    {
        m_Interactable = GetComponent<XRGrabInteractable>();
        m_Scaler = GetComponent<VRTwoHandScale>();
        ConfigureGrab();

        var body = GetComponent<Rigidbody>();
        body.isKinematic = false;
        body.useGravity = true;
    }

    void OnEnable()
    {
        m_Interactable.selectFilters.Add(this);
    }

    void OnDisable()
    {
        if (m_Scaler != null)
            m_Scaler.EndScale();

        if (m_Interactable != null)
            m_Interactable.selectFilters.Remove(this);
    }

    void ConfigureGrab()
    {
        m_Interactable.selectMode = InteractableSelectMode.Single;
        m_Interactable.movementType = XRBaseInteractable.MovementType.Kinematic;
        m_Interactable.useDynamicAttach = true;
        m_Interactable.matchAttachPosition = true;
        m_Interactable.matchAttachRotation = true;
        m_Interactable.farAttachMode = InteractableFarAttachMode.Near;
        m_Interactable.attachEaseInTime = Mathf.Max(0.01f, m_PullDuration);
        m_Interactable.trackPosition = true;
        m_Interactable.trackRotation = true;
        m_Interactable.trackScale = false;
        m_Interactable.throwOnDetach = true;

        var transformer = GetComponent<XRGeneralGrabTransformer>();
        transformer.allowOneHandedScaling = false;
        transformer.allowTwoHandedScaling = false;
    }

    void OnValidate()
    {
        m_PullDuration = Mathf.Max(0.01f, m_PullDuration);
        if (Application.isPlaying && m_Interactable != null)
            m_Interactable.attachEaseInTime = m_PullDuration;
    }

    public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable target)
    {
        if (interactionBlocked)
            return false;

        if (m_Scaler != null && m_Scaler.isScaling)
            return false;

        if (m_Interactable.IsSelected(interactor))
            return true;

        return interactor is XRBaseInputInteractor controller &&
            controller.handedness != InteractorHandedness.None &&
            !VRTwoHandScale.IsControllerLocked(controller) &&
            !ExceedsHeadsetHeight(controller);
    }

    public void SetInteractionBlocked(bool blocked)
    {
        if (interactionBlocked == blocked)
            return;

        interactionBlocked = blocked;
        if (m_Interactable == null)
            return;

        if (!blocked)
        {
            m_Interactable.throwOnDetach = m_ThrowOnDetachBeforeBlock;
            return;
        }

        // Entering the house cancels manipulation without throwing the house.
        // Keep throwing off through the toolkit's deferred detach until the player leaves.
        m_ThrowOnDetachBeforeBlock = m_Interactable.throwOnDetach;
        m_Interactable.throwOnDetach = false;
        if (m_Scaler != null)
            m_Scaler.EndScale();
        if (m_Interactable.isSelected && m_Interactable.interactionManager != null)
            m_Interactable.interactionManager.CancelInteractableSelection((IXRSelectInteractable)m_Interactable);

        var body = GetComponent<Rigidbody>();
        if (body != null && !body.isKinematic)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }

    bool ExceedsHeadsetHeight(XRBaseInputInteractor controller)
    {
        var origin = controller.GetComponentInParent<XROrigin>();
        if (origin == null || origin.Camera == null)
            return true;

        Transform floor = origin.Origin != null ? origin.Origin.transform : origin.transform;
        float headsetHeight = origin.Camera.transform.position.y - floor.position.y;
        if (headsetHeight <= Mathf.Epsilon)
            return true;

        // Bounds must include the latest resizing, rotation, and parent transforms.
        Physics.SyncTransforms();
        Bounds bounds = default;
        bool hasBounds = false;
        foreach (var collider in m_Interactable.colliders)
        {
            if (collider == null || !collider.enabled || collider.isTrigger ||
                !collider.gameObject.activeInHierarchy)
                continue;

            if (hasBounds)
                bounds.Encapsulate(collider.bounds);
            else
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
        }

        return hasBounds && bounds.size.y > headsetHeight &&
            !Mathf.Approximately(bounds.size.y, headsetHeight);
    }
}

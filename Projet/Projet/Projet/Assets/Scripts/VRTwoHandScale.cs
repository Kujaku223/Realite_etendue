using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>Hold both triggers over an object to resize it around its fixed pivot.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(VRGrabObject))]
public sealed class VRTwoHandScale : MonoBehaviour
{
    const float k_MinimumHandSeparation = 0.01f;

    [SerializeField, Range(0.01f, 1f)]
    [Tooltip("Smallest size relative to the object's initial local scale.")]
    float m_MinimumScaleMultiplier = 0.25f;

    [SerializeField, Min(1f)]
    [Tooltip("Largest size relative to the object's initial local scale.")]
    float m_MaximumScaleMultiplier = 4f;

    // A latched pair must not resize another object when its rays drift away.
    static VRTwoHandScale s_ActiveScale;

    VRGrabObject m_Grab;
    XRGrabInteractable m_Interactable;
    Rigidbody m_Body;
    XRBaseInputInteractor m_Left;
    XRBaseInputInteractor m_Right;
    Vector3 m_InitialScale;
    Vector3 m_StartScale;
    Vector3 m_LockedPosition;
    Quaternion m_LockedRotation;
    float m_StartMultiplier;
    float m_StartSeparation;
    bool m_WasKinematic;
    bool m_UsedGravity;

    public bool isScaling { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetLock()
    {
        s_ActiveScale = null;
    }

    void Awake()
    {
        m_Grab = GetComponent<VRGrabObject>();
        m_Interactable = GetComponent<XRGrabInteractable>();
        m_Body = GetComponent<Rigidbody>();
        m_InitialScale = transform.localScale;
    }

    void OnDisable() => EndScale();

    void OnValidate()
    {
        m_MinimumScaleMultiplier = Mathf.Clamp(m_MinimumScaleMultiplier, 0.01f, 1f);
        m_MaximumScaleMultiplier = Mathf.Max(1f, m_MaximumScaleMultiplier);
    }

    void LateUpdate()
    {
        if (isScaling)
        {
            if (!CanContinue())
            {
                EndScale();
                return;
            }

            float separation = Vector3.Distance(m_Left.transform.position, m_Right.transform.position);
            float multiplier = Mathf.Clamp(m_StartMultiplier * separation / m_StartSeparation,
                m_MinimumScaleMultiplier, m_MaximumScaleMultiplier);
            transform.localScale = m_StartScale * (multiplier / m_StartMultiplier);
            ApplyLockedPose();
            return;
        }

        if (s_ActiveScale != null || !m_Grab.isActiveAndEnabled ||
            !m_Interactable.isActiveAndEnabled || m_Interactable.isSelected)
            return;

        XRBaseInputInteractor left = null;
        XRBaseInputInteractor right = null;
        foreach (var hovering in m_Interactable.interactorsHovering)
        {
            if (!(hovering is XRBaseInputInteractor controller) ||
                !IsTriggerHeld(controller))
                continue;

            if (controller.handedness == InteractorHandedness.Left)
                left = controller;
            else if (controller.handedness == InteractorHandedness.Right)
                right = controller;
        }

        if (left != null && right != null)
            BeginScale(left, right);
    }

    void BeginScale(XRBaseInputInteractor left, XRBaseInputInteractor right)
    {
        float separation = Vector3.Distance(left.transform.position, right.transform.position);
        if (separation < k_MinimumHandSeparation)
            return;

        m_Left = left;
        m_Right = right;
        m_StartSeparation = separation;
        m_StartScale = transform.localScale;

        // Rebase each lock from the actual scale, so regrabbing never resets the size.
        float initialLength = m_InitialScale.magnitude;
        if (initialLength < Mathf.Epsilon)
            return;
        m_StartMultiplier = m_StartScale.magnitude / initialLength;
        if (m_StartMultiplier < Mathf.Epsilon)
            return;

        m_LockedPosition = transform.position;
        m_LockedRotation = transform.rotation;
        m_WasKinematic = m_Body.isKinematic;
        m_UsedGravity = m_Body.useGravity;
        ClearVelocity();
        m_Body.isKinematic = true;
        m_Body.useGravity = false;
        isScaling = true;
        s_ActiveScale = this;
        ApplyLockedPose();
    }

    bool CanContinue()
    {
        return m_Grab.isActiveAndEnabled && m_Interactable.isActiveAndEnabled &&
            !m_Interactable.isSelected && IsTriggerHeld(m_Left) && IsTriggerHeld(m_Right);
    }

    static bool IsTriggerHeld(XRBaseInputInteractor controller)
    {
        if (controller == null || !controller.isActiveAndEnabled || controller.hasSelection ||
            !controller.activateInput.ReadIsPerformed())
            return false;

        // A sibling direct/ray interactor on this hand may be carrying something.
        if (controller.containingGroup?.activeInteractor is IXRSelectInteractor active && active.hasSelection)
            return false;

        TrackingStatus status;
        if (controller.handedness == InteractorHandedness.Left)
            status = XRInputTrackingAggregator.GetLeftControllerStatus();
        else if (controller.handedness == InteractorHandedness.Right)
            status = XRInputTrackingAggregator.GetRightControllerStatus();
        else
            return false;

        const InputTrackingState pose = InputTrackingState.Position | InputTrackingState.Rotation;
        return status.isConnected && (status.isTracked || (status.trackingState & pose) == pose);
    }

    void ApplyLockedPose()
    {
        m_Body.position = m_LockedPosition;
        m_Body.rotation = m_LockedRotation;
        transform.SetPositionAndRotation(m_LockedPosition, m_LockedRotation);
    }

    void ClearVelocity()
    {
        if (!m_Body.isKinematic)
        {
            m_Body.linearVelocity = Vector3.zero;
            m_Body.angularVelocity = Vector3.zero;
        }
    }

    public void EndScale()
    {
        if (!isScaling)
            return;

        isScaling = false;
        if (s_ActiveScale == this)
            s_ActiveScale = null;

        if (m_Body != null)
        {
            ApplyLockedPose();
            m_Body.isKinematic = m_WasKinematic;
            m_Body.useGravity = m_UsedGravity;
            ClearVelocity();
        }

        m_Left = null;
        m_Right = null;
    }

    public static bool IsControllerLocked(XRBaseInputInteractor controller)
    {
        return s_ActiveScale != null && controller != null &&
            (controller.handedness == InteractorHandedness.Left ||
             controller.handedness == InteractorHandedness.Right);
    }
}

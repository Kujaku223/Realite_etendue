using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

/// <summary>Grip to carry an object, with grabbing blocked during resizing.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(XRGrabInteractable), typeof(Rigidbody), typeof(XRGeneralGrabTransformer))]
public sealed class VRGrabObject : MonoBehaviour, IXRSelectFilter
{
    XRGrabInteractable m_Interactable;
    VRTwoHandScale m_Scaler;

    public XRGrabInteractable interactable => m_Interactable;
    public bool canProcess => isActiveAndEnabled;

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
        m_Interactable.useDynamicAttach = false;
        m_Interactable.farAttachMode = InteractableFarAttachMode.Near;
        m_Interactable.attachEaseInTime = 0.1f;
        m_Interactable.trackPosition = true;
        m_Interactable.trackRotation = true;
        m_Interactable.trackScale = false;
        m_Interactable.throwOnDetach = true;

        var transformer = GetComponent<XRGeneralGrabTransformer>();
        transformer.allowOneHandedScaling = false;
        transformer.allowTwoHandedScaling = false;
    }

    public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable target)
    {
        if (m_Scaler != null && m_Scaler.isScaling)
            return false;

        if (m_Interactable.IsSelected(interactor))
            return true;

        return interactor is XRBaseInputInteractor controller &&
            controller.handedness != InteractorHandedness.None &&
            !VRTwoHandScale.IsControllerLocked(controller);
    }
}

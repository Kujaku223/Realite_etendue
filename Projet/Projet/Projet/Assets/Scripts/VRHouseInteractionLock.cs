using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>Disables house manipulation while the headset is inside its interior.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(XRInteractionUpdateOrder.k_InteractionManager - 1)]
public sealed class VRHouseInteractionLock : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Interior zones in the house root's local space. Select the house to see their outlines.")]
    Bounds[] m_InteriorVolumes = new Bounds[0];

    [SerializeField]
    [Tooltip("XR interaction layers used while the player is inside. Default keeps the house off the controller filter.")]
    InteractionLayerMask m_InsideLayers = 1;

    XRGrabInteractable m_Interactable;
    VRGrabObject m_Grab;
    Transform m_Headset;
    InteractionLayerMask m_OutsideLayers;
    bool m_PlayerInside;
    bool m_CapturedOutsideLayers;

    void Awake() => FindTarget();

    void OnEnable()
    {
        m_CapturedOutsideLayers = false;
    }

    void Update()
    {
        // The prefab may have no grab component until one is added to its scene instance.
        // Only the root interactable is controlled; the door keeps its own interaction.
        if (!FindTarget())
            return;

        // Capture the Inspector's outside layer after the target has initialized.
        if (!m_CapturedOutsideLayers)
        {
            m_OutsideLayers = m_Interactable.interactionLayers;
            m_CapturedOutsideLayers = true;
        }

        if (m_Headset == null || !m_Headset.gameObject.activeInHierarchy)
        {
            Camera camera = Camera.main;
            m_Headset = camera != null ? camera.transform : null;
        }

        bool inside = false;
        if (m_Headset != null && m_InteriorVolumes != null)
        {
            Vector3 headLocal = transform.InverseTransformPoint(m_Headset.position);
            foreach (Bounds volume in m_InteriorVolumes)
            {
                if (volume.Contains(headLocal))
                {
                    inside = true;
                    break;
                }
            }
        }

        if (inside == m_PlayerInside)
            return;

        m_PlayerInside = inside;
        m_Interactable.interactionLayers = inside ? m_InsideLayers : m_OutsideLayers;
        m_Grab.SetInteractionBlocked(inside);
    }

    bool FindTarget()
    {
        if (m_Interactable != null && m_Grab != null)
            return true;

        m_Interactable = GetComponent<XRGrabInteractable>();
        m_Grab = GetComponent<VRGrabObject>();
        if (m_Interactable == null || m_Grab == null)
            return false;

        return true;
    }

    void OnDisable()
    {
        if (!m_PlayerInside)
            return;

        m_PlayerInside = false;
        if (m_Interactable != null)
            m_Interactable.interactionLayers = m_OutsideLayers;
        if (m_Grab != null)
            m_Grab.SetInteractionBlocked(false);
    }

    void OnDrawGizmosSelected()
    {
        if (m_InteriorVolumes == null)
            return;

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 1f);
        foreach (Bounds volume in m_InteriorVolumes)
            Gizmos.DrawWireCube(volume.center, volume.size);
        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>Locks house manipulation inside and manages parenting of its movable contents.</summary>
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
    sealed class HouseItem
    {
        public XRGrabInteractable interactable;
        public Transform originalParent;
        public bool retainTransformParent;
    }

    readonly List<HouseItem> m_InteriorItems = new List<HouseItem>();

    void Awake()
    {
        FindTarget();

        // Remember the original contents: XRI temporarily unparents held objects.
        foreach (var item in GetComponentsInChildren<XRGrabInteractable>(true))
        {
            if (item.transform != transform &&
                item.GetComponentInParent<VRHouseInteractionLock>(true) == this &&
                ContainsWorldPoint(item.transform.position))
            {
                m_InteriorItems.Add(new HouseItem
                {
                    interactable = item,
                    originalParent = item.transform.parent,
                    retainTransformParent = item.retainTransformParent,
                });
            }
        }
    }

    void OnEnable()
    {
        m_CapturedOutsideLayers = false;
    }

    void Update()
    {
        UpdateItemParents();

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

        bool inside = m_Headset != null && ContainsWorldPoint(m_Headset.position);

        if (inside == m_PlayerInside)
            return;

        m_PlayerInside = inside;
        m_Interactable.interactionLayers = inside ? m_InsideLayers : m_OutsideLayers;
        m_Grab.SetInteractionBlocked(inside);
    }

    bool ContainsWorldPoint(Vector3 worldPoint)
    {
        if (m_InteriorVolumes == null)
            return false;

        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        foreach (Bounds volume in m_InteriorVolumes)
        {
            if (volume.Contains(localPoint))
                return true;
        }

        return false;
    }

    void UpdateItemParents()
    {
        for (int i = m_InteriorItems.Count - 1; i >= 0; --i)
        {
            HouseItem entry = m_InteriorItems[i];
            XRGrabInteractable item = entry.interactable;
            if (item == null)
            {
                m_InteriorItems.RemoveAt(i);
                continue;
            }

            if (ContainsWorldPoint(item.transform.position))
            {
                item.retainTransformParent = entry.retainTransformParent;

                // Let XRI keep held objects unparented. It may have captured a null
                // parent on an outside grab, so also restore parenting after release.
                if (!item.isSelected && !item.transform.IsChildOf(transform))
                {
                    Transform parent = entry.originalParent;
                    if (parent == null || !parent.IsChildOf(transform))
                        parent = transform;
                    item.transform.SetParent(parent, true);
                }
                continue;
            }

            // Drop must not restore the house parent captured by XRI on grab.
            item.retainTransformParent = false;
            if (item.transform.IsChildOf(transform))
                item.transform.SetParent(null, true);

            // Keep tracking detached contents so they can return to the house.
        }
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

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>Locks house manipulation inside and manages its settled contents.</summary>
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

    enum ContentState { OutsideOrInteracting, Falling, Settled, Paused }

    sealed class HouseItem
    {
        public Transform transform;
        public Rigidbody body;
        public XRGrabInteractable interactable;
        public VRTwoHandScale scaler;
        public VRHouseInteractionLock owner;
        public Transform preferredParent;
        public bool originalRetainParent;
        public bool hasHouseHistory;
        public bool interacting;
        public bool releasePending;
        public ContentState state;
        public bool frozen;
        public bool originalKinematic;
        public RigidbodyInterpolation originalInterpolation;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
        public readonly List<(Collider item, Collider house)> ignoredPairs =
            new List<(Collider item, Collider house)>();
    }

    static readonly List<VRHouseInteractionLock> s_Houses = new List<VRHouseInteractionLock>();
    static readonly List<HouseItem> s_Items = new List<HouseItem>();
    static int s_UpdateFrame = -1;
    static double s_FixedTime = double.NegativeInfinity;

    XRGrabInteractable m_Interactable;
    VRGrabObject m_Grab;
    VRTwoHandScale m_Scaler;
    Transform m_Headset;
    InteractionLayerMask m_OutsideLayers;
    bool m_PlayerInside;
    bool m_CapturedOutsideLayers;
    bool m_ReleasePending;

    bool manipulating => m_ReleasePending ||
        (m_Interactable != null && m_Interactable.isActiveAndEnabled && m_Interactable.isSelected) ||
        (m_Scaler != null && m_Scaler.isActiveAndEnabled && m_Scaler.isScaling);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRegistries()
    {
        s_Houses.Clear();
        s_Items.Clear();
        s_UpdateFrame = -1;
        s_FixedTime = double.NegativeInfinity;
    }

    void OnEnable()
    {
        FindTarget();
        m_Scaler = GetComponent<VRTwoHandScale>();
        m_CapturedOutsideLayers = false;
        m_ReleasePending = false;
        if (!s_Houses.Contains(this))
            s_Houses.Add(this);

        // Existing non-grabbable dynamic props also belong to the house.
        foreach (Rigidbody body in GetComponentsInChildren<Rigidbody>(true))
        {
            if (body.transform == transform ||
                body.GetComponentInParent<VRHouseInteractionLock>(true) != this)
                continue;
            var grab = body.GetComponent<XRGrabInteractable>();
            if (grab != null)
                RegisterItem(grab);
            else if (!body.isKinematic && FindItem(body.transform) == null)
                s_Items.Add(new HouseItem { transform = body.transform, body = body });
        }
    }

    internal static void RegisterItem(XRGrabInteractable item)
    {
        if (item == null || item.GetComponent<VRHouseInteractionLock>() != null || FindItem(item.transform) != null)
            return;
        s_Items.Add(new HouseItem
        {
            transform = item.transform,
            body = item.GetComponent<Rigidbody>(),
            interactable = item,
            scaler = item.GetComponent<VRTwoHandScale>(),
            originalRetainParent = item.retainTransformParent,
        });
    }

    internal static void UnregisterItem(XRGrabInteractable item)
    {
        if (item == null)
            return;
        if (item.TryGetComponent<VRHouseInteractionLock>(out var house))
        {
            house.m_ReleasePending = false;
            TickItems(false);
            return;
        }
        HouseItem entry = FindItem(item.transform);
        if (entry == null)
            return;
        RestorePhysics(entry);
        item.retainTransformParent = entry.originalRetainParent;
        s_Items.Remove(entry);
    }

    static HouseItem FindItem(Transform item)
    {
        foreach (HouseItem entry in s_Items)
        {
            if (entry.transform == item)
                return entry;
        }
        return null;
    }

    internal static void PrepareForInteraction(XRGrabInteractable item)
    {
        if (item.TryGetComponent<VRHouseInteractionLock>(out var house))
        {
            TickItems(false, house);
            return;
        }
        RegisterItem(item);
        HouseItem entry = FindItem(item.transform);
        if (entry == null)
            return;
        TickItem(entry, false, null);
        SyncFrozenPose(entry);
        RestorePhysics(entry);
        if (entry.owner != null && entry.transform.IsChildOf(entry.owner.transform))
            entry.transform.SetParent(null, true);
        entry.interacting = true;
        entry.releasePending = false;
        entry.state = ContentState.OutsideOrInteracting;
    }

    internal static void MarkReleasePending(XRGrabInteractable item)
    {
        if (item.TryGetComponent<VRHouseInteractionLock>(out var house))
            house.m_ReleasePending = true;
        else
        {
            HouseItem entry = FindItem(item.transform);
            if (entry != null)
            {
                entry.releasePending = true;
                if (entry.owner != null)
                    item.retainTransformParent = entry.originalRetainParent &&
                        entry.owner.isActiveAndEnabled && entry.owner.ContainsWorldPoint(item.transform.position);
            }
        }
    }

    internal static void FinishInteraction(XRGrabInteractable item)
    {
        if (item == null || !item.isActiveAndEnabled)
            return;
        if (item.TryGetComponent<VRHouseInteractionLock>(out var house))
        {
            house.m_ReleasePending = false;
            TickItems(false);
            return;
        }
        RegisterItem(item);
        HouseItem entry = FindItem(item.transform);
        if (entry == null)
            return;
        entry.interacting = false;
        entry.releasePending = false;
        entry.state = ContentState.OutsideOrInteracting;
        TickItem(entry, false, null);
    }

    void Update()
    {
        UpdatePlayerLock();
        if (s_UpdateFrame != Time.frameCount)
        {
            s_UpdateFrame = Time.frameCount;
            TickItems(false);
        }
    }

    void FixedUpdate()
    {
        if (s_FixedTime != Time.fixedTimeAsDouble)
        {
            s_FixedTime = Time.fixedTimeAsDouble;
            TickItems(true);
        }
    }

    void LateUpdate() => SyncFrozenContents();

    // Called after house scaling/XRI late processing so the final hierarchy pose wins.
    internal void SyncFrozenContents()
    {
        if (!isActiveAndEnabled)
            return;
        foreach (HouseItem entry in s_Items)
        {
            if (entry.owner == this)
                SyncFrozenPose(entry);
        }
    }

    void UpdatePlayerLock()
    {
        if (!FindTarget())
            return;
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

    bool ContainsWorldPoint(Vector3 point)
    {
        if (m_InteriorVolumes == null)
            return false;
        Vector3 local = transform.InverseTransformPoint(point);
        foreach (Bounds volume in m_InteriorVolumes)
        {
            if (volume.Contains(local))
                return true;
        }
        return false;
    }

    static VRHouseInteractionLock FindHouse(HouseItem entry)
    {
        if (entry.owner != null && entry.owner.isActiveAndEnabled &&
            entry.owner.ContainsWorldPoint(entry.transform.position))
            return entry.owner;
        VRHouseInteractionLock nearest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (var house in s_Houses)
        {
            if (house == null || !house.isActiveAndEnabled || house.transform == entry.transform ||
                house.transform.IsChildOf(entry.transform) || !house.ContainsWorldPoint(entry.transform.position))
                continue;
            float distance = (house.transform.position - entry.transform.position).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearest = house;
                nearestDistance = distance;
            }
        }
        return nearest;
    }

    static void TickItems(bool checkSleeping, VRHouseInteractionLock startingHouse = null)
    {
        for (int i = s_Items.Count - 1; i >= 0; --i)
        {
            HouseItem entry = s_Items[i];
            if (entry.transform == null || entry.body == null)
            {
                RestorePhysics(entry);
                s_Items.RemoveAt(i);
                continue;
            }
            TickItem(entry, checkSleeping, startingHouse);
        }
    }

    static void TickItem(HouseItem entry, bool checkSleeping, VRHouseInteractionLock startingHouse)
    {
        if (!entry.transform.gameObject.activeInHierarchy ||
            (entry.interactable != null && !entry.interactable.isActiveAndEnabled))
        {
            ReleaseOwner(entry, false);
            return;
        }

        // Update/physics can run before XRI's deferred late detach: do not freeze then.
        bool directlyManipulated = entry.interacting || entry.releasePending ||
            (entry.interactable != null && entry.interactable.isSelected) ||
            (entry.scaler != null && entry.scaler.isScaling);
        if (directlyManipulated)
        {
            if (entry.owner != null && entry.interactable != null)
                entry.interactable.retainTransformParent = entry.originalRetainParent &&
                    entry.owner.isActiveAndEnabled && entry.owner.ContainsWorldPoint(entry.transform.position);
            return;
        }

        SyncFrozenPose(entry);
        VRHouseInteractionLock house = FindHouse(entry);
        if (house != entry.owner)
        {
            ReleaseOwner(entry, true);
            if (house != null)
                Adopt(entry, house);
        }
        else if (house != null && entry.state == ContentState.OutsideOrInteracting)
            BeginFalling(entry);
        if (entry.owner == null)
            return;

        EnsureParent(entry);
        if (entry.interactable != null)
            entry.interactable.retainTransformParent = entry.originalRetainParent;
        bool movingHouse = entry.owner == startingHouse || entry.owner.manipulating;
        if (entry.state == ContentState.Falling)
        {
            if (checkSleeping && entry.body.IsSleeping())
                Freeze(entry, ContentState.Settled);
            else if (movingHouse)
                Freeze(entry, ContentState.Paused);
        }
        else if (entry.state == ContentState.Paused && !movingHouse)
            BeginFalling(entry);
        SyncFrozenPose(entry);
    }

    static void Adopt(HouseItem entry, VRHouseInteractionLock house)
    {
        entry.owner = house;
        entry.hasHouseHistory = true;
        if (entry.preferredParent == null || !entry.preferredParent.IsChildOf(house.transform))
            entry.preferredParent = entry.transform.parent != null && entry.transform.parent.IsChildOf(house.transform)
                ? entry.transform.parent : house.transform;
        EnsureParent(entry);
        if (entry.interactable != null)
            entry.interactable.retainTransformParent = entry.originalRetainParent;
        BeginFalling(entry);
    }

    static void EnsureParent(HouseItem entry)
    {
        if (entry.owner == null || entry.transform.IsChildOf(entry.owner.transform))
            return;
        Transform parent = entry.preferredParent;
        if (parent == null || !parent.IsChildOf(entry.owner.transform))
            parent = entry.owner.transform;
        entry.transform.SetParent(parent, true);
    }

    static void BeginFalling(HouseItem entry)
    {
        RestorePhysics(entry);
        entry.state = ContentState.Falling;
        if (entry.body.isKinematic)
            Freeze(entry, ContentState.Settled); // Preserve intentionally kinematic props.
        else
            entry.body.WakeUp();
    }

    static void Freeze(HouseItem entry, ContentState state)
    {
        if (!entry.frozen)
        {
            entry.originalKinematic = entry.body.isKinematic;
            entry.originalInterpolation = entry.body.interpolation;
            entry.localPosition = entry.transform.localPosition;
            entry.localRotation = entry.transform.localRotation;
            entry.localScale = entry.transform.localScale;
            if (!entry.body.isKinematic)
            {
                entry.body.linearVelocity = Vector3.zero;
                entry.body.angularVelocity = Vector3.zero;
            }
            IgnoreHouseContacts(entry);
            entry.body.interpolation = RigidbodyInterpolation.None;
            entry.body.isKinematic = true;
            entry.frozen = true;
        }
        entry.state = state;
        SyncFrozenPose(entry);
    }

    static void SyncFrozenPose(HouseItem entry)
    {
        if (!entry.frozen || entry.transform == null || entry.body == null || entry.owner == null)
            return;
        entry.transform.SetLocalPositionAndRotation(entry.localPosition, entry.localRotation);
        entry.transform.localScale = entry.localScale;
        entry.body.position = entry.transform.position;
        entry.body.rotation = entry.transform.rotation;
    }

    static void IgnoreHouseContacts(HouseItem entry)
    {
        Rigidbody houseBody = entry.owner.GetComponent<Rigidbody>();
        if (houseBody == null)
            return;
        Collider[] houseColliders = entry.owner.GetComponentsInChildren<Collider>(true);
        foreach (Collider own in entry.body.GetComponentsInChildren<Collider>(true))
        {
            if (!PhysicalCollider(own, entry.body))
                continue;
            foreach (Collider other in houseColliders)
            {
                if (PhysicalCollider(other, houseBody) && !Physics.GetIgnoreCollision(own, other))
                {
                    Physics.IgnoreCollision(own, other);
                    entry.ignoredPairs.Add((own, other));
                }
            }
        }
    }

    static bool PhysicalCollider(Collider collider, Rigidbody body)
    {
        if (!collider.enabled || collider.isTrigger || !collider.gameObject.activeInHierarchy ||
            collider.attachedRigidbody != body)
            return false;
        return !(collider is MeshCollider mesh) ||
            (mesh.sharedMesh != null && mesh.sharedMesh.vertexCount > 0);
    }

    static void RestorePhysics(HouseItem entry)
    {
        if (!entry.frozen)
            return;
        entry.frozen = false;
        if (entry.body != null)
        {
            entry.body.isKinematic = entry.originalKinematic;
            entry.body.interpolation = entry.originalInterpolation;
            if (!entry.body.isKinematic)
            {
                entry.body.linearVelocity = Vector3.zero;
                entry.body.angularVelocity = Vector3.zero;
            }
        }
        foreach (var pair in entry.ignoredPairs)
        {
            if (pair.item != null && pair.house != null)
                Physics.IgnoreCollision(pair.item, pair.house, false);
        }
        entry.ignoredPairs.Clear();
    }

    static void ReleaseOwner(HouseItem entry, bool unparent)
    {
        VRHouseInteractionLock owner = entry.owner;
        RestorePhysics(entry);
        if (unparent && owner != null && entry.transform != null && entry.transform.IsChildOf(owner.transform))
            entry.transform.SetParent(null, true);
        entry.owner = null;
        entry.state = ContentState.OutsideOrInteracting;
        if (entry.interactable != null)
            entry.interactable.retainTransformParent = unparent && entry.hasHouseHistory
                ? false : entry.originalRetainParent;
    }

    bool FindTarget()
    {
        if (m_Interactable != null && m_Grab != null)
            return true;
        m_Interactable = GetComponent<XRGrabInteractable>();
        m_Grab = GetComponent<VRGrabObject>();
        return m_Interactable != null && m_Grab != null;
    }

    void OnDisable()
    {
        s_Houses.Remove(this);
        m_ReleasePending = false;
        foreach (HouseItem entry in s_Items)
        {
            if (entry.owner == this)
                ReleaseOwner(entry, false);
        }
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

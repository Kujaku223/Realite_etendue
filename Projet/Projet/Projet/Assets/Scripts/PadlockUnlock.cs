using UnityEngine;
using UnityEngine.Events;

/// <summary>A matching key releases this padlock and opens its assigned door.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public sealed class PadlockUnlock : MonoBehaviour
{
    const float k_SensorPadding = 0.005f;

    [SerializeField] string m_KeyTag = "Key";
    [SerializeField] Rigidbody m_DoorHinge;
    [SerializeField] float m_OpenAngle = 90f;
    [SerializeField, Min(0.01f)] float m_OpeningSpeed = 140f;

    [Header("Key size")]
    [Tooltip("Only a key of about this padlock's size opens it. Sizes are real world sizes, so a key that grew " +
        "with the house must be shrunk back. A key without VRTwoHandScale is not checked.")]
    [SerializeField] bool m_RequireKeySize = true;
    [Tooltip("Required key size divided by the padlock size (1 = same size, 0.5 = half). Each size is the " +
        "diagonal of the object's collider box.")]
    [SerializeField, Min(0.01f)] float m_RequiredKeySizeRatio = 1f;
    [Tooltip("Accepted gap around the required size (0.2 = ±20 %).")]
    [SerializeField, Range(0.01f, 0.5f)] float m_KeySizeTolerance = 0.2f;

    public UnityEvent onUnlocked = new UnityEvent();

    Rigidbody m_Body;
    BoxCollider m_Sensor;
    Quaternion m_ClosedLocalRotation;
    float m_CurrentAngle;
    bool m_Unlocked;
    bool m_DoorOpened;
    AudioSource m_Creak;
    Vector3 m_LocalSize;          // padlock collider box, in padlock space
    Rigidbody m_MeasuredKey;
    Vector3 m_KeyLocalSize;       // key collider box, in key space (does not change when the key is resized)

    void Awake()
    {
        m_Body = GetComponent<Rigidbody>();
        m_Sensor = GetComponent<BoxCollider>();
        m_Sensor.isTrigger = true;
        FitSensor();

        m_Body.isKinematic = true;
        m_Body.useGravity = false;
        if (m_DoorHinge == null)
        {
            Debug.LogWarning("PadlockUnlock needs a Door Hinge Rigidbody. This padlock will stay locked.", this);
            return;
        }

        m_ClosedLocalRotation = m_DoorHinge.transform.localRotation;
    }

    void OnValidate() => m_OpeningSpeed = Mathf.Max(0.01f, m_OpeningSpeed);

    void OnTriggerEnter(Collider other) => TryUnlock(other);
    void OnTriggerStay(Collider other) => TryUnlock(other);

    void TryUnlock(Collider other)
    {
        if (!isActiveAndEnabled || m_Unlocked || m_DoorHinge == null)
            return;

        // Child colliders belong to the key's Rigidbody, even while XRI carries it.
        Rigidbody keyBody = other.attachedRigidbody;
        if (keyBody == null || !keyBody.CompareTag(m_KeyTag) || !KeyHasRequiredSize(keyBody))
            return;

        m_Unlocked = true;
        m_Sensor.enabled = false;
        transform.SetParent(null, true);
        m_Body.isKinematic = false;
        m_Body.useGravity = true;
        m_Body.linearVelocity = Vector3.zero;
        m_Body.angularVelocity = Vector3.zero;
        m_Body.WakeUp();
        VRDoorAudio.Play(ref m_Creak, m_DoorHinge.gameObject);
        onUnlocked.Invoke();
    }

    bool KeyHasRequiredSize(Rigidbody keyBody)
    {
        if (!m_RequireKeySize || !keyBody.TryGetComponent<VRTwoHandScale>(out var scaler))
            return true;
        // Sweeping through the right size while resizing does not count: the key must be set first.
        if (scaler.isScaling)
            return false;

        if (m_MeasuredKey != keyBody)
        {
            m_MeasuredKey = null;
            m_KeyLocalSize = Vector3.zero;
            Bounds bounds = default;
            bool hasBounds = false;
            foreach (var collider in keyBody.GetComponentsInChildren<Collider>())
            {
                if (collider.enabled && !collider.isTrigger && collider.attachedRigidbody == keyBody &&
                    TryGetShapeBounds(collider, out Bounds shape))
                    Encapsulate(ref bounds, ref hasBounds, shape, keyBody.transform, collider.transform);
            }
            if (hasBounds)
            {
                m_MeasuredKey = keyBody;
                m_KeyLocalSize = bounds.size;
            }
        }

        // Real world sizes: the key grows with the house, the padlock outside does not.
        float keySize = WorldSize(m_KeyLocalSize, keyBody.transform);
        float padlockSize = WorldSize(m_LocalSize, transform);
        if (keySize <= 0f || padlockSize <= 0f)
            return true;   // nothing to measure: behave as before
        return Mathf.Abs(keySize / (padlockSize * m_RequiredKeySizeRatio) - 1f) <= m_KeySizeTolerance;
    }

    static float WorldSize(Vector3 localSize, Transform space)
    {
        Vector3 scale = space.lossyScale;
        return Vector3.Scale(localSize, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z))).magnitude;
    }

    // Collider box in its own transform space. Unlike Collider.bounds, it does not depend on rotation.
    static bool TryGetShapeBounds(Collider collider, out Bounds bounds)
    {
        switch (collider)
        {
            case BoxCollider box:
                bounds = new Bounds(box.center, box.size);
                return true;
            case MeshCollider mesh when mesh.sharedMesh != null:
                bounds = mesh.sharedMesh.bounds;
                return true;
            case SphereCollider sphere:
                bounds = new Bounds(sphere.center, Vector3.one * (2f * sphere.radius));
                return true;
            case CapsuleCollider capsule:
                Vector3 size = Vector3.one * (2f * capsule.radius);
                size[capsule.direction] = Mathf.Max(capsule.height, 2f * capsule.radius);
                bounds = new Bounds(capsule.center, size);
                return true;
            default:
                bounds = default;
                return false;
        }
    }

    static void Encapsulate(ref Bounds bounds, ref bool hasBounds, Bounds shape, Transform root, Transform shapeSpace)
    {
        Matrix4x4 toRoot = root.worldToLocalMatrix * shapeSpace.localToWorldMatrix;
        for (int i = 0; i < 8; ++i)
        {
            Vector3 corner = shape.center + Vector3.Scale(shape.extents,
                new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
            Vector3 point = toRoot.MultiplyPoint3x4(corner);
            if (!hasBounds)
            {
                bounds = new Bounds(point, Vector3.zero);
                hasBounds = true;
            }
            else
                bounds.Encapsulate(point);
        }
    }

    void FixedUpdate()
    {
        // Held bodies deliberately have no physics contacts. Test their shapes
        // directly so a held key can still touch this sensor without pushing it.
        if (!m_Unlocked && m_DoorHinge != null && m_Sensor.enabled)
        {
            foreach (var held in VRHitPointGrabInteractable.HeldWithoutCollisions)
            {
                if (held == null || !held.CompareTag(m_KeyTag)) continue;
                foreach (var collider in held.colliders)
                {
                    if (collider == null || !collider.enabled || collider.isTrigger ||
                        !collider.gameObject.activeInHierarchy) continue;
                    if (Physics.ComputePenetration(m_Sensor, m_Sensor.transform.position,
                        m_Sensor.transform.rotation, collider, collider.transform.position,
                        collider.transform.rotation, out _, out _))
                    {
                        TryUnlock(collider);
                        break;
                    }
                }
                if (m_Unlocked) break;
            }
        }
        if (!m_Unlocked || m_DoorOpened || m_DoorHinge == null)
            return;

        m_CurrentAngle = Mathf.MoveTowards(m_CurrentAngle, m_OpenAngle,
            m_OpeningSpeed * Time.fixedDeltaTime);
        Quaternion localRotation = m_ClosedLocalRotation * Quaternion.Euler(0f, m_CurrentAngle, 0f);
        Transform parent = m_DoorHinge.transform.parent;
        m_DoorHinge.MoveRotation(parent != null ? parent.rotation * localRotation : localRotation);
        m_DoorOpened = Mathf.Approximately(m_CurrentAngle, m_OpenAngle);
    }

    void OnDisable() => VRDoorAudio.Stop(m_Creak);
    void OnDestroy() => VRDoorAudio.Release(m_Creak);

    void FitSensor()
    {
        // Use mesh bounds in padlock-local space, including the shackle's offset.
        // Mesh.bounds is available even when mesh Read/Write is disabled.
        Bounds bounds = default;
        bool hasBounds = false;
        foreach (var collider in GetComponentsInChildren<MeshCollider>(true))
        {
            if (!collider.enabled || collider.sharedMesh == null || collider.attachedRigidbody != m_Body)
                continue;
            Encapsulate(ref bounds, ref hasBounds, collider.sharedMesh.bounds, transform, collider.transform);
        }

        if (!hasBounds)
        {
            m_LocalSize = m_Sensor.size;
            return;
        }

        m_LocalSize = bounds.size;
        Vector3 scale = transform.lossyScale;
        Vector3 padding = new Vector3(
            k_SensorPadding / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
            k_SensorPadding / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
            k_SensorPadding / Mathf.Max(0.0001f, Mathf.Abs(scale.z)));
        m_Sensor.center = bounds.center;
        m_Sensor.size = bounds.size + 2f * padding;
    }
}

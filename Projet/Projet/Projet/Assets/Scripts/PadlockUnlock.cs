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

    public UnityEvent onUnlocked = new UnityEvent();

    Rigidbody m_Body;
    BoxCollider m_Sensor;
    Quaternion m_ClosedLocalRotation;
    float m_CurrentAngle;
    bool m_Unlocked;
    bool m_DoorOpened;

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
        if (keyBody == null || !keyBody.CompareTag(m_KeyTag))
            return;

        m_Unlocked = true;
        m_Sensor.enabled = false;
        transform.SetParent(null, true);
        m_Body.isKinematic = false;
        m_Body.useGravity = true;
        m_Body.linearVelocity = Vector3.zero;
        m_Body.angularVelocity = Vector3.zero;
        m_Body.WakeUp();
        onUnlocked.Invoke();
    }

    void FixedUpdate()
    {
        if (!m_Unlocked || m_DoorOpened || m_DoorHinge == null)
            return;

        m_CurrentAngle = Mathf.MoveTowards(m_CurrentAngle, m_OpenAngle,
            m_OpeningSpeed * Time.fixedDeltaTime);
        Quaternion localRotation = m_ClosedLocalRotation * Quaternion.Euler(0f, m_CurrentAngle, 0f);
        Transform parent = m_DoorHinge.transform.parent;
        m_DoorHinge.MoveRotation(parent != null ? parent.rotation * localRotation : localRotation);
        m_DoorOpened = Mathf.Approximately(m_CurrentAngle, m_OpenAngle);
    }

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

            Bounds meshBounds = collider.sharedMesh.bounds;
            Matrix4x4 toRoot = transform.worldToLocalMatrix * collider.transform.localToWorldMatrix;
            for (int i = 0; i < 8; ++i)
            {
                Vector3 corner = meshBounds.center + Vector3.Scale(meshBounds.extents,
                    new Vector3((i & 1) == 0 ? -1f : 1f,
                        (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
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

        if (!hasBounds)
            return;

        Vector3 scale = transform.lossyScale;
        Vector3 padding = new Vector3(
            k_SensorPadding / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
            k_SensorPadding / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
            k_SensorPadding / Mathf.Max(0.0001f, Mathf.Abs(scale.z)));
        m_Sensor.center = bounds.center;
        m_Sensor.size = bounds.size + 2f * padding;
    }
}

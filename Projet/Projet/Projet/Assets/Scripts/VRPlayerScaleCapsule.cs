using Unity.XR.CoreUtils;
using UnityEngine;

/// <summary>A headset-following body capsule used to prevent object growth into the player.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CapsuleCollider))]
public sealed class VRPlayerScaleCapsule : MonoBehaviour
{
    [SerializeField, Min(0.01f)]
    [Tooltip("Player body radius in rig-local metres.")]
    float m_Radius = 0.25f;

    CapsuleCollider m_Capsule;
    XROrigin m_Origin;

    public CapsuleCollider capsule => m_Capsule;

    void Awake()
    {
        m_Capsule = GetComponent<CapsuleCollider>();
        m_Origin = GetComponentInParent<XROrigin>();
        m_Capsule.isTrigger = true;
        m_Capsule.direction = 1;
        UpdateShape();
    }

    void LateUpdate() => UpdateShape();

    void OnValidate() => m_Radius = Mathf.Max(0.01f, m_Radius);

    public void UpdateShape()
    {
        if (m_Capsule == null || m_Origin == null || m_Origin.Camera == null)
            return;

        // This child is at the rig's floor origin. Room-scale movement changes
        // its horizontal centre; crouching changes the capsule's height.
        Vector3 head = transform.InverseTransformPoint(m_Origin.Camera.transform.position);
        float height = Mathf.Max(2f * m_Radius, head.y);
        m_Capsule.radius = m_Radius;
        m_Capsule.height = height;
        m_Capsule.center = new Vector3(head.x, height * 0.5f, head.z);
    }
}

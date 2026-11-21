using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Aligns the actual tracked head to the doorway's spawn anchor once per arrival.</summary>
[DisallowMultipleComponent]
public sealed class XRDoorwaySpawn : MonoBehaviour
{
    [SerializeField] Transform m_SpawnPoint;
    bool m_Aligned;

    IEnumerator Start()
    {
        yield return null;
        Align();
    }

    public static void AlignScene(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var spawn in root.GetComponentsInChildren<XRDoorwaySpawn>())
                spawn.Align();
    }

    void Align()
    {
        if (m_Aligned || m_SpawnPoint == null || Camera.main == null) return;
        var origin = Camera.main.GetComponentInParent<XROrigin>();
        if (origin == null || origin.gameObject.scene != gameObject.scene) return;
        Vector3 forward = Vector3.ProjectOnPlane(m_SpawnPoint.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.001f) return;
        var body = origin.GetComponentInChildren<CharacterController>();
        bool bodyEnabled = body != null && body.enabled;
        if (bodyEnabled) body.enabled = false;
        try
        {
            float height = Vector3.Dot(Camera.main.transform.position - origin.transform.position, Vector3.up);
            if (!origin.MatchOriginUpCameraForward(Vector3.up, forward)) return;
            m_Aligned = origin.MoveCameraToWorldLocation(m_SpawnPoint.position + Vector3.up * height);
        }
        finally
        {
            if (bodyEnabled && body != null) body.enabled = true;
        }
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

/// <summary>
/// Fait apparaître le vrai objet exactement à la place du cube fantôme, puis efface la projection.
/// À brancher sur : AnamorphicAlignmentDetector.onAligned → SpawnOnAligned.Spawn()
/// </summary>
public class SpawnOnAligned : MonoBehaviour
{
    [Header("Objet")]
    [Tooltip("Prefab du vrai cube (ex. cube bleu + Rigidbody + XRGrabInteractable).")]
    [SerializeField] GameObject prefab;
    [Tooltip("Pose d'apparition : la racine du cube fantôme.")]
    [SerializeField] Transform spawnPose;
    [Tooltip("Copie l'échelle monde du fantôme, pour que l'objet le recouvre exactement.")]
    [SerializeField] bool matchGhostScale = true;
    [SerializeField] Transform parent;

    [Header("Transition")]
    [SerializeField] AnamorphicProjector projector;
    [SerializeField] float fadeDuration = 0.4f;
    [Tooltip("Petit « pop » d'échelle à l'apparition (0 = aucun).")]
    [SerializeField] float punchScale = 0.08f;
    [SerializeField] float punchDuration = 0.25f;

    [Header("Retours")]
    [SerializeField] AudioClip spawnSound;
    [Range(0f, 1f)] [SerializeField] float soundVolume = 1f;
    [Range(0f, 1f)] [SerializeField] float hapticAmplitude = 0.5f;
    [SerializeField] float hapticDuration = 0.15f;

    public UnityEvent<GameObject> onSpawned = new UnityEvent<GameObject>();

    public GameObject SpawnedInstance { get; private set; }

    public void Spawn()
    {
        if (SpawnedInstance != null) return;
        if (prefab == null || spawnPose == null)
        {
            Debug.LogWarning($"{name} : prefab ou spawnPose manquant.", this);
            return;
        }

        SpawnedInstance = Instantiate(prefab, spawnPose.position, spawnPose.rotation, parent);
        if (matchGhostScale) SetWorldScale(SpawnedInstance.transform, spawnPose.lossyScale);

        if (spawnSound != null)
            AudioSource.PlayClipAtPoint(spawnSound, spawnPose.position, soundVolume);
        SendHaptics();

        StartCoroutine(Transition(SpawnedInstance.transform));
        onSpawned?.Invoke(SpawnedInstance);
    }

    IEnumerator Transition(Transform obj)
    {
        Vector3 baseScale = obj.localScale;
        float startOpacity = projector != null ? projector.Opacity : 0f;
        float duration = Mathf.Max(fadeDuration, punchDuration);

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            if (projector != null && fadeDuration > 0f)
                projector.Opacity = Mathf.Lerp(startOpacity, 0f, t / fadeDuration);

            if (obj != null && punchScale > 0f && t < punchDuration)
                obj.localScale = baseScale * (1f + punchScale * Mathf.Sin(Mathf.PI * t / punchDuration));

            yield return null;
        }

        if (obj != null) obj.localScale = baseScale;
        if (projector != null) projector.enabled = false; // OnDisable remet l'opacité à 0
    }

    void SendHaptics()
    {
        if (hapticAmplitude <= 0f || hapticDuration <= 0f) return;
        // Les manettes du XR Origin (XRI Starter Assets) portent un HapticImpulsePlayer
        foreach (var player in FindObjectsByType<HapticImpulsePlayer>(FindObjectsSortMode.None))
            player.SendHapticImpulse(hapticAmplitude, hapticDuration);
    }

    static void SetWorldScale(Transform t, Vector3 worldScale)
    {
        Vector3 p = t.parent != null ? t.parent.lossyScale : Vector3.one;
        t.localScale = new Vector3(worldScale.x / p.x, worldScale.y / p.y, worldScale.z / p.z);
    }
}

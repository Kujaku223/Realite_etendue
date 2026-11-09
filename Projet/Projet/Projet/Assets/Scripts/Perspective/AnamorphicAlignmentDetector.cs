using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Détecte quand la tête du joueur est au point de vue P ET regarde la cible,
/// pendant un temps de maintien donné.
/// </summary>
public class AnamorphicAlignmentDetector : MonoBehaviour
{
    [Header("Références")]
    [Tooltip("Tête du joueur. Vide = Camera.main (Main Camera du XR Origin).")]
    [SerializeField] Transform head;
    [Tooltip("Point de vue P. Vide = ce transform (pratique si placé sur le projecteur).")]
    [SerializeField] Transform viewpoint;
    [Tooltip("Centre de l'objet à faire apparaître (le cube fantôme).")]
    [SerializeField] Transform target;

    [Header("Tolérances")]
    [Tooltip("Écart horizontal max entre la tête et P (m).")]
    [SerializeField] float horizontalTolerance = 0.12f;
    [Tooltip("Écart vertical max (m). Plus large pour s'adapter à la taille du joueur.")]
    [SerializeField] float verticalTolerance = 0.20f;
    [Tooltip("Angle max entre le regard et la direction de la cible (degrés).")]
    [SerializeField] float angleTolerance = 12f;
    [Tooltip("Durée pendant laquelle l'alignement doit être tenu (s).")]
    [SerializeField] float dwellTime = 0.75f;
    [Tooltip("Vitesse à laquelle le minuteur redescend quand l'alignement est perdu.")]
    [SerializeField] float decaySpeed = 2f;
    [SerializeField] bool triggerOnce = true;

    [Header("Événements")]
    public UnityEvent onAligned = new UnityEvent();
    [Tooltip("Progression du maintien, de 0 à 1 (pour un retour visuel ou sonore).")]
    public UnityEvent<float> onDwellProgress = new UnityEvent<float>();

    public bool IsAligned { get; private set; }
    /// <summary>1 = parfaitement aligné, 0 = à 3× la tolérance ou plus. Utile pour du feedback continu.</summary>
    public float Alignment01 { get; private set; }

    float _timer;
    bool _triggered;

    /// <summary>
    /// Erreurs normalisées : x = position (ellipsoïde autour de P), y = angle du regard.
    /// Une valeur ≤ 1 signifie « dans la tolérance ».
    /// </summary>
    public static Vector2 ComputeErrors(
        Vector3 headPosition, Vector3 headForward, Vector3 viewpointPosition, Vector3 targetPosition,
        float horizontalTolerance, float verticalTolerance, float angleTolerance)
    {
        Vector3 d = headPosition - viewpointPosition;
        float h = new Vector2(d.x, d.z).magnitude / horizontalTolerance;
        float v = Mathf.Abs(d.y) / verticalTolerance;
        float positionError = Mathf.Sqrt(h * h + v * v);

        float angle = Vector3.Angle(headForward, targetPosition - headPosition);
        float angleError = angle / angleTolerance;

        return new Vector2(positionError, angleError);
    }

    void Awake()
    {
        if (viewpoint == null) viewpoint = transform;
    }

    void Update()
    {
        if (_triggered && triggerOnce) return;
        if (target == null) return;
        if (head == null)
        {
            var cam = Camera.main;
            if (cam == null) return;
            head = cam.transform;
        }

        // 1) Position : la tête est dans un ellipsoïde autour de P (plus tolérant en hauteur)
        // 2) Orientation : le regard pointe vers la cible
        Vector2 errors = ComputeErrors(head.position, head.forward, viewpoint.position, target.position,
                                       horizontalTolerance, verticalTolerance, angleTolerance);

        IsAligned = errors.x <= 1f && errors.y <= 1f;
        Alignment01 = Mathf.InverseLerp(3f, 0f, Mathf.Max(errors.x, errors.y));

        // Mode répétable : on réarme seulement quand le joueur quitte la zone
        if (_triggered)
        {
            if (!IsAligned) _triggered = false;
            return;
        }

        // 3) Maintien du regard
        float previous = _timer;
        _timer = IsAligned
            ? _timer + Time.deltaTime
            : Mathf.Max(0f, _timer - Time.deltaTime * decaySpeed);

        if (_timer != previous)
            onDwellProgress?.Invoke(Mathf.Clamp01(_timer / dwellTime));

        if (_timer >= dwellTime)
        {
            _triggered = true;
            _timer = 0f;
            onAligned?.Invoke();
        }
    }

    public void ResetDetector()
    {
        _triggered = false;
        _timer = 0f;
    }

    void OnDrawGizmosSelected()
    {
        Transform p = viewpoint != null ? viewpoint : transform;
        Gizmos.color = Application.isPlaying && IsAligned ? Color.green : Color.yellow;
        Gizmos.matrix = Matrix4x4.TRS(p.position, Quaternion.identity,
            new Vector3(horizontalTolerance, verticalTolerance, horizontalTolerance) * 2f);
        Gizmos.DrawWireSphere(Vector3.zero, 0.5f);
        Gizmos.matrix = Matrix4x4.identity;
        if (target != null) Gizmos.DrawLine(p.position, target.position);
    }
}

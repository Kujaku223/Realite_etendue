using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

/// <summary>
/// Détecte quand la tête du joueur est au point de vue P ET regarde la cible,
/// pendant un temps de maintien donné.
/// Pensé pour un joueur en casque : la zone est tolérante, l'alignement ne se perd pas au moindre
/// tremblement, et une vibration lui indique qu'il est au bon endroit. P ne bouge jamais.
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
    [SerializeField] float horizontalTolerance = 0.25f;
    [Tooltip("Écart vertical max entre la tête et P (m).")]
    [SerializeField] float verticalTolerance = 0.30f;
    [Tooltip("Angle max entre l'avant du casque et la cible (degrés). En VR on vise avec les yeux : rester large.")]
    [SerializeField] float angleTolerance = 30f;
    [Tooltip("Une fois aligné, on ne perd l'alignement qu'au-delà de tolérance × ce facteur (évite les clignotements).")]
    [SerializeField] float exitToleranceScale = 1.3f;
    [Tooltip("Durée pendant laquelle l'alignement doit être tenu (s).")]
    [SerializeField] float dwellTime = 0.5f;
    [Tooltip("Vitesse à laquelle le minuteur redescend quand l'alignement est perdu.")]
    [SerializeField] float decaySpeed = 2f;
    [SerializeField] bool triggerOnce = true;

    [Header("Aide au joueur")]
    [Tooltip("Optionnel : repère posé automatiquement au sol sous P (traces de pas, cercle…).")]
    [SerializeField] Transform floorMarker;
    [Tooltip("Petite vibration des manettes quand le joueur entre dans la zone (0 = aucune).")]
    [Range(0f, 1f)] [SerializeField] float enterHapticAmplitude = 0.2f;

    [Header("Événements")]
    public UnityEvent onAligned = new UnityEvent();
    [Tooltip("Progression du maintien, de 0 à 1 (pour un retour visuel ou sonore).")]
    public UnityEvent<float> onDwellProgress = new UnityEvent<float>();
    [Tooltip("Le joueur vient d'entrer dans la zone (il doit maintenant tenir sa position).")]
    public UnityEvent onZoneEntered = new UnityEvent();
    [Tooltip("Le joueur vient de sortir de la zone.")]
    public UnityEvent onZoneExited = new UnityEvent();

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

    void Start()
    {
        if (!Physics.Raycast(viewpoint.position, Vector3.down, out RaycastHit floor, 20f, ~0, QueryTriggerInteraction.Ignore))
            return;

        // Les yeux d'un joueur debout sont entre ~1,4 et ~1,9 m ; assis, vers 1,1–1,3 m.
        // Le rayon s'arrête sur la première surface sous P (sol, ou table / plafond si P est mal placé).
        float eyeHeight = viewpoint.position.y - floor.point.y;
        if (eyeHeight - verticalTolerance > 1.9f || eyeHeight + verticalTolerance < 1.1f)
            Debug.LogWarning($"{name} : P est à {eyeHeight:0.00} m au-dessus de la surface en dessous ({floor.collider.name}). " +
                             "Aucun joueur ne pourra avoir les yeux à cette hauteur : place P vers 1,6 m au-dessus du sol.", this);

        if (floorMarker != null)
            floorMarker.position = floor.point + floor.normal * 0.005f;
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

        // 1) Position : la tête est dans un ellipsoïde autour de P
        // 2) Orientation : le casque est à peu près tourné vers la cible
        Vector2 errors = ComputeErrors(head.position, head.forward, viewpoint.position, target.position,
                                       horizontalTolerance, verticalTolerance, angleTolerance);

        // Hystérésis : plus facile de rester dans la zone que d'y entrer
        bool wasAligned = IsAligned;
        float limit = wasAligned ? exitToleranceScale : 1f;
        IsAligned = errors.x <= limit && errors.y <= limit;
        Alignment01 = Mathf.InverseLerp(3f, 0f, Mathf.Max(errors.x, errors.y));

        if (IsAligned && !wasAligned)
        {
            onZoneEntered?.Invoke();
            if (!_triggered) SendHaptics(enterHapticAmplitude, 0.05f);
        }
        else if (!IsAligned && wasAligned)
        {
            onZoneExited?.Invoke();
        }

        // Mode répétable : on réarme seulement quand le joueur quitte la zone
        if (_triggered)
        {
            if (!IsAligned) _triggered = false;
            return;
        }

        // 3) Maintien
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

    static void SendHaptics(float amplitude, float duration)
    {
        if (amplitude <= 0f) return;
        foreach (var player in FindObjectsByType<HapticImpulsePlayer>(FindObjectsSortMode.None))
            player.SendHapticImpulse(amplitude, duration);
    }

    public void ResetDetector()
    {
        _triggered = false;
        _timer = 0f;
    }

    void OnDrawGizmosSelected()
    {
        Transform p = viewpoint != null ? viewpoint : transform;

        // Zone de déclenchement (verte quand le joueur est aligné)
        Gizmos.color = Application.isPlaying && IsAligned ? Color.green : Color.yellow;
        Gizmos.matrix = Matrix4x4.TRS(p.position, Quaternion.identity,
            new Vector3(horizontalTolerance, verticalTolerance, horizontalTolerance) * 2f);
        Gizmos.DrawWireSphere(Vector3.zero, 0.5f);

        Gizmos.matrix = Matrix4x4.identity;
        if (target != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(p.position, target.position);
        }
    }
}

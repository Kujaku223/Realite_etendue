using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// Put this on a pad with a trigger collider. Only the assigned GameObject is weighed.
/// The pad compresses as the weight approaches the target and changes colour: empty / too light / correct / too heavy.
public class WeighingScale : MonoBehaviour
{
    [Header("Challenge")]
    [SerializeField] GameObject requiredObject; // the ONE object this scale accepts
    [SerializeField] float targetWeight = 10f;
    [SerializeField] float tolerance = 0.5f;    // +/- allowed error
    [SerializeField] float holdTime = 1f;       // must stay correct this long
    [SerializeField] float exitGraceTime = 0.25f; // ignore trigger exits shorter than this (stops flicker)
    [SerializeField] bool scaleAffectsWeight = true;

    [Header("Feedback")]
    [SerializeField] Renderer indicator;        // lamp / pad surface
    [SerializeField] Transform needle;          // optional dial needle
    [SerializeField] float needleMaxAngle = 60f;
    [SerializeField] float wrongFlashTime = 0.5f;
    [SerializeField] float smoothing = 8f;      // higher = snappier

    [Header("Colors")]
    [SerializeField] Color empty = new Color32(0x13, 0x28, 0x2F, 255); // #13282F (nothing on the scale)
    [SerializeField] Color tooLight = new Color32(0x8E, 0xBD, 0xCD, 255); // #8EBDCD
    [SerializeField] Color correct = new Color32(0x1E, 0x2D, 0x00, 255); // #1E2D00
    [SerializeField] Color tooHeavy = new Color32(0x2F, 0x63, 0x74, 255); // #2F6374
    [SerializeField] Color wrongFlash = new Color32(0xBC, 0x23, 0x23, 255); // #BC2323 (wrong object)

    [Header("Compression")]
    [SerializeField] Transform pad;             // the visual cube to squash (leave empty to disable)
    [Range(0f, 0.9f)]
    [SerializeField] float maxCompression = 0.5f;      // fraction of height lost at full travel
    [Range(0.1f, 0.95f)]
    [SerializeField] float correctDepth = 0.6667f;     // how far down (0-1 of full travel) the correct weight sits

    [Header("Events")]
    public UnityEvent onSolved;
    public UnityEvent onUnsolved;
    public UnityEvent<GameObject> onWrongObject; // fires when a different object lands on the pad
    public UnityEvent<float> onWeightChanged;    // hook up a UI text here
    public bool IsSolved => solved;              // used to compare states of the 3 scales

    Rigidbody requiredBody;
    Transform weightReferenceFrame;
    float initialRelativeVolume;
    bool onPad;
    int overlaps;                                // colliders of the required object inside the trigger
    float exitTime = -999f;
    bool solved;
    float correctTimer;
    float flashTimer;
    float shownWeight;                           // smoothed weight used for the compression
    float lastHeightFactor = -1f;
    Vector3 padBaseScale;
    Vector3 padBasePos;

    float RelativeVolume()
    {
        float volume = Mathf.Abs(requiredObject.transform.localToWorldMatrix.determinant);
        float frameVolume = weightReferenceFrame != null ?
            Mathf.Abs(weightReferenceFrame.localToWorldMatrix.determinant) : 1f;
        return volume / Mathf.Max(1e-12f, frameVolume);
    }

    // Authored Rigidbody.mass is the weight at the authored size. Measure in
    // house space so resizing Maison itself does not change its puzzle targets.
    float GetWeight() => requiredBody == null ? 0f : requiredBody.mass *
        (scaleAffectsWeight && initialRelativeVolume > 0f ? RelativeVolume() / initialRelativeVolume : 1f);

    void Awake()
    {
        if (requiredObject != null)
        {
            requiredBody = requiredObject.GetComponentInChildren<Rigidbody>();
            var house = GetComponentInParent<VRHouseInteractionLock>();
            weightReferenceFrame = house != null ? house.transform : transform.root;
            initialRelativeVolume = RelativeVolume();
        }

        if (pad != null)
        {
            padBaseScale = pad.localScale;
            padBasePos = pad.localPosition;
        }
    }

    bool IsRequired(Collider other) =>
        requiredObject != null && other.transform.IsChildOf(requiredObject.transform);

    void OnTriggerEnter(Collider other)
    {
        if (other.transform.IsChildOf(transform)) return; // ignore this scale's own parts

        if (IsRequired(other)) { overlaps++; return; }

        // Only react to physical props, not the player, floor, etc.
        var rb = other.attachedRigidbody;
        if (rb != null)
        {
            flashTimer = wrongFlashTime;
            onWrongObject?.Invoke(rb.gameObject);
            PlayWrongObjectSound(rb);
        }
    }

    const float wrongSoundCooldown = 1f;   // an object with several colliders enters several times
    float nextWrongSoundTime;

    void PlayWrongObjectSound(Rigidbody rb)
    {
        // Only grabbable props, not the house that contains this scale (its walls and table also have its body).
        if (transform.IsChildOf(rb.transform) || rb.GetComponent<XRBaseInteractable>() == null) return;
        if (Time.time < nextWrongSoundTime) return;
        var profile = VRFeedbackProfile.Default;
        if (profile == null || profile.wrongObjectClip == null) return;
        // Never overlap the clip with itself, even when it is longer than the cooldown.
        nextWrongSoundTime = Time.time + Mathf.Max(wrongSoundCooldown, profile.wrongObjectClip.length);
        AudioSource.PlayClipAtPoint(profile.wrongObjectClip, transform.position, profile.wrongObjectVolume);
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsRequired(other)) return;
        overlaps = Mathf.Max(0, overlaps - 1);
        if (overlaps == 0) exitTime = Time.time;
    }

    void Update()
    {
        if (requiredObject == null) return;

        // Stay "on the pad" briefly after the trigger reports an exit, so physics jitter can't flip the state
        onPad = overlaps > 0 || Time.time - exitTime < exitGraceTime;

        float weight = onPad ? GetWeight() : 0f;
        onWeightChanged?.Invoke(weight);

        float error = weight - targetWeight;
        bool isCorrect = onPad && Mathf.Abs(error) <= tolerance;

        // Smoothly follow the real weight so the pad animates instead of snapping
        shownWeight = Mathf.Lerp(shownWeight, weight, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
        if (Mathf.Abs(shownWeight - weight) < 0.01f) shownWeight = weight; // settle fully, stop micro-movement

        // ---- Colour: empty / too light / correct / too heavy ----
        Color c = !onPad ? empty
                : isCorrect ? correct
                : error > 0f ? tooHeavy
                : tooLight;

        // Wrong object overrides the colour briefly
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            c = wrongFlash;
        }
        if (indicator) indicator.material.color = c;

        // ---- Compression: pad gets shorter, bottom edge stays put ----
        if (pad != null)
        {
            float ratio = shownWeight / Mathf.Max(0.0001f, targetWeight); // 1 = at target
            // ratio 1 (correct weight) always lands at correctDepth; heavier goes deeper, lighter stays higher
            float depth = Mathf.Clamp01(ratio * correctDepth);
            float heightFactor = 1f - maxCompression * depth;

            // Only touch the transform when the value actually changed (moving colliders every frame causes jitter)
            if (!Mathf.Approximately(heightFactor, lastHeightFactor))
            {
                lastHeightFactor = heightFactor;
                pad.localScale = new Vector3(padBaseScale.x, padBaseScale.y * heightFactor, padBaseScale.z);
                // Assumes a centre-pivot mesh (default Unity cube); lowers the centre so the bottom stays fixed
                pad.localPosition = padBasePos + Vector3.down * (padBaseScale.y * (1f - heightFactor) * 0.5f);
            }
        }

        // ---- Needle ----
        if (needle)
        {
            float t = onPad ? Mathf.Clamp(error / targetWeight, -1f, 1f) : -1f;
            Quaternion goal = Quaternion.Euler(0, 0, -t * needleMaxAngle);
            needle.localRotation = Quaternion.Slerp(needle.localRotation, goal, Time.deltaTime * 8f);
        }

        // ---- Solved logic ----
        if (solved && !isCorrect)
        {
            solved = false;
            onUnsolved?.Invoke();
        }

        correctTimer = isCorrect ? correctTimer + Time.deltaTime : 0f;
        if (!solved && correctTimer >= holdTime)
        {
            solved = true;
            onSolved?.Invoke();
        }
    }
}

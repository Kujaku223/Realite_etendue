using UnityEngine;
using UnityEngine.Events;

/// Put this on a pad with a trigger collider. Only the assigned GameObject is weighed.
/// Weight is read from the object's Rigidbody mass, so no custom class is needed.
public class WeighingScale : MonoBehaviour
{
    [Header("Challenge")]
    [SerializeField] GameObject requiredObject; // the ONE object this scale accepts
    [SerializeField] float targetWeight = 10f;
    [SerializeField] float tolerance = 0.5f;    // +/- allowed error
    [SerializeField] float holdTime = 1f;       // must stay correct this long

    [Header("Feedback")]
    [SerializeField] Renderer indicator;        // lamp / pad surface
    [SerializeField] Transform needle;          // optional dial needle
    [SerializeField] float needleMaxAngle = 60f;
    [SerializeField] Color tooLight = new Color(0.2f, 0.5f, 1f);
    [SerializeField] Color correct = new Color(0.2f, 0.9f, 0.3f);
    [SerializeField] Color tooHeavy = new Color(1f, 0.25f, 0.2f);
    [SerializeField] Color empty = Color.gray;
    [SerializeField] Color wrongFlash = Color.magenta;
    [SerializeField] float wrongFlashTime = 0.5f;

    [Header("Events")]
    public UnityEvent onSolved;
    public UnityEvent<GameObject> onWrongObject; // fires when a different object lands on the pad
    public UnityEvent<float> onWeightChanged;    // hook up a UI text here
    public bool IsSolved => solved; //saves the status, to compare states of the 3 scales

    Rigidbody requiredBody;
    bool onPad;
    bool solved;
    float correctTimer;
    float flashTimer;

    // Override this if your project stores weight somewhere other than Rigidbody.mass
    float GetWeight() => requiredBody != null ? requiredBody.mass : 0f;

    void Awake()
    {
        if (requiredObject != null)
            requiredBody = requiredObject.GetComponentInChildren<Rigidbody>();
    }

    bool IsRequired(Collider other) =>
        requiredObject != null && other.transform.IsChildOf(requiredObject.transform);

    void OnTriggerEnter(Collider other)
    {
        if (IsRequired(other)) { onPad = true; return; }

        // Only react to physical props, not the player, floor, etc.
        var rb = other.attachedRigidbody;
        if (rb != null)
        {
            flashTimer = wrongFlashTime;
            onWrongObject?.Invoke(rb.gameObject);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (IsRequired(other)) onPad = false;
    }

    void Update()
    {
        if (requiredObject == null) return;

        float weight = onPad ? GetWeight() : 0f;
        onWeightChanged?.Invoke(weight);

        float error = weight - targetWeight;
        bool isCorrect = onPad && Mathf.Abs(error) <= tolerance;

        if (IsSolved && !isCorrect)
        {
            solved = false;
        }
        else if (IsSolved && isCorrect)
        {
            return;
        }

        // Colour: blue (light) -> green (correct) -> red (heavy)
        Color c = !onPad ? empty
                : isCorrect ? correct
                : error < 0 ? Color.Lerp(tooLight, correct, Mathf.InverseLerp(0f, targetWeight, weight))
                            : Color.Lerp(correct, tooHeavy, Mathf.InverseLerp(0f, targetWeight, error));

        // Wrong-object flash overrides the colour briefly
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            c = wrongFlash;
        }
        if (indicator) indicator.material.color = c;

        if (needle)
        {
            float t = onPad ? Mathf.Clamp(error / targetWeight, -1f, 1f) : -1f;
            Quaternion goal = Quaternion.Euler(0, 0, -t * needleMaxAngle);
            needle.localRotation = Quaternion.Slerp(needle.localRotation, goal, Time.deltaTime * 8f);
        }

        correctTimer = isCorrect ? correctTimer + Time.deltaTime : 0f;
        if (correctTimer >= holdTime)
        {
            solved = true;
            onSolved?.Invoke();
        }
    }
}
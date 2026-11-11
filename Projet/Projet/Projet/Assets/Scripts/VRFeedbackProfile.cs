using UnityEngine;

/// <summary>Shared, replaceable visuals and sounds for VR objects and the level timer.</summary>
[CreateAssetMenu(menuName = "VR/Interaction Feedback Profile")]
public sealed class VRFeedbackProfile : ScriptableObject
{
    [Header("Outline")]
    public Color outlineColor = Color.white;
    [Range(0.5f, 5f)] public float outlineWidthPixels = 2f;
    public Material outlineMask;
    public Material outlineShell;
    public Material outlineClear;

    [Header("Replaceable sound clips")]
    public AudioClip thudClip;
    public AudioClip grabClip;
    public AudioClip resizeLoopClip;
    public AudioClip timerBeepClip;
    [Range(0f, 1f)] public float thudVolume = 0.6f;
    [Range(0f, 1f)] public float grabVolume = 0.35f;
    [Range(0f, 1f)] public float resizeVolume = 0.2f;
    [Range(0f, 1f)] public float timerBeepVolume = 0.4f;
    [Min(0f)] public float minimumImpactSpeed = 0.35f;
    [Min(0.01f)] public float fullVolumeImpactSpeed = 3f;
    [Min(0f)] public float impactCooldown = 0.2f;

    static VRFeedbackProfile s_Default;
    public static VRFeedbackProfile Default => s_Default != null ? s_Default :
        s_Default = Resources.Load<VRFeedbackProfile>("VRFeedbackDefaults");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetDefault() => s_Default = null;
}

using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>Hold grip and pull this cube into the controller to change scenes.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(VRGrabObject))]
public sealed class GreenCubeWarp : MonoBehaviour, IXRSelectFilter
{
    public enum WarpMode { ToPlains, RestartLevel }

    [SerializeField] WarpMode m_Mode;
    [SerializeField] string m_PlainsScene = "Assets/Scenes/EmptyPlains.unity";
    [SerializeField] string m_FallbackLevel = "Assets/Scenes/Gyms/Gym Interaction.unity";
    [SerializeField, Min(0f)] float m_ContactTolerance = 0.01f;
    [SerializeField, Min(0f)] float m_FadeOutDuration = 0.6f;
    [SerializeField, Min(0f)] float m_FadeInDuration = 0.6f;

    // Only routing information survives a scene change, never level state or poses.
    static string s_LevelScene;
    static bool s_Loading;
    static bool s_RequireGripRelease;
    public static bool IsTransitioning => s_Loading || XRSceneTransition.IsTransitioning;

    XRGrabInteractable m_Interactable;
    Rigidbody m_Body;
    XRBaseInputInteractor m_SelectedHand;
    XRBaseInputInteractor[] m_Hands;
    bool m_LeftReady;
    bool m_RightReady;
    bool m_Initialized;
    bool m_OwnsTransition;
    bool m_LoadStarted;

    public bool canProcess => isActiveAndEnabled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRouting()
    {
        s_LevelScene = null;
        s_Loading = false;
        s_RequireGripRelease = false;
    }

    void Awake()
    {
        m_Interactable = GetComponent<XRGrabInteractable>();
        m_Body = GetComponent<Rigidbody>();
        m_Initialized = m_Mode == WarpMode.ToPlains;
    }

    void OnEnable()
    {
        m_LeftReady = m_RightReady = !s_RequireGripRelease;
        m_Interactable.selectFilters.Add(this);
        m_Interactable.selectEntered.AddListener(OnGrabbed);
        m_Interactable.selectExited.AddListener(OnReleased);
        StartCoroutine(Initialize());
    }

    IEnumerator Initialize()
    {
        // Let the scene's input actions and tracked camera update before placement/arming.
        yield return null;
        m_Hands = FindObjectsByType<XRBaseInputInteractor>(FindObjectsSortMode.None);
        if (!m_Initialized && m_Mode == WarpMode.RestartLevel)
        {
            Camera head = Camera.main;
            if (head != null)
            {
                Vector3 forward = Vector3.ProjectOnPlane(head.transform.forward, Vector3.up).normalized;
                if (forward.sqrMagnitude < 0.001f)
                    forward = Vector3.forward;
                Vector3 position = head.transform.position + forward * 2f;
                position.y = 0.2f; // EmptyPlains has a floor at y=0 and a 0.4 m cube.
                m_Body.position = position;
                if (!m_Body.isKinematic)
                {
                    m_Body.linearVelocity = Vector3.zero;
                    m_Body.angularVelocity = Vector3.zero;
                }
            }
            m_Initialized = true;
        }
    }

    void Update()
    {
        if (m_Hands == null || (m_LeftReady && m_RightReady))
            return;

        bool leftPresent = false, rightPresent = false;
        bool leftHeld = false, rightHeld = false;
        foreach (var hand in m_Hands)
        {
            if (hand == null || !hand.isActiveAndEnabled)
                continue;
            if (hand.handedness == InteractorHandedness.Left)
            {
                leftPresent = true;
                leftHeld |= hand.selectInput.ReadIsPerformed();
            }
            else if (hand.handedness == InteractorHandedness.Right)
            {
                rightPresent = true;
                rightHeld |= hand.selectInput.ReadIsPerformed();
            }
        }
        m_LeftReady |= leftPresent && !leftHeld;
        m_RightReady |= rightPresent && !rightHeld;
    }

    public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
    {
        if (m_Interactable.IsSelected(interactor))
            return true;
        if (!m_Initialized || s_Loading || !(interactor is XRBaseInputInteractor hand))
            return false;
        return hand.handedness == InteractorHandedness.Left ? m_LeftReady :
            hand.handedness == InteractorHandedness.Right && m_RightReady;
    }

    void OnGrabbed(SelectEnterEventArgs args) => m_SelectedHand = args.interactorObject as XRBaseInputInteractor;

    void OnReleased(SelectExitEventArgs args)
    {
        if (args.interactorObject as XRBaseInputInteractor == m_SelectedHand)
            m_SelectedHand = null;
    }

    void LateUpdate()
    {
        if (IsTransitioning || !IsHeldAtController())
            return;

        string level = gameObject.scene.path;
        string destination = m_Mode == WarpMode.ToPlains ? m_PlainsScene :
            (string.IsNullOrEmpty(s_LevelScene) ? m_FallbackLevel : s_LevelScene);
        if (string.IsNullOrEmpty(destination) || !Application.CanStreamedLevelBeLoaded(destination) ||
            (m_Mode == WarpMode.ToPlains && (string.IsNullOrEmpty(level) || !Application.CanStreamedLevelBeLoaded(level))))
        {
            Debug.LogError("GreenCubeWarp cannot load its destination/return level. Enable both scenes in the build scene list.", this);
            m_SelectedHand = null; // Retry on another grab instead of logging every frame.
            return;
        }

        s_Loading = true;
        m_OwnsTransition = true;
        StartCoroutine(Warp(destination, level));
    }

    bool IsHeldAtController()
    {
        if (m_SelectedHand == null || !m_SelectedHand.isActiveAndEnabled ||
            !m_Interactable.IsSelected(m_SelectedHand) || !m_SelectedHand.selectInput.ReadIsPerformed())
            return false;

        // A Near-Far attach anchor can be extended toward a distant hit. Use the
        // actual tracked controller pose so the ray hit cannot count as hand contact.
        Vector3 controllerPosition = m_SelectedHand.transform.position;
        float toleranceSquared = m_ContactTolerance * m_ContactTolerance;
        foreach (var collider in m_Interactable.colliders)
        {
            if (collider != null && collider.enabled && !collider.isTrigger &&
                collider.gameObject.activeInHierarchy && collider.attachedRigidbody == m_Body &&
                (collider.ClosestPoint(controllerPosition) - controllerPosition).sqrMagnitude <= toleranceSquared)
                return true;
        }
        return false;
    }

    IEnumerator Warp(string destination, string level)
    {
        yield return new WaitForEndOfFrame();
        if (!IsHeldAtController())
        {
            s_Loading = false;
            m_OwnsTransition = false;
            yield break;
        }

        XROrigin origin = m_SelectedHand.GetComponentInParent<XROrigin>();
        m_LoadStarted = StartTransition(destination, level, m_Mode == WarpMode.ToPlains, origin,
            m_FadeOutDuration, m_FadeInDuration);
        if (!m_LoadStarted)
        {
            m_OwnsTransition = false;
            m_SelectedHand = null;
        }
    }

    /// <summary>Reserves the same route/guard as the cubes, including optional timeout beeps.</summary>
    public static bool TryWarpToPlains(Scene sourceScene, AudioClip warningClip = null,
        int warningCount = 0, float warningInterval = 0.5f, float warningVolume = 0.4f)
    {
        if (IsTransitioning) return false;
        const string plains = "Assets/Scenes/EmptyPlains.unity";
        if (!sourceScene.IsValid() || string.IsNullOrEmpty(sourceScene.path) ||
            !Application.CanStreamedLevelBeLoaded(sourceScene.path) || !Application.CanStreamedLevelBeLoaded(plains))
        {
            Debug.LogError("GreenCubeWarp needs the source level and EmptyPlains enabled in the build scene list.");
            return false;
        }
        XROrigin origin = null;
        foreach (var candidate in FindObjectsByType<XROrigin>(FindObjectsSortMode.None))
        {
            if (candidate.gameObject.scene == sourceScene) { origin = candidate; break; }
        }
        s_Loading = true;
        return StartTransition(plains, sourceScene.path, true, origin, 0.6f, 0.6f,
            warningClip, warningCount, warningInterval, warningVolume);
    }

    static bool StartTransition(string destination, string level, bool saveLevel, XROrigin origin,
        float fadeOut, float fadeIn, AudioClip warningClip = null, int warningCount = 0,
        float warningInterval = 0.5f, float warningVolume = 0.4f)
    {
        string previousLevel = s_LevelScene;
        bool started = false;
        try
        {
            CancelHandSelections(origin);
            if (saveLevel)
                s_LevelScene = level;
            s_RequireGripRelease = true;
            // Only the fade overlay survives; the level and rig restart fresh.
            started = XRSceneTransition.TryLoad(destination, fadeOut, fadeIn, loaded =>
            {
                if (!loaded)
                    s_LevelScene = previousLevel;
                s_Loading = false;
            }, warningClip, warningCount, warningInterval, warningVolume);
        }
        catch (System.Exception exception)
        {
            Debug.LogError("GreenCubeWarp could not prepare the transition: " + exception.Message);
        }
        if (!started)
        {
            s_LevelScene = previousLevel;
            s_Loading = false;
            Debug.LogError("GreenCubeWarp could not start the headset fade. Release grip and try again.");
        }
        return started;
    }

    static void CancelHandSelections(XROrigin origin)
    {
        var selections = new List<IXRSelectInteractable>();
        foreach (var hand in FindObjectsByType<XRBaseInputInteractor>(FindObjectsSortMode.None))
        {
            if (origin != null && hand.GetComponentInParent<XROrigin>() != origin)
                continue;
            selections.Clear();
            selections.AddRange(hand.interactablesSelected);
            foreach (var selected in selections)
                hand.interactionManager?.CancelInteractableSelection(selected);
        }
    }

    void OnDisable()
    {
        StopAllCoroutines();
        m_Interactable.selectEntered.RemoveListener(OnGrabbed);
        m_Interactable.selectExited.RemoveListener(OnReleased);
        m_Interactable.selectFilters.Remove(this);
        if (m_OwnsTransition && !m_LoadStarted)
            s_Loading = false;
        m_OwnsTransition = false;
        m_SelectedHand = null;
    }
}

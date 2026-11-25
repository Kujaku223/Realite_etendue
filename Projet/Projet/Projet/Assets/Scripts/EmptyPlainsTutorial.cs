using System;
using System.Collections;
using UnityEngine;

/// <summary>Action-verified tutorial; completion belongs only to this scene visit.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public sealed class EmptyPlainsTutorial : MonoBehaviour
{
    public enum TutorialStep { Movement, Grab, Resize, ReadyToWarp }

    [Header("Scene References")]
    [SerializeField] Unity.XR.CoreUtils.XROrigin m_Origin;
    [SerializeField] UnityEngine.InputSystem.InputActionReference m_MoveAction;
    [SerializeField] UnityEngine.InputSystem.InputActionReference m_TurnAction;
    [SerializeField] UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable m_Cube;
    [SerializeField] VRTwoHandScale m_Scaler;

    [Header("Editor Simulator Testing")]
    [SerializeField, Tooltip("Editor only: count movement and turning of the tracked camera without requiring XR thumbstick actions. Disable when testing real headset controls in the Editor. Ignored in player builds.")]
    bool m_EditorSimulatorPoseProgress;

    [Header("Completion Thresholds")]
    [SerializeField, Min(0.05f)] float m_MoveDistance = 0.75f;
    [SerializeField, Range(1f, 180f)] float m_TurnDegrees = 30f;
    [SerializeField, Range(0.05f, 0.5f)] float m_ResizeFraction = 0.2f;

    [Header("Lost Cube Recovery")]
    [SerializeField, Min(10f)] float m_RecoveryDistance = 12f;
    [SerializeField] float m_MinimumCubeHeight = -2f;
    [SerializeField, Min(1f)] float m_RecoveryForwardDistance = 2f;

    UnityEngine.InputSystem.InputAction m_Move;
    UnityEngine.InputSystem.InputAction m_Turn;
    Rigidbody m_CubeBody;
    Collider[] m_CubeColliders;
    Vector3 m_LastPosition;
    float m_LastYaw;
    float m_Distance;
    float m_Turned;
    float m_ResizeBaseline;
    bool m_MoveActive;
    bool m_TurnActive;
    bool m_ResizeQualified;
    bool m_HadScaleLock;
    bool m_Subscribed;

    public TutorialStep Step { get; private set; }
    public bool Initialized { get; private set; }
    public bool IsComplete => Initialized && Step == TutorialStep.ReadyToWarp;
    public bool UsesSimulatorPoseProgress => Application.isEditor && m_EditorSimulatorPoseProgress;
    Transform MovementTarget => UsesSimulatorPoseProgress ? m_Origin.Camera.transform : m_Origin.transform;
    public bool MovementComplete => m_Distance >= m_MoveDistance;
    public bool TurningComplete => m_Turned >= m_TurnDegrees;
    public bool ResizeQualified => m_ResizeQualified;
    public float ResizeTargetPercent => m_ResizeFraction * 100f;
    public event Action ProgressChanged;
    void Awake()
    {
        if (m_Origin == null || m_Origin.Camera == null || m_MoveAction == null ||
            m_TurnAction == null || m_Cube == null || m_Scaler == null ||
            m_Scaler.gameObject != m_Cube.gameObject)
        {
            Debug.LogError("EmptyPlainsTutorial needs the XR Origin, move/turn actions, cube and its scaler. Warp remains locked.", this);
            enabled = false;
            return;
        }
        m_Move = m_MoveAction.action;
        m_Turn = m_TurnAction.action;
        m_CubeBody = m_Cube.GetComponent<Rigidbody>();
        m_CubeColliders = m_Cube.GetComponentsInChildren<Collider>();
        if (m_Move == null || m_Turn == null || m_CubeBody == null)
        {
            Debug.LogError("EmptyPlainsTutorial has invalid actions or a cube without a Rigidbody. Warp remains locked.", this);
            enabled = false;
        }
    }

    void OnEnable()
    {
        if (m_Move == null || m_Turn == null || m_Cube == null) return;
        m_Move.performed += OnMove;
        m_Move.canceled += OnMove;
        m_Turn.performed += OnTurn;
        m_Turn.canceled += OnTurn;
        m_Cube.selectEntered.AddListener(OnGrabbed);
        m_Subscribed = true;
        StartCoroutine(Initialize());
    }

    IEnumerator Initialize()
    {
        Initialized = false;
        // The doorway and return cube initialize on the first yielded frame.
        yield return null;
        yield return null;
        while (GreenCubeWarp.IsTransitioning) yield return null;
        yield return new WaitForEndOfFrame();
        if (!UsesSimulatorPoseProgress && (!m_Move.enabled || !m_Turn.enabled))
        {
            Debug.LogError("Tutorial locomotion actions are disabled. Check the rig's InputActionManager and controller locomotion settings.", this);
            enabled = false;
            yield break;
        }
        Step = TutorialStep.Movement;
        m_Distance = m_Turned = 0f;
        m_ResizeQualified = m_HadScaleLock = false;
        m_MoveActive = m_Move.ReadValue<Vector2>().sqrMagnitude > 0f;
        m_TurnActive = Mathf.Abs(m_Turn.ReadValue<Vector2>().x) > 0f;
        SnapshotMotion();
        Initialized = true;
        ProgressChanged?.Invoke();
    }

    void OnMove(UnityEngine.InputSystem.InputAction.CallbackContext context)
        => m_MoveActive = !context.canceled && context.ReadValue<Vector2>().sqrMagnitude > 0f;

    void OnTurn(UnityEngine.InputSystem.InputAction.CallbackContext context)
        => m_TurnActive = !context.canceled && Mathf.Abs(context.ReadValue<Vector2>().x) > 0f;

    void SnapshotMotion()
    {
        m_LastPosition = MovementTarget.position;
        m_LastYaw = MovementTarget.eulerAngles.y;
    }

    void LateUpdate()
    {
        if (!Initialized || GreenCubeWarp.IsTransitioning) return;
        if (Step == TutorialStep.Movement) TrackMovement();
        else if (Step == TutorialStep.Resize) TrackResize();
        RecoverLostCube();
    }

    void TrackMovement()
    {
        bool movedBefore = MovementComplete;
        bool turnedBefore = TurningComplete;
        Vector3 position = MovementTarget.position;
        float yaw = MovementTarget.eulerAngles.y;
        if (UsesSimulatorPoseProgress || (m_MoveActive && m_Move.enabled))
            m_Distance += Vector3.ProjectOnPlane(position - m_LastPosition, Vector3.up).magnitude;
        if (UsesSimulatorPoseProgress || (m_TurnActive && m_Turn.enabled))
            m_Turned += Mathf.Abs(Mathf.DeltaAngle(m_LastYaw, yaw));
        m_LastPosition = position;
        m_LastYaw = yaw;
        if (MovementComplete && TurningComplete)
        {
            Step = TutorialStep.Grab;
            ProgressChanged?.Invoke();
        }
        else if (movedBefore != MovementComplete || turnedBefore != TurningComplete)
            ProgressChanged?.Invoke();
    }

    void OnGrabbed(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs args)
    {
        if (!Initialized || Step != TutorialStep.Grab) return;
        var hand = args.interactorObject as UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInputInteractor;
        if (hand == null || hand.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>() != m_Origin ||
            hand.handedness == UnityEngine.XR.Interaction.Toolkit.Interactors.InteractorHandedness.None) return;
        Step = TutorialStep.Resize;
        m_ResizeBaseline = m_Cube.transform.lossyScale.magnitude;
        m_ResizeQualified = m_HadScaleLock = false;
        ProgressChanged?.Invoke();
    }

    void TrackResize()
    {
        if (m_Scaler.isScaling)
        {
            m_HadScaleLock = true;
            float change = m_ResizeBaseline > Mathf.Epsilon
                ? Mathf.Abs(m_Cube.transform.lossyScale.magnitude / m_ResizeBaseline - 1f) : 0f;
            if (!m_ResizeQualified && change >= m_ResizeFraction - 0.0001f)
            {
                m_ResizeQualified = true;
                ProgressChanged?.Invoke();
            }
        }
        else if (m_HadScaleLock && m_ResizeQualified)
        {
            Step = TutorialStep.ReadyToWarp;
            ProgressChanged?.Invoke();
        }
    }

    void RecoverLostCube()
    {
        if (m_Cube.isSelected || m_Scaler.isScaling) return;
        Vector3 head = m_Origin.Camera.transform.position;
        Vector3 cubePosition = m_Cube.transform.position;
        if (cubePosition.y >= m_MinimumCubeHeight &&
            Vector3.ProjectOnPlane(cubePosition - head, Vector3.up).sqrMagnitude <= m_RecoveryDistance * m_RecoveryDistance) return;
        Vector3 forward = Vector3.ProjectOnPlane(m_Origin.Camera.transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.001f) forward = m_Origin.transform.forward;
        Vector3 destination = head + forward * m_RecoveryForwardDistance;
        // The authored plains surface is y=0. Determine the actual rotated/scaled bottom.
        Physics.SyncTransforms();
        float bottom = cubePosition.y;
        foreach (var collider in m_CubeColliders)
            if (collider != null && collider.enabled && !collider.isTrigger && collider.attachedRigidbody == m_CubeBody)
                bottom = Mathf.Min(bottom, collider.bounds.min.y);
        destination.y = cubePosition.y - bottom + 0.02f;
        m_CubeBody.position = destination;
        m_Cube.transform.position = destination;
        if (!m_CubeBody.isKinematic)
        {
            m_CubeBody.linearVelocity = Vector3.zero;
            m_CubeBody.angularVelocity = Vector3.zero;
        }
    }

    void OnDisable()
    {
        StopAllCoroutines();
        Initialized = false;
        if (!m_Subscribed) return;
        m_Move.performed -= OnMove;
        m_Move.canceled -= OnMove;
        m_Turn.performed -= OnTurn;
        m_Turn.canceled -= OnTurn;
        if (m_Cube != null) m_Cube.selectEntered.RemoveListener(OnGrabbed);
        m_Subscribed = false;
    }
}

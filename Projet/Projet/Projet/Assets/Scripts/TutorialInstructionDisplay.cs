using UnityEngine;

/// <summary>A readable world-space instruction board that does not intercept XR targeting.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(10010)]
public sealed class TutorialInstructionDisplay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] EmptyPlainsTutorial m_Tutorial;
    [SerializeField] Transform m_Head;
    [SerializeField] GameObject m_Panel;
    [SerializeField] TMPro.TextMeshPro m_Title;
    [SerializeField] TMPro.TextMeshPro m_Body;
    [SerializeField] TMPro.TextMeshPro m_Progress;

    [Header("Comfort")]
    [SerializeField, Tooltip("Disable to keep the board at its scene Transform, without following the camera.")]
    bool m_FollowHead = true;
    [SerializeField, Min(1f)] float m_Distance = 1.8f;
    [SerializeField, Range(0f, 1f)] float m_BelowEyes = 0.1f;
    [SerializeField, Range(30f, 100f)] float m_RecenterAngle = 65f;
    [SerializeField, Min(0.1f)] float m_FollowSpeed = 1.5f;
    [SerializeField, Min(0.1f)] float m_RecenterDistance = 1f;

    [Header("Instructions")]
    [SerializeField, TextArea] string m_MovementText = "Use the LEFT thumbstick to move.\nPush the RIGHT thumbstick left or right to turn.\nMove a little and try turning.";
    [SerializeField, TextArea] string m_GrabText = "Point at or touch the GREEN cube.\nHold either GRIP button to grab it.\nRelease GRIP to put it down.";
    [SerializeField, TextArea] string m_ResizeText = "Release GRIP so both hands are free.\nPoint BOTH controllers at the cube. Hold BOTH TRIGGERS.\nHands apart: grow. Hands together: shrink.\nChange its size, then release a trigger.";
    [SerializeField, TextArea] string m_ReadyText = "Hold GRIP to pull the green cube to your hand\nand enter Environnement.\nIf it is too large to grab, shrink it first.";

    bool m_Positioned;
    bool m_Recentering;
    Vector3 m_AnchorHead;
    Vector3 m_TargetPosition;
    Quaternion m_TargetRotation;

    void OnEnable()
    {
        if (m_Tutorial != null) m_Tutorial.ProgressChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        if (m_Tutorial != null) m_Tutorial.ProgressChanged -= Refresh;
    }

    void Refresh()
    {
        if (m_Tutorial == null || m_Title == null || m_Body == null || m_Progress == null) return;
        switch (m_Tutorial.Step)
        {
            case EmptyPlainsTutorial.TutorialStep.Movement:
                m_Title.text = "1 / 3   MOVE & TURN";
                m_Body.text = m_Tutorial.UsesSimulatorPoseProgress ?
                    "EDITOR SIMULATOR TEST\nUse the simulator controls to move your view.\nTurn your view left or right to continue." : m_MovementText;
                m_Progress.text = (m_Tutorial.MovementComplete ? "Move: DONE" :
                    m_Tutorial.UsesSimulatorPoseProgress ? "Move: move your view" : "Move: try the left stick") +
                    "     |     " + (m_Tutorial.TurningComplete ? "Turn: DONE" :
                    m_Tutorial.UsesSimulatorPoseProgress ? "Turn: turn your view" : "Turn: try the right stick");
                break;
            case EmptyPlainsTutorial.TutorialStep.Grab:
                m_Title.text = "2 / 3   GRAB THE CUBE";
                m_Body.text = m_GrabText;
                m_Progress.text = "Movement complete. Grab the green cube to continue.";
                break;
            case EmptyPlainsTutorial.TutorialStep.Resize:
                m_Title.text = "3 / 3   RESIZE THE CUBE";
                m_Body.text = m_ResizeText;
                m_Progress.text = m_Tutorial.ResizeQualified ? "Size change: DONE. Release a trigger to finish." :
                    string.Format("Grow or shrink the cube by {0:0}%.", m_Tutorial.ResizeTargetPercent);
                break;
            default:
                m_Title.text = "TUTORIAL COMPLETE";
                m_Body.text = m_ReadyText;
                m_Progress.text = "Release the triggers, then make a NEW grip grab to warp.";
                break;
        }
    }

    void LateUpdate()
    {
        if (m_Panel == null || m_Tutorial == null) return;
        bool visible = m_Tutorial.Initialized && !GreenCubeWarp.IsTransitioning;
        if (m_Panel.activeSelf != visible) m_Panel.SetActive(visible);
        if (!visible || !m_FollowHead || m_Head == null) return;
        Vector3 forward = Vector3.ProjectOnPlane(m_Head.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.001f) return;
        Vector3 toPanel = Vector3.ProjectOnPlane(transform.position - m_Head.position, Vector3.up);
        bool needsRecenter = !m_Positioned || Vector3.Angle(forward, toPanel) > m_RecenterAngle ||
            Vector3.ProjectOnPlane(m_Head.position - m_AnchorHead, Vector3.up).sqrMagnitude > m_RecenterDistance * m_RecenterDistance;
        if (needsRecenter && !m_Recentering)
        {
            m_TargetPosition = m_Head.position + forward * m_Distance - Vector3.up * m_BelowEyes;
            m_TargetRotation = Quaternion.LookRotation(forward, Vector3.up);
            m_AnchorHead = m_Head.position;
            m_Recentering = true;
        }
        if (!m_Positioned)
        {
            transform.SetPositionAndRotation(m_TargetPosition, m_TargetRotation);
            m_Positioned = true;
            m_Recentering = false;
        }
        else if (m_Recentering)
        {
            float blend = 1f - Mathf.Exp(-m_FollowSpeed * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, m_TargetPosition, blend);
            transform.rotation = Quaternion.Slerp(transform.rotation, m_TargetRotation, blend);
            if (Vector3.Distance(transform.position, m_TargetPosition) < 0.02f && Quaternion.Angle(transform.rotation, m_TargetRotation) < 1f)
                m_Recentering = false;
        }
    }
}

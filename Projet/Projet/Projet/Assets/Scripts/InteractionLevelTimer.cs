using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>Scene-owned wrist countdown. Its timeout uses the shared warp route.</summary>
[DisallowMultipleComponent]
public sealed class InteractionLevelTimer : MonoBehaviour
{
    [SerializeField, Min(0f)] float m_DurationSeconds = 300f;
    [SerializeField] VRFeedbackProfile m_FeedbackProfile;
    [SerializeField] XRBaseInputInteractor m_LeftController;

    GameObject m_Display;
    TextMeshProUGUI m_Label;
    float m_Remaining;
    float m_NextBind;
    int m_ShownSeconds = -1;
    bool m_Started;
    bool m_Expired;

    void Awake()
    {
        m_Remaining = m_DurationSeconds;
        if (m_FeedbackProfile == null) m_FeedbackProfile = VRFeedbackProfile.Default;
    }

    void OnEnable()
    {
        if (m_Display != null) m_Display.SetActive(true);
        m_NextBind = 0f;
    }

    void Update()
    {
        if (m_Display == null && Time.unscaledTime >= m_NextBind)
        {
            m_NextBind = Time.unscaledTime + 0.5f;
            BindWrist();
        }
        if (m_Expired) return;
        // Freshly loaded levels begin their five minutes after the entry fade.
        if (!m_Started && GreenCubeWarp.IsTransitioning) return;
        m_Started = true;
        m_Remaining = Mathf.Max(0f, m_Remaining - Time.unscaledDeltaTime);
        ShowTime();
        if (m_Remaining > 0f || GreenCubeWarp.IsTransitioning) return;

        // A failed setup logs once; an already committed cube warp takes precedence.
        m_Expired = true;
        GreenCubeWarp.TryWarpToPlains(gameObject.scene,
            m_FeedbackProfile != null ? m_FeedbackProfile.timerBeepClip : null,
            warningCount: 3, warningInterval: 0.5f,
            warningVolume: m_FeedbackProfile != null ? m_FeedbackProfile.timerBeepVolume : 0.4f);
    }

    void BindWrist()
    {
        if (m_LeftController == null)
        {
            foreach (var origin in FindObjectsByType<XROrigin>(FindObjectsSortMode.None))
            {
                if (origin.gameObject.scene != gameObject.scene) continue;
                foreach (var hand in origin.GetComponentsInChildren<XRBaseInputInteractor>(true))
                {
                    if (hand.handedness != InteractorHandedness.Left) continue;
                    if (m_LeftController == null || hand is NearFarInteractor)
                        m_LeftController = hand;
                    if (hand is NearFarInteractor) break;
                }
                if (m_LeftController != null) break;
            }
        }
        if (m_LeftController == null) return;

        m_Display = new GameObject("Interaction Wrist Timer", typeof(RectTransform), typeof(Canvas));
        m_Display.layer = 5; // UI; no colliders or graphic raycaster are added.
        var root = m_Display.GetComponent<RectTransform>();
        root.SetParent(m_LeftController.transform, false);
        root.localPosition = new Vector3(0f, 0.08f, 0.02f);
        root.localRotation = Quaternion.Euler(60f, 0f, 0f);
        root.localScale = Vector3.one * 0.001f;
        root.sizeDelta = new Vector2(120f, 60f);
        var canvas = m_Display.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvas.sortingOrder = 10;

        var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.layer = 5;
        Stretch(background.GetComponent<RectTransform>(), root);
        var image = background.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.8f);
        image.raycastTarget = false;

        var text = new GameObject("Countdown", typeof(RectTransform), typeof(TextMeshProUGUI));
        text.layer = 5;
        var textRect = text.GetComponent<RectTransform>();
        Stretch(textRect, root);
        textRect.offsetMin = new Vector2(6f, 4f);
        textRect.offsetMax = new Vector2(-6f, -4f);
        m_Label = text.GetComponent<TextMeshProUGUI>();
        m_Label.font = TMP_Settings.defaultFontAsset;
        m_Label.fontSize = 32f;
        m_Label.alignment = TextAlignmentOptions.Center;
        m_Label.color = Color.white;
        m_Label.raycastTarget = false;
        m_ShownSeconds = -1;
        ShowTime();
    }

    static void Stretch(RectTransform rect, Transform parent)
    {
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    void ShowTime()
    {
        int seconds = Mathf.CeilToInt(m_Remaining);
        if (m_Label == null || seconds == m_ShownSeconds) return;
        m_ShownSeconds = seconds;
        m_Label.text = (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
    }

    void OnDisable()
    {
        if (m_Display != null) m_Display.SetActive(false);
    }

    void OnDestroy()
    {
        if (m_Display != null) Destroy(m_Display);
    }
}

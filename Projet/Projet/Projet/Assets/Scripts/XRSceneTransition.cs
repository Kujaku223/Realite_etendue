using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Fades the headset through a scene load without preserving the old rig.</summary>
public sealed class XRSceneTransition : MonoBehaviour
{
    static XRSceneTransition s_Active;
    Canvas m_Canvas;
    CanvasGroup m_Group;
    Action<bool> m_Finished;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetTransition() => s_Active = null;

    public static bool TryLoad(string scenePath, float fadeOut, float fadeIn, Action<bool> finished)
    {
        if (s_Active != null)
            return false;
        if (Camera.main == null)
        {
            Debug.LogError("XRSceneTransition needs an active MainCamera for the headset fade.");
            return false;
        }

        var root = new GameObject("XR Scene Transition");
        DontDestroyOnLoad(root);
        s_Active = root.AddComponent<XRSceneTransition>();
        s_Active.m_Finished = finished;
        try
        {
            s_Active.CreateFadeCanvas();
            SceneManager.sceneLoaded += s_Active.OnSceneLoaded;
            s_Active.StartCoroutine(s_Active.Load(scenePath, fadeOut, fadeIn));
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError("XRSceneTransition could not create the fade: " + exception.Message);
            Destroy(root);
            return false;
        }
    }

    void CreateFadeCanvas()
    {
        var canvasObject = new GameObject("Headset Fade", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);
        m_Canvas = canvasObject.GetComponent<Canvas>();
        // Overlay-mode canvases do not render in the headset. Camera mode renders
        // in both eyes and stays locked to the active tracked camera.
        m_Canvas.renderMode = RenderMode.ScreenSpaceCamera;
        m_Canvas.overrideSorting = true;
        m_Canvas.sortingOrder = short.MaxValue;
        m_Group = canvasObject.GetComponent<CanvasGroup>();
        m_Group.alpha = 0f;
        m_Group.blocksRaycasts = false;
        m_Group.interactable = false;

        var imageObject = new GameObject("Black", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(canvasObject.transform, false);
        var rect = imageObject.GetComponent<RectTransform>();
        // Overscan the near-camera plane so stereo eye offsets cannot expose
        // scene edges around the fade, including on wide-FOV headsets.
        rect.anchorMin = new Vector2(-2f, -2f);
        rect.anchorMax = new Vector2(3f, 3f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var image = imageObject.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
        BindCamera();
    }

    void BindCamera()
    {
        Camera head = Camera.main;
        if (head == null || m_Canvas == null)
            return;
        m_Canvas.worldCamera = head;
        m_Canvas.planeDistance = Mathf.Max(0.02f, head.nearClipPlane + 0.01f);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => BindCamera();

    IEnumerator Load(string scenePath, float fadeOut, float fadeIn)
    {
        yield return Fade(1f, fadeOut);
        // Present a completely black frame before any scene loading begins.
        yield return null;

        AsyncOperation loading = null;
        try
        {
            loading = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Single);
        }
        catch (Exception exception)
        {
            Debug.LogError("XRSceneTransition failed to load the scene: " + exception.Message);
        }

        bool loaded = false;
        if (loading != null)
        {
            yield return loading;
            // The new rig/input actions and return-cube placement initialize
            // under black before the view is revealed.
            yield return null;
            BindCamera();
            yield return null;
            loaded = SceneManager.GetActiveScene().path == scenePath;
            if (!loaded)
                Debug.LogError("XRSceneTransition did not arrive in the requested scene: " + scenePath);
        }

        yield return Fade(0f, fadeIn);
        Action<bool> finished = m_Finished;
        m_Finished = null;
        finished?.Invoke(loaded);
        Destroy(gameObject);
    }

    IEnumerator Fade(float target, float duration)
    {
        float start = m_Group.alpha;
        duration = Mathf.Max(0f, duration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            m_Group.alpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
        }
        m_Group.alpha = target;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (s_Active == this)
            s_Active = null;
        Action<bool> finished = m_Finished;
        m_Finished = null;
        finished?.Invoke(false);
    }
}

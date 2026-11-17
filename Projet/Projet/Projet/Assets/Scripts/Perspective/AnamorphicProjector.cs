using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Point de vue P de l'anamorphose. Une camÃ©ra cachÃ©e rend le cube fantÃ´me dans une
/// RenderTexture, puis envoie au shader Â« AnamorphicProjection Â» ce qu'il faut pour
/// reprojeter cette image sur les surfaces de la scÃ¨ne.
/// Avec l'occlusion, elle rend aussi une carte de profondeur de la scÃ¨ne vue depuis P
/// (comme une shadow map) : le dessin n'apparaÃ®t que sur la premiÃ¨re surface touchÃ©e.
/// </summary>
[RequireComponent(typeof(Camera))]
public class AnamorphicProjector : MonoBehaviour
{
    public static readonly int VpId          = Shader.PropertyToID("_AnamorphVP");
    public static readonly int TexId         = Shader.PropertyToID("_AnamorphTex");
    public static readonly int PosId         = Shader.PropertyToID("_AnamorphPos");
    public static readonly int OpacityId     = Shader.PropertyToID("_AnamorphOpacity");
    public static readonly int DepthTexId    = Shader.PropertyToID("_AnamorphDepth");
    public static readonly int DepthParamsId = Shader.PropertyToID("_AnamorphDepthParams");
    public static readonly int TexelScaleId  = Shader.PropertyToID("_AnamorphTexelScale");

    [Header("Cube fantÃ´me")]
    [Tooltip("Racine du cube fantÃ´me. Ses renderers ne sont activÃ©s que pendant le rendu du projecteur.")]
    [SerializeField] Transform ghostRoot;
    [Tooltip("Calque du cube fantÃ´me (ex. Â« Anamorphose Â»). C'est le seul que cette camÃ©ra rend.")]
    [SerializeField] LayerMask ghostLayers;

    [Header("Texture")]
    [SerializeField] int resolution = 2048;
    [Tooltip("Refaire le rendu Ã  chaque frame (si P, le fantÃ´me ou les objets bougent). CoÃ»teux sur Quest.")]
    [SerializeField] bool refreshEveryFrame;
    [Tooltip("Dedicated projector renderer index in every active URP configuration.")]
    [SerializeField, Min(0)] int projectorRendererIndex = 1;

    [Header("Occlusion")]
    [Tooltip("N'affiche le dessin que sur la premiÃ¨re surface touchÃ©e depuis P, comme une ombre.")]
    [SerializeField] bool occlusion = true;
    [Tooltip("Objets qui peuvent cacher le dessin. Le calque du fantÃ´me est toujours exclu.")]
    [SerializeField] LayerMask occluderLayers = ~0;
    [Tooltip("Marge en mÃ¨tres. Augmente-la si le dessin est striÃ© ou trouÃ© ; diminue-la s'il dÃ©borde juste derriÃ¨re les objets.")]
    [SerializeField] float depthBias = 0.03f;

    [Header("Affichage")]
    [Range(0f, 1f)] [SerializeField] float opacity = 1f;

    Camera _cam;
    RenderTexture _rt;
    RenderTexture _depthRt;
    Renderer[] _ghostRenderers = System.Array.Empty<Renderer>();
    Matrix4x4 _renderedView, _renderedProjection, _renderedGhost;
    bool _hasSnapshot;
    bool _captureFailed;
    bool _depthCaptureRecorded;
    public bool IsCapturingDepth { get; private set; }
    internal void MarkDepthCaptureRecorded() => _depthCaptureRecorded = true;

    public float Opacity
    {
        get => opacity;
        set
        {
            opacity = Mathf.Clamp01(value);
            Shader.SetGlobalFloat(OpacityId, opacity);
        }
    }

    /// <summary>Texture contenant l'image du fantÃ´me vue depuis P (alpha 0 = pas de dessin).</summary>
    public RenderTexture Texture => _rt;

    /// <summary>Carte de profondeur de la scÃ¨ne vue depuis P (utilisÃ©e pour l'occlusion).</summary>
    public RenderTexture DepthTexture => _depthRt;

    void Awake()
    {
        _cam = GetComponent<Camera>();

        if (ghostRoot != null)
        {
            _ghostRenderers = ghostRoot.GetComponentsInChildren<Renderer>(true);
            // Le fantÃ´me ne doit ni bloquer le joueur ni intercepter les rayons XR.
            foreach (var c in ghostRoot.GetComponentsInChildren<Collider>(true))
                c.enabled = false;
        }
        SetGhostVisible(false); // le joueur ne voit jamais le fantÃ´me directement

        // Une seule AudioListener dans la scÃ¨ne : celle du XR Origin.
        if (TryGetComponent(out AudioListener listener)) Destroy(listener);
        if (CompareTag("MainCamera"))
            Debug.LogWarning($"{name} : la camÃ©ra du projecteur ne doit pas avoir le tag MainCamera.", this);

        _rt = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.ARGB32)
        {
            name = "Anamorphic_RT",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Trilinear,
            useMipMap = true,
            autoGenerateMips = true,
        };
        _rt.Create();

        // Composition Layers injects passes requiring both attachments, even on
        // offscreen cameras. Store raw depth in a linear color output instead.
        var depthFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RFloat) ?
            RenderTextureFormat.RFloat : RenderTextureFormat.ARGBHalf;
        _depthRt = new RenderTexture(resolution, resolution, 24, depthFormat, RenderTextureReadWrite.Linear)
        {
            name = "Anamorphic_Depth",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Point,
        };
        _depthRt.Create();

        _cam.enabled = false;                 // rendu manuel uniquement
        _cam.targetTexture = _rt;
        _cam.aspect = 1f;
        _cam.cullingMask = ghostLayers;
        _cam.clearFlags = CameraClearFlags.SolidColor;
        _cam.backgroundColor = Color.clear;   // alpha 0 = Â« pas de dessin Â»
        _cam.allowHDR = false;
        _cam.allowMSAA = false;

        var urpData = _cam.GetUniversalAdditionalCameraData();
        urpData.renderPostProcessing = false;
        urpData.renderShadows = false;
        urpData.allowXRRendering = false;
        urpData.antialiasing = AntialiasingMode.None;
        urpData.SetRenderer(projectorRendererIndex);
    }

    void OnEnable()
    {
        _hasSnapshot = false;
        Shader.SetGlobalFloat(OpacityId, opacity);
    }
    void Start() => Refresh();

    void LateUpdate()
    {
        if (refreshEveryFrame || !_hasSnapshot ||
            _renderedView != _cam.worldToCameraMatrix || _renderedProjection != _cam.projectionMatrix ||
            (ghostRoot != null && _renderedGhost != ghostRoot.localToWorldMatrix))
            Refresh();
    }

    void OnDisable() => Shader.SetGlobalFloat(OpacityId, 0f);

    void OnDestroy()
    {
        if (_cam != null && (_cam.targetTexture == _rt || _cam.targetTexture == _depthRt))
            _cam.targetTexture = null;
        Release(_rt);
        Release(_depthRt);
    }

    void OnValidate()
    {
        if (Application.isPlaying && isActiveAndEnabled)
            Shader.SetGlobalFloat(OpacityId, opacity);
    }

    /// <summary>Rend la profondeur de la scÃ¨ne et le fantÃ´me, puis met Ã  jour les globales du shader.</summary>
    [ContextMenu("Refresh")]
    public void Refresh()
    {
        if (_rt == null || !isActiveAndEnabled) return;

        var previousTarget = _cam.targetTexture;
        int previousMask = _cam.cullingMask;
        Color previousBackground = _cam.backgroundColor;
        var previousClear = _cam.clearFlags;
        _captureFailed = false;
        Application.logMessageReceived += ObserveCaptureLog;
        try
        {

            // 1) Profondeur de la scÃ¨ne vue depuis P, sans le fantÃ´me
            if (occlusion)
            {
                IsCapturingDepth = true;
                _depthCaptureRecorded = false;
                _cam.cullingMask = occluderLayers.value & ~ghostLayers.value;
                _cam.backgroundColor = SystemInfo.usesReversedZBuffer ? Color.black : Color.white;
                RenderInto(_depthRt);
                if (!_depthCaptureRecorded)
                    throw new System.InvalidOperationException("The dedicated renderer is missing its Anamorphic Depth Capture feature.");
            }

            // 2) Image du fantÃ´me vue depuis P
            IsCapturingDepth = false;
            SetGhostVisible(true);
            _cam.cullingMask = ghostLayers;
            _cam.backgroundColor = Color.clear;
            RenderInto(_rt);
            SetGhostVisible(false);

            // Matrice Â« OpenGL Â» (pas GL.GetGPUProjectionMatrix) : NDC.xy âˆˆ [-1, 1] â†’ UV = NDC * 0.5 + 0.5,
            // et w = distance Ã  P le long de l'axe de la camÃ©ra (comparÃ©e Ã  la carte de profondeur).
            Shader.SetGlobalMatrix(VpId, _cam.projectionMatrix * _cam.worldToCameraMatrix);
            Shader.SetGlobalTexture(TexId, _rt);
            Shader.SetGlobalVector(PosId, transform.position);
            Shader.SetGlobalFloat(OpacityId, opacity);

            Shader.SetGlobalTexture(DepthTexId, _depthRt);
            Shader.SetGlobalVector(DepthParamsId,
                new Vector4(_cam.nearClipPlane, _cam.farClipPlane, depthBias, occlusion ? 1f : 0f));
            // Taille d'un texel de la carte Ã  1 m de P (pour la marge sur les surfaces rasantes)
            Shader.SetGlobalFloat(TexelScaleId, 2f * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / resolution);
            _renderedView = _cam.worldToCameraMatrix;
            _renderedProjection = _cam.projectionMatrix;
            _renderedGhost = ghostRoot != null ? ghostRoot.localToWorldMatrix : Matrix4x4.identity;
            _hasSnapshot = true;
        }
        catch (System.Exception exception)
        {
            enabled = false;
            Shader.SetGlobalFloat(OpacityId, 0f);
            Debug.LogWarning("Anamorphic projection disabled after a capture failure: " + exception.Message, this);
        }
        finally
        {
            Application.logMessageReceived -= ObserveCaptureLog;
            IsCapturingDepth = false;
            SetGhostVisible(false);
            _cam.targetTexture = previousTarget;
            _cam.cullingMask = previousMask;
            _cam.backgroundColor = previousBackground;
            _cam.clearFlags = previousClear;
        }
    }

    void ObserveCaptureLog(string message, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            _captureFailed = true;
    }

    void RenderInto(RenderTexture target)
    {
        _cam.targetTexture = target;
        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
        if (RenderPipeline.SupportsRenderRequest(_cam, request))
            RenderPipeline.SubmitRenderRequest(_cam, request);
        else
            throw new System.NotSupportedException("The active render pipeline does not support projector camera requests.");
        if (_captureFailed)
            throw new System.InvalidOperationException("The render pipeline reported an error while capturing the projector.");
    }

    void SetGhostVisible(bool visible)
    {
        foreach (var r in _ghostRenderers)
            if (r != null) r.enabled = visible;
    }

    static void Release(RenderTexture rt)
    {
        if (rt == null) return;
        rt.Release();
        Destroy(rt);
    }

    void OnDrawGizmosSelected()
    {
        var cam = GetComponent<Camera>();
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.8f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawFrustum(Vector3.zero, cam.fieldOfView, 5f, cam.nearClipPlane, 1f);
        Gizmos.matrix = Matrix4x4.identity;
        if (ghostRoot != null) Gizmos.DrawLine(transform.position, ghostRoot.position);
    }
}

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Point de vue P de l'anamorphose. Une caméra cachée rend le cube fantôme dans une
/// RenderTexture, puis envoie au shader « AnamorphicProjection » ce qu'il faut pour
/// reprojeter cette image sur les surfaces de la scène.
/// Avec l'occlusion, elle rend aussi une carte de profondeur de la scène vue depuis P
/// (comme une shadow map) : le dessin n'apparaît que sur la première surface touchée.
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

    [Header("Cube fantôme")]
    [Tooltip("Racine du cube fantôme. Ses renderers ne sont activés que pendant le rendu du projecteur.")]
    [SerializeField] Transform ghostRoot;
    [Tooltip("Calque du cube fantôme (ex. « Anamorphose »). C'est le seul que cette caméra rend.")]
    [SerializeField] LayerMask ghostLayers;

    [Header("Texture")]
    [SerializeField] int resolution = 2048;
    [Tooltip("Refaire le rendu à chaque frame (si P, le fantôme ou les objets bougent). Coûteux sur Quest.")]
    [SerializeField] bool refreshEveryFrame;

    [Header("Occlusion")]
    [Tooltip("N'affiche le dessin que sur la première surface touchée depuis P, comme une ombre.")]
    [SerializeField] bool occlusion = true;
    [Tooltip("Objets qui peuvent cacher le dessin. Le calque du fantôme est toujours exclu.")]
    [SerializeField] LayerMask occluderLayers = ~0;
    [Tooltip("Marge en mètres. Augmente-la si le dessin est strié ou troué ; diminue-la s'il déborde juste derrière les objets.")]
    [SerializeField] float depthBias = 0.03f;

    [Header("Affichage")]
    [Range(0f, 1f)] [SerializeField] float opacity = 1f;

    Camera _cam;
    RenderTexture _rt;
    RenderTexture _depthRt;
    Renderer[] _ghostRenderers = System.Array.Empty<Renderer>();

    public float Opacity
    {
        get => opacity;
        set
        {
            opacity = Mathf.Clamp01(value);
            Shader.SetGlobalFloat(OpacityId, opacity);
        }
    }

    /// <summary>Texture contenant l'image du fantôme vue depuis P (alpha 0 = pas de dessin).</summary>
    public RenderTexture Texture => _rt;

    /// <summary>Carte de profondeur de la scène vue depuis P (utilisée pour l'occlusion).</summary>
    public RenderTexture DepthTexture => _depthRt;

    void Awake()
    {
        _cam = GetComponent<Camera>();

        if (ghostRoot != null)
        {
            _ghostRenderers = ghostRoot.GetComponentsInChildren<Renderer>(true);
            // Le fantôme ne doit ni bloquer le joueur ni intercepter les rayons XR.
            foreach (var c in ghostRoot.GetComponentsInChildren<Collider>(true))
                c.enabled = false;
        }
        SetGhostVisible(false); // le joueur ne voit jamais le fantôme directement

        // Une seule AudioListener dans la scène : celle du XR Origin.
        if (TryGetComponent(out AudioListener listener)) Destroy(listener);
        if (CompareTag("MainCamera"))
            Debug.LogWarning($"{name} : la caméra du projecteur ne doit pas avoir le tag MainCamera.", this);

        _rt = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.ARGB32)
        {
            name = "Anamorphic_RT",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Trilinear,
            useMipMap = true,
            autoGenerateMips = true,
        };
        _rt.Create();

        // Format Depth : URP ne rend que la profondeur des objets (chemin « offscreen depth »)
        _depthRt = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.Depth)
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
        _cam.backgroundColor = Color.clear;   // alpha 0 = « pas de dessin »
        _cam.stereoTargetEye = StereoTargetEyeMask.None;
        _cam.allowHDR = false;
        _cam.allowMSAA = false;

        var urpData = _cam.GetUniversalAdditionalCameraData();
        urpData.renderPostProcessing = false;
        urpData.renderShadows = false;
    }

    void OnEnable() => Shader.SetGlobalFloat(OpacityId, opacity);
    void Start() => Refresh();

    void LateUpdate()
    {
        if (refreshEveryFrame) Refresh();
    }

    void OnDisable() => Shader.SetGlobalFloat(OpacityId, 0f);

    void OnDestroy()
    {
        Release(_rt);
        Release(_depthRt);
    }

    void OnValidate()
    {
        if (Application.isPlaying && isActiveAndEnabled)
            Shader.SetGlobalFloat(OpacityId, opacity);
    }

    /// <summary>Rend la profondeur de la scène et le fantôme, puis met à jour les globales du shader.</summary>
    [ContextMenu("Refresh")]
    public void Refresh()
    {
        if (_rt == null) return;

        // 1) Profondeur de la scène vue depuis P, sans le fantôme
        if (occlusion)
        {
            _cam.cullingMask = occluderLayers.value & ~ghostLayers.value;
            RenderInto(_depthRt);
        }

        // 2) Image du fantôme vue depuis P
        SetGhostVisible(true);
        _cam.cullingMask = ghostLayers;
        RenderInto(_rt);
        SetGhostVisible(false);

        // Matrice « OpenGL » (pas GL.GetGPUProjectionMatrix) : NDC.xy ∈ [-1, 1] → UV = NDC * 0.5 + 0.5,
        // et w = distance à P le long de l'axe de la caméra (comparée à la carte de profondeur).
        Shader.SetGlobalMatrix(VpId, _cam.projectionMatrix * _cam.worldToCameraMatrix);
        Shader.SetGlobalTexture(TexId, _rt);
        Shader.SetGlobalVector(PosId, transform.position);
        Shader.SetGlobalFloat(OpacityId, opacity);

        Shader.SetGlobalTexture(DepthTexId, _depthRt);
        Shader.SetGlobalVector(DepthParamsId,
            new Vector4(_cam.nearClipPlane, _cam.farClipPlane, depthBias, occlusion ? 1f : 0f));
        // Taille d'un texel de la carte à 1 m de P (pour la marge sur les surfaces rasantes)
        Shader.SetGlobalFloat(TexelScaleId, 2f * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / resolution);
    }

    void RenderInto(RenderTexture target)
    {
        _cam.targetTexture = target;
        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
        if (RenderPipeline.SupportsRenderRequest(_cam, request))
            RenderPipeline.SubmitRenderRequest(_cam, request);
        else
            _cam.Render();
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

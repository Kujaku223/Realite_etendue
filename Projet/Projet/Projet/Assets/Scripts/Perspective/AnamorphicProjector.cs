using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Point de vue P de l'anamorphose. Une caméra cachée rend le cube fantôme dans une
/// RenderTexture, puis envoie au shader « AnamorphicProjection » ce qu'il faut pour
/// reprojeter cette image sur les surfaces de la scène.
/// </summary>
[RequireComponent(typeof(Camera))]
public class AnamorphicProjector : MonoBehaviour
{
    public static readonly int VpId      = Shader.PropertyToID("_AnamorphVP");
    public static readonly int TexId     = Shader.PropertyToID("_AnamorphTex");
    public static readonly int PosId     = Shader.PropertyToID("_AnamorphPos");
    public static readonly int OpacityId = Shader.PropertyToID("_AnamorphOpacity");

    [Header("Cube fantôme")]
    [Tooltip("Racine du cube fantôme. Ses renderers ne sont activés que pendant le rendu du projecteur.")]
    [SerializeField] Transform ghostRoot;
    [Tooltip("Calque du cube fantôme (ex. « Anamorphose »). C'est le seul que cette caméra rend.")]
    [SerializeField] LayerMask ghostLayers;

    [Header("Texture")]
    [SerializeField] int resolution = 2048;
    [Tooltip("Refaire le rendu à chaque frame (si P ou le fantôme bougent). Coûteux sur Quest.")]
    [SerializeField] bool refreshEveryFrame;

    [Header("Affichage")]
    [Range(0f, 1f)] [SerializeField] float opacity = 1f;

    Camera _cam;
    RenderTexture _rt;
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
        if (_rt != null)
        {
            _rt.Release();
            Destroy(_rt);
        }
    }

    void OnValidate()
    {
        if (Application.isPlaying && isActiveAndEnabled)
            Shader.SetGlobalFloat(OpacityId, opacity);
    }

    /// <summary>Rend le fantôme dans la texture et met à jour les globales du shader.</summary>
    [ContextMenu("Refresh")]
    public void Refresh()
    {
        if (_rt == null) return;

        SetGhostVisible(true);
        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = _rt };
        if (RenderPipeline.SupportsRenderRequest(_cam, request))
            RenderPipeline.SubmitRenderRequest(_cam, request);
        else
            _cam.Render();
        SetGhostVisible(false);

        // Matrice « OpenGL » (pas GL.GetGPUProjectionMatrix) : NDC.xy ∈ [-1, 1] → UV = NDC * 0.5 + 0.5
        Shader.SetGlobalMatrix(VpId, _cam.projectionMatrix * _cam.worldToCameraMatrix);
        Shader.SetGlobalTexture(TexId, _rt);
        Shader.SetGlobalVector(PosId, transform.position);
        Shader.SetGlobalFloat(OpacityId, opacity);
    }

    void SetGhostVisible(bool visible)
    {
        foreach (var r in _ghostRenderers)
            if (r != null) r.enabled = visible;
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

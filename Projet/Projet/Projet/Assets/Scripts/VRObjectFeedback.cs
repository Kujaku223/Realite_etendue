using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>Owns hover outlines, grab cues, resize audio, and physical impact sounds.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(VRHitPointGrabInteractable))]
[DefaultExecutionOrder(20)]
public sealed class VRObjectFeedback : MonoBehaviour
{
    [SerializeField] VRFeedbackProfile m_Profile;

    sealed class OutlinePart
    {
        public Renderer source;
        public Renderer mask, shell, clear;
    }

    readonly List<OutlinePart> m_Parts = new List<OutlinePart>();
    readonly List<LODGroup> m_PatchedGroups = new List<LODGroup>();
    readonly HashSet<Renderer> m_OwnedRenderers = new HashSet<Renderer>();
    readonly List<GameObject> m_OwnedObjects = new List<GameObject>();
    readonly List<Material> m_Materials = new List<Material>();
    readonly Dictionary<Mesh, Mesh> m_ShellMeshes = new Dictionary<Mesh, Mesh>();
    XRGrabInteractable m_Interactable;
    VRTwoHandScale m_Scaler;
    Rigidbody m_Body;
    AudioSource m_GrabAudio, m_ImpactAudio, m_ResizeAudio;
    Vector3 m_LastScale;
    float m_LastScaleMotion = float.NegativeInfinity;
    float m_LastImpact = float.NegativeInfinity;
    bool m_OutlineBuilt;

    void Reset() => m_Profile = VRFeedbackProfile.Default;

    void Awake()
    {
        if (m_Profile == null)
            m_Profile = VRFeedbackProfile.Default;
        m_Interactable = GetComponent<XRGrabInteractable>();
        m_Scaler = GetComponent<VRTwoHandScale>();
        m_Body = GetComponent<Rigidbody>();
        m_GrabAudio = CreateAudio("Grab Sound");
        m_ImpactAudio = CreateAudio("Impact Sound");
        m_ResizeAudio = CreateAudio("Resize Sound");
        m_ResizeAudio.loop = true;
        m_LastScale = transform.lossyScale;
    }

    AudioSource CreateAudio(string name)
    {
        var child = new GameObject(name);
        child.transform.SetParent(transform, false);
        m_OwnedObjects.Add(child);
        var source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.minDistance = 1f;
        source.maxDistance = 15f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        return source;
    }

    void OnEnable()
    {
        m_Interactable.selectEntered.AddListener(OnGrab);
        m_LastScale = transform.lossyScale;
        m_LastScaleMotion = float.NegativeInfinity;
    }

    void OnGrab(SelectEnterEventArgs args)
    {
        if (m_Profile != null && m_Profile.grabClip != null)
            m_GrabAudio.PlayOneShot(m_Profile.grabClip, m_Profile.grabVolume);
    }

    void LateUpdate()
    {
        if (m_Profile == null)
            return;
        bool scaling = m_Scaler != null && m_Scaler.isActiveAndEnabled && m_Scaler.isScaling;
        bool outlined = m_Interactable.isActiveAndEnabled &&
            (m_Interactable.isHovered || m_Interactable.isSelected || scaling);
        if (outlined && !m_OutlineBuilt)
            BuildOutline();
        foreach (var part in m_Parts)
        {
            bool show = outlined && part.source != null && part.source.enabled && part.source.gameObject.activeInHierarchy;
            if (part.mask != null) part.mask.enabled = show;
            if (part.shell != null) part.shell.enabled = show;
            if (part.clear != null) part.clear.enabled = show;
        }

        Vector3 scale = transform.lossyScale;
        float relativeSpeed = (scale - m_LastScale).magnitude /
            Mathf.Max(0.0001f, m_LastScale.magnitude * Time.unscaledDeltaTime);
        if (scaling && relativeSpeed > 0.01f)
            m_LastScaleMotion = Time.unscaledTime;
        bool moving = scaling && Time.unscaledTime - m_LastScaleMotion < 0.1f;
        if (moving && m_Profile.resizeLoopClip != null)
        {
            m_ResizeAudio.clip = m_Profile.resizeLoopClip;
            m_ResizeAudio.volume = m_Profile.resizeVolume;
            if (!m_ResizeAudio.isPlaying) m_ResizeAudio.Play();
        }
        else if (m_ResizeAudio.isPlaying)
            m_ResizeAudio.Stop();
        m_LastScale = scale;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!isActiveAndEnabled || m_Profile == null || m_Profile.thudClip == null ||
            collision.contactCount == 0 || Time.unscaledTime - m_LastImpact < m_Profile.impactCooldown)
            return;
        float speed = collision.relativeVelocity.magnitude;
        if (speed < m_Profile.minimumImpactSpeed)
            return;
        m_LastImpact = Time.unscaledTime;
        m_ImpactAudio.transform.position = collision.GetContact(0).point;
        float strength = Mathf.Lerp(0.15f, 1f, Mathf.InverseLerp(m_Profile.minimumImpactSpeed,
            Mathf.Max(m_Profile.minimumImpactSpeed + 0.01f, m_Profile.fullVolumeImpactSpeed), speed));
        m_ImpactAudio.PlayOneShot(m_Profile.thudClip, m_Profile.thudVolume * strength);
    }

    Material CopyMaterial(Material source)
    {
        var material = new Material(source) { name = source.name + " (Runtime)" };
        material.SetColor("_OutlineColor", m_Profile.outlineColor);
        material.SetFloat("_OutlineWidth", m_Profile.outlineWidthPixels);
        m_Materials.Add(material);
        return material;
    }

    void BuildOutline()
    {
        m_OutlineBuilt = true;
        if (m_Profile.outlineMask == null || m_Profile.outlineShell == null || m_Profile.outlineClear == null)
            return;
        Material mask = CopyMaterial(m_Profile.outlineMask);
        Material shell = CopyMaterial(m_Profile.outlineShell);
        Material clear = CopyMaterial(m_Profile.outlineClear);
        // Snapshot before adding proxy renderers so they cannot recursively outline themselves.
        var sources = GetComponentsInChildren<Renderer>(true);
        foreach (var source in sources)
        {
            if (source.GetComponentInParent<XRBaseInteractable>(true) != m_Interactable ||
                source.GetComponentInParent<Rigidbody>(true) != m_Body)
                continue;
            Mesh mesh = source is SkinnedMeshRenderer skinned ? skinned.sharedMesh :
                source.TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null;
            if (mesh == null)
                continue;
            m_Parts.Add(new OutlinePart
            {
                source = source,
                mask = MakeProxy(source, mesh, mask),
                shell = MakeProxy(source, GetShellMesh(mesh), shell, !mesh.isReadable),
                clear = MakeProxy(source, mesh, clear),
            });
        }

        foreach (var group in GetComponentsInChildren<LODGroup>(true))
        {
            LOD[] lods = group.GetLODs();
            bool patched = false;
            for (int i = 0; i < lods.Length; ++i)
            {
                var renderers = new List<Renderer>(lods[i].renderers);
                foreach (var part in m_Parts)
                {
                    if (!renderers.Contains(part.source)) continue;
                    renderers.Add(part.mask); renderers.Add(part.shell); renderers.Add(part.clear);
                    patched = true;
                }
                lods[i].renderers = renderers.ToArray();
            }
            if (patched)
            {
                group.SetLODs(lods);
                m_PatchedGroups.Add(group);
            }
        }
    }

    Mesh GetShellMesh(Mesh source)
    {
        if (!source.isReadable) return source;
        if (m_ShellMeshes.TryGetValue(source, out var cached)) return cached;
        // Hard edges duplicate vertices. Average their normals only on our shell
        // so cube silhouettes join up without changing the original lighting.
        Vector3[] vertices = source.vertices;
        Vector3[] normals = source.normals;
        if (normals.Length != vertices.Length) return source;
        var sums = new Dictionary<Vector3, Vector3>();
        for (int i = 0; i < vertices.Length; ++i)
        {
            sums.TryGetValue(vertices[i], out var normal);
            sums[vertices[i]] = normal + normals[i];
        }
        for (int i = 0; i < normals.Length; ++i)
            normals[i] = sums[vertices[i]].normalized;
        var copy = Instantiate(source);
        copy.name = source.name + " (Outline Shell)";
        copy.normals = normals;
        m_ShellMeshes.Add(source, copy);
        return copy;
    }

    Renderer MakeProxy(Renderer source, Mesh mesh, Material material, bool radialNormals = false)
    {
        var child = new GameObject("Feedback Outline " + material.name);
        child.layer = source.gameObject.layer;
        child.transform.SetParent(source.transform, false);
        m_OwnedObjects.Add(child);
        Renderer proxy;
        if (source is SkinnedMeshRenderer skin)
        {
            var skinned = child.AddComponent<SkinnedMeshRenderer>();
            skinned.sharedMesh = mesh;
            skinned.bones = skin.bones;
            skinned.rootBone = skin.rootBone;
            skinned.localBounds = skin.localBounds;
            proxy = skinned;
        }
        else
        {
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            proxy = child.AddComponent<MeshRenderer>();
        }
        var materials = new Material[mesh.subMeshCount];
        for (int i = 0; i < materials.Length; ++i) materials[i] = material;
        proxy.sharedMaterials = materials;
        if (radialNormals)
        {
            // Imported meshes can disallow CPU reads. Use their bounds to keep
            // an outward shell without requiring Read/Write on source assets.
            var properties = new MaterialPropertyBlock();
            properties.SetVector("_OutlineCenter", mesh.bounds.center);
            properties.SetFloat("_OutlineRadialNormals", 1f);
            proxy.SetPropertyBlock(properties);
        }
        proxy.shadowCastingMode = ShadowCastingMode.Off;
        proxy.receiveShadows = false;
        proxy.lightProbeUsage = LightProbeUsage.Off;
        proxy.reflectionProbeUsage = ReflectionProbeUsage.Off;
        proxy.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        proxy.renderingLayerMask = source.renderingLayerMask;
        proxy.enabled = false;
        m_OwnedRenderers.Add(proxy);
        return proxy;
    }

    void OnDisable()
    {
        if (m_Interactable != null) m_Interactable.selectEntered.RemoveListener(OnGrab);
        foreach (var renderer in m_OwnedRenderers)
            if (renderer != null) renderer.enabled = false;
        if (m_GrabAudio != null) m_GrabAudio.Stop();
        if (m_ImpactAudio != null) m_ImpactAudio.Stop();
        if (m_ResizeAudio != null) m_ResizeAudio.Stop();
    }

    void OnDestroy()
    {
        foreach (var group in m_PatchedGroups)
        {
            if (group == null) continue;
            LOD[] lods = group.GetLODs();
            for (int i = 0; i < lods.Length; ++i)
            {
                var renderers = new List<Renderer>(lods[i].renderers);
                renderers.RemoveAll(renderer => m_OwnedRenderers.Contains(renderer));
                lods[i].renderers = renderers.ToArray();
            }
            group.SetLODs(lods);
        }
        foreach (var child in m_OwnedObjects)
            if (child != null) Destroy(child);
        foreach (var material in m_Materials)
            if (material != null) Destroy(material);
        foreach (var mesh in m_ShellMeshes.Values)
            if (mesh != null) Destroy(mesh);
    }
}

using System;
using Unity.InferenceEngine;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class HandTracker : MonoBehaviour
{
    public const int ThumbTip = 4;
    public const int IndexTip = 8;

    const int k_NumAnchors = 2016;
    const int k_NumKeypoints = 21;
    const int k_DetectorInputSize = 192;
    const int k_LandmarkerInputSize = 224;

    [SerializeField]
    ModelAsset m_HandDetector;

    [SerializeField]
    ModelAsset m_HandLandmarker;

    [SerializeField]
    TextAsset m_AnchorsCSV;

    [SerializeField]
    ARCameraBackground m_CameraBackground;

    [SerializeField]
    float m_ScoreThreshold = 0.5f;

    [SerializeField]
    int m_CaptureHeight = 640;

    public bool HandDetected { get; private set; }

    public Vector2[] KeypointsScreen { get; } = new Vector2[k_NumKeypoints];

    float[,] m_Anchors;
    Worker m_DetectorWorker;
    Worker m_LandmarkerWorker;
    Tensor<float> m_DetectorInput;
    Tensor<float> m_LandmarkerInput;
    RenderTexture m_CameraTexture;

    async void Start()
    {
        m_Anchors = BlazeUtils.LoadAnchors(m_AnchorsCSV.text, k_NumAnchors);

        var detectorModel = ModelLoader.Load(m_HandDetector);
        var graph = new FunctionalGraph();
        var input = graph.AddInput(detectorModel, 0);
        var outputs = Functional.Forward(detectorModel, input);
        var filtered = BlazeUtils.ArgMaxFiltering(outputs[0], outputs[1]);
        detectorModel = graph.Compile(filtered.Item1, filtered.Item2, filtered.Item3);

        m_DetectorWorker = new Worker(detectorModel, BackendType.GPUCompute);
        m_LandmarkerWorker = new Worker(ModelLoader.Load(m_HandLandmarker), BackendType.GPUCompute);

        m_DetectorInput = new Tensor<float>(new TensorShape(1, k_DetectorInputSize, k_DetectorInputSize, 3));
        m_LandmarkerInput = new Tensor<float>(new TensorShape(1, k_LandmarkerInputSize, k_LandmarkerInputSize, 3));

        while (true)
        {
            try
            {
                await Awaitable.NextFrameAsync(destroyCancellationToken);
                await DetectFromCamera();
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        m_DetectorWorker.Dispose();
        m_LandmarkerWorker.Dispose();
        m_DetectorInput.Dispose();
        m_LandmarkerInput.Dispose();
        if (m_CameraTexture != null)
            m_CameraTexture.Release();
    }

    void EnsureCameraTexture()
    {
        var width = Mathf.RoundToInt(m_CaptureHeight * (float)Screen.width / Screen.height);
        if (m_CameraTexture != null && m_CameraTexture.width == width && m_CameraTexture.height == m_CaptureHeight)
            return;

        if (m_CameraTexture != null)
            m_CameraTexture.Release();
        m_CameraTexture = new RenderTexture(width, m_CaptureHeight, 0, RenderTextureFormat.ARGB32);
    }

    async Awaitable DetectFromCamera()
    {
        var material = m_CameraBackground.material;
        if (material == null)
            return;

        EnsureCameraTexture();
        Graphics.Blit(null, m_CameraTexture, material);

        var texture = m_CameraTexture;
        var size = Mathf.Max(texture.width, texture.height);
        var scale = size / (float)k_DetectorInputSize;
        var M = BlazeUtils.mul(
            BlazeUtils.TranslationMatrix(0.5f * (new Vector2(texture.width, texture.height) + new Vector2(-size, size))),
            BlazeUtils.ScaleMatrix(new Vector2(scale, -scale)));
        BlazeUtils.SampleImageAffine(texture, m_DetectorInput, M);

        m_DetectorWorker.Schedule(m_DetectorInput);

        using var outputIdx = await (m_DetectorWorker.PeekOutput(0) as Tensor<int>).ReadbackAndCloneAsync();
        using var outputScore = await (m_DetectorWorker.PeekOutput(1) as Tensor<float>).ReadbackAndCloneAsync();
        using var outputBox = await (m_DetectorWorker.PeekOutput(2) as Tensor<float>).ReadbackAndCloneAsync();

        HandDetected = outputScore[0] >= m_ScoreThreshold;
        if (!HandDetected)
            return;

        var idx = outputIdx[0];
        var anchorPosition = k_DetectorInputSize * new float2(m_Anchors[idx, 0], m_Anchors[idx, 1]);

        var boxCentre = anchorPosition + new float2(outputBox[0, 0, 0], outputBox[0, 0, 1]);
        var boxSize = math.max(outputBox[0, 0, 2], outputBox[0, 0, 3]);

        var kp0 = anchorPosition + new float2(outputBox[0, 0, 4], outputBox[0, 0, 5]);
        var kp2 = anchorPosition + new float2(outputBox[0, 0, 8], outputBox[0, 0, 9]);
        var delta = kp2 - kp0;
        var up = delta / math.length(delta);
        var rotation = 0.5f * Mathf.PI - math.atan2(delta.y, delta.x);
        boxCentre += 0.5f * boxSize * up;
        boxSize *= 2.6f;

        var origin = new float2(0.5f * k_LandmarkerInputSize, 0.5f * k_LandmarkerInputSize);
        var boxScale = boxSize / k_LandmarkerInputSize;
        var M2 = BlazeUtils.mul(M, BlazeUtils.mul(BlazeUtils.mul(BlazeUtils.mul(
            BlazeUtils.TranslationMatrix(boxCentre),
            BlazeUtils.ScaleMatrix(new float2(boxScale, -boxScale))),
            BlazeUtils.RotationMatrix(rotation)),
            BlazeUtils.TranslationMatrix(-origin)));
        BlazeUtils.SampleImageAffine(texture, m_LandmarkerInput, M2);

        m_LandmarkerWorker.Schedule(m_LandmarkerInput);
        using var landmarks = await (m_LandmarkerWorker.PeekOutput("Identity") as Tensor<float>).ReadbackAndCloneAsync();

        var toScreen = (float)Screen.height / texture.height;
        for (var i = 0; i < k_NumKeypoints; i++)
        {
            var imagePosition = BlazeUtils.mul(M2, new float2(landmarks[3 * i], landmarks[3 * i + 1]));
            KeypointsScreen[i] = new Vector2(imagePosition.x, imagePosition.y) * toScreen;
        }
    }
}

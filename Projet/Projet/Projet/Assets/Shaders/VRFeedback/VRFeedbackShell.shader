Shader "VR/Feedback Outline Shell"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _OutlineWidth ("Width in Pixels", Float) = 2
        [HideInInspector] _OutlineCenter ("Mesh Center", Vector) = (0,0,0,0)
        [HideInInspector] _OutlineRadialNormals ("Use Radial Normals", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="AlphaTest+2" }
        Pass
        {
            Name "FeedbackShell"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite Off
            ZTest LEqual
            ColorMask RGBA
            Stencil { Ref 8 ReadMask 8 WriteMask 0 Comp NotEqual Pass Keep }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON
            #define OUTLINE_SHELL 1
            #include "VRFeedbackOutline.hlsl"
            ENDHLSL
        }
    }
    Fallback Off
}

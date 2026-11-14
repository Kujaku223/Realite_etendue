#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
CBUFFER_START(UnityPerMaterial)
    half4 _OutlineColor;
    float _OutlineWidth;
    float4 _OutlineCenter;
    float _OutlineRadialNormals;
CBUFFER_END
struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};
struct Varyings
{
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_OUTPUT_STEREO
};
Varyings Vert(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
#if defined(OUTLINE_SHELL)
    float3 shellNormal = _OutlineRadialNormals > 0.5 ?
        input.positionOS.xyz - _OutlineCenter.xyz : input.normalOS;
    float3 normalWS = TransformObjectToWorldNormal(shellNormal);
    float2 direction = TransformWorldToViewDir(normalWS, true).xy;
    float lengthSquared = dot(direction, direction);
    direction *= rsqrt(max(lengthSquared, 0.000001));
    output.positionCS.xy += direction * (2.0 * _OutlineWidth / _ScaledScreenParams.xy) * output.positionCS.w;
#endif
    return output;
}
half4 Frag(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    return _OutlineColor;
}

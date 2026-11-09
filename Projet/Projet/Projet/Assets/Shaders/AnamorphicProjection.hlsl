#ifndef ANAMORPHIC_PROJECTION_INCLUDED
#define ANAMORPHIC_PROJECTION_INCLUDED

// Globales envoyées par AnamorphicProjector.cs.
// Déclarées hors CBUFFER : compatible avec le SRP Batcher.
// Sans projecteur dans la scène : matrice nulle → Mask = 0 → aucun effet.
float4x4 _AnamorphVP;
float3   _AnamorphPos;
float    _AnamorphOpacity;
TEXTURE2D(_AnamorphTex);
SAMPLER(sampler_AnamorphTex);

// À utiliser dans un nœud Custom Function de Shader Graph :
//   Type = File, Name = AnamorphicProjection
//   Entrées  : PositionWS (Vector3), NormalWS (Vector3)
//   Sorties  : Color (Vector3), Mask (Float)
void AnamorphicProjection_float(float3 PositionWS, float3 NormalWS, out float3 Color, out float Mask)
{
    float4 clipPos = mul(_AnamorphVP, float4(PositionWS, 1.0));
    float  w       = max(clipPos.w, 1e-5);
    float2 uv      = clipPos.xy / w * 0.5 + 0.5;
    // Si le dessin apparaît à l'envers sur une plateforme : uv.y = 1.0 - uv.y;

    // Devant le projecteur et dans son champ de vision
    float inFront = step(1e-5, clipPos.w);
    float inside  = step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);

    // Seulement sur les faces tournées vers P (pas de dessin au dos des murs)
    float3 toProj = normalize(_AnamorphPos - PositionWS);
    float  facing = saturate(dot(normalize(NormalWS), toProj) * 5.0);

    float4 c = SAMPLE_TEXTURE2D(_AnamorphTex, sampler_AnamorphTex, saturate(uv));
    Color = c.rgb;
    Mask  = c.a * inFront * inside * facing * _AnamorphOpacity;
}

#endif

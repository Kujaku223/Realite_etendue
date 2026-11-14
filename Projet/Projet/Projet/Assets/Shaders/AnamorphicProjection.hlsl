#ifndef ANAMORPHIC_PROJECTION_INCLUDED
#define ANAMORPHIC_PROJECTION_INCLUDED

// Globales envoyées par AnamorphicProjector.cs.
// Déclarées hors CBUFFER : compatible avec le SRP Batcher.
// Sans projecteur dans la scène : matrice nulle → Mask = 0 → aucun effet.
float4x4 _AnamorphVP;
float3   _AnamorphPos;
float    _AnamorphOpacity;
float4   _AnamorphDepthParams;  // x = near, y = far, z = marge (m), w = 1 si occlusion active
float    _AnamorphTexelScale;   // taille d'un texel de la carte de profondeur à 1 m de P
TEXTURE2D(_AnamorphTex);
SAMPLER(sampler_AnamorphTex);
TEXTURE2D_FLOAT(_AnamorphDepth);
SAMPLER(sampler_AnamorphDepth);

// 1 si ce point est la première surface vue depuis P, 0 s'il est caché derrière un autre objet.
// eyeDepth = distance du point à P le long de l'axe du projecteur ; cosTheta = cos(normale, direction vers P).
float AnamorphicVisibility(float2 uv, float eyeDepth, float cosTheta)
{
    float raw = SAMPLE_TEXTURE2D_LOD(_AnamorphDepth, sampler_AnamorphDepth, uv, 0).r;
#if UNITY_REVERSED_Z
    raw = 1.0 - raw;
#endif
    // Profondeur brute [0, 1] → distance en mètres
    float n = _AnamorphDepthParams.x;
    float f = _AnamorphDepthParams.y;
    float sceneDepth = n * f / max(f - raw * (f - n), 1e-6);

    // Marge : constante + proportionnelle à la taille d'un texel, plus grande sur les surfaces rasantes
    float tanTheta = sqrt(saturate(1.0 - cosTheta * cosTheta)) / max(cosTheta, 0.1);
    float bias = _AnamorphDepthParams.z + eyeDepth * (0.002 + _AnamorphTexelScale * tanTheta);

    float visible = step(eyeDepth, sceneDepth + bias);
    visible = max(visible, step(0.99999, raw));        // rien dans la carte → rien ne cache
    return lerp(1.0, visible, _AnamorphDepthParams.w); // occlusion désactivée → toujours visible
}

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
    float3 toProj   = normalize(_AnamorphPos - PositionWS);
    float  cosTheta = dot(normalize(NormalWS), toProj);
    float  facing   = saturate(cosTheta * 5.0);

    // Seulement sur la première surface touchée depuis P
    float visible = AnamorphicVisibility(saturate(uv), w, saturate(cosTheta));

    float4 c = SAMPLE_TEXTURE2D(_AnamorphTex, sampler_AnamorphTex, saturate(uv));
    Color = c.rgb;
    Mask  = c.a * inFront * inside * facing * visible * _AnamorphOpacity;
}

#endif

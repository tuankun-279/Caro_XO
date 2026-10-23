// Ink pieces that can be drawn along the pen path and erased diagonally.
// Piece texture: A = ink coverage, R = drawing order (0 = where the pen starts, 1 = where it ends).
// _Reveal 0 → 1 draws the stroke progressively; colour comes from SpriteRenderer.color.
// _Erase  0 → 1 sweeps an eraser from the sprite's top-left corner to its bottom-right corner
//         (values below 0 leave the piece untouched).
Shader "Caro/InkReveal"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Reveal ("Reveal", Range(0, 1.2)) = 1.2
        _Soft ("Pen tip softness", Range(0.001, 0.2)) = 0.03
        _Erase ("Erase", Range(-1, 1.2)) = -1
        _EraseSoft ("Eraser edge softness", Range(0.001, 0.3)) = 0.06
        _EraseRough ("Eraser edge roughness", Range(0, 0.1)) = 0.025
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float _Reveal;
                float _Soft;
                float _Erase;
                float _EraseSoft;
                float _EraseRough;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);

                // Pen: only the part of the stroke whose drawing order is behind the pen tip is visible.
                half drawn = saturate((_Reveal - t.r) / _Soft);

                // Eraser: distance along the top-left → bottom-right diagonal (0 at top-left, 1 at bottom-right),
                // with a wobbly edge along the perpendicular so it looks rubbed out rather than cut.
                float along = (i.uv.x + (1.0 - i.uv.y)) * 0.5;
                float across = i.uv.x - (1.0 - i.uv.y);
                float wobble = (sin(across * 41.0) * 0.6 + sin(across * 97.0 + 1.3) * 0.4) * _EraseRough;
                half kept = saturate((along + wobble - _Erase) / _EraseSoft);

                return half4(i.color.rgb, i.color.a * t.a * drawn * kept);
            }
            ENDHLSL
        }
    }
}

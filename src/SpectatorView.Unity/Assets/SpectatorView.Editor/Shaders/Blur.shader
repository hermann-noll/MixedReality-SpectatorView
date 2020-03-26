// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License. See LICENSE in the project root for license information.

Shader "SV/Blur"
{
    Properties
    {
        _MaskTexture("MaskTexture", 2D) = "white" {}
        _BlurSize("BlurSize", Range(0, 10)) = 5
        [KeywordEnum(Horizontal, Vertical)] Blur("Blur direction", Float) = 1
    }

    Category
    {
        // We must be transparent, so other objects are drawn before this one.
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Opaque" }


        SubShader
        {
            // No culling or depth
            Cull Off ZWrite Off ZTest Always
            // Horizontal blur
            Pass
            {
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #pragma multi_compile_local BLUR_HORIZONTAL BLUR_VERTICAL

                #include "UnityCG.cginc"

                struct appdata
                {
                    float4 vertex : POSITION;
                    float2 uv : TEXCOORD0;
                };

                struct v2f
                {
                    float2 uv : TEXCOORD0;
                    float4 vertex : SV_POSITION;
                };

                v2f vert(appdata v)
                {
                    v2f o;
                    o.vertex = UnityObjectToClipPos(v.vertex);
                    o.uv = v.uv;
                    return o;
                }

                float4 _MaskTexture_TexelSize;
                Texture2D _MaskTexture;
                SamplerState sampler_point_clamp;
                float _BlurSize;

                fixed4 frag(v2f i) : SV_Target
                {
                    float sum = 0.0;

                    #ifdef BLUR_HORIZONTAL
                        #define SAMPLE_POS(kernel) float2(i.uv[0] - kernel * _BlurSize * _MaskTexture_TexelSize.x, i.uv[1])
                    #else
                        #define SAMPLE_POS(kernel) float2(i.uv[0], i.uv[1] - kernel * _BlurSize * _MaskTexture_TexelSize.y)
                    #endif
                    #define MASKSAMPLE(weight, kernel) _MaskTexture.Sample(sampler_point_clamp, SAMPLE_POS(kernel)).r * weight;

                    sum += MASKSAMPLE(0.025, -5.0);
                    sum += MASKSAMPLE(0.05, -4.0);
                    sum += MASKSAMPLE(0.085, -3.0);
                    sum += MASKSAMPLE(0.09, -2.0);
                    sum += MASKSAMPLE(0.15, -1.0);
                    sum += MASKSAMPLE(0.2, 0.0);
                    sum += MASKSAMPLE(0.15, 1.0);
                    sum += MASKSAMPLE(0.09, 2.0);
                    sum += MASKSAMPLE(0.085, 3.0);
                    sum += MASKSAMPLE(0.05, 4.0);
                    sum += MASKSAMPLE(0.025, 5.0);

                    return half4(sum, 0, 0, 0);
                }
                ENDCG
            }

        }
    }
}

Shader "Custom/DoomScreenBlit"
{
    Properties
    {
        _MainTex ("Source Texture", 2D) = "black" {}
        [KeywordEnum(None, Rotate90CW, Rotate180, Rotate270CW)] _Rotation ("Screen Rotation", Float) = 1
        [Toggle] _FlipX ("Flip Horizontal", Float) = 0
        [Toggle] _FlipY ("Flip Vertical", Float) = 0
        _Zoom ("Zoom / Scale", Float) = 1.0
        _AspectCorrection ("Aspect Ratio Stretch", Float) = 1.0
        _OffsetX ("Offset X", Float) = 0.0
        _OffsetY ("Offset Y", Float) = 0.0
        [Range(0, 1)] _ScanlineIntensity ("Scanline Intensity", Float) = 0.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "DoomBlit"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ROTATION_NONE _ROTATION_ROTATE90CW _ROTATION_ROTATE180 _ROTATION_ROTATE270CW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float _FlipX;
            float _FlipY;
            float _Zoom;
            float _AspectCorrection;
            float _OffsetX;
            float _OffsetY;
            float _ScanlineIntensity;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                // Center around (0.5, 0.5)
                float2 c = uv - 0.5;

                // Apply Flips
                if (_FlipX > 0.5) c.x = -c.x;
                if (_FlipY > 0.5) c.y = -c.y;

                // Apply Rotation
                #if defined(_ROTATION_ROTATE90CW)
                    c = float2(c.y, -c.x);
                #elif defined(_ROTATION_ROTATE180)
                    c = float2(-c.x, -c.y);
                #elif defined(_ROTATION_ROTATE270CW)
                    c = float2(-c.y, c.x);
                #endif

                // Apply Zoom & Aspect
                float zoom = max(_Zoom, 0.001);
                c /= zoom;

                float aspect = max(_AspectCorrection, 0.001);
                c.x *= aspect;

                // Apply Offset
                c -= float2(_OffsetX, _OffsetY);

                float2 finalUV = c + 0.5;

                // Check bounds (letterbox if rotated aspect falls outside)
                if (finalUV.x < 0.0 || finalUV.x > 1.0 || finalUV.y < 0.0 || finalUV.y > 1.0)
                {
                    return float4(0, 0, 0, 1);
                }

                float4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, finalUV);

                // Optional subtle scanlines
                if (_ScanlineIntensity > 0.01)
                {
                    float scanline = sin(finalUV.y * 200.0 * 3.14159);
                    col.rgb -= col.rgb * (scanline * 0.5 + 0.5) * _ScanlineIntensity;
                }

                return col;
            }
            ENDHLSL
        }
    }
    FallBack Off
}

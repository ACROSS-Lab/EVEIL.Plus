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

        [Header(Transition Effect)]
        [KeywordEnum(None, CrtPowerOn, DigitalGlitch, RetroStatic)] _TransitionType ("Transition Effect", Float) = 1
        _TransitionProgress ("Transition Progress", Range(0, 1)) = 1.0
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
            #pragma multi_compile_local _TRANSITIONTYPE_NONE _TRANSITIONTYPE_CRTPOWERON _TRANSITIONTYPE_DIGITALGLITCH _TRANSITIONTYPE_RETROSTATIC

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
            float _TransitionProgress;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float rand(float2 co)
            {
                return frac(sin(dot(co, float2(12.9898, 78.233))) * 43758.5453);
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

                // -------------------------------------------------------------
                // TRANSITION: CRT POWER-ON / OFF
                // -------------------------------------------------------------
                #if defined(_TRANSITIONTYPE_CRTPOWERON)
                if (_TransitionProgress < 0.999)
                {
                    // Phase 1: Horizontal slit expands (0.0 to 0.3)
                    float hProgress = saturate(_TransitionProgress / 0.3);
                    // Phase 2: Vertical expansion (0.2 to 0.95)
                    float vProgress = saturate((_TransitionProgress - 0.2) / 0.75);

                    float hSpread = smoothstep(0.0, 1.0, hProgress) * 0.5;
                    float vSpread = smoothstep(0.0, 1.0, vProgress) * 0.5;

                    // Keep a thin visible horizontal beam line even before vertical expansion
                    vSpread = max(vSpread, 0.006 * smoothstep(0.02, 0.15, _TransitionProgress));

                    if (abs(c.x) > hSpread || abs(c.y) > vSpread)
                    {
                        return float4(0, 0, 0, 1);
                    }

                    // CRT line jitter on warmup
                    float slice = floor(uv.y * 30.0);
                    float jitter = rand(float2(slice, floor(_Time.y * 24.0)));
                    if (jitter > 0.75 && _TransitionProgress < 0.7)
                    {
                        finalUV.x += (jitter - 0.75) * 0.04 * (1.0 - _TransitionProgress);
                    }

                    // Check bounds
                    if (finalUV.x < 0.0 || finalUV.x > 1.0 || finalUV.y < 0.0 || finalUV.y > 1.0)
                    {
                        return float4(0, 0, 0, 1);
                    }

                    float4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, finalUV);

                    // Phosphor bloom flash on boot
                    float flash = (1.0 - _TransitionProgress) * 1.5;
                    col.rgb += flash * float3(0.7, 1.0, 0.8);

                    // Bright horizontal center beam glow
                    float beamGlow = (1.0 - vProgress) * 2.5 * smoothstep(0.03, 0.0, abs(c.y));
                    col.rgb += beamGlow;

                    return col;
                }
                #endif

                // -------------------------------------------------------------
                // TRANSITION: DIGITAL GLITCH / SIGNAL INTERRUPT
                // -------------------------------------------------------------
                #if defined(_TRANSITIONTYPE_DIGITALGLITCH)
                if (_TransitionProgress < 0.999)
                {
                    float glitch = (1.0 - _TransitionProgress);
                    float slice = floor(uv.y * 22.0);
                    float noise = rand(float2(slice, floor(_Time.y * 25.0)));

                    float displacement = (noise > 0.6) ? (noise - 0.6) * 0.2 * glitch : 0.0;

                    // RGB Channel Separation (Chromatic Aberration Burst)
                    float2 uvR = finalUV + float2(displacement + 0.03 * glitch, 0.0);
                    float2 uvG = finalUV + float2(displacement, 0.0);
                    float2 uvB = finalUV + float2(displacement - 0.03 * glitch, 0.0);

                    float r = (uvR.x >= 0.0 && uvR.x <= 1.0 && uvR.y >= 0.0 && uvR.y <= 1.0) ? SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvR).r : 0.0;
                    float g = (uvG.x >= 0.0 && uvG.x <= 1.0 && uvG.y >= 0.0 && uvG.y <= 1.0) ? SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvG).g : 0.0;
                    float b = (uvB.x >= 0.0 && uvB.x <= 1.0 && uvB.y >= 0.0 && uvB.y <= 1.0) ? SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvB).b : 0.0;

                    float4 col = float4(r, g, b, 1.0);

                    // Static snow bars
                    float snow = rand(uv * 100.0 + _Time.yy * 40.0);
                    if (snow > 0.65)
                    {
                        col.rgb = lerp(col.rgb, float3(snow, snow, snow), glitch * 0.5);
                    }

                    col.rgb *= smoothstep(0.0, 0.2, _TransitionProgress);
                    return col;
                }
                #endif

                // -------------------------------------------------------------
                // TRANSITION: RETRO STATIC SNOW
                // -------------------------------------------------------------
                #if defined(_TRANSITIONTYPE_RETROSTATIC)
                if (_TransitionProgress < 0.999)
                {
                    float snow = rand(uv * 120.0 + _Time.yy * 50.0);
                    float staticAmount = (1.0 - _TransitionProgress);

                    if (finalUV.x < 0.0 || finalUV.x > 1.0 || finalUV.y < 0.0 || finalUV.y > 1.0)
                    {
                        return float4(0, 0, 0, 1);
                    }

                    float4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, finalUV);
                    col.rgb = lerp(col.rgb, float3(snow, snow, snow), staticAmount);
                    col.rgb *= smoothstep(0.0, 0.15, _TransitionProgress);
                    return col;
                }
                #endif

                // -------------------------------------------------------------
                // NORMAL GAMEPLAY RENDERING (Post-Transition)
                // -------------------------------------------------------------
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

Shader "Painting"
{
    Properties
    {
        _MainTex ("MainTex", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Rotation ("Rotation", Float) = 0
        _InkDiffusion ("Ink Diffusion", Range(0, 1)) = 0.5
        _InkEdgeHardness ("Ink Edge Hardness", Range(0.1, 2)) = 1
        _PressureEffect ("Pressure Effect", Range(0, 1)) = 0.5
        _HoldTimeEffect ("Hold Time Effect", Range(0, 1)) = 0
        _TipSharpness ("Tip Sharpness", Range(1, 3)) = 1
        _BrushStretch ("Brush Stretch", Range(1, 2.5)) = 1
        _Speed ("Speed", Range(0, 1)) = 0
        _BrushDirection ("Brush Direction", Vector) = (1,0,0,0)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Fog { Mode Off }
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 worldPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float _Rotation;
            float _InkDiffusion;
            float _InkEdgeHardness;
            float _PressureEffect;
            float _HoldTimeEffect;
            float _TipSharpness;
            float _BrushStretch;
            float _Speed;
            float2 _BrushDirection;

            // 노이즈 함수
            float rand(float2 co)
            {
                return frac(sin(dot(co.xy ,float2(12.9898,78.233))) * 43758.5453);
            }

            float2 rand2(float2 st)
            {
                st = float2(dot(st,float2(127.1,311.7)),
                           dot(st,float2(269.5,183.3)));
                return -1.0 + 2.0 * frac(sin(st) * 43758.5453123);
            }

            // Perlin 노이즈
            float perlinNoise(float2 st) 
            {
                float2 p = floor(st);
                float2 f = frac(st);
                float2 u = f * f * (3.0 - 2.0 * f);

                float v00 = rand2(p + float2(0,0));
                float v10 = rand2(p + float2(1,0));
                float v01 = rand2(p + float2(0,1));
                float v11 = rand2(p + float2(1,1));

                return lerp(
                    lerp(dot(v00, f - float2(0,0)), dot(v10, f - float2(1,0)), u.x),
                    lerp(dot(v01, f - float2(0,1)), dot(v11, f - float2(1,1)), u.x),
                    u.y) + 0.5;
            }

            float2 rotateUV(float2 uv, float rotation)
            {
                float sine = sin(rotation * 0.0174533);
                float cosine = cos(rotation * 0.0174533);
                
                float2 pivot = float2(0.5, 0.5);
                float2 rotated = uv - pivot;
                float2 result;
                result.x = rotated.x * cosine - rotated.y * sine;
                result.y = rotated.x * sine + rotated.y * cosine;
                result += pivot;
                
                return result;
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = rotateUV(v.uv, _Rotation);
                o.worldPos = v.vertex.xy;
                return o;
            }

            float4 ApplyBrushEffects(float4 color, float2 uv)
            {
                // 기본 거리 계산
                float2 center = float2(0.5, 0.5);
                float2 dir = normalize(_BrushDirection);
                
                // 붓 방향에 따른 UV 변형
                float2 stretchedUV = (uv - center) / float2(1, _BrushStretch) + center;
                
                // 붓 끝 효과
                float tipDistance = distance(stretchedUV, center);
                float tipEffect = pow(tipDistance * 2, _TipSharpness);
                
                // 방향성을 가진 알파 계산
                float dirDot = dot(normalize(stretchedUV - center), dir);
                float dirMask = pow(max(0, dirDot), _TipSharpness);
                
                // 속도에 따른 끝 부분 뾰족함 강화
                float speedEffect = lerp(1, 2, _Speed);
                float tipMask = 1 - saturate(tipEffect * speedEffect);
                
                // 방향성 마스크와 결합
                float finalMask = lerp(tipMask, tipMask * dirMask, _Speed * 0.7);
                
                // 붓털 텍스처 효과
                float bristlePattern = perlinNoise(uv * 20.0 + _Time.xy * 0.1);
                float bristleEffect = lerp(1, bristlePattern, _Speed * 0.5);
                
                // 잉크 확산 효과
                float spreadNoise = perlinNoise(uv * 3.0 + _Time.xy * 0.1);
                float spread = lerp(1, 0.7, _Speed) + spreadNoise * 0.3 * (1 - _Speed);
                
                // 압력과 시간에 따른 효과
                float pressureSpread = lerp(1, 0.7, _PressureEffect);
                float timeSpread = lerp(1, 1.3, _HoldTimeEffect);
                
                color.a *= finalMask * spread * bristleEffect * pressureSpread * timeSpread;
                
                return color;
            }

            float4 ApplyInkEffects(float4 color, float2 uv)
            {
                // 기본 잉크 확산
                float spread = saturate(1.0 - length(uv - 0.5) * 2.0);
                spread = pow(spread, _InkEdgeHardness);

                // 잉크 번짐 효과
                float inkBleed = perlinNoise(uv * 10.0 + _Time.xy) * _HoldTimeEffect * 0.3;
                
                // 시간에 따른 잉크 퍼짐
                float timeSpread = perlinNoise(uv * 5.0 + _Time.xy * 0.5) * _HoldTimeEffect;
                
                color.a *= spread * (1.0 + inkBleed + timeSpread * 0.3);
                
                return color;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float4 texColor = tex2D(_MainTex, i.uv);
                float4 col = texColor * _Color;
                
                // 붓 효과 적용
                col = ApplyBrushEffects(col, i.uv);
                
                // 잉크 효과 적용
                col = ApplyInkEffects(col, i.uv);
                
                // 최종 색상 조정
                col.rgb *= col.a;
                
                return col;
            }
            ENDCG
        }
    }
}
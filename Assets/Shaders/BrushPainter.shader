Shader "Custom/BrushPainter"
{
    Properties
    {
        _MainTex("Base (RenderTexture)", 2D) = "white" {}
        _BrushTex("Brush Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent"}
        Pass
        {
            // GPU상에서 (SrcAlpha, OneMinusSrcAlpha)로 합성하되,
            // fragment에서 수동으로 기존 픽셀을 샘플링해서 섞을 수도 있음
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

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
            };

            sampler2D _MainTex;   // 이미 그려진 텍스처(기존 그림)
            sampler2D _BrushTex;  // 새로 찍을 브러시
            // 만약 오프셋이나 회전을 주고 싶다면 추가 파라미터 (예: _BrushOffset) 선언 가능

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 1) 기존 그림 색상
                fixed4 baseColor = tex2D(_MainTex, i.uv);
                // 2) 브러시 텍스처 색상
                fixed4 brushColor = tex2D(_BrushTex, i.uv);

                // 3) 알파 합성 (단순 예: brushColor만큼 덮어쓰기)
                //    final = (1 - brushAlpha)*base + brushColor
                fixed4 finalColor = lerp(baseColor, brushColor, brushColor.a);

                return finalColor;
            }
            ENDCG
        }
    }
}

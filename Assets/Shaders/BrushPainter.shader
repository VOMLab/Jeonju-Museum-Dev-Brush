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
            // GPU�󿡼� (SrcAlpha, OneMinusSrcAlpha)�� �ռ��ϵ�,
            // fragment���� �������� ���� �ȼ��� ���ø��ؼ� ���� ���� ����
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

            sampler2D _MainTex;   // �̹� �׷��� �ؽ�ó(���� �׸�)
            sampler2D _BrushTex;  // ���� ���� �귯��
            // ���� �������̳� ȸ���� �ְ� �ʹٸ� �߰� �Ķ���� (��: _BrushOffset) ���� ����

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 1) ���� �׸� ����
                fixed4 baseColor = tex2D(_MainTex, i.uv);
                // 2) �귯�� �ؽ�ó ����
                fixed4 brushColor = tex2D(_BrushTex, i.uv);

                // 3) ���� �ռ� (�ܼ� ��: brushColor��ŭ �����)
                //    final = (1 - brushAlpha)*base + brushColor
                fixed4 finalColor = lerp(baseColor, brushColor, brushColor.a);

                return finalColor;
            }
            ENDCG
        }
    }
}

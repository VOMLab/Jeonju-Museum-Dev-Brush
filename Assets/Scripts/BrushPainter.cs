using UnityEditor.ShaderGraph.Internal;
using UnityEditor.TerrainTools;
using UnityEngine;

public class BrushPainter : MonoBehaviour
{
    [SerializeField] private RenderTexture targetTexture;
    [SerializeField] private Material paintMaterial;
    [SerializeField] private Texture2D[] brushTextures;
    [SerializeField] private float minBrushSize = 5f;
    [SerializeField] private float maxBrushSize = 50f;

    private Vector2 _lastUVPos;
    private bool _isDrawing = false;
    private Camera _mainCam;

    private float _lastTime;
    private Vector2 _lastScreenPos;
    private float _brushSizeCurrent;

    void Start()
    {
        _mainCam = Camera.main;

        Graphics.SetRenderTarget(targetTexture);
        GL.Clear(true, true, Color.clear);
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetMouseButtonDown(0))
        {
            _isDrawing = true;
            _lastScreenPos = Input.mousePosition;
            _lastTime = Time.time;

            Vector2 uv = GetUVPosition(Input.mousePosition);
            _lastUVPos = uv;
            _brushSizeCurrent = minBrushSize;

            DrawBrush(uv, _brushSizeCurrent);
        }
        else if (Input.GetMouseButtonUp(0))
        {
            _isDrawing = false;
        }
        else if (Input.GetMouseButton(0) && _isDrawing)
        {
            float dt = Time.time - _lastTime;
            Vector2 currentScreenPos = (Vector2)Input.mousePosition;
            float distance = Vector2.Distance(currentScreenPos, _lastScreenPos);
            float vel = distance / (dt + 0.0001f);

            float newBrushSize = Mathf.Lerp(maxBrushSize, minBrushSize, vel / 1000f);
            newBrushSize = Mathf.Clamp(newBrushSize, minBrushSize, maxBrushSize);

            _brushSizeCurrent = Mathf.Lerp(_brushSizeCurrent, newBrushSize, 0.1f);

            Vector2 uv = GetUVPosition(Input.mousePosition);

            float step = 1.0f / 10;

            for (float t = 0; t < 1; t += step)
            {
                Vector2 interp = Vector2.Lerp(_lastUVPos, uv, t);
                DrawBrush(interp, _brushSizeCurrent);
            }

            _lastUVPos = uv;
            _lastScreenPos = currentScreenPos;
            _lastTime = Time.time;
        }
    }

    private Vector2 GetUVPosition(Vector2 screenPos)
    {
        float u = screenPos.x / Screen.width;
        float v = screenPos.y / Screen.height;

        return new Vector2(u, v);
    }

    private void DrawBrush(Vector2 uv, float size)
    {
        Texture brushTex = brushTextures[0];

        paintMaterial.SetTexture("_BrushTex", brushTex);
        paintMaterial.SetFloat("_BrushSize", size);

        Graphics.SetRenderTarget(targetTexture);

        GL.PushMatrix();
        GL.LoadPixelMatrix(0, targetTexture.width, 0, targetTexture.height);

        float px = uv.x * targetTexture.width;
        float py = uv.y * targetTexture.height;
        float halfSize = size * 0.5f;

        // 머티리얼 사용하여 사각형 출력
        paintMaterial.SetPass(0);
        GL.Begin(GL.QUADS);
        GL.TexCoord2(0, 0);
        GL.Vertex3(px - halfSize, py - halfSize, 0);
        GL.TexCoord2(0, 1);
        GL.Vertex3(px - halfSize, py + halfSize, 0);
        GL.TexCoord2(1, 1);
        GL.Vertex3(px + halfSize, py + halfSize, 0);
        GL.TexCoord2(1, 0);
        GL.Vertex3(px + halfSize, py - halfSize, 0);
        GL.End();

        GL.PopMatrix();
    }
}

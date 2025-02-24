using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class PaintingResearch : MonoBehaviour
{
    private RenderTexture texRender;   
    public Material mat;     
    public Texture brushTypeTexture;   
    private Camera mainCamera;
    private float brushScale = 0.5f;
    private float brushPressure = 1.0f;
    private Vector2 lastBrushDir = Vector2.right;
    private float brushRotation = 0f;
    public Color brushColor = Color.black;
    public RawImage raw;                   
    private float lastDistance;
    private Vector3[] PositionArray = new Vector3[3];
    private int a = 0;
    private Vector3[] PositionArray1 = new Vector3[4];
    private int b = 0;
    private float[] speedArray = new float[4];
    private int s = 0;

    // 붓 매개변수
    private float elasticDeformation = 0.8f;  // 붓의 탄성 변형 계수
    private float bristleSpread = 0.6f;       // 붓털의 퍼짐 정도
    private float inkSaturation = 1.0f;       // 잉크 포화도

    [SerializeField] private int num = 50;
    [SerializeField] private float widthPower = 0.5f;
    [SerializeField] private float inkDiffusion = 0.5f;
    [SerializeField] private float pressureSensitivity = 1.0f;
    [SerializeField] private float edgeHardness = 1.0f;

    Vector2 rawMousePosition;            
    float rawWidth;                               
    float rawHeight;                              
    [SerializeField] private const int maxCancleStep = 5;  
    [SerializeField] private Stack<RenderTexture> savedList = new Stack<RenderTexture>(maxCancleStep);

    void Start()
    {
        rawWidth = raw.rectTransform.sizeDelta.x;
        rawHeight = raw.rectTransform.sizeDelta.y;
        Vector2 rawanchorPositon = new Vector2(
            raw.rectTransform.anchoredPosition.x - raw.rectTransform.sizeDelta.x / 2.0f, 
            raw.rectTransform.anchoredPosition.y - raw.rectTransform.sizeDelta.y / 2.0f
        );
        
        Canvas canvas = raw.canvas;
        Vector2 canvasOffset = RectTransformUtility.WorldToScreenPoint(Camera.main, canvas.transform.position) 
            - canvas.GetComponent<RectTransform>().sizeDelta/2;
        rawMousePosition = rawanchorPositon + new Vector2(Screen.width / 2.0f, Screen.height / 2.0f) + canvasOffset;

        texRender = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
        Clear(texRender);
    }

    Vector3 startPosition = Vector3.zero;
    Vector3 endPosition = Vector3.zero;
    private float lastPressure = 1f;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            SaveTexture();
        }
        if (Input.GetMouseButton(0))
        {
            OnMouseMove(new Vector3(Input.mousePosition.x, Input.mousePosition.y, 0));
        }
        if (Input.GetMouseButtonUp(0))
        {
            OnMouseUp();
        }
        if (Input.GetKeyDown(KeyCode.R))
        {
            CanclePaint();
        }
        if (Input.GetKeyDown(KeyCode.C))
        {
            OnClickClear();
        }
        DrawImage();
    }

    void SaveTexture()
    {
        RenderTexture newRenderTexture = new RenderTexture(texRender);
        Graphics.Blit(texRender, newRenderTexture);
        savedList.Push(newRenderTexture);
    }

    void CanclePaint()
    {
        if (savedList.Count > 0)
        {
            texRender.Release();
            texRender = savedList.Pop();
        }
    }

    void OnMouseUp()
    {
        startPosition = Vector3.zero;
        a = 0;
        b = 0;
        s = 0;
        lastPressure = 1f;
    }

    float SetScale(float distance)
    {
        float velocityFactor = Mathf.Clamp01(distance / 100f);
        
        // 속도에 따른 붓의 탄성 변형 계산
        float deformation = Mathf.Lerp(1.0f, elasticDeformation, velocityFactor);
        
        // 압력에 따른 붓털의 퍼짐 효과
        float pressureFactor = Mathf.Lerp(1.0f, 0.3f, velocityFactor);
        brushPressure = pressureFactor * pressureSensitivity * deformation;
        
        float baseScale = Mathf.Lerp(0.9f, 0.2f, velocityFactor * brushPressure);
        return baseScale * widthPower * (1 + bristleSpread * (1 - brushPressure));
    }

    void OnMouseMove(Vector3 pos)
    {
        if (startPosition == Vector3.zero)
        {
            startPosition = new Vector3(Input.mousePosition.x, Input.mousePosition.y, 0);
        }

        endPosition = pos;
        float distance = Vector3.Distance(startPosition, endPosition);
        brushScale = SetScale(distance);
        ThreeOrderBézierCurse(pos, distance, 4.5f);

        startPosition = endPosition;
        lastDistance = distance;
    }

    void Clear(RenderTexture destTexture)
    {
        Graphics.SetRenderTarget(destTexture);
        GL.PushMatrix();
        GL.Clear(true, true, Color.white);
        GL.PopMatrix();
    }

    void DrawBrush(RenderTexture destTexture, int x, int y, Texture sourceTexture, Color color, float scale)
    {
        DrawBrush(destTexture, new Rect(x, y, sourceTexture.width, sourceTexture.height), sourceTexture, color, scale);
    }

    float CalculateDiffusion(float speed, float pressure)
    {
        float speedFactor = Mathf.Clamp01(speed / 100f);
        float pressureFactor = Mathf.Clamp01(pressure);
        
        // 속도와 압력에 따른 잉크 확산 효과 개선
        float diffusion = Mathf.Lerp(1.0f, 0.1f, speedFactor);
        diffusion *= Mathf.Lerp(0.3f, 1.8f, pressureFactor);
        
        // 잉크 포화도에 따른 확산 조절
        float saturationEffect = Mathf.Lerp(0.5f, 1.2f, inkSaturation);
        diffusion *= saturationEffect;
        
        return diffusion * inkDiffusion;
    }

    void DrawBrush(RenderTexture destTexture, Rect destRect, Texture sourceTexture, Color color, float scale)
    {
        Vector2 brushDir = new Vector2(endPosition.x - startPosition.x, endPosition.y - startPosition.y).normalized;
        if (brushDir != Vector2.zero)
        {
            lastBrushDir = brushDir;
        }
        
        brushRotation = Mathf.Atan2(lastBrushDir.y, lastBrushDir.x) * Mathf.Rad2Deg;
        float distance = Vector2.Distance(startPosition, endPosition);
        float deformationAngle = brushRotation + (distance * 0.1f * (1 - brushPressure));

        float left = (destRect.xMin-rawMousePosition.x)*Screen.width/rawWidth - destRect.width * scale / 2.0f;
        float right = (destRect.xMin - rawMousePosition.x) * Screen.width / rawWidth + destRect.width * scale / 2.0f;
        float top = (destRect.yMin - rawMousePosition.y) *Screen.height / rawHeight - destRect.height * scale / 2.0f;
        float bottom = (destRect.yMin - rawMousePosition.y) * Screen.height / rawHeight + destRect.height * scale / 2.0f;

        float diffusion = CalculateDiffusion(distance, brushPressure);

        Graphics.SetRenderTarget(destTexture);
        GL.PushMatrix();
        GL.LoadOrtho();

        mat.SetTexture("_MainTex", brushTypeTexture);
        mat.SetColor("_Color", new Color(
            color.r, 
            color.g, 
            color.b, 
            color.a * brushPressure * inkSaturation
        ));
        mat.SetFloat("_Rotation", deformationAngle);
        mat.SetFloat("_InkDiffusion", diffusion);
        mat.SetFloat("_InkEdgeHardness", edgeHardness * (1 + (1 - brushPressure) * 0.5f));
        mat.SetFloat("_PressureEffect", brushPressure);
        mat.SetPass(0);

        GL.Begin(GL.QUADS);
        GL.TexCoord2(0.0f, 0.0f); GL.Vertex3(left / Screen.width, top / Screen.height, 0);
        GL.TexCoord2(1.0f, 0.0f); GL.Vertex3(right / Screen.width, top / Screen.height, 0);
        GL.TexCoord2(1.0f, 1.0f); GL.Vertex3(right / Screen.width, bottom / Screen.height, 0);
        GL.TexCoord2(0.0f, 1.0f); GL.Vertex3(left / Screen.width, bottom / Screen.height, 0);
        GL.End();
        GL.PopMatrix();
    }

    void DrawImage()
    {
        raw.texture = texRender;
    }

    public void OnClickClear()
    {
        Clear(texRender);
        savedList.Clear();
    }

    private void ThreeOrderBézierCurse(Vector3 pos, float distance, float targetPosOffset)
    {
        PositionArray1[b] = pos;
        b++;
        speedArray[s] = distance;
        s++;

        if (b == 4)
        {
            Vector3 temp1 = PositionArray1[1];
            Vector3 temp2 = PositionArray1[2];

            Vector3 middle = (PositionArray1[0] + PositionArray1[2]) / 2;
            float tension = Mathf.Lerp(1.5f, 2.0f, distance / 100f);
            float speedFactor = Mathf.Clamp01(speedArray[0] / 100f);
            float controlStrength = Mathf.Lerp(1.2f, 1.8f, speedFactor);
            
            PositionArray1[1] = (PositionArray1[1] - middle) * (tension * controlStrength) + middle;
            middle = (temp1 + PositionArray1[3]) / 2;
            PositionArray1[2] = (PositionArray1[2] - middle) * (tension * 1.4f * controlStrength) + middle;

            for (int index1 = 0; index1 < num / 1.5f; index1++)
            {
                float t1 = (1.0f / num) * index1;
                Vector3 target = Mathf.Pow(1 - t1, 3) * PositionArray1[0] +
                                3 * PositionArray1[1] * t1 * Mathf.Pow(1 - t1, 2) +
                                3 * PositionArray1[2] * t1 * t1 * (1 - t1) + 
                                PositionArray1[3] * Mathf.Pow(t1, 3);

                float deltaspeed = (float)(speedArray[3] - speedArray[0]) / num;
                float currentSpeed = speedArray[0] + (deltaspeed * index1);
                float diffusionFactor = CalculateDiffusion(currentSpeed, brushPressure);
                
                float spreadRange = targetPosOffset * diffusionFactor;
                Vector2 spreadOffset = Random.insideUnitCircle * spreadRange;
                
                DrawBrush(texRender, 
                    (int)(target.x + spreadOffset.x), 
                    (int)(target.y + spreadOffset.y), 
                    brushTypeTexture, 
                    brushColor, 
                    SetScale(currentSpeed));
            }

            PositionArray1[0] = temp1;
            PositionArray1[1] = temp2;
            PositionArray1[2] = PositionArray1[3];

            speedArray[0] = speedArray[1];
            speedArray[1] = speedArray[2];
            speedArray[2] = speedArray[3];
            b = 3;
            s = 3;
        }
        else
        {
            DrawBrush(texRender, (int)endPosition.x, (int)endPosition.y, brushTypeTexture,
                brushColor, brushScale);
        }
    }
}
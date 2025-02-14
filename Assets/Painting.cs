using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class Painting : MonoBehaviour
{
    private RenderTexture texRender;   // 캔버스
    public Material mat;     // 주어진 셰이더로 생성된 머티리얼
    public Texture brushTypeTexture;   // 붓의 텍스처 (반투명)
    private Camera mainCamera;
    private float brushScale = 0.5f;
    public Color brushColor = Color.black;
    public RawImage raw;                   // UGUI의 RawImage를 사용하여 UI 추가 가능, pivot을 (0.5, 0.5)로 설정
    private float lastDistance;
    private Vector3[] PositionArray = new Vector3[3];
    private int a = 0;
    private Vector3[] PositionArray1 = new Vector3[4];
    private int b = 0;
    private float[] speedArray = new float[4];
    private int s = 0;
    [SerializeField]
    private int num = 50; // 두 점 사이에 보간할 점 개수
    [SerializeField]
    private float widthPower = 0.5f; // 선의 두께 조절

    Vector2 rawMousePosition;            // raw 이미지의 좌측 하단이 마우스와 대응하는 위치
    float rawWidth;                               // raw 이미지의 너비
    float rawHeight;                              // raw 이미지의 높이
    [SerializeField]
    private const int maxCancleStep = 5;  // 최대 실행 취소 단계 (값이 클수록 메모리 사용량 증가)
    [SerializeField]
    private Stack<RenderTexture> savedList = new Stack<RenderTexture>(maxCancleStep);

    void Start()
    {
        // raw 이미지의 마우스 기준 위치 및 크기 계산
        rawWidth = raw.rectTransform.sizeDelta.x;
        rawHeight = raw.rectTransform.sizeDelta.y;
        Vector2 rawanchorPositon = new Vector2(raw.rectTransform.anchoredPosition.x - raw.rectTransform.sizeDelta.x / 2.0f, raw.rectTransform.anchoredPosition.y - raw.rectTransform.sizeDelta.y / 2.0f);
        
        // Canvas 위치 편차 계산
        Canvas canvas = raw.canvas;
        Vector2 canvasOffset = RectTransformUtility.WorldToScreenPoint(Camera.main, canvas.transform.position) - canvas.GetComponent<RectTransform>().sizeDelta / 2;
        
        // 최종적으로 마우스가 캔버스에서의 위치
        rawMousePosition = rawanchorPositon + new Vector2(Screen.width / 2.0f, Screen.height / 2.0f) + canvasOffset;

        texRender = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
        Clear(texRender);
    }

    Vector3 startPosition = Vector3.zero;
    Vector3 endPosition = Vector3.zero;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            SaveTexture(); // 현재 텍스처 저장
        }
        if (Input.GetMouseButton(0))
        {
            OnMouseMove(new Vector3(Input.mousePosition.x, Input.mousePosition.y, 0)); // 마우스 이동 처리
        }
        if (Input.GetMouseButtonUp(0))
        {
            OnMouseUp(); // 마우스 버튼 떼기 처리
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            CanclePaint(); // 실행 취소
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            OnClickClear(); // 캔버스 지우기
        }

        DrawImage();
    }

    [SerializeField] private RawImage saveImage;

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
    }

    // 선의 굵기 설정
    float SetScale(float distance)
    {
        float Scale = 0;
        if (distance < 100)
        {
            Scale = 0.8f - 0.005f * distance;
        }
        else
        {
            Scale = 0.425f - 0.00125f * distance;
        }
        if (Scale <= 0.05f)
        {
            Scale = 0.05f;
        }
        return Scale * widthPower;
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

    void DrawBrush(RenderTexture destTexture, Rect destRect, Texture sourceTexture, Color color, float scale)
    {
        // 마우스 위치를 raw 이미지 위치에 맞게 변환
        float left = (destRect.xMin - rawMousePosition.x) * Screen.width / rawWidth - destRect.width * scale / 2.0f;
        float right = (destRect.xMin - rawMousePosition.x) * Screen.width / rawWidth + destRect.width * scale / 2.0f;
        float top = (destRect.yMin - rawMousePosition.y) * Screen.height / rawHeight - destRect.height * scale / 2.0f;
        float bottom = (destRect.yMin - rawMousePosition.y) * Screen.height / rawHeight + destRect.height * scale / 2.0f;

        Graphics.SetRenderTarget(destTexture);
        GL.PushMatrix();
        GL.LoadOrtho();

        mat.SetTexture("_MainTex", brushTypeTexture);
        mat.SetColor("_Color", color);
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
    // 2차 베지어 곡선 (효과가 좋지 않아서 아래 3차 베지어 곡선을 사용)
    public void TwoOrderBézierCurse(Vector3 pos, float distance)
    {
        PositionArray[a] = pos;
        a++;
        if (a == 3)
        {
            for (int index = 0; index < num; index++)
            {
                Vector3 middle = (PositionArray[0] + PositionArray[2]) / 2;
                PositionArray[1] = (PositionArray[1] - middle) / 2 + middle;

                float t = (1.0f / num) * index / 2;
                Vector3 target = Mathf.Pow(1 - t, 2) * PositionArray[0] + 2 * (1 - t) * t * PositionArray[1] +
                                Mathf.Pow(t, 2) * PositionArray[2];
                float deltaSpeed = (float)(distance - lastDistance) / num;
                DrawBrush(texRender, (int)target.x, (int)target.y, brushTypeTexture, brushColor, SetScale(lastDistance + (deltaSpeed * index)));
            }
            PositionArray[0] = PositionArray[1];
            PositionArray[1] = PositionArray[2];
            a = 2;
        }
        else
        {
            DrawBrush(texRender, (int)endPosition.x, (int)endPosition.y, brushTypeTexture,
                brushColor, brushScale);
        }
    }
    // 3차 베지어 곡선
    // 연속된 4개의 점 좌표를 받아, 중간 2개의 좌표를 조정하여 곡선을 일부만 그림 (num / 1.5로 곡선의 일부를 그림)
    // 속도를 기반으로 곡선의 두께를 조절함
    private void ThreeOrderBézierCurse(Vector3 pos, float distance, float targetPosOffset)
    {
        // 좌표 기록
        PositionArray1[b] = pos;
        b++;
        // 속도 기록
        speedArray[s] = distance;
        s++;
        if (b == 4)
        {
            Vector3 temp1 = PositionArray1[1];
            Vector3 temp2 = PositionArray1[2];

            // 중간 두 점의 좌표 조정
            Vector3 middle = (PositionArray1[0] + PositionArray1[2]) / 2;
            PositionArray1[1] = (PositionArray1[1] - middle) * 1.5f + middle;
            middle = (temp1 + PositionArray1[3]) / 2;
            PositionArray1[2] = (PositionArray1[2] - middle) * 2.1f + middle;

            for (int index1 = 0; index1 < num / 1.5f; index1++)
            {
                float t1 = (1.0f / num) * index1;
                Vector3 target = Mathf.Pow(1 - t1, 3) * PositionArray1[0] +
                                3 * PositionArray1[1] * t1 * Mathf.Pow(1 - t1, 2) +
                                3 * PositionArray1[2] * t1 * t1 * (1 - t1) + PositionArray1[3] * Mathf.Pow(t1, 3);
                
                // 속도 차이 계산 (참고용)
                float deltaspeed = (float)(speedArray[3] - speedArray[0]) / num;
                
                // 노이즈 효과 추가
                float randomOffset = Random.Range(-targetPosOffset, targetPosOffset);
                DrawBrush(texRender, (int)(target.x + randomOffset), (int)(target.y + randomOffset), brushTypeTexture, brushColor, SetScale(speedArray[0] + (deltaspeed * index1)));
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
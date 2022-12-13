using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TimelineScript : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Texture2D tex;
    private Color[] colors;
    RawImage rawImage;
    private RectTransform rt;

    private int oldSizeX = 1;
    private AllPhotosData photosData;

    float startFrame = 0f;
    float pixelsPerFrame = 5f;
    public event Action OnValueChanged;
    public event Action<int, int> OnCopy;
    public event Action<int> OnPaste;
    public event Action<int, int, int> OnMoveSelection;
    public event Action<int, int> OnDelete;


    private int frame;

    public int Value
    {
        get { return frame; }
        set
        {
            frame = value;
            Redraw();
        }
    }

    public Color emptyColor;
    public Color frameLineColor;
    public Color cursorColor;
    public Color keyColor;

    public Color selectionLineColor;
    public Color selectionFillColor;

    private bool pointerInside = false;
    private Vector3 oldMousePosition;

    private int selectionFrame1, selectionFrame2;
    private Vector3 moveSelectionStartPoint;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
        rawImage = GetComponent<RawImage>();
        pixelsPerFrame = 2f;
        //tex = new Texture2D(10, 10, TextureFormat.RGBA32, false);
        RegenerateTexture();
        oldMousePosition = Input.mousePosition;
        selectionFrame1 = selectionFrame2 = -1;
    }

    private void RegenerateTexture()
    {
        Debug.Log("Regenerate tex");
        int width = Screen.width;
        int height = (int)((float)width * (rt.rect.height / rt.rect.width));

        tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        colors = tex.GetPixels();
        oldSizeX = Screen.width;
        rawImage.texture = tex;
        Redraw();
    }

    private void Update()
    {
        //Debug.Log(rt.rect.width+" "+rt.rect.height+" "+oldSizeX+" "+oldSizeY);
        if (Screen.width != oldSizeX)
            RegenerateTexture();

        if (Mathf.Abs(Input.GetAxis("Mouse ScrollWheel")) > 0.01f && pointerInside)
        {
            float scaleDelta = 1f + Mathf.Sign(Input.GetAxis("Mouse ScrollWheel")) * 0.1f;
            float oldPixelsPerFrame = pixelsPerFrame;
            pixelsPerFrame *= scaleDelta;
            if (pixelsPerFrame < 0.2f)
                pixelsPerFrame = 0.2f;
            if (pixelsPerFrame > 100f)
                pixelsPerFrame = 100f;

            float pointerOldFrame = Input.mousePosition.x / oldPixelsPerFrame + startFrame;
            float pointerNewFrame = Input.mousePosition.x / pixelsPerFrame + startFrame;
            startFrame -= pointerNewFrame - pointerOldFrame;

            //startFrame -= (Input.mousePosition.x/oldPixelsPerFrame) * (pixelsPerFrame / oldPixelsPerFrame);

            Redraw();
        }

        if (Input.GetMouseButton(0) && pointerInside)
        {
            float pointerFrame = Input.mousePosition.x / pixelsPerFrame + startFrame;
            int newFrame = Mathf.RoundToInt(pointerFrame);
            if (newFrame != Value)
            {
                Value = newFrame;
                if (Value < 0)
                    Value = 0;
                if (Value >= photosData.photoData.Count)
                    Value = photosData.photoData.Count - 1;
                Redraw();
                OnValueChanged?.Invoke();
            }
        }

        if (Input.GetMouseButton(1) && pointerInside)
        {
            Vector3 delta = Input.mousePosition - oldMousePosition;
            //Debug.Log(delta.x);
            if (delta.x != 0f)
            {
                startFrame -= delta.x / pixelsPerFrame;
                Redraw();
            }
        }

        if (Input.GetKeyDown(KeyCode.LeftBracket) && pointerInside)
        {
            selectionFrame1 = Value;
            if (selectionFrame2 < 0)
                selectionFrame2 = selectionFrame1;
            Redraw();
        }

        if (Input.GetKeyDown(KeyCode.RightBracket) && pointerInside)
        {
            selectionFrame2 = Value;
            if (selectionFrame1 < 0)
                selectionFrame1 = selectionFrame2;
            Redraw();
        }

        if (Input.GetKey(KeyCode.LeftControl) && pointerInside)
        {
            if (Input.GetMouseButtonDown(0))
            {
                selectionFrame1 = selectionFrame2 = Value;
                Redraw();
            }

            if (Input.GetMouseButton(0))
            {
                if (selectionFrame2 != Value)
                {
                    selectionFrame2 = Value;
                    Redraw();
                }
            }
        }

        int moveDelta = 0;
        if (Input.GetKeyDown(KeyCode.Comma))
            moveDelta = -1;

        if (Input.GetKeyDown(KeyCode.Period))
            moveDelta = 1;
        
        if (Input.GetKey(KeyCode.A))
        {
            if (Input.GetMouseButton(0))
            {
                float frameClicked = (Input.mousePosition.x / pixelsPerFrame) + startFrame;
                if (frameClicked >= Mathf.Min(selectionFrame1, selectionFrame2) &&
                    frameClicked <= Mathf.Max(selectionFrame1, selectionFrame2))
                {

                    if (Input.GetMouseButtonDown(0))
                    {
                        moveSelectionStartPoint = Input.mousePosition;
                    }

                    Vector3 delta = Input.mousePosition - moveSelectionStartPoint;
                    float deltaKeys = delta.x / pixelsPerFrame;
                    if (Mathf.Abs(deltaKeys) >= 1f)
                    {
                        float deltaInt = Mathf.Round(deltaKeys);
                        moveSelectionStartPoint.x += deltaInt * pixelsPerFrame;

                        moveDelta = (int)deltaInt;
                    }
                }
            }
            
        }
        
        if (moveDelta != 0)
        {

            int s1 = Mathf.Min(selectionFrame1, selectionFrame2);
            int s2 = Mathf.Max(selectionFrame1, selectionFrame2);

            selectionFrame1 += moveDelta;
            selectionFrame2 += moveDelta;
            if (Mathf.Min(selectionFrame1, selectionFrame2) < 0)
            {
                int backDelta = Mathf.Min(selectionFrame1, selectionFrame2);
                selectionFrame1 -= backDelta;
                selectionFrame2 -= backDelta;
                moveDelta -= backDelta;
            }

            if (Mathf.Max(selectionFrame1, selectionFrame2) >= photosData.photoData.Count)
            {
                int backDelta = Mathf.Max(selectionFrame1, selectionFrame2) - photosData.photoData.Count -
                                1;
                selectionFrame1 -= backDelta;
                selectionFrame2 -= backDelta;
                moveDelta -= backDelta;
            }

            Redraw();

            //Debug.Log("Move " + s1 + " " + s2 + " " + moveDelta);
            OnMoveSelection?.Invoke(s1, s2, moveDelta);

            //Debug.Log(deltaInt);

        }
        
        if (Input.GetKeyDown(KeyCode.D))
        {
            if (selectionFrame1 >= 0 && selectionFrame2 >= 0)
            {
                OnDelete?.Invoke(Mathf.Min(selectionFrame1, selectionFrame2), Mathf.Max(selectionFrame1, selectionFrame2));
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            selectionFrame1 = selectionFrame2 = -100000;
            Redraw();
        }

        if (selectionFrame1 >= 0 && selectionFrame2 >= 0 && Input.GetKeyDown(KeyCode.C))
        {
            OnCopy?.Invoke(Mathf.Min(selectionFrame1, selectionFrame2), Mathf.Max(selectionFrame1, selectionFrame2));
        }

        if (Input.GetKeyDown(KeyCode.V))
        {
            OnPaste?.Invoke(Value);
        }

        oldMousePosition = Input.mousePosition;

    }

    public void SetSelection(int f1, int f2)
    {
        selectionFrame1 = f1;
        selectionFrame2 = f2;
        Redraw();
    }

    public void SetData(AllPhotosData photosData)
    {
        this.photosData = photosData;
        Redraw();
    }
    
    /*public void SetFrame(int currentPhoto)
    {
        throw new System.NotImplementedException();
    }*/

    
    public void Redraw()
    {
        //Debug.Log("Redraw");
        
        // Clear
        for (int k = 0; k < colors.Length; k++)
            colors[k] = emptyColor;

        if (photosData != null)
        {
            // Lines
            int lineStep = 1;
            if (pixelsPerFrame < 2f)
                lineStep = 5;
            if (pixelsPerFrame < 1f)
                lineStep = 10;
            if (pixelsPerFrame <= 0.5f)
                lineStep = 30;
            for (int k = 0; k < photosData.photoData.Count; k+=lineStep)
            {
                int x = Mathf.RoundToInt(((float)k - startFrame) * (float)pixelsPerFrame);
                if (x >= 0 && x < tex.width)
                {
                    for (int y = 0; y < tex.height; y++)
                    {
                        colors[x + y * tex.width] = frameLineColor; //(k==Value)?cursorColor:frameLineColor;
                    }
                }
            }
            
            // Keys colors
            for (int x = 0; x < tex.width; x++)
            {
                int frame = Mathf.RoundToInt(x / pixelsPerFrame + startFrame);
                if( frame<0 || frame>=photosData.photoData.Count )
                    continue;

                int keyCount = photosData.photoData[frame].keyData.Count;
                for (int b = 0; b < keyCount; b++)
                {
                    int startY = Mathf.RoundToInt((float)b / (float)keyCount * (tex.height));
                    int endY = Mathf.RoundToInt((float)(b + 1) / (float)keyCount * (tex.height));
                    if (endY > tex.height)
                        endY = tex.height;
                    for (int y = startY; y < endY; y++)
                    {
                        float keyValue = photosData.photoData[frame].keyData[b].key * 0.01f;
                        Color color = new Color(keyValue, keyValue, keyValue, 1f);
                        colors[x + y * tex.width] += color;
                    }
                }
                
            }
            
            // Keys points
            for (int k = 0; k < photosData.photoData.Count; k++)
            {
                int x = Mathf.RoundToInt(((float)k - startFrame) * (float)pixelsPerFrame);
                if (x < 2 || x >= tex.width - 2)
                    continue;

                int keyCount = photosData.photoData[k].keyData.Count;
                for (int b = 0; b < photosData.photoData[k].keyData.Count; b++)
                {
                    if (photosData.photoData[k].keyData[b].isKey)
                    {
                        int y = Mathf.RoundToInt(((float)b+0.5f) / (float)keyCount * (tex.height));
                        for( int x1=x-2; x1<=x+2; x1++ )
                        for (int y1 = y - 2; y1 <= y + 2; y1++)
                        {
                            if( y1<0 || y1>=tex.height )
                                continue;
                            colors[x1 + y1 * tex.width] = keyColor;
                        }
                    }
                }
            }
            
            // Selection
            RedrawSelection();
            
            // CurrentFrame
            int currentFrameX = Mathf.RoundToInt(((float)Value - startFrame) * (float)pixelsPerFrame);
            if (currentFrameX >= 0 && currentFrameX < tex.width)
            {
                for (int y = 0; y < tex.height; y++)
                {
                    colors[currentFrameX + y * tex.width] = cursorColor;
                }
            }

        }
        
        tex.SetPixels(colors);
        tex.Apply();

    }

    void RedrawSelection()
    {
        if (selectionFrame1 < 0 || selectionFrame2 < 0)
            return;
        
        int sx1 = Mathf.RoundToInt(((float)Mathf.Min(selectionFrame1, selectionFrame2) - startFrame) * pixelsPerFrame);
        int sx2 = Mathf.RoundToInt(((float)Mathf.Max(selectionFrame1, selectionFrame2) - startFrame) * pixelsPerFrame);
        
        for (int x = sx1; x <= sx2; x++)
        {
            if (x < 0 || x >= tex.width)
                continue;
            for (int y = 0; y < tex.height; y++)
            {
                colors[x + y * tex.width] += selectionFillColor;
            }

            if (x == sx1 || x == sx2)
            {
                for (int y = 0; y < tex.height; y++)
                {
                    colors[x + y * tex.width] = selectionLineColor;
                }
            }
        }
        
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
    }
}

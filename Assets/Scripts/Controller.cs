using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class Controller : MonoBehaviour
{

    public string folder;
    public AllPhotosData photoData;
    public GameObject modelPrefab;
    public Transform modelHolder;
    GameObject modelGo;
    SkinnedMeshRenderer skinnedMeshRenderer;
    public RawImage rawImage;
    public AspectRatioFitter rawImageAspectRatioFitter;
    public RawImage timelineImage;
    public Texture2D timelineTex;
    private Color[] timelineTexColors;
    public Button saveButton;
    public Slider timelineSlider;
    public int lerpDistance = 15;

    public TMP_Text frameNumberText;
    //public int lerpInVoidDistance = 15;

    private int currentPhoto;
    public Texture2D currentTexture;

    public WeightSlider weightSliderPrefab;
    public List<WeightSlider> weightSliders;

    public Button playButton;
    public TMP_InputField playFpsInput;
    public int playFps;
    private bool isPlaying;
    private float playingLastFrameTime;


    private void Awake()
    {
        weightSliderPrefab.gameObject.SetActive(false);
        saveButton.onClick.AddListener(Save);
        playFpsInput.onEndEdit.AddListener((s) =>
        {
            float f = (float)playFps;
            float.TryParse(s, out f);
            playFps = (int)f;
        });
        playButton.onClick.AddListener(() => OnPlayPressed());
    }

    private void OnPlayPressed()
    {
        if (isPlaying)
        {
            isPlaying = false;
        }
        else
        {
            isPlaying = true;
            playingLastFrameTime = Time.time;
        }
    }

    async void Save()
    {
        Debug.Log("Saving, wait");

        string allDataPath = Path.Combine(folder, Path.GetFileName(folder)+".json");
        File.WriteAllText(allDataPath, JsonUtility.ToJson(photoData));
        
        /*float timeStart = Time.time;
        int numSaved = 0;
        foreach (var data in photoData.photoData)
        {
            string dataPath = Path.Combine(folder, data.photoName+".key");
            File.WriteAllText(dataPath, JsonUtility.ToJson(data));
            await Task.Delay(50);
            numSaved++;
            if (Time.time - timeStart > 1f)
            {
                timeStart = Time.time;
                Debug.Log((100f*(float)numSaved/(float)photoData.photoData.Count).ToString("0.0")+"%");
            }
        }*/
        Debug.Log("Saved");
    }

    private void Start()
    {
        folder = EditorUtility.OpenFolderPanel("Выберите папку с фото", null, null);
        LoadFolderData(folder, modelPrefab);

        currentPhoto = 0;
        SetCurrentPhoto();
    }

    private void Update()
    {
        //if (Input.GetKey(KeyCode.Z))
        {
            if (Input.GetKeyDown(KeyCode.Alpha2))
                NextPhoto();
            if (Input.GetKeyDown(KeyCode.Alpha1))
                PreviousPhoto();
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            OnPlayPressed();
        }
        
        if (isPlaying)
        {
            while (Time.time - playingLastFrameTime > 1f / (float)playFps)
            {
                NextPhoto();
                if (currentPhoto >= photoData.photoData.Count-1)
                    isPlaying = false;
                playingLastFrameTime += 1f / (float)playFps;
            }
        }
    }

    private void PreviousPhoto()
    {
        if (currentPhoto > 0)
        {
            currentPhoto--;
            SetCurrentPhoto();
        }
    }

    private void NextPhoto()
    {
        if (currentPhoto < photoData.photoData.Count - 1)
        {
            currentPhoto++;
            SetCurrentPhoto();
        }
    }

    void SetCurrentPhoto()
    {
        
        if( currentTexture != null )
            Destroy(currentTexture);

        timelineSlider.value = (float)currentPhoto;
        frameNumberText.text = "" + currentPhoto;

        string photoPath = Path.Combine(folder, photoData.photoData[currentPhoto].photoPath);
        currentTexture = (new Texture2D(1, 1, TextureFormat.RGBA32, false));
        currentTexture.LoadImage(File.ReadAllBytes(photoPath));
        rawImage.texture = currentTexture;

        rawImageAspectRatioFitter.aspectRatio = (float)currentTexture.width / (float)currentTexture.height;

        for (int k = 0; k < photoData.photoData[currentPhoto].keyData.Count; k++)
        {
            skinnedMeshRenderer.SetBlendShapeWeight(k, photoData.photoData[currentPhoto].keyData[k].key);
            weightSliders[k].Set(photoData.photoData[currentPhoto].keyData[k].isKey,
                photoData.photoData[currentPhoto].keyData[k].key);
        }
        
    }

    void LoadFolderData(string folder, GameObject prefab)
    {
        photoData.photoData = new List<PhotoData>();
        
        if( modelGo != null )
            Destroy(modelGo);
        modelGo = Instantiate(prefab, modelHolder);
        modelGo.transform.localPosition = Vector3.zero;
        modelGo.transform.localRotation = Quaternion.identity;
        modelGo.transform.localScale = Vector3.one;
        skinnedMeshRenderer = modelGo.GetComponentInChildren<SkinnedMeshRenderer>();

        if (weightSliders == null)
            weightSliders = new List<WeightSlider>();
        foreach (var weightSlider in weightSliders)
            Destroy(weightSlider.gameObject);
        weightSliders.Clear();

        //for (int k = 0; k < skinnedMeshRenderer.sharedMesh.blendShapeCount; k++)
        foreach(var k in Enumerable.Range(0, skinnedMeshRenderer.sharedMesh.blendShapeCount))
        {
            var weightSlider = (Instantiate(weightSliderPrefab, weightSliderPrefab.transform.parent))
                .GetComponent<WeightSlider>();
            weightSlider.gameObject.SetActive(true);
            weightSlider.Init(skinnedMeshRenderer.sharedMesh.GetBlendShapeName(k), 0f,
                (f) =>
            {
                skinnedMeshRenderer.SetBlendShapeWeight(k, f);
                weightSlider.Set(true, f);
                photoData.photoData[currentPhoto].keyData[k].isKey = true;
                photoData.photoData[currentPhoto].keyData[k].key = f;
                
                LerpKeysAroundKey(currentPhoto, k);

                UpdateTimelineTex();

            }, () =>
            {
                if (photoData.photoData[currentPhoto].keyData[k].isKey)
                {
                    photoData.photoData[currentPhoto].keyData[k].isKey = false;
                    photoData.photoData[currentPhoto].keyData[k].key = 0f;
                    weightSlider.Set(false);

                    int leftKey = -1;
                    for (int i = currentPhoto - 1; i >= currentPhoto - lerpDistance; i--)
                    {
                        if (photoData.photoData[i].keyData[k].isKey)
                        {
                            leftKey = i;
                            break;
                        }

                        photoData.photoData[i].keyData[k].key = 0f;
                    }

                    int rightKey = -1;
                    for (int i = currentPhoto + 1; i < currentPhoto + lerpDistance; i++)
                    {
                        if (photoData.photoData[i].keyData[k].isKey)
                        {
                            rightKey = i;
                            break;
                        }

                        photoData.photoData[i].keyData[k].key = 0f;
                    }

                    if (leftKey != -1) LerpKeysAroundKey(leftKey, k);
                    if (rightKey != -1) LerpKeysAroundKey(rightKey, k);
                    UpdateTimelineTex();
                }

            }, () =>
            {
                for (int i = 0; i < skinnedMeshRenderer.sharedMesh.blendShapeCount; i++)
                {
                    weightSliders[i].Set(false, i==k?100f:0f );
                    skinnedMeshRenderer.SetBlendShapeWeight(i, i==k?100f:0f);
                }
            }, () => 
            {
                for (int i = 0; i < skinnedMeshRenderer.sharedMesh.blendShapeCount; i++)
                {
                    weightSliders[i].Set(photoData.photoData[currentPhoto].keyData[i].isKey, photoData.photoData[currentPhoto].keyData[i].key );
                    skinnedMeshRenderer.SetBlendShapeWeight(i, photoData.photoData[currentPhoto].keyData[i].key);
                }
            });
            weightSliders.Add(weightSlider);
        }
        
        string allDataPath = Path.Combine(folder, Path.GetFileName(folder)+".json");
        if (File.Exists(allDataPath))
        {
            photoData = JsonUtility.FromJson<AllPhotosData>(File.ReadAllText(allDataPath));
        }
        else
        {

            foreach (var photoPath in Directory.EnumerateFiles(folder))
            {
                PhotoData newPhotoData = null;

                string extention = Path.GetExtension(photoPath);
                if (!(extention == ".jpg" || extention == ".png" || extention == ".jpeg"))
                    continue;

                string dataName = Path.GetFileNameWithoutExtension(photoPath) + ".key";
                string dataPath = Path.Combine(folder, dataName);
                if (File.Exists(dataPath))
                {
                    string dataJson = File.ReadAllText(dataPath);
                    newPhotoData = JsonUtility.FromJson<PhotoData>(dataJson);
                }

                if (newPhotoData == null)
                {
                    newPhotoData = new PhotoData();
                    newPhotoData.photoPath = Path.GetFileName(photoPath);
                    newPhotoData.photoName = Path.GetFileNameWithoutExtension(photoPath);
                    newPhotoData.keyData = new List<KeyData>();
                }

                for (int k = 0; k < skinnedMeshRenderer.sharedMesh.blendShapeCount; k++)
                {
                    string blendshapeName = skinnedMeshRenderer.sharedMesh.GetBlendShapeName(k);
                    if (newPhotoData.keyData.FirstOrDefault(key => key.name == blendshapeName) == null)
                    {
                        var keyData = new KeyData();
                        keyData.name = blendshapeName;
                        keyData.key = 0f;
                        keyData.isKey = false;
                        newPhotoData.keyData.Add(keyData);
                    }
                }

                photoData.photoData.Add(newPhotoData);

            }
        }

        timelineSlider.minValue = 0;
        timelineSlider.maxValue = photoData.photoData.Count - 1;
        timelineSlider.onValueChanged.AddListener((f) =>
        {
            currentPhoto = (int)f;
            SetCurrentPhoto();
        });
        UpdateTimelineTex();


    }

    void LerpKeysAroundKey(int keyNum, int lineNum)
    {
        // Smooth all not keys around this key
        int nearestLeftKey = keyNum - lerpDistance;
        for (int i = keyNum - 1; i >= keyNum - lerpDistance; i--)
        {
            if (i < 0)
                break;
            if (photoData.photoData[i].keyData[lineNum].isKey)
            {
                nearestLeftKey = i;
                break;
            }
        }

        // Lerp between this and nearestLeft
        if (nearestLeftKey != -1)
        {
            for (int i = nearestLeftKey + 1; i < keyNum; i++)
            {
                float alpha = (float)(i - nearestLeftKey) / (float)(keyNum - nearestLeftKey);
                photoData.photoData[i].keyData[lineNum].key = Mathf.Lerp(photoData.photoData[nearestLeftKey].keyData[lineNum].key,
                    photoData.photoData[keyNum].keyData[lineNum].key, alpha);
            }
        }

        // Smooth all not keys around this key
        int nearestRightKey = keyNum + lerpDistance;
        for (int i = keyNum + 1; i <= keyNum + lerpDistance; i++)
        {
            if (i >= photoData.photoData.Count)
                break;
            if (photoData.photoData[i].keyData[lineNum].isKey)
            {
                nearestRightKey = i;
                break;
            }
        }

        // Lerp between this and nearestLeft
        if (nearestRightKey != -1)
        {
            for (int i = keyNum + 1; i <= nearestRightKey - 1; i++)
            {
                float alpha = (float)(nearestRightKey - i) / (float)(nearestRightKey - keyNum);
                photoData.photoData[i].keyData[lineNum].key = Mathf.Lerp(photoData.photoData[keyNum].keyData[lineNum].key,
                    photoData.photoData[nearestRightKey].keyData[lineNum].key, 1f - alpha);
            }
        }
    }

    void UpdateTimelineTex()
    {
        if (timelineTex == null)
        {
            timelineTex = new Texture2D(photoData.photoData.Count, photoData.photoData[0].keyData.Count, TextureFormat.RGBA32, false);
            timelineTex.filterMode = FilterMode.Point;
            timelineTexColors = timelineTex.GetPixels();
            for (int k = 0; k < timelineTexColors.Length; k++)
                timelineTexColors[k] = Color.black;
            timelineTex.SetPixels(timelineTexColors);
            timelineTex.Apply();
            timelineImage.texture = timelineTex;
        }

        for (int x = 0; x < photoData.photoData.Count; x++)
        {
            for (int y = 0; y < photoData.photoData[x].keyData.Count; y++)
            {
                Color color = Color.black;
                if( photoData.photoData[x].keyData[y].isKey ) color = Color.magenta;
                else if (photoData.photoData[x].keyData[y].key > 0f)
                {
                    float colorValue = photoData.photoData[x].keyData[y].key / 100f;
                    color = new Color(colorValue, colorValue, colorValue, 1f);
                }

                timelineTexColors[x + (timelineTex.height-y-1) * timelineTex.width] = color;
            }
        }
        
        timelineTex.SetPixels(timelineTexColors);
        timelineTex.Apply();
        
    }

}

[Serializable]
public class KeyData
{
    public string name;
    public float key;
    public bool isKey;
}

[Serializable]
public class PhotoData
{
    public string photoPath;
    public string photoName;
    public List<KeyData> keyData;

    public bool HasKey()
    {
        foreach (var data in keyData)
        {
            if (data.isKey)
                return true;
        }

        return false;
    }
}

[Serializable]
public class AllPhotosData
{
    public List<PhotoData> photoData;
}

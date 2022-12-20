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
using UnityEngine.Networking;
using UnityEngine.UI;

public class Controller : MonoBehaviour
{

    public string folder;
    public AllPhotosData photoData;
    public GameObject modelPrefab;
    public Transform modelHolder;
    GameObject modelGo;
    SkinnedMeshRenderer[] skinnedMeshRenderers;
    public RawImage rawImage;
    public AspectRatioFitter rawImageAspectRatioFitter;

    public TimelineScript timeline;
    //public RawImage timelineImage;
    //public Texture2D timelineTex;
    ///private Color[] timelineTexColors;
    public Button saveButton;
    //public Slider timelineSlider;
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

    public Dictionary<string, Texture2D> texRepo;
    

    public AudioSource audioSource;


    private void Awake()
    {
        weightSliderPrefab.gameObject.SetActive(false);
        saveButton.onClick.AddListener(Save);
        playFpsInput.onEndEdit.AddListener((s) =>
        {
            SetFps();
        });
        playButton.onClick.AddListener(() => OnPlayPressed());
        timeline.OnMoveSelection += OnMoveTimelineSelection;
        timeline.OnCopy += (i1, i2) => copyBuffer = OnTimelineCopy(i1,i2);
        timeline.OnPaste += (i) => OnTimelinePaste(i, copyBuffer);
        timeline.OnDelete += OnDeleteTimelineSelection;
        texRepo = new Dictionary<string, Texture2D>();
    }

    private void OnTimelinePaste(int pasteFrame, AllPhotosData bufferToPaste)
    {
        if (bufferToPaste == null)
        {
            Debug.Log("No copy made. Select area and press C");
            return;
        }

        OnDeleteTimelineSelection(pasteFrame, pasteFrame + bufferToPaste.photoData.Count);
        
        // Paste
        for (int f = 0; f < bufferToPaste.photoData.Count; f++)
        {
            if( (f + pasteFrame) < 0 || (f+pasteFrame)>=photoData.photoData.Count )
                continue;
            for (int b = 0; b < bufferToPaste.photoData[f].keyData.Count; b++)
            {
                //if (copyBuffer.photoData[f].keyData[b].key)
                {
                    photoData.photoData[f + pasteFrame].keyData[b].isKey = bufferToPaste.photoData[f].keyData[b].isKey;
                    photoData.photoData[f + pasteFrame].keyData[b].key = bufferToPaste.photoData[f].keyData[b].key;
                }
            }
        }
        
        // Lerp
        for (int f = 0; f < bufferToPaste.photoData.Count; f++)
        {
            if( (f + pasteFrame) < 0 || (f+pasteFrame)>=photoData.photoData.Count )
                continue;
            if ((f+pasteFrame) > (Mathf.Max(pasteFrame, 0) + lerpDistance) && (f+pasteFrame) <
                (Mathf.Min(pasteFrame + bufferToPaste.photoData.Count, photoData.photoData.Count - 1) - lerpDistance))
                continue;
            for (int b = 0; b < bufferToPaste.photoData[f].keyData.Count; b++)
            {
                if (photoData.photoData[f+pasteFrame].keyData[b].isKey)
                {
                    LerpKeysAroundKey(f+pasteFrame, b);
                }
            }
        }
        
        timeline.SetSelection(pasteFrame, pasteFrame + bufferToPaste.photoData.Count-1);
        
        timeline.RedrawAll();
    }

    public void OnDeleteTimelineSelection(int f1, int f2)
    {
        if (f1 < 0)
            f1 = 0;
        if (f2 < 0)
            f2 = 0;
        if (f1 >= photoData.photoData.Count)
            f1 = photoData.photoData.Count - 1;
        if (f2 >= photoData.photoData.Count)
            f2 = photoData.photoData.Count - 1;
        if (f1 > f2)
        {
            int t = f2;
            f2 = f1;
            f2 = t;
        }
        
        // Delete

        List<(int, int)> keysToDelete = new List<(int, int)>();
        
        for (int f = f1; f <= f2; f++)
        {
            for (int b = 0; b < photoData.photoData[f].keyData.Count; b++)
            {
                if (photoData.photoData[f].keyData[b].isKey)
                {
                    keysToDelete.Add((f,b));
                    photoData.photoData[f].keyData[b].isKey = false;
                    //TryToDeleteKey(f,b);
                }

                photoData.photoData[f].keyData[b].key = 0f;
            }
        }

        foreach (var tuple in keysToDelete)
        {
            if( Mathf.Abs(f1-tuple.Item1)<=lerpDistance || Mathf.Abs(f2-tuple.Item1)<=lerpDistance)
                TryToDeleteKey(tuple.Item1, tuple.Item2, true);
        }
        
        timeline.RedrawAll();
        
    }

    private AllPhotosData copyBuffer, moveCopyBuffer;
    private AllPhotosData OnTimelineCopy(int f1, int f2)
    {
        if (f1 < 0 || f2 < 0 || f1 >= photoData.photoData.Count || f2 >= photoData.photoData.Count)
            return null;
        if (f1 > f2)
        {
            int t = f2;
            f2 = f1;
            f2 = t;
        }
        var bufferToCopy = new AllPhotosData();
        bufferToCopy.photoData = new List<PhotoData>();
        for (int f = f1; f <= f2; f++)
        {
            var newPhotoData = new PhotoData();
            newPhotoData.keyData = new List<KeyData>();
            for (int b = 0; b < photoData.photoData[f].keyData.Count; b++)
            {
                var newKeyData = new KeyData();
                newKeyData.isKey = photoData.photoData[f].keyData[b].isKey;
                newKeyData.key = photoData.photoData[f].keyData[b].key;
                newPhotoData.keyData.Add(newKeyData);
            }
            bufferToCopy.photoData.Add(newPhotoData);
        }

        return bufferToCopy;
        //Debug.Log("Copied frames from "+f1+" to "+f2);
    }

    private int lastMoveCopySize = -1;
    
    private void OnMoveTimelineSelection(int f1, int f2, int delta)
    {
        if (delta == 0)
            return;

        int copySize = f2 - f1;
        if (copySize != lastMoveCopySize)
        {
            moveCopyBuffer = OnTimelineCopy(f1, f2);
            lastMoveCopySize = copySize;
        }

        OnDeleteTimelineSelection(f1, f2);
        OnTimelinePaste(f1+delta, moveCopyBuffer);

        /*if (delta > 0)
        {
            for (int f = f2; f >= f1; f--)
            {
                for (int b = 0; b < photoData.photoData[f].keyData.Count; b++)
                {
                    if (photoData.photoData[f].keyData[b].isKey)
                    {
                        photoData.photoData[f + delta].keyData[b].isKey = photoData.photoData[f].keyData[b].isKey;
                        photoData.photoData[f + delta].keyData[b].key = photoData.photoData[f].keyData[b].key;
                        TryToDeleteKey(f, b);
                    }
                }
            }
            for (int f = f2+delta; f >= f1+delta; f--)
            {
                for (int b = 0; b < photoData.photoData[f].keyData.Count; b++)
                {
                    if (photoData.photoData[f].keyData[b].isKey)
                    {
                        LerpKeysAroundKey(f, b);
                    }
                }
            }
            timeline.Redraw();
        }
        else if (delta < 0)
        {
            for (int f = f1; f <= f2; f++)
            {
                for (int b = 0; b < photoData.photoData[f].keyData.Count; b++)
                {
                    if (photoData.photoData[f].keyData[b].isKey)
                    {
                        photoData.photoData[f + delta].keyData[b].isKey = photoData.photoData[f].keyData[b].isKey;
                        photoData.photoData[f + delta].keyData[b].key = photoData.photoData[f].keyData[b].key;
                        TryToDeleteKey(f, b);
                    }
                }
            }
            for (int f = f1+delta; f <= f2+delta; f++)
            {
                for (int b = 0; b < photoData.photoData[f].keyData.Count; b++)
                {
                    if (photoData.photoData[f].keyData[b].isKey)
                    {
                        LerpKeysAroundKey(f, b);
                    }
                }
            }
            timeline.Redraw();
        }*/
    }

    void SetFps()
    {
        float f = (float)playFps;
        float.TryParse(playFpsInput.text, out f);
        playFps = (int)f;

        if (audioSource.clip != null)
        {
            var videoLength = (float)photoData.photoData.Count/(float)playFps;
            
            //Debug.Log(videoLength+" "+audioSource.clip.length);
            audioSource.pitch = audioSource.clip.length / videoLength;
        }
        
    }

    private void OnPlayPressed()
    {
        if (isPlaying)
        {
            isPlaying = false;
            audioSource.Stop();
        }
        else
        {
            isPlaying = true;
            playingLastFrameTime = Time.time;
            if (audioSource.clip != null)
            {
                float audioNormalizedTime = (float)currentPhoto / (float)(photoData.photoData.Count - 1);
                audioSource.Play();
                audioSource.time = audioNormalizedTime * audioSource.clip.length;
            }
        }
    }

    async void Save()
    {
        Debug.Log("Saving, wait");

        string allDataPath = Path.Combine(folder, Path.GetFileName(folder)+".json");
        File.WriteAllText(allDataPath, JsonUtility.ToJson(photoData));
        await Task.Delay(500);
        string allDataBackupPath = Path.Combine(folder, Path.GetFileName(folder)+".json_"+DateTime.Now.Ticks+".bkp");
        File.Copy(allDataPath, allDataBackupPath, true);
        
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
        
        Debug.Log("Exporting maxscript");

        string maxScriptPath = Path.Combine(folder, Path.GetFileName(folder) + "_maxscript.ms");
        GenerateMaxScriptWithMorphs(maxScriptPath, photoData);
        
        Debug.Log("Saved");
    }

    private void Start()
    {
        folder = EditorUtility.OpenFolderPanel("Выберите папку с фото", null, null);
        Debug.Log("Selected folder = "+folder);
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
                if (currentPhoto >= photoData.photoData.Count - 1)
                    OnPlayPressed();
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
        if (photoData == null)
            return;
        if (photoData.photoData == null)
            return;
        if (photoData.photoData.Count == 0)
            return;

        //if( !isPlaying )
        timeline.Value = currentPhoto;
        //timelineSlider.SetValueWithoutNotify((float)currentPhoto);
        //timelineSlider.value = (float)currentPhoto;
        frameNumberText.text = "" + currentPhoto;

        string photoPath = Path.Combine(folder, photoData.photoData[currentPhoto].photoPath);
        currentTexture = (new Texture2D(1, 1, TextureFormat.RGBA32, false));
        currentTexture.LoadImage(File.ReadAllBytes(photoPath));
        //currentTexture = texRepo[photoData.photoData[currentPhoto].photoName];
        rawImage.texture = currentTexture;

        rawImageAspectRatioFitter.aspectRatio = (float)currentTexture.width / (float)currentTexture.height;

        for (int k = 0; k < photoData.photoData[currentPhoto].keyData.Count; k++)
        {
            foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
                skinnedMeshRenderer.SetBlendShapeWeight(k, photoData.photoData[currentPhoto].keyData[k].key);
            weightSliders[k].Set(photoData.photoData[currentPhoto].keyData[k].isKey,
                photoData.photoData[currentPhoto].keyData[k].key);
        }
        
    }

    async void LoadFolderData(string folder, GameObject prefab)
    {
        photoData.photoData = new List<PhotoData>();
        
        if( modelGo != null )
            Destroy(modelGo);
        modelGo = Instantiate(prefab, modelHolder);
        modelGo.transform.localPosition = Vector3.zero;
        modelGo.transform.localRotation = Quaternion.identity;
        modelGo.transform.localScale = Vector3.one;
        skinnedMeshRenderers = modelGo.GetComponentsInChildren<SkinnedMeshRenderer>();

        if (weightSliders == null)
            weightSliders = new List<WeightSlider>();
        foreach (var weightSlider in weightSliders)
            Destroy(weightSlider.gameObject);
        weightSliders.Clear();

        //for (int k = 0; k < skinnedMeshRenderer.sharedMesh.blendShapeCount; k++)
        foreach(var k in Enumerable.Range(0, skinnedMeshRenderers[0].sharedMesh.blendShapeCount))
        {
            var weightSlider = (Instantiate(weightSliderPrefab, weightSliderPrefab.transform.parent))
                .GetComponent<WeightSlider>();
            weightSlider.gameObject.SetActive(true);
            weightSlider.Init(skinnedMeshRenderers[0].sharedMesh.GetBlendShapeName(k), 0f,
                (f) =>
            {
                skinnedMeshRenderers[0].SetBlendShapeWeight(k, f);
                weightSlider.Set(true, f);
                photoData.photoData[currentPhoto].keyData[k].isKey = true;
                photoData.photoData[currentPhoto].keyData[k].key = f;
                
                LerpKeysAroundKey(currentPhoto, k);

                //UpdateTimelineTex();
                timeline.RedrawAll();

            }, () =>
            {
                TryToDeleteKey(currentPhoto, k);

            }, () =>
            {
                for (int i = 0; i < skinnedMeshRenderers[0].sharedMesh.blendShapeCount; i++)
                {
                    weightSliders[i].Set(false, i==k?100f:0f );
                    foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
                        skinnedMeshRenderer.SetBlendShapeWeight(i, i==k?100f:0f);
                }
            }, () => 
            {
                for (int i = 0; i < skinnedMeshRenderers[0].sharedMesh.blendShapeCount; i++)
                {
                    weightSliders[i].Set(photoData.photoData[currentPhoto].keyData[i].isKey, photoData.photoData[currentPhoto].keyData[i].key );
                    foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
                        skinnedMeshRenderer.SetBlendShapeWeight(i, photoData.photoData[currentPhoto].keyData[i].key);
                }
            });
            weightSliders.Add(weightSlider);
        }

        // loading all json's
        //List<AllPhotosData> datas = new List<AllPhotosData>();
        photoData = new AllPhotosData();
        
        foreach (var jsonPath in Directory.EnumerateFiles(folder))
        {
            string extention = Path.GetExtension(jsonPath);
            if (extention != ".json")
                continue;

            Debug.Log(jsonPath);
            
            string jsonData = File.ReadAllText(jsonPath);
            var data = new AllPhotosData();
            data = JsonUtility.FromJson<AllPhotosData>(jsonData);
            //datas.Add(data);

            foreach (var pData in data.photoData)
            {
                var existingData = photoData.photoData.FirstOrDefault((x) => x.photoName == pData.photoName);
                if (existingData != null)
                {
                    existingData.keyData = pData.keyData;
                }
                else
                {
                    photoData.photoData.Add(pData);
                    //Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    //tex.LoadImage(File.ReadAllBytes(Path.Combine(folder, pData.photoPath)));
                    //texRepo.Add(pData.photoName, tex);
                }
            }
        }

        
        foreach (var photoPath in Directory.EnumerateFiles(folder))
        {
            PhotoData newPhotoData = null;

            string extention = Path.GetExtension(photoPath);

            if (!(extention == ".jpg" || extention == ".png" || extention == ".jpeg"))
                continue;
            
            string fileName = Path.GetFileNameWithoutExtension(photoPath);
            if (photoData.photoData.FirstOrDefault((x) => x.photoName == fileName) != null)
                continue;

            newPhotoData = new PhotoData();
            newPhotoData.photoPath = Path.GetFileName(photoPath);
            newPhotoData.photoName = Path.GetFileNameWithoutExtension(photoPath);
            newPhotoData.keyData = new List<KeyData>();

            for (int k = 0; k < skinnedMeshRenderers[0].sharedMesh.blendShapeCount; k++)
            {
                string blendshapeName = skinnedMeshRenderers[0].sharedMesh.GetBlendShapeName(k);
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

        }//*/
        
        photoData.photoData.Sort((x,y) =>
            x.photoName.CompareTo(y.photoName));    


        /*string allDataPath = Path.Combine(folder, Path.GetFileName(folder)+".json");
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

                for (int k = 0; k < skinnedMeshRenderers[0].sharedMesh.blendShapeCount; k++)
                {
                    string blendshapeName = skinnedMeshRenderers[0].sharedMesh.GetBlendShapeName(k);
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
        }*/

        foreach (var photoPath in Directory.EnumerateFiles(folder))
        {
            string extention = Path.GetExtension(photoPath);

            if (extention == ".wav")
            {
                Debug.Log("Loading audio");
                string audioUrl = "file://" + photoPath;
                using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(audioUrl, AudioType.WAV))
                {
                    var request = www.SendWebRequest();
                    while (!request.isDone)
                        Task.Delay(50);

                    if (www.result == UnityWebRequest.Result.ConnectionError)
                    {
                        Debug.Log(www.error);
                    }
                    else
                    {
                        Debug.Log("Loaded audio");
                        AudioClip myClip = DownloadHandlerAudioClip.GetContent(www);
                        audioSource.clip = myClip;
                    }
                }
            }
        }

        //timeline.SetRange(0, photoData.photoData.Count - 1);
        //timelineSlider.minValue = 0;
        //timelineSlider.maxValue = photoData.photoData.Count - 1;
        timeline.SetData(photoData);
        timeline.OnValueChanged += () =>
        {
            currentPhoto = timeline.Value;
            if (audioSource.clip != null)
            {
                float audioNormalizedTime = (float)currentPhoto / (float)(photoData.photoData.Count - 1);
                audioSource.time = audioNormalizedTime * audioSource.clip.length;
            }

            SetCurrentPhoto();
        };
        /*timelineSlider.onValueChanged.AddListener((f) =>
        {
            currentPhoto = (int)f;
            if (audioSource.clip != null)
            {
                float audioNormalizedTime = (float)currentPhoto / (float)(photoData.photoData.Count - 1);
                audioSource.time = audioNormalizedTime * audioSource.clip.length;
            }

            SetCurrentPhoto();
        });*/
        //UpdateTimelineTex();

        SetFps();


    }

    void TryToDeleteKey(int frame, int key, bool force = false)
    {
        if (photoData.photoData[frame].keyData[key].isKey || force)
        {
            photoData.photoData[frame].keyData[key].isKey = false;
            photoData.photoData[frame].keyData[key].key = 0f;
            weightSliders[key].Set(false);

            int leftKey = -1;
            for (int i = frame - 1; i >= frame - lerpDistance; i--)
            {
                if (i < 0 || i >= photoData.photoData.Count-1)
                    continue;
                if (photoData.photoData[i].keyData[key].isKey)
                {
                    leftKey = i;
                    break;
                }

                photoData.photoData[i].keyData[key].key = 0f;
            }

            int rightKey = -1;
            for (int i = frame + 1; i < frame + lerpDistance; i++)
            {
                if (i < 0 || i >= photoData.photoData.Count-1)
                    continue;
                if (photoData.photoData[i].keyData[key].isKey)
                {
                    rightKey = i;
                    break;
                }

                photoData.photoData[i].keyData[key].key = 0f;
            }

            if (leftKey != -1) LerpKeysAroundKey(leftKey, key);
            if (rightKey != -1) LerpKeysAroundKey(rightKey, key);
                    
            //timeline.RedrawAll();
            //UpdateTimelineTex();
        }
    }

    void LerpKeysAroundKey(int keyNum, int lineNum)
    {

        if (keyNum < 0 || keyNum >= photoData.photoData.Count - 1)
            return;
        
        // Smooth all not keys around this key
        int nearestLeftKey = Mathf.Clamp(keyNum - lerpDistance, 0, photoData.photoData.Count-1);
        for (int i = keyNum - 1; i >= keyNum - lerpDistance; i--)
        {
            if (i < 0 || i >= photoData.photoData.Count-1)
                continue;
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
                if (i < 0 || i >= photoData.photoData.Count-1)
                    continue;
                float alpha = (float)(i - nearestLeftKey) / (float)(keyNum - nearestLeftKey);
                photoData.photoData[i].keyData[lineNum].key = Mathf.Lerp(photoData.photoData[nearestLeftKey].keyData[lineNum].key,
                    photoData.photoData[keyNum].keyData[lineNum].key, alpha);
            }
        }

        // Smooth all not keys around this key
        int nearestRightKey = Mathf.Clamp(keyNum + lerpDistance, 0, photoData.photoData.Count-1);
        for (int i = keyNum + 1; i <= keyNum + lerpDistance; i++)
        {
            if (i < 0 || i >= photoData.photoData.Count-1)
                continue;
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
                if (i < 0 || i >= photoData.photoData.Count-1)
                    continue;
                float alpha = (float)(nearestRightKey - i) / (float)(nearestRightKey - keyNum);
                photoData.photoData[i].keyData[lineNum].key = Mathf.Lerp(photoData.photoData[keyNum].keyData[lineNum].key,
                    photoData.photoData[nearestRightKey].keyData[lineNum].key, 1f - alpha);
            }
        }
    }

    /*void UpdateTimelineTex()
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
        
    }*/

    public static void GenerateMaxScriptWithMorphs(string path, AllPhotosData photosData)
    {
        string maxScriptOut = "morphIndex = #(";
        for (int k = 0; k < photosData.photoData[0].keyData.Count; k++)
        {
            if (k > 0) maxScriptOut += ",";
            maxScriptOut += "" + (k + 1);
        }
        maxScriptOut += ")\nfor i = 1 to 100 do\n(\n\tif (WM3_MC_HasData $.morpher i) == true then\n\t(\n";
        for (int k = 0; k < photosData.photoData[0].keyData.Count; k++)
        {
            maxScriptOut += "\t\tif (WM3_MC_GetName $.morpher i) == \"" + photosData.photoData[0].keyData[k].name + "\" then morphIndex[" + (k + 1) + "] = i\n";
        }
        maxScriptOut += "\t)\n)\n\n";

        maxScriptOut += "n = 0\n\n";

        maxScriptOut += "animate on\n(\n";
        
        for (int k = 0; k < photosData.photoData.Count; k++)
        {
            for (int i = 0; i < photosData.photoData[k].keyData.Count; i++)
            {

                bool needWrite = false;

                /*if (photosData.photoData[k].keyData[i].key > 0f)
                    needWrite = true;
                else
                {
                    if (k == 0 || k == photosData.photoData.Count - 1)
                        needWrite = true;
                    else
                    {
                        if (photosData.photoData[k - 1].keyData[i].key > 0f ||
                            photosData.photoData[k + 1].keyData[i].key > 0f)
                            needWrite = true;
                    }
                }*/

                if (photosData.photoData[k].keyData[i].isKey)
                    needWrite = true;
                else if( photosData.photoData[k].keyData[i].key < 0.1f )
                {
                    if (k > 0 && photosData.photoData[k - 1].keyData[i].key > 0.1f)
                        needWrite = true;
                    if (k < (photosData.photoData.Count-1) && photosData.photoData[k + 1].keyData[i].key > 0.1f)
                        needWrite = true;
                }
                
                if( needWrite )
                    maxScriptOut += "\tat time( n+ " + (k * 1) + " ) WM3_MC_SetValue $.morpher morphIndex[" + (i + 1) + "] " + Mathf.RoundToInt(photosData.photoData[k].keyData[i].key) + ".0\n";
            }
        }
        
        maxScriptOut += ")";
        System.IO.File.WriteAllText(path, maxScriptOut);
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

    public AllPhotosData()
    {
        photoData = new List<PhotoData>();
    }
}

using System.Collections.Generic;
using Unity.Sentis;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using System.Collections;
using UnityEngine.Events;

public class RunYOLO : MonoBehaviour
{
    public ModelAsset modelAsset; 
    //const string modelName = "yolov8m-oiv7.sentis";
    const string modelName = "yolov7-tiny.sentis";
    // Change this to the name of the video you put in StreamingAssets folder:
    //const string videoName = "giraffe.mp4";

    public RawImage inputImage;
    // Link the classes.txt here:
    public TextAsset labelsAsset;
    // Create a Raw Image in the scene and link it here:
    public RawImage displayImage;
    // Link to a bounding box texture here:
    public Sprite borderSprite;
    public Texture2D borderTexture;
    // Link to the font for the labels:
    public Font font;

    private Transform displayLocation;
    private Model model;
    private Worker engine;
    private string[] labels;
    private RenderTexture targetRT;
    const BackendType backend = BackendType.GPUPixel;

    //Image size for the model
    private const int imageWidth = 640;
    private const int imageHeight = 640;

    private const int questWidth = 4128;
    private const int questHeight = 2208; //based on Wikipedia

    private VideoPlayer video;

    List<GameObject> boxPool = new List<GameObject>();

    public UnityEvent<RenderTexture> OnNewFrameEnabled = new();
    //bounding box data
    

    void Setup()
    {
        //Application.targetFrameRate = 60;
        Screen.orientation = ScreenOrientation.LandscapeLeft;

        //Parse neural net labels
        labels = labelsAsset.text.Split('\n');

        //Load model
        model = ModelLoader.Load(modelAsset);
        //model = ModelLoader.Load(Application.streamingAssetsPath + "/" + modelName);

        //Create image to display video
        displayLocation = displayImage.transform;

        targetRT = new RenderTexture(imageWidth, imageHeight, 0);
        targetRT.enableRandomWrite = true;
        targetRT.Create();

        //Create engine to run model
        engine = new Worker(model, backend);

        //SetupInput();

        if (borderSprite == null)
        {
            borderSprite = Sprite.Create(borderTexture, new Rect(0, 0, borderTexture.width, borderTexture.height), new Vector2(borderTexture.width / 2, borderTexture.height / 2));
        }

        //StartCoroutine(runModelCoroutine());
    }
    // void SetupInput()
    // {
    //     video = gameObject.AddComponent<VideoPlayer>();
    //     video.renderMode = VideoRenderMode.APIOnly;
    //     video.source = VideoSource.Url;
    //     video.url = Application.streamingAssetsPath + "/" + videoName;
    //     video.isLooping = true;
    //     video.Play();
    // }

    private IEnumerator runModelCoroutine() {
        yield return new WaitForSeconds(10f);
        ExecuteML();
    }

    public void ExecuteML(Texture2D input) {
        inputImage.texture = input;
        Setup();
        ExecuteML();
    }

    public void ExecuteML()
    {
        ClearAnnotations();
        // if (video && video.texture)
        // {
        //     float aspect = video.width * 1f / video.height;
        //     Graphics.Blit(video.texture, targetRT, new Vector2(1f / aspect, 1), new Vector2(0, 0));
        //     displayImage.texture = targetRT;
        // }
        // else if (inputImage) {
        //     float aspect = inputImage.texture.width * 1f / inputImage.texture.height;
        //     Graphics.Blit(inputImage.texture, targetRT, new Vector2(1f / aspect, 1), new Vector2(0, 0));
        //     displayImage.texture = inputImage.texture;
        // }
        if (inputImage.texture) {
            float aspect = inputImage.texture.width * 1f / inputImage.texture.height;
            Graphics.Blit(inputImage.texture, targetRT, new Vector2(1f / aspect, 1), new Vector2(0, 0));
            displayImage.texture = inputImage.texture;
        }
        //OnNewFrameEnabled.Invoke(inputImage.texture as RenderTexture);
        // if (targetRT == null) {
        //     DRmanager.Instance.text.text += "TargetRT is null";
        // }
        // if (targetRT.IsCreated() == false) {
        //     DRmanager.Instance.text.text += "TargetRT is not created";
        // }
        // if (targetRT.enableRandomWrite == false) {
        //     DRmanager.Instance.text.text += "TargetRT is not random write";
        // }
        try {
            Tensor<float> input = TextureConverter.ToTensor(targetRT, imageWidth, imageHeight, 3);
            engine.Schedule(input);
        }
        catch (System.Exception e) {
            DRmanager.Instance.text.text += e.Message;
        }

        //Read output tensors
        var output1 = engine.PeekOutput() as Tensor<float>;
        var output = output1.ReadbackAndClone();
        output1.Dispose();

        float displayWidth = displayImage.rectTransform.rect.width;
        float displayHeight = displayImage.rectTransform.rect.height;

        float scaleX = displayWidth / imageWidth;
        float scaleY = displayHeight / imageHeight;

        int foundBoxes = output.shape[0];

        float scaleXquest = (float)questWidth / imageWidth;
        float scaleYquest = (float)questHeight / imageHeight;

        DRmanager.Instance.text.text += "Found Boxes: " + foundBoxes + "\n";


        //Draw the bounding boxes
        // for (int n = 0; n < foundBoxes; n++)
        // {
        //     //Debug.Log("Box Number is " + (int)output[n, 5]);
        //     //filter the bounding boxes
        //     if (!LLManager.Instance.FindLabelIndex((output[n,5]).ToString())) {
        //         var box = new BoundingBox
        //         {
        //             centerX = ((output[n, 1] + output[n, 3]) * scaleX - displayWidth) / 2,
        //             centerY = ((output[n, 2] + output[n, 4]) * scaleY - displayHeight) / 2,
        //             width = (output[n, 3] - output[n, 1]) * scaleX,
        //             height = (output[n, 4] - output[n, 2]) * scaleY,
        //             label = labels[(int)output[n, 5]],
        //             confidence = Mathf.FloorToInt(output[n, 6] * 100 + 0.5f)
        //         };
        //         DrawBox(box, n);
        //     } else {
        //         continue;
        //     }
        // }

        for (int n = 0; n < foundBoxes; n++)
        {
            //Debug.Log("Box Number is " + (int)output[n, 5]);
            //filter the bounding boxes
            // Debug.Log(scaleXquest + " " + scaleYquest);
            // Debug.Log(questWidth + " " + questHeight);
            // Debug.Log(output[n, 1] + " " + output[n, 3] + " " + output[n, 2] + " " + output[n, 4]);

            // var box = new BoundingBox
            // {
            //     centerX = ((output[n, 1] + output[n, 3]) * scaleX - displayWidth) / 2,
            //     centerY = ((output[n, 2] + output[n, 4]) * scaleY - displayHeight) / 2,
            //     width = (output[n, 3] - output[n, 1]) * scaleX,
            //     height = (output[n, 4] - output[n, 2]) * scaleY,
            //     label = labels[(int)output[n, 5]],
            //     confidence = Mathf.FloorToInt(output[n, 6] * 100 + 0.5f)
            // };
            var box = new BoundingBox
            {
                centerX = ((output[n, 1] + output[n, 3]) * scaleXquest - questWidth) / 2,
                centerY = ((output[n, 2] + output[n, 4]) * scaleYquest - questHeight) / 2,
                width = (output[n, 3] - output[n, 1]) * scaleXquest,
                height = (output[n, 4] - output[n, 2]) * scaleYquest,
                label = labels[(int)output[n, 5]],
                confidence = Mathf.FloorToInt(output[n, 6] * 100 + 0.5f)
            };
            //DrawBox(box, n);
            //Debug.Log(box.centerX + " " + box.centerY + " " + box.width + " " + box.height);
            if (box.label != "keyboard" || box.label != "mouse" || box.label != "tv monitor") {
                DRmanager.Instance.drawBox(box, n);
            }
            DrawBox(box, n);
        }
        output.Dispose();
    }

    public void DrawBox(BoundingBox box, int id)
    {
        //Create the bounding box graphic or get from pool
        GameObject panel;
        if (id < boxPool.Count)
        {
            panel = boxPool[id];
            panel.SetActive(true);
        }
        else
        {
            panel = CreateNewBox(Color.yellow);
        }
        //Set box position
        panel.transform.localPosition = new Vector3(box.centerX, -box.centerY);

        //Set box size
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(box.width, box.height);

        //Set label text
        var label = panel.GetComponentInChildren<Text>();
        label.text = box.label + " (" + box.confidence + "%)";
    }

    public GameObject CreateNewBox(Color color)
    {
        //Create the box and set image
        var panel = new GameObject("ObjectBox");
        panel.AddComponent<CanvasRenderer>();
        Image img = panel.AddComponent<Image>();
        img.color = color;
        img.sprite = borderSprite;
        img.type = Image.Type.Sliced;
        panel.transform.SetParent(displayLocation, false);

        //Create the label
        var text = new GameObject("ObjectLabel");
        text.AddComponent<CanvasRenderer>();
        text.transform.SetParent(panel.transform, false);
        Text txt = text.AddComponent<Text>();
        txt.font = font;
        txt.color = color;
        txt.fontSize = 40;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;

        RectTransform rt2 = text.GetComponent<RectTransform>();
        rt2.offsetMin = new Vector2(20, rt2.offsetMin.y);
        rt2.offsetMax = new Vector2(0, rt2.offsetMax.y);
        rt2.offsetMin = new Vector2(rt2.offsetMin.x, 0);
        rt2.offsetMax = new Vector2(rt2.offsetMax.x, 30);
        rt2.anchorMin = new Vector2(0, 0);
        rt2.anchorMax = new Vector2(1, 1);

        boxPool.Add(panel);
        return panel;
    }

    public void ClearAnnotations()
    {
        foreach (var box in boxPool)
        {
            box.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        engine?.Dispose();
    }
}
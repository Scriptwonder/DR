using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using Unity.Sentis;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.Video;

public class RunYOLOs : MonoBehaviour
{
    private ModelAsset modelAsset;

    Model runtimeModel;
    
    const string modelName = "yolov8m-oiv7.onnx";

    const string picName = "curFrame.jpg";

    public Texture2D inputImage;

    public RawImage displayImage;

    public Texture2D outputImage;

    private Worker engine;

    public UnityEvent<Texture2D> onStart = new();

    public Sprite borderSprite;
    public Texture2D borderTexture;

    private const int imageWidth = 640;
    private const int imageHeight = 640;

    private string[] labels;
    private RenderTexture targetRT;
    public Font font;
    public TextAsset labelsAsset;

    List<GameObject> boxPool = new List<GameObject>();
    private Transform displayLocation;

    public struct BoundingBox
    {
        public float centerX;
        public float centerY;
        public float width;
        public float height;
        public string label;
        public float confidence;
    }

    private void Awake()
    {
        //modelAsset = Resources.Load(modelName) as ModelAsset;
        //runtimeModel = ModelLoader.Load(modelAsset);
        runtimeModel = ModelLoader.Load(Application.streamingAssetsPath + "/yolov8m-oiv7.sentis");
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        onStart.Invoke(outputImage);

        labels = labelsAsset.text.Split('\n');

        targetRT = new RenderTexture(imageWidth, imageHeight, 0);
        engine = new Worker(runtimeModel, BackendType.GPUCompute);
        runModel();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void runModel() {
        //clear
        ClearAnnotations();
        float aspect = inputImage.width * 1f / inputImage.height;
        Graphics.Blit(inputImage, targetRT, new Vector2(1f / aspect, 1), new Vector2(0, 0));
        displayImage.texture = targetRT;

        //run model on the input image
        engine.Schedule(Tex2Tensor(ResizeTexture(inputImage, imageHeight, imageWidth)));


        //process the output tensor
        var output = engine.PeekOutput() as Tensor<float>;
        output = output.ReadbackAndClone();

        float displayWidth = displayImage.rectTransform.rect.width;
        float displayHeight = displayImage.rectTransform.rect.height;

        float scaleX = displayWidth / imageWidth;
        float scaleY = displayHeight / imageHeight;
        Debug.Log("tensorShape: " + output.shape);
        int foundBoxes = output.shape[0];
        
        //Draw the bounding boxes
        for (int n = 0; n < foundBoxes; n++)
        {
            var box = new BoundingBox
            {
                centerX = ((output[n, 1] + output[n, 3]) * scaleX - displayWidth) / 2,
                centerY = ((output[n, 2] + output[n, 4]) * scaleY - displayHeight) / 2,
                width = (output[n, 3] - output[n, 1]) * scaleX,
                height = (output[n, 4] - output[n, 2]) * scaleY,
                label = labels[(int)output[n, 5]],
                confidence = Mathf.FloorToInt(output[n, 6] * 100 + 0.5f)
            };
            DrawBox(box, n);
        }
        //outputImage = Tensor2Tex();
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

    private Texture2D convertInput(Texture2D tex) {
    // Convert input image to tensor
        Texture2D resizedTex = new Texture2D(640, 640, TextureFormat.RGB24, false);
        
        // Use RenderTexture to resize and copy the texture
        RenderTexture rt = RenderTexture.GetTemporary(640, 640);
        Graphics.Blit(tex, rt);
        RenderTexture.active = rt;
        resizedTex.ReadPixels(new Rect(0, 0, 640, 640), 0, 0);
        resizedTex.Apply();
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);

        return resizedTex;
}

    private Tensor<float> Tex2Tensor(Texture2D tex) {
        // Tensor<float> inputTensor = TextureConverter.ToTensor(tex);
        // return inputTensor;
        // Ensure the texture is in RGB format
        Color32[] pixels = tex.GetPixels32();
        float[] tensorData = new float[640 * 640 * 3];
        for (int i = 0; i < pixels.Length; i++) {
            tensorData[i * 3] = pixels[i].r / 255.0f;
            tensorData[i * 3 + 1] = pixels[i].g / 255.0f;
            tensorData[i * 3 + 2] = pixels[i].b / 255.0f;
        }

        // Create the tensor with the correct shape
        Tensor<float> tensor = new Tensor<float>(new TensorShape(1, 3, 640, 640), tensorData);

        Debug.Log("Tensor shape: " + tensor.shape);
        return tensor;
    }

    private Tensor<float> Tex2Tensor(string TextureName) {
        Texture2D inputTex = Resources.Load(TextureName) as Texture2D;
        Tensor<float> inputTensor = TextureConverter.ToTensor(inputTex);
        return inputTensor;
    }

    //tensor2Tex2D
    private Texture2D Tensor2Tex(Tensor<float> tensor) {
        RenderTexture outputTex = TextureConverter.ToTexture(tensor);
        Texture2D outputTex2D = new Texture2D(outputTex.width, outputTex.height);
        Graphics.CopyTexture(outputTex, outputTex2D);
        return outputTex2D;
    }

    private Texture2D Tensor2Tex() {
        Tensor<float> tensor = engine.PeekOutput() as Tensor<float>;
        //Tensor<float> slicedTensor = tensor.Slice(1,3);
        RenderTexture outputTex = TextureConverter.ToTexture(tensor);
        Texture2D outputTex2D = new Texture2D(outputTex.width, outputTex.height);
        Graphics.CopyTexture(outputTex, outputTex2D);
        return outputTex2D;
    }

    void OnDisable()
    {
       engine.Dispose(); 
    }

    private Texture2D ResizeTexture(Texture2D source, int width, int height)
    {
        RenderTexture rt = RenderTexture.GetTemporary(width, height);
        RenderTexture.active = rt;
        Graphics.Blit(source, rt);

        Texture2D result = new Texture2D(width, height, TextureFormat.RGB24, false);
        result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        result.Apply();

        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);

        return result;
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

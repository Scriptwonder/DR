using System.Data.Common;
using Anaglyph.DisplayCapture;
using TMPro;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.Events;

public struct BoundingBox
{
        public float centerX;
        public float centerY;
        public float width;
        public float height;
        public string label;
        public float confidence;
}

public class DRmanager : MonoBehaviour
{
    private Texture2D screenTexture;

    [SerializeField] private Texture2D temp;

    [SerializeField] private RunYOLO runYOLO;

    [SerializeField] private DepthCast depthCast;

    [SerializeField] private GameObject boundingBoxPrefab;

    private static DRmanager instance;

    private Camera cam;
    private GameObject parentTrans;

    public TMP_Text text;

    

    public UnityEvent<Texture2D> OnNewFrameEnabled = new();

    public static DRmanager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<DRmanager>();
                
                // Create new instance if none exists
                if (instance == null)
                {
                    Debug.LogError("No instance of DRmanager found in scene. Please add one to the scene.");   
                    // GameObject go = new GameObject("DRmanager");
                    // instance = go.AddComponent<DRmanager>();
                }
            }
            return instance;
        }
    }

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }

        cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("Main camera not found. Make sure a camera is tagged as 'MainCamera'.");
        }

        if (parentTrans == null)
        {
            parentTrans = new GameObject("BoundingBoxes");
        }

        DontDestroyOnLoad(gameObject);
        
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        if (OVRInput.GetDown(OVRInput.Button.One) || Input.GetKeyDown(KeyCode.Space))
			{
				Debug.Log("Start Screen Capture");
                if (temp != null) {
				    RunYOLO(temp);
                } else {
                    Debug.Log("Temp is null");
                }
			}
    }

    public void pipeline() {
        //first get the texture from the MediaProjector
        CopyTextureToScreen(DisplayCaptureManager.Instance.ScreenCaptureTexture);

        OnNewFrameEnabled.Invoke(screenTexture);

        //run YOLO Model with the texture, will require possible down sampling
        RunYOLO(screenTexture);

        //retrieve the boxes and find the depth associated with each
    }

    //copy the texture to the screen
    public void CopyTextureToScreen(Texture2D texture)
    {
        screenTexture = texture;
    }

    public void RunYOLO(Texture2D texture = null)
    {
        if (texture != null)
        {
            runYOLO.ExecuteML(texture);
        }
        else
        {
            runYOLO.ExecuteML();
        }
    }

    // public void viewToWorldSpace(Vector2 viewSpaceCoordinate)
    // {
    //     //convert the view space coordinate to world space
    //     Matrix4x4 projectionMatrix = Camera.main.projectionMatrix;
    //     Matrix4x4 viewMatrix = Camera.main.worldToCameraMatrix;
    //     Matrix4x4 mvp = projectionMatrix * viewMatrix;
    //     Vector3 worldSpaceCoordinate = mvp.inverse.MultiplyPoint(new Vector3(viewSpaceCoordinate.x, viewSpaceCoordinate.y, 0));
    // }

    public void drawBox(BoundingBox box, int n) {
        //draw one single box on the screen
        //LocalPosition should be around box.centerX, -box.centerY
        //size is based on box.width and box.height
        //label and confidence is for LLM so does not matter right now

        //create the bounding box graphic
        //Debug.Log(box.centerX/640f + " " + box.centerY/640f + " " + box.width/640f + " " + box.height/640f);
        // Debug.Log(box.centerX/4128f + " " + box.centerY/2208f + " " + box.width/4128f + " " + box.height/2208f);     
        Vector3 worldCoordinate = depthCast.GetWorldSpaceCoordinate(new Vector2(box.centerX/4128f + 0.5f, -box.centerY/2208f + 0.5f));
        GameObject panel = Instantiate(boundingBoxPrefab, new Vector3(box.centerX/4128f + cam.gameObject.transform.position.x, -box.centerY/2208f + cam.gameObject.transform.position.y, worldCoordinate.z), Quaternion.identity);
        panel.transform.SetParent(parentTrans.transform);
        panel.name = box.label + n;
        
        //panel.transform.localScale = new Vector3(box.width, box.height, 1);
        // Vector3 worldCoordL1 = depthCast.GetWorldSpaceCoordinate(new Vector2((box.centerX - box.width/2)/4128f + 0.5f, (-box.centerY - box.height/2)/2208f+ 0.5f));
        // Vector3 worldCoordL2 = depthCast.GetWorldSpaceCoordinate(new Vector2((box.centerX + box.width/2)/4128f + 0.5f, (-box.centerY + box.height/2)/2208f+ 0.5f));
        // float worldWidth = Mathf.Abs(worldCoordL1.x - worldCoordL2.x);
        // float worldHeight = Mathf.Abs(worldCoordL1.y - worldCoordL2.y);

        float worldWidth = box.width/4128f;
        float worldHeight = box.height/2208f;
        panel.transform.localScale = new Vector3(worldWidth * 3, worldHeight * 3, 0.01f);

        // text.text += "Box " + n + " " + box.label + " drawn at " + worldCoordinate + " with size " + worldWidth + " " + worldHeight + "\n"; 
        //Debug.Log("Box " + n + " " + box.label + " drawn at " + worldCoordinate + " with size " + worldWidth + " " + worldHeight);

    }
}

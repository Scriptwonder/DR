using UnityEngine;
using System;
using UnityEngine.Events;

//Modified with the code from Trev3D https://github.com/trev3d/QuestDisplayAccessDemo

[DefaultExecutionOrder(-1000)]
public class MediaProjection : MonoBehaviour
{
    //android setup
    private AndroidJavaObject byteBuffer;
    private unsafe sbyte* imageData;
    private int bufferSize;
    public static MediaProjection Instance { get; private set; }

    private AndroidJavaClass UnityPlayer;
    private AndroidJavaObject UnityPlayerActivityWithMediaProjector;

    private Texture2D screenTexture;
    private RenderTexture flipTexture;
	public Texture2D ScreenCaptureTexture => screenTexture;

    public bool startScreenCaptureOnStart = true;
	public bool flipTextureOnGPU = false;

    public UnityEvent<Texture2D> OnTextureInitialized = new();
    public UnityEvent OnScreenCaptureStarted = new();
    public UnityEvent OnScreenCapturePermissionDeclined = new();
    public UnityEvent OnScreenCaptureStopped = new();
    public UnityEvent OnNewFrameIncoming = new();
    public UnityEvent OnNewFrame = new();

    public static readonly Vector2Int Size = new(1024, 1024);
    
    private void Awake()
    {
        Instance = this;
        screenTexture = new Texture2D(Size.x, Size.y, TextureFormat.RGBA32, 1, false);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        UnityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        UnityPlayerActivityWithMediaProjector = UnityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

        flipTexture = new RenderTexture(Size.x, Size.y, 0, RenderTextureFormat.ARGB32);
        flipTexture.Create();

        OnTextureInitialized.Invoke(screenTexture);
        if (startScreenCaptureOnStart)
        {
            StartScreenCapture();
        }
        bufferSize = Size.x * Size.y * 4;   // RGBA_8888 format: 4 bytes per pixel
    }

    private unsafe void InitializeByteBufferRetrieved()
	{
		// Retrieve the ByteBuffer from Java and cache it
		byteBuffer = UnityPlayerActivityWithMediaProjector.Call<AndroidJavaObject>("getLastFrameBytesBuffer");

		// Get the memory address of the direct ByteBuffer
		imageData = AndroidJNI.GetDirectBufferAddress(byteBuffer.GetRawObject());
	}

	public void StartScreenCapture()
	{
		UnityPlayerActivityWithMediaProjector.Call("startScreenCaptureWithPermission", gameObject.name, Size.x, Size.y);
	}

	public void StopScreenCapture()
	{
		UnityPlayerActivityWithMediaProjector.Call("stopScreenCapture");
	}

	// Messages sent from android activity

	private void ScreenCaptureStarted()
	{
		OnScreenCaptureStarted.Invoke();
		InitializeByteBufferRetrieved();
	}

	private void ScreenCapturePermissionDeclined()
	{
		OnScreenCapturePermissionDeclined.Invoke();
	}

	private void NewFrameIncoming()
	{
		OnNewFrameIncoming.Invoke();
	}

	private unsafe void NewFrameAvailable()
	{
		if (imageData == default) return;
		screenTexture.LoadRawTextureData((IntPtr)imageData, bufferSize);
		screenTexture.Apply();

		if (flipTextureOnGPU)
		{
			Graphics.Blit(screenTexture, flipTexture, new Vector2(1, -1), Vector2.zero);
			Graphics.CopyTexture(flipTexture, screenTexture);
		}

		OnNewFrame.Invoke();
	}

	private void ScreenCaptureStopped()
	{
		OnScreenCaptureStopped.Invoke();
	}
}

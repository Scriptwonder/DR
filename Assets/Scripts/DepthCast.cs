using UnityEngine;
using System.Linq;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using Meta.XR.EnvironmentDepth;
using static Unity.XR.Oculus.Utils;
using static OVRPlugin;

//based on @TudorJude at https://github.com/oculus-samples/Unity-DepthAPI/issues/49

[DefaultExecutionOrder(-30)]
public class DepthCast : MonoBehaviour
{
    private static readonly int raycastResultsId = Shader.PropertyToID("RaycastResults");
    private static readonly int raycastRequestsId = Shader.PropertyToID("RaycastRequests");

    [SerializeField] private ComputeShader _computeShader;

    [SerializeField] private Camera _centerEyeCamera;
    [SerializeField] private GameObject _leftControllerAnchor;
    [SerializeField] private GameObject _ObjectToPlace;

    private ComputeBuffer _requestsCB;
    private ComputeBuffer _resultsCB;

    private readonly Matrix4x4[] _threeDofReprojectionMatrices = new Matrix4x4[2];

    public struct DepthRaycastResult {
        public Vector3 Position;
        public Vector3 Normal;
    }

    public void RaycastViewSpaceBlocking(List<Vector2> viewSpaceCoords, out List<DepthRaycastResult> result) {
        result = DispatchCompute(viewSpaceCoords);
    }
    public DepthRaycastResult RaycastViewSpaceBlocking(Vector2 viewSpaceCoord) {
        var depthRaycastResult = DispatchCompute(new List<Vector2>() { viewSpaceCoord });
        return depthRaycastResult[0];
    }

    private List<DepthRaycastResult> DispatchCompute(List<Vector2> requestedPositions) {
        UpdateCurrentRenderingState();

        int count = requestedPositions.Count;

        var (requestsCB, resultsCB) = GetComputeBuffers(count);
        requestsCB.SetData(requestedPositions);

        _computeShader.SetBuffer(0, raycastRequestsId, requestsCB);
        _computeShader.SetBuffer(0, raycastResultsId, resultsCB);

        _computeShader.Dispatch(0, count, 1, 1);

        var raycastResults = new DepthRaycastResult[count];
        resultsCB.GetData(raycastResults);

        return raycastResults.ToList();
    }

    (ComputeBuffer, ComputeBuffer) GetComputeBuffers(int size) {
        if (_requestsCB != null && _resultsCB != null && _requestsCB.count != size) {
            _requestsCB.Release();
            _requestsCB = null;
            _resultsCB.Release();
            _resultsCB = null;
        }

        if (_requestsCB == null || _resultsCB == null) {
            _requestsCB = new ComputeBuffer(size, Marshal.SizeOf<Vector2>(), ComputeBufferType.Structured);
            _resultsCB = new ComputeBuffer(size, Marshal.SizeOf<DepthRaycastResult>(), ComputeBufferType.Structured);
        }

        return (_requestsCB, _resultsCB);
    }

    private void UpdateCurrentRenderingState()
    {
        // Get Frame Descs
        var leftEyeData = GetEnvironmentDepthFrameDesc(0);
        var rightEyeData = GetEnvironmentDepthFrameDesc(1);

        OVRPlugin.GetNodeFrustum2(OVRPlugin.Node.EyeLeft, out var leftEyeFrustrum);
        OVRPlugin.GetNodeFrustum2(OVRPlugin.Node.EyeRight, out var rightEyeFrustrum);
        _threeDofReprojectionMatrices[0] = Calculate3DOFReprojection(leftEyeData, leftEyeFrustrum.Fov);
        _threeDofReprojectionMatrices[1] = Calculate3DOFReprojection(rightEyeData, rightEyeFrustrum.Fov);


        _computeShader.SetTextureFromGlobal(0, Shader.PropertyToID("_EnvironmentDepthTexture"),
            Shader.PropertyToID("_EnvironmentDepthTexture"));
        _computeShader.SetMatrixArray(Shader.PropertyToID("_EnvironmentDepthReprojectionMatrices"),
            _threeDofReprojectionMatrices);
        _computeShader.SetVector(Shader.PropertyToID("_EnvironmentDepthZBufferParams"),
            Shader.GetGlobalVector(Shader.PropertyToID("_EnvironmentDepthZBufferParams")));

        // See UniversalRenderPipelineCore for property IDs
        _computeShader.SetVector("_ZBufferParams", Shader.GetGlobalVector("_ZBufferParams"));
        _computeShader.SetMatrixArray("unity_StereoMatrixInvVP",
            Shader.GetGlobalMatrixArray("unity_StereoMatrixInvVP"));
    }

    private void OnDestroy()
    {
        _resultsCB.Release();
    }

    internal static Matrix4x4 Calculate3DOFReprojection(EnvironmentDepthFrameDesc frameDesc, Fovf fov)
    {
        // Screen To Depth represents the transformation matrix used to map normalised screen UV coordinates to
        // normalised environment depth texture UV coordinates. This needs to account for 2 things:
        // 1. The field of view of the two textures may be different, Unreal typically renders using a symmetric fov.
        //    That is to say the FOV of the left and right eyes is the same. The environment depth on the other hand
        //    has a different FOV for the left and right eyes. So we need to scale and offset accordingly to account
        //    for this difference.
        var screenCameraToScreenNormCoord = MakeUnprojectionMatrix(
            fov.RightTan, fov.LeftTan,
            fov.UpTan, fov.DownTan);

        var depthNormCoordToDepthCamera = MakeProjectionMatrix(
            frameDesc.fovRightAngle, frameDesc.fovLeftAngle,
            frameDesc.fovTopAngle, frameDesc.fovDownAngle);

        // 2. The headset may have moved in between capturing the environment depth and rendering the frame. We
        //    can only account for rotation of the headset, not translation.
        var depthCameraToScreenCamera = MakeScreenToDepthMatrix(frameDesc);

        var screenToDepth = depthNormCoordToDepthCamera * depthCameraToScreenCamera *
                            screenCameraToScreenNormCoord;

        return screenToDepth;
    }

    private static Matrix4x4 MakeScreenToDepthMatrix(EnvironmentDepthFrameDesc frameDesc)
    {
        // The pose extrapolated to the predicted display time of the current frame
        // assuming left eye rotation == right eye
        var screenOrientation =
            GetNodePose(Node.EyeLeft, Step.Render).Orientation.FromQuatf();

        var depthOrientation = new Quaternion(
            -frameDesc.createPoseRotation.x,
            -frameDesc.createPoseRotation.y,
            frameDesc.createPoseRotation.z,
            frameDesc.createPoseRotation.w
        );

        var screenToDepthQuat = (Quaternion.Inverse(screenOrientation) * depthOrientation).eulerAngles;
        screenToDepthQuat.z = -screenToDepthQuat.z;

        return Matrix4x4.Rotate(Quaternion.Euler(screenToDepthQuat));
    }

    private static Matrix4x4 MakeProjectionMatrix(float rightTan, float leftTan, float upTan, float downTan)
    {
        var matrix = Matrix4x4.identity;
        float tanAngleWidth = rightTan + leftTan;
        float tanAngleHeight = upTan + downTan;

        // Scale
        matrix.m00 = 1.0f / tanAngleWidth;
        matrix.m11 = 1.0f / tanAngleHeight;

        // Offset
        matrix.m03 = leftTan / tanAngleWidth;
        matrix.m13 = downTan / tanAngleHeight;
        matrix.m23 = -1.0f;

        return matrix;
    }

    private static Matrix4x4 MakeUnprojectionMatrix(float rightTan, float leftTan, float upTan, float downTan)
    {
        var matrix = Matrix4x4.identity;

        // Scale
        matrix.m00 = rightTan + leftTan;
        matrix.m11 = upTan + downTan;

        // Offset
        matrix.m03 = -leftTan;
        matrix.m13 = -downTan;
        matrix.m23 = 1.0f;

        return matrix;
    }


    private void Awake()
    {
        _resultsCB?.Release();
        _resultsCB = null;

        _centerEyeCamera = Camera.main;
    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        //will be called if the user hold left hand trigger button
        if (OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch))
        {
                // Raycasting at the controller anchor's position
            var worldSpaceCoordinate =
                _leftControllerAnchor.transform.position + _leftControllerAnchor.transform.forward * 0.1f;

            // Convert world space to the view center in left eye's view coordinate system
            var viewSpaceCoordinate =
                _centerEyeCamera.WorldToViewportPoint(worldSpaceCoordinate,
                Camera.MonoOrStereoscopicEye.Left);

            // Perform a ray cast
            var raycastResult = RaycastViewSpaceBlocking(viewSpaceCoordinate);

            // position some object on the ray hit
            _ObjectToPlace.transform.position = raycastResult.Position;

            // use normal to rotate the indicator (be aware that LookRotation takes forward not up direction)
            //_ObjectToPlace.transform.rotation = Quaternion.LookRotation(raycastResult.Normal);
        }
        
    }
}

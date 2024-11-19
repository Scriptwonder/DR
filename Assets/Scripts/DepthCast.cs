using UnityEngine;
using System.Linq;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using Meta.XR.EnvironmentDepth;
using static Unity.XR.Oculus.Utils;
using static OVRPlugin;

//based on @TudorJude at https://github.com/oculus-samples/Unity-DepthAPI/issues/49

public class DepthCast : MonoBehaviour
{

    [SerializeField] private Camera _centerEyeCamera;
    [SerializeField] private GameObject _leftControllerAnchor;
    [SerializeField] private GameObject _ObjectToPlace;

    [SerializeField] private EnvironmentDepthAccess DepthAccess;

    [SerializeField] private EnvironmentDepthManager _depthManager;



    private void Awake()
    {
        _centerEyeCamera = Camera.main;
    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (_depthManager.IsDepthAvailable) {
            Vector2 coord = new Vector2(0.5f, 0.5f); 
            EnvironmentDepthAccess.DepthRaycastResult result = DepthAccess.RaycastViewSpaceBlocking(coord);
            //Debug.Log($"Raycast result: {result.Position}, {result.Normal}");
            //_ObjectToPlace.transform.position = result.Position;
        }
        // //will be called if the user hold left hand trigger button
        // // if (OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch))
        // // {
        //         // Raycasting at the controller anchor's position
        // var worldSpaceCoordinate = 
        //     _leftControllerAnchor.transform.position + _leftControllerAnchor.transform.forward * 0.1f;

        // // Convert world space to the view center in left eye's view coordinate system
        // var viewSpaceCoordinate = 
        //     _centerEyeCamera.WorldToViewportPoint(worldSpaceCoordinate,
        //     Camera.MonoOrStereoscopicEye.Left);

        // // position some object on the ray hit
        // _ObjectToPlace.transform.position = raycastResult.Position;

        // Debug.Log(raycastResult.Position);

        //     // use normal to rotate the indicator (be aware that LookRotation takes forward not up direction)
        //     //_ObjectToPlace.transform.rotation = Quaternion.LookRotation(raycastResult.Normal);
        // //}
        
    }
}

using UnityEngine;

public class UIAlignment : MonoBehaviour
{
    public Transform camera;

    private bool isSet;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        isSet = false;
    }

    // Update is called once per frame
    void Update()
    {
        if (camera != null && !isSet)
        {
            transform.SetParent(camera);
            transform.localPosition = new Vector3(0f, 0f, 1f);
            transform.localRotation = Quaternion.identity;
            isSet = true;
        }
    }
}

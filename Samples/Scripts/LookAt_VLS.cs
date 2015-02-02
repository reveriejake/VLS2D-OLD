using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Light2D))]
public class LookAt_VLS : MonoBehaviour
{
    public Transform target = null;
    public float smoothLookSpeed = 5.0f;

    private Vector3 tPos = Vector3.zero;
    private Light2D lightRef = null;

    void Start()
    {
        lightRef = gameObject.GetComponent<Light2D>();
    }

    void Update()
    {
        tPos = Vector3.Lerp(tPos, target.position, Time.deltaTime * smoothLookSpeed);
        lightRef.LookAt(tPos);
    }
}

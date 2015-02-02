using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SinRotate_VLS : MonoBehaviour 
{
    public Vector3 axis = new Vector3(0, 0, 1);
    public float magnitude = 5;
    public float speed = 5;

    void Update()
    {
        transform.eulerAngles = axis * Mathf.Sin(Time.time * speed) * magnitude;
    }
}

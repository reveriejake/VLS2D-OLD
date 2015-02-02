using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RandomAimRotate_VLS : MonoBehaviour 
{
    int position = 0;
    Vector3 tPos = Vector3.zero;
    Light2D l;

    void Start()
    {
        l = gameObject.GetComponent<Light2D>();
        InvokeRepeating("ChangePosition", 0, 1);
    }

    void Update()
    {
        tPos = Vector3.MoveTowards(tPos, new Vector3(0, position, 0), Time.deltaTime * 25f);
        l.LookAt(tPos);
    }

    void ChangePosition()
    {
        position = (int)(Random.Range(-4, 5) * 2);
    }
}

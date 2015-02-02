using UnityEngine;
using System.Collections;

public class Rotate_VLS : MonoBehaviour 
{
    public float speed = 100;
    public Vector3 Axis = new Vector3(0, 0, 1);

	void FixedUpdate ()
    {
        transform.Rotate(Axis * Time.deltaTime * speed);
	}
}

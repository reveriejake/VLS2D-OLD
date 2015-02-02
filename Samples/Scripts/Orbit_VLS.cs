using UnityEngine;
using System.Collections;

public class Orbit_VLS : MonoBehaviour 
{
    public Transform objectToOrbit = null;
    public Vector3 pointToOrbit = Vector3.zero;
    public float orbitSpeed = 25f;

	void Update () 
    {
        if(!objectToOrbit)
            transform.RotateAround(pointToOrbit, Vector3.forward, orbitSpeed * Time.deltaTime);
        else
            transform.RotateAround(objectToOrbit.position, Vector3.forward, orbitSpeed * Time.deltaTime);
	}
}

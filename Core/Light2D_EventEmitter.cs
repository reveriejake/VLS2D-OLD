using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Light2D))]
public class Light2D_EventEmitter : MonoBehaviour 
{
    public LayerMask eventLayer = -1;

    private Light2D kLight;
    private Light2D.LightTypeSetting kLightType;

    private List<GameObject> identifiedObjects = new List<GameObject>();
    private List<GameObject> unidentifiedObjects = new List<GameObject>();
    private Light2D.ColliderObjects objs = new Light2D.ColliderObjects();

    void Start()
    {
        kLight = GetComponent<Light2D>();
    }

    void Update()
    {
        unidentifiedObjects.Clear();
        CollectColliders();

        if (Application.isPlaying)
        {
            for (int i = 0; i < unidentifiedObjects.Count; i++)
            {
                if (identifiedObjects.Contains(unidentifiedObjects[i]))
                {
                    kLight.TriggerBeamEvent(LightEventListenerType.OnStay, unidentifiedObjects[i]);
                }

                if (!identifiedObjects.Contains(unidentifiedObjects[i]))
                {
                    identifiedObjects.Add(unidentifiedObjects[i]);

                    kLight.TriggerBeamEvent(LightEventListenerType.OnEnter, unidentifiedObjects[i]);
                }
            }

            for (int i = 0; i < identifiedObjects.Count; i++)
            {
                if (!unidentifiedObjects.Contains(identifiedObjects[i]))
                {
                    kLight.TriggerBeamEvent(LightEventListenerType.OnExit, identifiedObjects[i]);

                    identifiedObjects.Remove(identifiedObjects[i]);
                }
            }
        }
    }

    void CollectColliders()
    {
        if (kLight.LightType != Light2D.LightTypeSetting.Directional)
        {
            objs._3DColliders = Physics.OverlapSphere(transform.position, kLight.LightRadius, eventLayer);

            #if !(UNITY_2_6 || UNITY_2_6_1 || UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5 || UNITY_4_0 || UNITY_4_0_1 || UNITY_4_1 || UNITY_4_2)
            objs._2DColliders = Physics2D.OverlapAreaAll(transform.position + new Vector3(-kLight.LightRadius, kLight.LightRadius, 0), transform.position + new Vector3(kLight.LightRadius, -kLight.LightRadius, 0), eventLayer);
            #endif
            
        }
        else
        {
            objs._3DColliders = Physics.OverlapSphere(kLight.DiectionalLightPivotPoint + transform.position, kLight.DirectionalLightSphereSize, kLight.ShadowLayer);
            
            #if !(UNITY_2_6 || UNITY_2_6_1 || UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5 || UNITY_4_0 || UNITY_4_0_1 || UNITY_4_1 || UNITY_4_2)
            objs._2DColliders = Physics2D.OverlapAreaAll(transform.TransformPoint(kLight.DiectionalLightPivotPoint + transform.position + new Vector3(-kLight.LightBeamSize, kLight.LightBeamRange, 0)), transform.TransformPoint(kLight.DiectionalLightPivotPoint + new Vector3(kLight.LightBeamSize, -kLight.LightBeamRange, 0)), eventLayer);    
            #endif
           
        }

        #if !(UNITY_2_6 || UNITY_2_6_1 || UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5 || UNITY_4_0 || UNITY_4_0_1 || UNITY_4_1 || UNITY_4_2)
        objs.totalColliders = objs._3DColliders.Length + objs._2DColliders.Length;
        #else
        objs.totalColliders = objs._3DColliders.Length;
        #endif

        if (objs.totalColliders > 0)
        {
            foreach (Collider c in objs._3DColliders)
                unidentifiedObjects.Add(c.gameObject);

            #if !(UNITY_2_6 || UNITY_2_6_1 || UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5 || UNITY_4_0 || UNITY_4_0_1 || UNITY_4_1 || UNITY_4_2)
            foreach (Collider2D c in objs._2DColliders)
                unidentifiedObjects.Add(c.gameObject);
            #endif
        }
    }
}

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EventSample_VLS : MonoBehaviour 
{
    public AudioClip hitSound;
    public AudioClip whiteSound;
    public Material hitLightMaterial;

    int id = 0;
    bool isDetected = false;
    Color c = Color.black;

    void Start()
    {
        id = gameObject.GetInstanceID();

        Light2D.RegisterEventListener(LightEventListenerType.OnStay, OnLightStay);
        Light2D.RegisterEventListener(LightEventListenerType.OnEnter, OnLightEnter);
        Light2D.RegisterEventListener(LightEventListenerType.OnExit, OnLightExit);
    }

    void OnDestroy()
    {
        /* (!) Make sure you unregister your events on destroy. If you do not
         * you might get strange errors (!) */

        Light2D.UnregisterEventListener(LightEventListenerType.OnStay, OnLightStay);
        Light2D.UnregisterEventListener(LightEventListenerType.OnEnter, OnLightEnter);
        Light2D.UnregisterEventListener(LightEventListenerType.OnExit, OnLightExit);
    }

    void Update()
    {
        if (isDetected)
            renderer.material.color = Color.Lerp(renderer.material.color, c, Time.deltaTime * 10f);
        else
            renderer.material.color = Color.Lerp(renderer.material.color, Color.black, Time.deltaTime * 5f);

        isDetected = false;
    }

    void OnLightEnter(Light2D l, GameObject g)
    {
        if (g.GetInstanceID() == id)
        {
            c += l.LightColor;
            AudioSource.PlayClipAtPoint(hitSound, transform.position, 0.1f);
        }
    }

    void OnLightStay(Light2D l, GameObject g)
    {
        if (g.GetInstanceID() == id)
        {
            isDetected = true;
        }
    }

    void OnLightExit(Light2D l, GameObject g)
    {
        if (g.GetInstanceID() == id)
        {
            c -= l.LightColor;

            if ((renderer.material.color.r > 0.95f) && (renderer.material.color.g > 0.95f) && (renderer.material.color.b > 0.95f))
            {
                AudioSource.PlayClipAtPoint(whiteSound, transform.position, 0.5f);
                Light2D l2d = Light2D.Create(transform.position, hitLightMaterial, new Color(.8f, .8f, 0.6f), Random.Range(3, 5f));
                l2d.ShadowLayer = 0;
                l2d.transform.Rotate(0, 0, Random.Range(10, 80f));
                GameObject.Destroy(l2d.gameObject, 0.2f);
            }
        }
    }
}

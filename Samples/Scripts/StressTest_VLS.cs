/*
 *  Used to test the mesh leakage
 */

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class StressTest_VLS : MonoBehaviour
{
    public int lightsPerIteration = 5;

    private Light2D[] lightObjs = new Light2D[0];
    private static int lightsGenerated = 0;

    void Update()
    {
        if (lightObjs.Length > 0)
        {
            for (int i = 0; i < lightsPerIteration; i++)
                Destroy(lightObjs[i].gameObject);
        }

        lightObjs = new Light2D[lightsPerIteration];
        lightsGenerated += lightsPerIteration;

        for (int i = 0; i < lightsPerIteration; i++)
        {
            lightObjs[i] = Light2D.Create(new Vector3(Random.Range(-5, 5), Random.Range(-4, 4), 0), GetRandColor(), Random.Range(1f, 2f));
        }

        TickFPSCounter();
    }

    Color GetRandColor()
    {
        int r = Random.Range(0, 6);
        switch (r)
        {
            case 0:
                return Color.red;
            case 1:
                return Color.green;
            case 2:
                return Color.blue;
            case 3:
                return Color.yellow;
            case 4:
                return Color.cyan;
            case 5:
                return Color.magenta;
            default:
                return Color.red;
        }
    }

    int frameCount = 0;
    float nextUpdate = 0f;
    float updateRate = 3.0f;
    void TickFPSCounter()
    {
        frameCount++;
        if (Time.time > nextUpdate)
        {
            nextUpdate = Time.time + (1f / updateRate);
            frameCount = 0;
        }
    }
}

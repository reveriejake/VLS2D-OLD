using UnityEngine;
using UnityEditor;
using System.Collections;

public class Light2DMenu : Editor
{
    [MenuItem("GameObject/Create Other/2DVLS (2D Lights)/Add Radial Light", false, 50)]
    public static void CreateNewRadialLight()
    {
        Light2D light = Light2D.Create(GetPlacement(), (Material)Resources.Load("RadialLight", typeof(Material)), new Color(1f, 0.6f, 0f, 0f), GetSize(), 360, Light2D.LightDetailSetting.Rays_500, false, Light2D.LightTypeSetting.Radial);
        light.ShadowLayer = -1;

        Selection.activeGameObject = light.gameObject;
        Undo.RegisterCreatedObjectUndo(light.gameObject, "Created 2D Radial Light");
    }

    [MenuItem("GameObject/Create Other/2DVLS (2D Lights)/Add Spot Light", false, 51)]
    public static void CreateNewSpotLight()
    {
        Light2D light = Light2D.Create(GetPlacement(), (Material)Resources.Load("RadialLight", typeof(Material)), new Color(1f, 0.6f, 0f, 0f), GetSize(), 45, Light2D.LightDetailSetting.Rays_100, false, Light2D.LightTypeSetting.Radial);
        light.ShadowLayer = -1;

        Selection.activeGameObject = light.gameObject;
        Undo.RegisterCreatedObjectUndo(light.gameObject, "Created 2D Spot Light");
    }

    [MenuItem("GameObject/Create Other/2DVLS (2D Lights)/Add Directional Light", false, 52)]
    public static void CreateNewDirectionalLight()
    {
        Light2D light = Light2D.Create(GetPlacement(), (Material)Resources.Load("DirectionalLight", typeof(Material)), new Color(1f, 0.6f, 0f, 0f), 25, 10, Light2D.LightDetailSetting.Rays_500, false, Light2D.LightTypeSetting.Directional);
        light.ShadowLayer = -1;

        Selection.activeGameObject = light.gameObject;
        Undo.RegisterCreatedObjectUndo(light.gameObject, "Created 2D Directional Light");
    }

    [MenuItem("GameObject/Create Other/2DVLS (2D Lights)/Add Shadow Emitter", false, 53)]
    public static void CreateNewShadowLight()
    {
        Light2D light = Light2D.Create(GetPlacement(), (Material)Resources.Load("RadialShadow", typeof(Material)), Color.black, GetSize(), 360, Light2D.LightDetailSetting.Rays_500, false, Light2D.LightTypeSetting.Shadow);
        light.ShadowLayer = -1;

        Selection.activeGameObject = light.gameObject;
        Undo.RegisterCreatedObjectUndo(light.gameObject, "Created 2D Shadow Emitter");
    }

    [MenuItem("GameObject/Create Other/2DVLS (2D Lights)/Help/Documentation")]
    public static void SeekHelp_Documentation()
    {
        Application.OpenURL("http://reverieinteractive.com/2DVLS/Documentation/");
    }

    [MenuItem("GameObject/Create Other/2DVLS (2D Lights)/Help/Online Contact Form")]
    public static void SeekHelp_Form()
    {
        Application.OpenURL("http://reverieinteractive.com/contact/");
    }

    [MenuItem("GameObject/Create Other/2DVLS (2D Lights)/Help/Unity Forum Thread")]
    public static void SeekHelp_UnityForum()
    {
        Application.OpenURL("http://forum.unity3d.com/threads/142532-2D-Mesh-Based-Volumetric-Lights");
    }

    [MenuItem("GameObject/Create Other/2DVLS (2D Lights)/Help/Direct [jake@reverieinteractive.com]")]
    public static void SeekHelp_Direct()
    {
        Application.OpenURL("mailto:jake@reverieinteractive.com");
    }

    private static Vector3 GetPlacement()
    {
        Camera c = SceneView.currentDrawingSceneView.camera;
        
#if !(UNITY_2_6 || UNITY_2_6_1 || UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5 || UNITY_4_0 || UNITY_4_0_1 || UNITY_4_1 || UNITY_4_2)
        if (SceneView.currentDrawingSceneView.in2DMode)
            return c.transform.position + new Vector3(0, 0, -c.transform.position.z);
        else
            return c.transform.position + c.transform.forward * 15f;
#else
        return c.transform.position + c.transform.forward * 15f;
#endif

    }

    private static float GetSize()
    {
#if !(UNITY_2_6 || UNITY_2_6_1 || UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5 || UNITY_4_0 || UNITY_4_0_1 || UNITY_4_1 || UNITY_4_2)
        if (SceneView.currentDrawingSceneView.in2DMode)
            return 5;
        else
            return 1;
#else
        return 1;
#endif
    }
}
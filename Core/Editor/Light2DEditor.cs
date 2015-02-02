using UnityEngine;
using UnityEditor;
using System.Collections;

//[CanEditMultipleObjects()]
[CustomEditor(typeof(Light2D))]
public class Light2DEditor : Editor
{
    SerializedObject sObj;

    SerializedProperty sweepStart;
    SerializedProperty sweepSize;
    SerializedProperty lightRadius;
    SerializedProperty lightMaterial;
    SerializedProperty lightDetail;
    SerializedProperty lightColor;
    SerializedProperty useEvents;
    SerializedProperty shadowLayer;
    SerializedProperty lightType;
    SerializedProperty beamSize;
    SerializedProperty beamRange;
    SerializedProperty pivotPoint;
    SerializedProperty pivotPointType;
    SerializedProperty uvTiling;
    SerializedProperty uvOffset;

    void OnEnable()
    {
        sObj = new SerializedObject(target);

        lightType = sObj.FindProperty("lightType");
        lightDetail = sObj.FindProperty("lightDetail");
        lightColor = sObj.FindProperty("lightColor");
        sweepSize = sObj.FindProperty("coneAngle");
        sweepStart = sObj.FindProperty("coneStart");
        lightRadius = sObj.FindProperty("lightRadius");
        lightMaterial = sObj.FindProperty("lightMaterial");
        useEvents = sObj.FindProperty("useEvents");
        shadowLayer = sObj.FindProperty("shadowLayer");
        beamSize = sObj.FindProperty("beamSize");
        beamRange = sObj.FindProperty("beamRange");
        pivotPoint = sObj.FindProperty("pivotPoint");
        pivotPointType = sObj.FindProperty("pivotPointType");
        uvTiling = sObj.FindProperty("uvTiling");
        uvOffset = sObj.FindProperty("uvOffset");

        #if !(UNITY_2_6 || UNITY_2_6_1 || UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5 || UNITY_4_0 || UNITY_4_0_1 || UNITY_4_1 || UNITY_4_2)
        Undo.undoRedoPerformed += UndoCall;
        #endif
    }

    #if !(UNITY_2_6 || UNITY_2_6_1 || UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5 || UNITY_4_0 || UNITY_4_0_1 || UNITY_4_1 || UNITY_4_2)
    void OnDisable()
    {
        Undo.undoRedoPerformed -= UndoCall;
    }
    #endif

    public override void OnInspectorGUI()
    {
        EditorGUI.BeginChangeCheck();
        {
            EditorGUILayout.Separator();

            EditorGUILayout.PropertyField(lightType, new GUIContent("Light Type", "Selects the type of light which will be used."));
            EditorGUILayout.PropertyField(shadowLayer, new GUIContent("Shadow Layer", "Objects on this layer will cast shadows."));
            EditorGUILayout.PropertyField(lightDetail, new GUIContent("Light Detail", "The number of rays the light checks for when generating shadows. Rays_500 will cast 500 raycasts."));

            EditorGUILayout.Separator();

            EditorGUILayout.PropertyField(lightColor);
            EditorGUILayout.PropertyField(lightMaterial);
            EditorGUILayout.PropertyField(uvTiling);
            EditorGUILayout.PropertyField(uvOffset);

            EditorGUILayout.Separator();

            if (lightType.intValue == 1)
            {
                EditorGUILayout.PropertyField(beamSize, new GUIContent("Beam Size", ""));
                EditorGUILayout.PropertyField(beamRange, new GUIContent("Beam Range", ""));

                EditorGUILayout.PropertyField(pivotPointType, new GUIContent("Pivot Type", ""));
                GUI.enabled = (pivotPointType.intValue == 2);
                EditorGUILayout.PropertyField(pivotPoint, new GUIContent("Beam Anchor Point", ""));
                GUI.enabled = true;
            }
            else
            {
                EditorGUILayout.PropertyField(sweepStart, new GUIContent("Light Cone Start"));
                EditorGUILayout.PropertyField(sweepSize, new GUIContent("Light Cone Angle", ""));
                sweepSize.floatValue = Mathf.Clamp(sweepSize.floatValue, 0, 360);
                EditorGUILayout.PropertyField(lightRadius);
                lightRadius.floatValue = Mathf.Clamp(lightRadius.floatValue, 0.001f, Mathf.Infinity);
            }

            EditorGUILayout.Separator();

            EditorGUILayout.PropertyField(useEvents);
            (target as Light2D).IsStatic = EditorGUILayout.Toggle("Is Static", (target as Light2D).IsStatic);
        }

        if (EditorGUI.EndChangeCheck())
        {
            UpdateLight();
            EditorUtility.SetDirty(target);
        }
    }

    int handle = 0;
    void OnSceneGUI()
    {
        Light2D l = (Light2D)target;
        EditorUtility.SetSelectedWireframeHidden(l.renderer, !l.EDITOR_SHOW_MESH);
        Tools.current = Tool.None;
        Event e = Event.current;

        EditorGUI.BeginChangeCheck();
        {
            switch (e.type)
            {
                case EventType.KeyDown:
                    ExecuteKeyDownEvent(e);
                    break;
            }

            if (handle == 0)
            {
                if (Tools.pivotRotation == PivotRotation.Local)
                    l.transform.position = Handles.PositionHandle(l.transform.position, l.transform.rotation);
                else
                    l.transform.position = Handles.PositionHandle(l.transform.position, Quaternion.identity);
            }
            else
            {
                l.transform.rotation = Handles.RotationHandle(l.transform.rotation, l.transform.position);
            }

            if (l.LightType != Light2D.LightTypeSetting.Directional)
            {
                Handles.color = Color.green;
                float widgetSize = Vector3.Distance(l.transform.position, SceneView.lastActiveSceneView.camera.transform.position) * 0.1f;
                float rad = (l.LightRadius);
                Handles.DrawWireDisc(l.transform.position, l.transform.forward, rad);
                lightRadius.floatValue = Mathf.Clamp(Handles.ScaleValueHandle(l.LightRadius, l.transform.TransformPoint(Vector3.right * rad), Quaternion.identity, widgetSize, Handles.CubeCap, 1), 0.001f, Mathf.Infinity);

                Handles.color = Color.red;
                Vector3 sPos = l.transform.TransformDirection(Mathf.Cos(Mathf.Deg2Rad * -((l.LightConeAngle / 2f) - l.LightConeStart)), Mathf.Sin(Mathf.Deg2Rad * -((l.LightConeAngle / 2f) - l.LightConeStart)), 0);
                Handles.DrawWireArc(l.transform.position, l.transform.forward, sPos, l.LightConeAngle, (rad * 0.8f));
                sweepSize.floatValue = Mathf.Clamp(Handles.ScaleValueHandle(l.LightConeAngle, l.transform.position - l.transform.right * (rad * 0.8f), Quaternion.identity, widgetSize, Handles.CubeCap, 1), 0, 360);
            }
        }

        if (EditorGUI.EndChangeCheck())
        {
            UpdateLight();
            EditorUtility.SetDirty(target);
        }
    }

    void ExecuteKeyDownEvent(Event e)
    {
        switch (e.keyCode)
        {
            case KeyCode.W:
                handle = 0;
                break;

            case KeyCode.E:
                break;

            case KeyCode.R:
                handle = 1;
                break;
        }
    }

    void UpdateLight()
    {
        Light2D l2d = (Light2D)target;

        #if !(UNITY_2_6 || UNITY_2_6_1 || UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5 || UNITY_4_0 || UNITY_4_0_1 || UNITY_4_1 || UNITY_4_2)
        Undo.RecordObject(l2d, "Changed Setting");
        #endif

        l2d.LightType = (Light2D.LightTypeSetting)lightType.intValue;
        l2d.LightConeAngle = sweepSize.floatValue;
        l2d.LightConeStart = sweepStart.floatValue;
        l2d.LightRadius = lightRadius.floatValue;
        l2d.LightMaterial = (Material)lightMaterial.objectReferenceValue;
        l2d.LightDetail = (Light2D.LightDetailSetting)lightDetail.intValue;
        l2d.LightColor = lightColor.colorValue;
        l2d.EnableEvents = useEvents.boolValue;
        l2d.ShadowLayer = shadowLayer.intValue;
        l2d.LightBeamSize = beamSize.floatValue;
        l2d.LightBeamRange = beamRange.floatValue;
        l2d.DiectionalLightPivotPoint = pivotPoint.vector3Value;
        l2d.DirectionalPivotPointType = (Light2D.PivotPointType)pivotPointType.intValue;
        l2d.UVTiling = uvTiling.vector2Value;
        l2d.UVOffset = uvOffset.vector2Value;
    }

    void UndoCall()
    {
        Light2D l2d = (Light2D)target;
        l2d.FlagMeshupdate();
    }
}

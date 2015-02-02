using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

public enum LightEventListenerType { OnEnter, OnStay, OnExit }
public delegate void Light2DEvent(Light2D lightObject, GameObject objectInLight);

[ExecuteInEditMode()]
[RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
public class Light2D : MonoBehaviour
{
    public struct ColliderObjects
    {
        public int totalColliders;
        public Collider[] _3DColliders;
#if !(UNITY_2_6 || UNITY_2_6_1 || UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5 || UNITY_4_0 || UNITY_4_0_1 || UNITY_4_1 || UNITY_4_2)
        public Collider2D[] _2DColliders;
#endif
    }

    public enum PivotPointType
    {
        Center,
        End,
        Custom
    }

    public enum LightDetailSetting
    {
        Rays_50 = 48,
        Rays_100 = 96,
        Rays_200 = 192,
        Rays_300 = 288,
        Rays_400 = 384,
        Rays_500 = 480,
        Rays_600 = 576,
        Rays_700 = 672,
        Rays_800 = 816,
        Rays_900 = 912,
        Rays_1000 = 1008,
        Rays_2000 = 2016,
        Rays_3000 = 3024,
        Rays_4000 = 4032,
        Rays_5000 = 5040
    }

    public enum LightTypeSetting
    {
        Radial,
        Directional,
        Shadow
    }

    [HideInInspector()]
    /// <summary>Variable used by editor. If 'TRUE' the bounds of the light will be drawn in grey</summary>
    public bool EDITOR_SHOW_BOUNDS = true;
    [HideInInspector()]
    /// <summary>Variable used by editor script. If 'TRUE' mesh gizmos will be drawn.</summary>
    public bool EDITOR_SHOW_MESH = false;
    
    private MeshRenderer _renderer;
    private MeshFilter _filter;
    private Mesh _mesh;

    private Quaternion lookAtRotation = Quaternion.identity;
    private Quaternion kRotation = Quaternion.identity;
    private float coneRangeMin = 0;
    private float coneRangeMax = 360;
    private float kPosition = 0;
    private float kColliderCount = 0;
    private float[] kColliderDistances = new float[0];
    private Vector3[] prevPoints = new Vector3[3];
    private ColliderObjects objs = new ColliderObjects();
    private bool coneEdgeGenerated = false;

    private List<GameObject> identifiedObjects = new List<GameObject>();
    private List<GameObject> unidentifiedObjects = new List<GameObject>();

    public static event Light2DEvent OnBeamEnter = null;
    public static event Light2DEvent OnBeamStay = null;
    public static event Light2DEvent OnBeamExit = null;

    private List<int> tris = new List<int>();
    private List<Vector3> verts = new List<Vector3>();
    private List<Vector3> normals = new List<Vector3>();
    private List<Vector2> uvs = new List<Vector2>();
    private List<Color32> colors = new List<Color32>();
    private List<Vector2> circleRef = new List<Vector2>();

    [SerializeField]
    private LightTypeSetting lightType = LightTypeSetting.Radial;
    [SerializeField]
    private float lightRadius = 1;
    [SerializeField]
    private float coneStart = 0;
    [SerializeField]
    private float coneAngle = 360f;
    [SerializeField]
    private Color lightColor = new Color(0.8f, 1f, 1f, 0);
    [SerializeField]
    private LightDetailSetting lightDetail = LightDetailSetting.Rays_300;
    [SerializeField]
    private Material lightMaterial;
    [SerializeField]
    private LayerMask shadowLayer = -1; // -1 = EVERYTHING, 0 = NOTHING, 1 = DEFAULT
    [SerializeField]
    private bool useEvents = false;
    [SerializeField]
    private bool lightEnabled = true;
    [SerializeField]
    private float beamSize = 25;
    [SerializeField]
    private float beamRange = 10;
    [SerializeField]
    private Vector2 uvTiling = new Vector2(1, 1);
    [SerializeField]
    private Vector2 uvOffset = new Vector2(0, 0);
    [SerializeField]
    private Vector3 pivotPoint = Vector3.zero;
    [SerializeField]
    private PivotPointType pivotPointType = PivotPointType.Center;

    private float directionalLightSphereSize = 0;
    public float DirectionalLightSphereSize { get { return directionalLightSphereSize; } }

    private static int totalLightsRendered = 0;
    private static int totalLightsUpdated = 0;

    /// <summary>Returns the number of Render updates currently occuring</summary>
    public static int TotalLightsRendered { get { return totalLightsRendered; } }
    /// <summary>Retunrs the number of light meshes being updated occuring</summary>
    public static int TotalLightsUpdated { get { return totalLightsUpdated; } }

    /// <summary>Sets the type of light to be used.</summary>
    public LightTypeSetting LightType { get { return lightType; } set { lightType = value; flagMeshUpdate = true; } }
    /// <summary>Sets the Radius of the light. Value clamped between 0.001f and Mathf.Infinity</summary>
    public float LightRadius { get { return lightRadius; } set { lightRadius = Mathf.Clamp(value, 0.001f, Mathf.Infinity); flagMeshUpdate = true; } }
    /// <summary>Sets the size of the directional light in the X axis. Value clamped between 0.001f and Mathf.Infinity</summary>
    public float LightBeamSize { get { return beamSize; } set { beamSize = Mathf.Clamp(value, 0.001f, Mathf.Infinity); directionalLightSphereSize = Vector3.Distance(Vector3.zero, new Vector3(beamSize, beamRange, 0)); flagMeshUpdate = true; } }
    /// <summary>Sets the size of the directional light in the Y axis. Value clamped between 0.001f and Mathf.Infinity</summary>
    public float LightBeamRange { get { return beamRange; } set { beamRange = Mathf.Clamp(value, 0.001f, Mathf.Infinity); directionalLightSphereSize = Vector3.Distance(Vector3.zero, new Vector3(beamSize, beamRange, 0)); flagMeshUpdate = true; } }
    /// <summary>Sets the light cone starting point. Value 0 = Aims Right, Value 90 = Aims Up.</summary>
    public float LightConeStart { get { return coneStart; } set { coneStart = value; flagCircleUpdate = true; flagMeshUpdate = true; } }
    /// <summary>Sets the light cone size (wedge shape). Value is clamped between 0 and 360.</summary>
    public float LightConeAngle { get { return coneAngle; } set { coneAngle = Mathf.Clamp(value, 0f, 360f); flagCircleUpdate = true; flagMeshUpdate = true; } }
    /// <summary>Sets the Color of the light.</summary>
    public Color LightColor { get { return lightColor; } set { lightColor = value; flagColorUpdate = true; } }
    /// <summary>Sets the ray count when the light is finding shadows.</summary>
    public LightDetailSetting LightDetail { get { return lightDetail; } set { lightDetail = value; flagNormalsUpdate = true; flagCircleUpdate = true; flagColorUpdate = true; flagMeshUpdate = true; } }
    /// <summary>Sets the lights material. Best to use the 2DVLS shaders or the Particle shaders.</summary>
    public Material LightMaterial { get { return lightMaterial; } set { lightMaterial = value; flagMaterialUpdate = true; } }
    /// <summary>The layer which responds to the raycasts. If a collider is on the same layer then a shadow will be cast from that collider</summary>
    public LayerMask ShadowLayer { get { return shadowLayer; } set { shadowLayer = value; flagMeshUpdate = true; } }
    /// <summary>When set to 'TRUE' the light will use events such as 'OnBeamEnter(Light2D, GameObject)', 'OnBeamStay(Light2D, GameObject)', and 'OnBeamExit(Light2D, GameObject)'</summary>
    public bool EnableEvents { get { return useEvents; } set { useEvents = value; } }
    /// <summary>Returns 'TRUE' when light is enabled</summary>
    public bool LightEnabled { get { return lightEnabled; } set { if (value != lightEnabled) { lightEnabled = value; /*if (isShadowCaster) UpdateMesh_RadialShadow(); else UpdateMesh_Radial();*/ } } }
    /// <summary>Returns 'TRUE' when light is visible</summary>
    public bool IsVisible { get { if (_renderer) return _renderer.isVisible; else return false; } }
    /// <summary>Sets the light to static. Alternativly you can use the "gameObject.isStatic" method or tick the static checkbox in the inspector.</summary>
    public bool IsStatic { get { return gameObject.isStatic; } set { gameObject.isStatic = value; } }
    /// <summary>Returns the directional lights custom pivot point Vector.</summary>
    public Vector3 DiectionalLightPivotPoint 
    { 
        get 
        { 
            switch(pivotPointType)
            {
                case PivotPointType.Center:
                    return Vector3.zero;

                case PivotPointType.End:
                    return new Vector3(0, beamRange * -0.5f, 0);

                default:
                    return pivotPoint;
            }
        } 
        set { pivotPoint = value; } 
    }
    /// <summary>Sets which type of pivot point will be used on the directional light</summary>
    public PivotPointType DirectionalPivotPointType 
    { 
        get { return pivotPointType; } 
        set { pivotPointType = value; flagMeshUpdate = true; }
    }
    /// <summary>Sets the UV tiling value</summary>
    public Vector2 UVTiling { get { return uvTiling; } set { uvTiling = value; flagMeshUpdate = true; } }
    /// <summary>Sets the UV offset value</summary>
    public Vector2 UVOffset { get { return uvOffset; } set { uvOffset = value; flagMeshUpdate = true; } }

    // Depriciated Variables
    [System.Obsolete("Depreciated. Use 'LightType' instead.")]
    /// <summary>[Depreciated] When set to 'TRUE' the light will produce inverse of what the light produces which is shadow.</summary>
    public bool IsShadowEmitter { get { return ((int)lightType == 2) ? true : false; } set { LightType = (value) ? LightTypeSetting.Shadow : LightTypeSetting.Radial; } }
    [System.Obsolete("Depreciated. Use 'LightConeAngle' instead.")]
    /// <summary>[Depreciated] Use 'LightConeAngle' instead.</summary>
    public float SweepSize { get { return LightConeAngle; } set { LightConeAngle = value; } }
    [System.Obsolete("Depreciated. Use 'transform.Rotate()' to rotate your light. This value is now calculated automatically via 'LightConeAngle'.")]
    /// <summary>[Depreciated] Use 'LightConeStart' instead.</summary>
    public float SweepStart { get { return LightConeStart; } set { LightConeStart = value; } }
    [System.Obsolete("Depreciated. No Longer Used.")]
    /// <summary>[Depreciated] No Longer Supported</summary>
    public bool ignoreOptimizations = false;
    [System.Obsolete("Depreciated. Use 'AllowLightsToHide' instead.")]
    /// <summary>[Depreciated] No Longer Supported.</summary>
    public bool allowHideInsideColliders { get { return false; } set { } }


    private bool flagCircleUpdate = true;
    private bool flagColorUpdate = true;
    private bool flagMeshUpdate = true;
    private bool flagUVUpdate = true;
    private bool flagNormalsUpdate = true;
    private bool flagMaterialUpdate = true;
    private bool flagObjsInRange = false;
    private bool initialized = false;

    void OnDrawGizmosSelected()
    {
        if (_renderer && EDITOR_SHOW_BOUNDS)
        {
            Gizmos.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            Gizmos.DrawWireCube(_renderer.bounds.center, _renderer.bounds.size);
        }        
    }

    void OnDrawGizmos()
    {
        switch (lightType)
        {
            case LightTypeSetting.Radial:
                Gizmos.DrawIcon(transform.position, "Light.png", false);
                break;
            case LightTypeSetting.Shadow:
                Gizmos.DrawIcon(transform.position, "Shadow.png", false);
                break;
            case LightTypeSetting.Directional:
                Gizmos.DrawIcon(transform.position, "Directional.png", false);
                break;
        }
    }

    void OnEnable()
    {
        LightEnabled = true;

        directionalLightSphereSize = Vector3.Distance(Vector3.zero, new Vector3(beamSize, beamRange, 0));

        _filter = gameObject.GetComponent<MeshFilter>();
        if (_filter == null)
            _filter = gameObject.AddComponent<MeshFilter>();

        _renderer = gameObject.GetComponent<MeshRenderer>();
        if (_renderer == null)
            _renderer = gameObject.AddComponent<MeshRenderer>();
    }

    void OnDisable()
    {
        LightEnabled = false;
        LateUpdate();
    }

    void OnDestroy()
    {
        if (Application.isPlaying)
        {
            if (useEvents)
            {
                for (int i = 0; i < identifiedObjects.Count; i++)
                {
                    if (OnBeamExit != null)
                        OnBeamExit(this, identifiedObjects[i]);

                    identifiedObjects.Remove(identifiedObjects[i]);
                }
            }

            Destroy(_mesh);
            Destroy(_renderer);
            Destroy(_filter);
        }
        else
        {
            DestroyImmediate(_mesh);
            _mesh = null;
            _renderer = null;
            _filter = null;
        }
    }

    void Awake()
    {
        lookAtRotation = Quaternion.FromToRotation(Vector3.forward, Vector3.right) * (lightType == LightTypeSetting.Directional ? Quaternion.Euler(-90, 0, 90) : Quaternion.Euler(-90, 0, 0));

        totalLightsRendered = 0;
        totalLightsUpdated = 0;
        kColliderCount = 0;
    }

    void Update()
    {
        totalLightsRendered = 0;
        totalLightsUpdated = 0;
    }

    void LateUpdate()
    {
        if (_renderer)
        {
            _renderer.enabled = lightEnabled;
            CollectColliders();

            if (objs.totalColliders > 0)
            {
                flagObjsInRange = true;

                float d = Vector3.SqrMagnitude(transform.position);
                if ((Quaternion.Angle(transform.rotation, kRotation) != 0) || (d != kPosition))
                {
                    kPosition = d;
                    kRotation = transform.rotation;
                    flagMeshUpdate = true;
                }
            }

            if (kColliderCount != objs.totalColliders)
                flagMeshUpdate = true;

            kColliderCount = objs.totalColliders;

            if (objs._3DColliders.Length > 0)
            {
                for (int c = 0; c < objs._3DColliders.Length; c++)
                {
                    float d = Vector3.SqrMagnitude(objs._3DColliders[c].transform.position) + Vector3.SqrMagnitude(objs._3DColliders[c].transform.eulerAngles) + Vector3.SqrMagnitude(objs._3DColliders[c].transform.localScale);

                    if (kColliderDistances[c] != d)
                    {
                        kColliderDistances[c] = d;
                        flagMeshUpdate = true;
                        //break;
                    }
                }
            }

            #if !(UNITY_2_6 || UNITY_2_6_1 || UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5 || UNITY_4_0 || UNITY_4_0_1 || UNITY_4_1 || UNITY_4_2)
            if (objs._2DColliders.Length > 0)
            {
                for (int c = 0; c < objs._2DColliders.Length; c++)
                {
                    float d = Vector3.SqrMagnitude(objs._2DColliders[c].transform.position) + Vector3.SqrMagnitude(objs._2DColliders[c].transform.eulerAngles) + Vector2.SqrMagnitude(objs._2DColliders[c].transform.localScale);

                    if (kColliderDistances[objs._3DColliders.Length + c] != d)
                    {
                        kColliderDistances[objs._3DColliders.Length + c] = d;
                        flagMeshUpdate = true;
                    }
                }
            }
            #endif

            if(Application.isPlaying && IsStatic)
            {
                if(Time.frameCount<5)
                    Draw();
            }
            else
                Draw();
        }
    }

    void CollectColliders()
    {
        if (lightType != LightTypeSetting.Directional)
        {
            objs._3DColliders = Physics.OverlapSphere(transform.position, lightRadius, shadowLayer);
            
            #if !(UNITY_2_6 || UNITY_2_6_1 || UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5 || UNITY_4_0 || UNITY_4_0_1 || UNITY_4_1 || UNITY_4_2)
            objs._2DColliders = Physics2D.OverlapAreaAll(transform.position + new Vector3(-lightRadius, lightRadius, 0), transform.position + new Vector3(lightRadius, -lightRadius, 0), shadowLayer);
            #endif
        }
        else
        {
            objs._3DColliders = Physics.OverlapSphere(DiectionalLightPivotPoint + transform.position, directionalLightSphereSize, shadowLayer);

            #if !(UNITY_2_6 || UNITY_2_6_1 || UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5 || UNITY_4_0 || UNITY_4_0_1 || UNITY_4_1 || UNITY_4_2)
            objs._2DColliders = Physics2D.OverlapAreaAll(transform.position - renderer.bounds.extents, transform.position + renderer.bounds.extents, shadowLayer); //transform.TransformPoint(DiectionalLightPivotPoint + transform.position + new Vector3(-beamSize, beamRange, 0)), transform.InverseTransformPoint(DiectionalLightPivotPoint + new Vector3(beamSize, -beamRange, 0)), shadowLayer);
            #endif
        }

        #if !(UNITY_2_6 || UNITY_2_6_1 || UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5 || UNITY_4_0 || UNITY_4_0_1 || UNITY_4_1 || UNITY_4_2)
        objs.totalColliders = objs._3DColliders.Length + objs._2DColliders.Length;
        #else
        objs.totalColliders = objs._3DColliders.Length;
        #endif

        if (objs.totalColliders != kColliderDistances.Length)
            kColliderDistances = new float[objs.totalColliders];
    }

    /// <summary>
    /// Only call Draw within an editor function. You would not normally need to make a call to Draw when in game.
    /// </summary>
    void Draw()
    {
        if (initialized && Application.isPlaying)
        {
            if (!_renderer.isVisible)
                return;

            totalLightsRendered++;
        }

        if (_mesh == null)
            CreateMeshObject();

        // Update Circle Ref
        if (circleRef.Count < 1 || flagCircleUpdate)
            UpdateCircleRef();

        // Update Mesh
        if (flagMeshUpdate)
        {
            // Check for Udpates and Clear Events
            if (Application.isPlaying && useEvents)
                unidentifiedObjects.Clear();

            switch (lightType)
            {
                case LightTypeSetting.Radial:
                    UpdateRadialMesh();
                    break;
                case LightTypeSetting.Shadow:
                    UpdateRadialShadowMesh();
                    break;
                case LightTypeSetting.Directional:
                    UpdateDirectionalMesh();
                    break;
            }

            totalLightsUpdated++;
        }

        // Update UVs
        if (flagUVUpdate)
            UpdateRadialUVs();

        // Update Normals
        if (flagNormalsUpdate)
            UpdateNormals();

        // Update Colors
        if (flagColorUpdate)
            UpdateColors();

        if (flagMaterialUpdate)
        {
            if (lightMaterial == null)
                lightMaterial = (Material)Resources.Load("RadialLight");

            _renderer.sharedMaterial = lightMaterial;
            flagMaterialUpdate = false;
        }

        // === Finish Event Checks
        if (Application.isPlaying && useEvents)
        {
            for (int i = 0; i < unidentifiedObjects.Count; i++)
            {
                if (identifiedObjects.Contains(unidentifiedObjects[i]))
                {
                    if (OnBeamStay != null)
                        OnBeamStay(this, unidentifiedObjects[i]);
                }

                if (!identifiedObjects.Contains(unidentifiedObjects[i]))
                {
                    identifiedObjects.Add(unidentifiedObjects[i]);

                    if (OnBeamEnter != null)
                        OnBeamEnter(this, unidentifiedObjects[i]);
                }
            }

            for (int i = 0; i < identifiedObjects.Count; i++)
            {
                if (!unidentifiedObjects.Contains(identifiedObjects[i]))
                {
                    if (OnBeamExit != null)
                        OnBeamExit(this, identifiedObjects[i]);

                    identifiedObjects.Remove(identifiedObjects[i]);
                }
            }
        }

        initialized = true;

        _filter.hideFlags = HideFlags.HideInInspector;
        _renderer.hideFlags = HideFlags.HideInInspector;
    }

    void CreateMeshObject()
    {
        _mesh = new Mesh();
        _mesh.name = "LightMesh_" + gameObject.GetInstanceID();
        _mesh.hideFlags = HideFlags.HideAndDontSave;
    }

    void UpdateDirectionalMesh()
    {
        verts.Clear();
        //tris.Clear();
        
        if (objs.totalColliders == 0)
        {
            verts.Add(DiectionalLightPivotPoint + new Vector3(-beamSize * 0.5f, -beamRange * 0.5f, 0));
            verts.Add(DiectionalLightPivotPoint + new Vector3(beamSize * 0.5f, -beamRange * 0.5f, 0));
            verts.Add(DiectionalLightPivotPoint + new Vector3(-beamSize * 0.5f, beamRange * 0.5f, 0));
            verts.Add(DiectionalLightPivotPoint + new Vector3(beamSize * 0.5f, beamRange * 0.5f, 0));
        }
        else
        {
            RaycastHit rhit = new RaycastHit();
            
            #if !(UNITY_2_6 || UNITY_2_6_1 || UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5 || UNITY_4_0 || UNITY_4_0_1 || UNITY_4_1 || UNITY_4_2)
            RaycastHit2D rhit2D = new RaycastHit2D();
            #endif
            
            int rays = (int)lightDetail;
            bool wasHit = false;
            float spacing = beamSize / (rays - 1);

            for (int i = 0; i < rays; i++)
            {
                bool hit2d = false;
                bool hit3d = false;

                Vector3 rayStart = transform.TransformPoint(DiectionalLightPivotPoint + new Vector3((-beamSize * 0.5f) + (spacing * i), beamRange * 0.5f, 0));

                #if !(UNITY_2_6 || UNITY_2_6_1 || UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5 || UNITY_4_0 || UNITY_4_0_1 || UNITY_4_1 || UNITY_4_2)
                if (objs._2DColliders != null && objs._2DColliders.Length > 0)
                {
                    rhit2D = Physics2D.Raycast(rayStart, -transform.up, beamRange, shadowLayer);
                    hit2d = !(rhit2D.point == Vector2.zero);
                }
                #endif

                if (objs._3DColliders != null && objs._3DColliders.Length > 0)
                {
                    hit3d = Physics.Raycast(rayStart, -transform.up, out rhit, beamRange, shadowLayer);
                }

                if (flagObjsInRange && (hit2d || hit3d))
                {
                    if (Application.isPlaying && useEvents)
                    {

                        #if !(UNITY_2_6 || UNITY_2_6_1 || UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5 || UNITY_4_0 || UNITY_4_0_1 || UNITY_4_1 || UNITY_4_2)
                        if (hit2d && !unidentifiedObjects.Contains(rhit2D.transform.gameObject))
                            unidentifiedObjects.Add(rhit2D.transform.gameObject);
                        #endif
                        
                        if (hit3d && !unidentifiedObjects.Contains(rhit.transform.gameObject))
                            unidentifiedObjects.Add(rhit.transform.gameObject);
                    }

                    if (!wasHit && i != 0)
                    {
                        verts.Add(DiectionalLightPivotPoint + new Vector3((-beamSize * 0.5f) + (spacing * i), beamRange * 0.5f, 0));
                        verts.Add(DiectionalLightPivotPoint + new Vector3((-beamSize * 0.5f) + (spacing * i), -beamRange * 0.5f, 0));
                    }

                    #if !(UNITY_2_6 || UNITY_2_6_1 || UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5 || UNITY_4_0 || UNITY_4_0_1 || UNITY_4_1 || UNITY_4_2)
                    if (hit2d && hit3d)
                    {
                        hit2d = hit3d = false;

                        if (Vector3.Distance(rhit2D.point, rayStart) < rhit.distance)
                            hit2d = true;
                        else
                            hit3d = true;
                    }

                    if (hit2d)
                    {
                        verts.Add(transform.InverseTransformPoint(rayStart));
                        verts.Add(transform.InverseTransformPoint(new Vector3(rhit2D.point.x, rhit2D.point.y, transform.position.z)));//new Vector3(rhit2D.point.x, rhit2D.point.y, transform.position.z));//transform.InverseTransformPoint(new Vector3(rhit2D.point.x, rhit2D.point.y, transform.position.z)));
                    }
                    #endif
                    
                    if (hit3d)
                    {
                        verts.Add(transform.InverseTransformPoint(rayStart));
                        verts.Add(transform.InverseTransformPoint(rhit.point));
                    }

                    //=============== Removes Unnecessary Vertices ===============================s
                    if (i != (rays - 1) && verts.Count > 4)
                    {
                        prevPoints[0] = verts[verts.Count - 5];
                        prevPoints[1] = verts[verts.Count - 3];
                        prevPoints[2] = verts[verts.Count - 1];

                        if (Vector3.SqrMagnitude((prevPoints[0] - prevPoints[1]).normalized - (prevPoints[1] - prevPoints[2]).normalized) <= 0.01f)
                        {
                            verts.RemoveAt(verts.Count - 3);
                            verts.RemoveAt(verts.Count - 2);
                        }
                    }
                    //=============== END Remove Code =================================*/
                    
                    wasHit = true;
                }
                else
                {
                    if (wasHit)
                    {
                        verts.Add(DiectionalLightPivotPoint + new Vector3((-beamSize * 0.5f) + (spacing * (i - 1)), beamRange * 0.5f, 0));
                        verts.Add(DiectionalLightPivotPoint + new Vector3((-beamSize * 0.5f) + (spacing * (i - 1)), -beamRange * 0.5f, 0));
                    }

                    if (i == 0 || i == (rays-1))
                    {
                        verts.Add(DiectionalLightPivotPoint + new Vector3((-beamSize * 0.5f) + (spacing * i), beamRange * 0.5f, 0));
                        verts.Add(DiectionalLightPivotPoint + new Vector3((-beamSize * 0.5f) + (spacing * i), -beamRange * 0.5f, 0));
                    }

                    wasHit = false;
                }
            }
        }

        // =====================================================================


        _mesh.Clear();
        _mesh.vertices = verts.ToArray();
        UpdateTriangles();
        UpdateDirectionalUVs();

        if (colors.Count != verts.Count)
            UpdateColors();

        if (normals.Count != verts.Count)
            UpdateNormals();

        _mesh.colors32 = colors.ToArray();
        _mesh.normals = normals.ToArray();
        _mesh.RecalculateBounds();

        if (!Application.isPlaying)
            _filter.sharedMesh = _mesh;
        else
            _filter.mesh = _mesh;

        flagMeshUpdate = false;
    }

    void UpdateRadialMesh()
    {
        RaycastHit rhit = new RaycastHit();

        #if !(UNITY_2_6 || UNITY_2_6_1 || UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5 || UNITY_4_0 || UNITY_4_0_1 || UNITY_4_1 || UNITY_4_2)
        RaycastHit2D rhit2D = new RaycastHit2D();
        #endif
        
        bool wasHit = true;
        coneEdgeGenerated = false;
        int rays = (int)lightDetail;

        verts.Clear();

        if (coneAngle != 0)
        {
            verts.Add(Vector3.zero);
            UpdateConeMinMax();

            for (int i = 0; i < rays + 1; i++)
            {
                float a = i * (360f / (float)rays);

                if (coneAngle == 360 || (a >= coneRangeMin && a < coneRangeMax))
                {
                    bool hit2d = false;
                    bool hit3d = false;

                    #if !(UNITY_2_6 || UNITY_2_6_1 || UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5 || UNITY_4_0 || UNITY_4_0_1 || UNITY_4_1 || UNITY_4_2)
                    if (objs._2DColliders != null && objs._2DColliders.Length > 0)
                    {
                        rhit2D = Physics2D.Raycast(transform.position, transform.TransformDirection(Quaternion.Euler(0, 0, coneStart) * circleRef[i]), lightRadius, shadowLayer);
                        hit2d = !(rhit2D.point == Vector2.zero);
                    }
                    #endif

                    if (objs._3DColliders != null && objs._3DColliders.Length > 0)
                    {
                        hit3d = Physics.Raycast(transform.position, transform.TransformDirection(Quaternion.Euler(0, 0, coneStart) * circleRef[i]), out rhit, lightRadius, shadowLayer);
                    }

                    if (flagObjsInRange && (hit2d || hit3d))
                    {
                        if (Application.isPlaying && useEvents)
                        {
                            #if !(UNITY_2_6 || UNITY_2_6_1 || UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5 || UNITY_4_0 || UNITY_4_0_1 || UNITY_4_1 || UNITY_4_2)
                            if (hit2d && !unidentifiedObjects.Contains(rhit2D.transform.gameObject))
                                unidentifiedObjects.Add(rhit2D.transform.gameObject);
                            #endif

                            if (hit3d && !unidentifiedObjects.Contains(rhit.transform.gameObject))
                                unidentifiedObjects.Add(rhit.transform.gameObject);
                        }

                        if (!wasHit)
                            verts.Add(Quaternion.Euler(0, 0, coneStart) * (circleRef[i - 1] * lightRadius));

                        //bool hit2DFirst = (Mathf.RoundToInt(Vector2.SqrMagnitude(rhit2D.point - transform.position) * 1000) - Mathf.RoundToInt(Vector3.SqrMagnitude(rhit.point) * 1000)) >= 0;
                        #if !(UNITY_2_6 || UNITY_2_6_1 || UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5 || UNITY_4_0 || UNITY_4_0_1 || UNITY_4_1 || UNITY_4_2)
                        if (hit2d && hit3d)
                        {
                            verts.Add((Vector3.Distance(rhit2D.point, transform.position) < rhit.distance) ? transform.InverseTransformPoint(rhit2D.point) : transform.InverseTransformPoint(rhit.point));
                        }
                        else if (hit2d)
                        {
                            verts.Add(transform.InverseTransformPoint(new Vector3(rhit2D.point.x, rhit2D.point.y, transform.position.z)));//new Vector3(rhit2D.point.x, rhit2D.point.y, transform.position.z));//transform.InverseTransformPoint(new Vector3(rhit2D.point.x, rhit2D.point.y, transform.position.z)));
                        }
                        else if (hit3d)
                        {
                            verts.Add(transform.InverseTransformPoint(rhit.point));
                        }
                        #else
                        verts.Add(transform.InverseTransformPoint(rhit.point));
                        #endif

                        //=============== Removes Unnecessary Vertices ===============================s
                        if (verts.Count > 2)
                        {
                            prevPoints[0] = verts[verts.Count - 3];
                            prevPoints[1] = verts[verts.Count - 2];
                            prevPoints[2] = verts[verts.Count - 1];

                            if (Vector3.SqrMagnitude((prevPoints[0] - prevPoints[1]).normalized - (prevPoints[1] - prevPoints[2]).normalized) <= 0.01f)
                            {
                                verts.RemoveAt(verts.Count - 2);
                            }
                        }
                        //=============== END Remove Code =================================*/

                        wasHit = true;
                    }
                    else
                    {
                        if (a != 0 && wasHit)
                            verts.Add(Quaternion.Euler(0, 0, coneStart) * (circleRef[i] * lightRadius));

                        if (a == 45 || a == 135 || a == 225 || a == 315)
                            verts.Add(Quaternion.Euler(0, 0, coneStart) * (circleRef[i] * lightRadius));

                        wasHit = false;
                    }
                }

                if (coneAngle != 360 && (a >= coneRangeMax && !coneEdgeGenerated))
                {
                    bool hit2d = false;
                    bool hit3d = false;

                    #if !(UNITY_2_6 || UNITY_2_6_1 || UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5 || UNITY_4_0 || UNITY_4_0_1 || UNITY_4_1 || UNITY_4_2)
                    if (objs._2DColliders != null && objs._2DColliders.Length > 0)
                    {
                        rhit2D = Physics2D.Raycast(transform.position, transform.TransformDirection(Quaternion.Euler(0, 0, coneStart) * circleRef[i]), lightRadius, shadowLayer);
                        hit2d = !(rhit2D.point == Vector2.zero);
                    }
                    #endif

                    if (objs._3DColliders != null && objs._3DColliders.Length > 0)
                    {
                        hit3d = Physics.Raycast(transform.position, transform.TransformDirection(Quaternion.Euler(0, 0, coneStart) * circleRef[i]), out rhit, lightRadius, shadowLayer);
                    }

                    if (flagObjsInRange && (hit2d || hit3d))
                    {
                        if (Application.isPlaying && useEvents)
                        {
                            #if !(UNITY_2_6 || UNITY_2_6_1 || UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5 || UNITY_4_0 || UNITY_4_0_1 || UNITY_4_1 || UNITY_4_2)
                            if (hit2d && !unidentifiedObjects.Contains(rhit2D.transform.gameObject))
                                unidentifiedObjects.Add(rhit2D.transform.gameObject);
                            #endif
                            
                            if (hit3d && !unidentifiedObjects.Contains(rhit.transform.gameObject))
                                unidentifiedObjects.Add(rhit.transform.gameObject);
                        }
                        
                        if (!wasHit)
                            verts.Add(Quaternion.Euler(0, 0, coneStart) * (circleRef[i - 1] * lightRadius));


                        #if !(UNITY_2_6 || UNITY_2_6_1 || UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5 || UNITY_4_0 || UNITY_4_0_1 || UNITY_4_1 || UNITY_4_2)
                        if (hit2d && hit3d)
                        {
                            //verts.Add(hit2DFirst ? transform.InverseTransformPoint(rhit2D.point) : transform.InverseTransformPoint(rhit.point));
                            verts.Add((Vector3.Distance(rhit2D.point, transform.position) < rhit.distance) ? transform.InverseTransformPoint(rhit2D.point) : transform.InverseTransformPoint(rhit.point));
                        }
                        else if (hit2d)
                        {
                            verts.Add(transform.InverseTransformPoint(new Vector3(rhit2D.point.x, rhit2D.point.y, transform.position.z)));
                        }
                        else if (hit3d)
                        {
                            verts.Add(transform.InverseTransformPoint(rhit.point));
                        }
                        #else
                        verts.Add(transform.InverseTransformPoint(rhit.point));
                        #endif

                        //=============== Removes Unnecessary Vertices ===============================s
                        if (verts.Count > 2)
                        {
                            prevPoints[0] = verts[verts.Count - 3];
                            prevPoints[1] = verts[verts.Count - 2];
                            prevPoints[2] = verts[verts.Count - 1];

                            if (Vector3.SqrMagnitude((prevPoints[0] - prevPoints[1]).normalized - (prevPoints[1] - prevPoints[2]).normalized) <= 0.01f)
                            {
                                verts.RemoveAt(verts.Count - 2);
                            }
                        }
                        //=============== END Remove Code =================================*/

                        wasHit = true;
                    }
                    else
                    {
                        if (a != 0 && wasHit)
                            verts.Add(Quaternion.Euler(0, 0, coneStart) * (circleRef[i] * lightRadius));

                        verts.Add(Quaternion.Euler(0, 0, coneStart) * (circleRef[i] * lightRadius));
                        wasHit = false;
                    }
                    coneEdgeGenerated = true;
                }
            }
        }

        _mesh.Clear();
        _mesh.vertices = verts.ToArray();
        UpdateTriangles();
        UpdateRadialUVs();

        if (colors.Count != verts.Count)
            UpdateColors();

        if (normals.Count != verts.Count)
            UpdateNormals();

        _mesh.colors32 = colors.ToArray();
        _mesh.normals = normals.ToArray();
        _mesh.RecalculateBounds();

        if (!Application.isPlaying)
            _filter.sharedMesh = _mesh;
        else
            _filter.mesh = _mesh;

        flagMeshUpdate = false;
    }

    void UpdateRadialShadowMesh()
    {
        verts.Clear();
        uvs.Clear();
        tris.Clear();
        normals.Clear();
        colors.Clear();

        int triCount = 0;
        RaycastHit rhit1 = new RaycastHit();
        RaycastHit rhit2 = new RaycastHit();

        #if !(UNITY_2_6 || UNITY_2_6_1 || UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5 || UNITY_4_0 || UNITY_4_0_1 || UNITY_4_1 || UNITY_4_2)
        RaycastHit2D rhit2D1 = new RaycastHit2D();
        RaycastHit2D rhit2D2 = new RaycastHit2D();
        #endif
        
        for (int i = 0; i < circleRef.Count - 1; i++)
        {
            Vector3 p1 = circleRef[i] * lightRadius;
            Vector3 p2 = circleRef[i + 1] * lightRadius;

            bool hit2d = false;
            bool hit3d = false;

            #if !(UNITY_2_6 || UNITY_2_6_1 || UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5 || UNITY_4_0 || UNITY_4_0_1 || UNITY_4_1 || UNITY_4_2)
            if (objs._2DColliders != null && objs._2DColliders.Length > 0)
            {
                rhit2D1 = Physics2D.Raycast(transform.position, transform.TransformDirection(p1), lightRadius, shadowLayer);
                rhit2D2 = Physics2D.Raycast(transform.position, transform.TransformDirection(p2), lightRadius, shadowLayer);

                hit2d = !((rhit2D1.point == Vector2.zero) || (rhit2D2.point == Vector2.zero));
            }
            #endif

            if (objs._3DColliders != null && objs._3DColliders.Length > 0)
            {
                hit3d = Physics.Raycast(transform.position, transform.TransformDirection(p1), out rhit1, lightRadius, shadowLayer)
                    && Physics.Raycast(transform.position, transform.TransformDirection(p2), out rhit2, lightRadius, shadowLayer);
            }

            if (flagObjsInRange && (hit2d || hit3d))
            {
                Vector3 uvp = Vector3.zero;

                if (Application.isPlaying && useEvents)
                {

                    #if !(UNITY_2_6 || UNITY_2_6_1 || UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5 || UNITY_4_0 || UNITY_4_0_1 || UNITY_4_1 || UNITY_4_2)
                    if (hit2d && !unidentifiedObjects.Contains(rhit2D1.transform.gameObject))
                        unidentifiedObjects.Add(rhit2D1.transform.gameObject);
                    #endif

                    if (hit3d && !unidentifiedObjects.Contains(rhit1.transform.gameObject))
                        unidentifiedObjects.Add(rhit1.transform.gameObject);
                }

                #if !(UNITY_2_6 || UNITY_2_6_1 || UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5 || UNITY_4_0 || UNITY_4_0_1 || UNITY_4_1 || UNITY_4_2)
                if (hit2d && hit3d)
                {
                    uvp = (Vector3.Distance(rhit2D1.point, transform.position) < rhit1.distance) ? transform.InverseTransformPoint(new Vector3(rhit2D1.point.x, rhit2D2.point.y, transform.position.z)) : transform.InverseTransformPoint(rhit1.point);
                }
                else if (hit2d)
                {
                    uvp = transform.InverseTransformPoint(new Vector3(rhit2D1.point.x, rhit2D1.point.y, transform.position.z));
                }
                else if (hit3d)
                {
                    uvp = transform.InverseTransformPoint(rhit1.point);  // 0 
                }
                #else
                uvp = transform.InverseTransformPoint(rhit1.point);
                #endif

                verts.Add(uvp);
                normals.Add(-Vector3.forward);
                colors.Add(lightColor);
                uvs.Add(new Vector2((0.5f + (uvp.x * 0.5f) / lightRadius), (0.5f + (uvp.y * 0.5f) / lightRadius)));

                uvp = p1;
                verts.Add(uvp);
                normals.Add(-Vector3.forward);
                colors.Add(lightColor);
                uvs.Add(new Vector2((0.5f + (uvp.x * 0.5f) / lightRadius), (0.5f + (uvp.y * 0.5f) / lightRadius)));

                #if !(UNITY_2_6 || UNITY_2_6_1 || UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5 || UNITY_4_0 || UNITY_4_0_1 || UNITY_4_1 || UNITY_4_2)
                if (hit2d && hit3d)
                {
                    uvp = (Vector3.Distance(rhit2D2.point, transform.position) < rhit2.distance) ? transform.InverseTransformPoint(new Vector3(rhit2D2.point.x, rhit2D2.point.y, transform.position.z)) : transform.InverseTransformPoint(rhit2.point);
                }
                else if (hit2d)
                {
                    uvp = transform.InverseTransformPoint(new Vector3(rhit2D2.point.x, rhit2D2.point.y, transform.position.z));
                }
                else if (hit3d)
                {
                    uvp = transform.InverseTransformPoint(rhit2.point);  // 0 
                }
                #else
                uvp = transform.InverseTransformPoint(rhit2.point);
                #endif

                verts.Add(uvp);
                normals.Add(-Vector3.forward);
                colors.Add(lightColor);
                uvs.Add(new Vector2((0.5f + (uvp.x * 0.5f) / lightRadius), (0.5f + (uvp.y * 0.5f) / lightRadius)));

                uvp = p2;
                verts.Add(uvp);
                normals.Add(-Vector3.forward);
                colors.Add(lightColor);
                uvs.Add(new Vector2((0.5f + (uvp.x * 0.5f) / lightRadius), (0.5f + (uvp.y * 0.5f) / lightRadius)));

                tris.Add(triCount + 2);
                tris.Add(triCount + 1);
                tris.Add(triCount + 0);

                tris.Add(triCount + 2);
                tris.Add(triCount + 3);
                tris.Add(triCount + 1);

                triCount += 4;
            }
        }

        _mesh.Clear();
        _mesh.vertices = verts.ToArray();
        _mesh.triangles = tris.ToArray();
        _mesh.normals = normals.ToArray();
        _mesh.uv = uvs.ToArray();
        _mesh.colors32 = colors.ToArray();
        _mesh.RecalculateBounds();

        if (!Application.isPlaying)
            _filter.sharedMesh = _mesh;
        else
            _filter.mesh = _mesh;

        flagMeshUpdate = false;
    }

    void UpdateCircleRef()
    {
        float x = 0;
        float y = 0;
        Vector3 v = Vector3.zero;
        int rays = (int)lightDetail;

        circleRef.Clear();

        for (int i = 0; i < rays + 1; i++)
        {
            float a = i * (360f / (float)rays);
            Vector2 circle = new Vector2(Mathf.Sin(a * Mathf.Deg2Rad) / Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Cos(a * Mathf.Deg2Rad) / Mathf.Sin(a * Mathf.Deg2Rad));

            if (a >= 315 || a <= 45)    // RIGHT SIDE
            {
                x = 1;
                y = x * circle.x;
            }

            if (a > 45 && a < 135)      // TOP SIDE
            {
                y = 1;
                x = y * circle.y;
            }

            if (a >= 135 && a <= 225)   // LEFT SIDE
            {
                x = -1;
                y = x * circle.x;
            }

            if (a > 225 && a < 315)     // BOTTOM SIDE
            {
                y = -1;
                x = y * circle.y;
            }

            v = new Vector3(x, y, 0);
            circleRef.Add(-v);
        }

        flagCircleUpdate = false;
    }

    void UpdateTriangles()
    {
        tris.Clear();

        if (lightType != LightTypeSetting.Directional)
        {
            for (int v = 0; v < _mesh.vertexCount - 1; v++)
            {
                tris.Add(0);
                tris.Add(v + 1);
                tris.Add(v);
            }

            if (coneAngle == 360)
            {
                // Add Final Triangle
                tris.Add(0);
                tris.Add(1);
                tris.Add(verts.Count - 1);
            }
        }
        else
        {
            for (var v = 2; v < verts.Count - 1; v += 2)
            {
                tris.Add(v);
                tris.Add(v - 1);
                tris.Add(v - 2);

                tris.Add(v + 1);
                tris.Add(v - 1);
                tris.Add(v);
            }
        }

        _mesh.triangles = tris.ToArray();
    }

    void UpdateNormals()
    {
        normals.Clear();

        for (int i = 0; i < verts.Count; i++)
            normals.Add(-Vector3.forward);

        _mesh.normals = normals.ToArray();
        flagNormalsUpdate = false;
    }

    void UpdateRadialUVs()
    {
        uvs.Clear();

        for (int i = 0; i < verts.Count; i++)
        {
            //uvs.Add(Quaternion.Euler(0, 0, -coneStart) * new Vector2((verts[i].x * 0.5f) / (lightRadius * uvTiling.x) + (0.5f + uvOffset.x), (verts[i].y * 0.5f) / (lightRadius * uvTiling.y) + (0.5f + uvOffset.y)));

            Vector2 uv = Quaternion.Euler(0, 0, -coneStart) * new Vector2((verts[i].x * 0.5f) / lightRadius, (verts[i].y * 0.5f) / lightRadius);
            uvs.Add(new Vector2(uv.x + 0.5f, uv.y + 0.5f));
        }

        _mesh.uv = uvs.ToArray();
        flagUVUpdate = false;
    }

    void UpdateDirectionalUVs()
    {
        uvs.Clear();
        Vector2 dlp = (Vector2)DiectionalLightPivotPoint;

        for (int i = 0; i < verts.Count; i++)
        {
            uvs.Add(new Vector2((verts[i].x - dlp.x) / (beamSize * uvTiling.x) + (0.5f + uvOffset.x), (verts[i].y - dlp.y) / (beamRange * uvTiling.y) + (0.5f + uvOffset.y)));
        }

        _mesh.uv = uvs.ToArray();
        flagUVUpdate = false;
    }

    void UpdateColors()
    {
        colors.Clear();

        for (int i = 0; i < verts.Count; i++)
            colors.Add(lightColor);

        _mesh.colors32 = colors.ToArray();
        flagColorUpdate = false;
    }

    void UpdateConeMinMax()
    {
        coneRangeMin = 0;
        coneRangeMax = 360;

        if (coneAngle != 360)
        {
            coneRangeMin = (360f - coneAngle) * 0.5f;
            coneRangeMax = 180 + (coneAngle * 0.5f);
        }
    }

    public void FlagMeshupdate()
    {
        flagCircleUpdate = true;
        flagColorUpdate = true;
        flagMeshUpdate = true;
        flagUVUpdate = true;
        flagNormalsUpdate = true;
        flagMaterialUpdate = true;
    }

    /// <summary>
    /// A custom 'LookAt' funtion which looks along the lights 'Right' direction. This function was implimented for those unfamiliar with Quaternion math as
    /// without that math its nearly impossible to get the right results using the typical 'transform.LookAt' function.
    /// </summary>
    /// <param name="_target">The GameObject you want the light to look at.</param>
    public void LookAt(GameObject _target)
    {
        LookAt(_target.transform.position);
    }
    /// <summary>
    /// A custom 'LookAt' funtion which looks along the lights 'Right' direction. This function was implimented for those unfamiliar with Quaternion math as
    /// without that math its nearly impossible to get the right results using the typical 'transform.LookAt' function.
    /// </summary>
    /// <param name="_target">The Transform you want the light to look at.</param>
    public void LookAt(Transform _target)
    {
        LookAt(_target.position);
    }
    /// <summary>
    /// A custom 'LookAt' funtion which looks along the lights 'Right' direction. This function was implimented for those unfamiliar with Quaternion math as
    /// without that math its nearly impossible to get the right results using the typical 'transform.LookAt' function.
    /// </summary>
    /// <param name="_target">The Vecto3 position you want the light to look at.</param>
    public void LookAt(Vector3 _target)
    {
        transform.rotation = Quaternion.LookRotation(transform.position - _target, Vector3.forward) * lookAtRotation;
    }

    /// <summary>
    /// Toggles the light on or off
    /// </summary>
    /// <param name="_updateMesh">If 'TRUE' mesh will be forced to update. Use this if your light is dynamic when toggling it on.</param>
    /// <returns>'TRUE' if light is on.</returns>
    public bool ToggleLight(bool _updateMesh = false)
    {
        lightEnabled = !lightEnabled;

        if (_updateMesh)
        {
            /*
            if (isShadowCaster)
                UpdateMesh_RadialShadow();
            else
                UpdateMesh_Radial();
            
            */
        }

        return lightEnabled;
    }

    /// <summary>
    /// Provides and easy way to register your event method. The delegate takes the form of 'Foo(Light2D, GameObject)'.
    /// </summary>
    /// <param name="_eventType">Choose from 3 event types. 'OnEnter', 'OnStay', or 'OnExit'. Does not accept flags as argument.</param>
    /// <param name="_eventMethod">A callback method in the form of 'Foo(Light2D, GameObject)'.</param>
    public static void RegisterEventListener(LightEventListenerType _eventType, Light2DEvent _eventMethod)
    {
        if (_eventType == LightEventListenerType.OnEnter)
            OnBeamEnter += _eventMethod;

        if (_eventType == LightEventListenerType.OnStay)
            OnBeamStay += _eventMethod;

        if (_eventType == LightEventListenerType.OnExit)
            OnBeamExit += _eventMethod;
    }

    /// <summary>
    /// Provides and easy way to unregister your events. Usually used in the 'OnDestroy' and 'OnDisable' functions of your gameobject.
    /// </summary>
    /// <param name="_eventType">Choose from 3 event types. 'OnEnter', 'OnStay', or 'OnExit'. Does not accept flags as argument.</param>
    /// <param name="_eventMethod">The callback method you wish to remove.</param>
    public static void UnregisterEventListener(LightEventListenerType _eventType, Light2DEvent _eventMethod)
    {
        if (_eventType == LightEventListenerType.OnEnter)
            OnBeamEnter -= _eventMethod;

        if (_eventType == LightEventListenerType.OnStay)
            OnBeamStay -= _eventMethod;

        if (_eventType == LightEventListenerType.OnExit)
            OnBeamExit -= _eventMethod;
    }

    public void TriggerBeamEvent(LightEventListenerType eventType, GameObject eventGameObject)
    {
        switch(eventType)
        {
            case LightEventListenerType.OnEnter:
                if (OnBeamEnter != null)
                    OnBeamEnter(this, eventGameObject);
                break;

            case LightEventListenerType.OnStay:
                if (OnBeamStay != null)
                    OnBeamStay(this, eventGameObject);
                break;

            case LightEventListenerType.OnExit:
                if (OnBeamExit != null)
                    OnBeamExit(this, eventGameObject);
                break;
        }
    }


    /// <summary>
    /// Easy static function for creating 2D lights.
    /// </summary>
    /// <param name="_position">Sets the position of the created light</param>
    /// <param name="_lightColor">Sets the color of the created light</param>
    /// <param name="_lightRadius">Sets the radius of the created light</param>
    /// <param name="_lightConeAngle">Sets the cone angle of the light</param>
    /// <param name="_lightDetail">Sets the detail of the light</param>
    /// <param name="_useEvents">If 'TRUE' event messages will be sent.</param>
    /// <param name="_lightType">Sets what type of light will be rendered. [Directional, Radial, Shadow]</param>
    /// <returns>Returns the created Light2D object, NOT the gameobject.</returns>
    public static Light2D Create(Vector3 _position, Color _lightColor, float _lightRadius = 1, int _lightConeAngle = 360, LightDetailSetting _lightDetail = LightDetailSetting.Rays_500, bool _useEvents = false, LightTypeSetting _lightType = LightTypeSetting.Radial)
    {
        return Create(_position, (Material)Resources.Load("RadialLight"), _lightColor, _lightRadius, _lightConeAngle, _lightDetail, _useEvents, _lightType);
    }

    /// <summary>
    /// Easy static function for creating 2D lights.
    /// </summary>
    /// <param name="_position">Sets the position of the created light</param>
    /// <param name="_lightMaterial">Sets the Material of the light</param>
    /// <param name="_lightColor">Sets the color of the created light</param>
    /// <param name="_lightRadius">Sets the radius of the created light. [If Directional this is equal to LightBeamSize]</param>
    /// <param name="_lightConeAngle">Sets the cone angle of the light. [If Directional this is equal to LightBeamRange]</param>
    /// <param name="_lightDetail">Sets the detail of the light</param>
    /// <param name="_useEvents">If 'TRUE' event messages will be sent.</param>
    /// <param name="_lightType">Sets what type of light will be rendered. [Directional, Radial, Shadow]</param>
    /// <returns>Returns the created Light2D object, NOT the gameobject.</returns>
    public static Light2D Create(Vector3 _position, Material _lightMaterial, Color _lightColor, float _lightRadius = 1, int _lightConeAngle = 360, LightDetailSetting _lightDetail = LightDetailSetting.Rays_500, bool _useEvents = false, LightTypeSetting _lightType = LightTypeSetting.Radial)
    {
        GameObject obj = new GameObject("New 2D-" + _lightType.ToString());
        obj.transform.position = _position;

        Light2D l2D = obj.AddComponent<Light2D>();
        l2D.LightMaterial = _lightMaterial;
        l2D.LightColor = _lightColor;
        l2D.LightDetail = _lightDetail;

        if (_lightType != Light2D.LightTypeSetting.Directional)
        {
            l2D.LightRadius = _lightRadius;
            l2D.LightConeAngle = _lightConeAngle;
        }
        else
        {
            l2D.LightBeamSize = _lightRadius;
            l2D.LightBeamRange = _lightConeAngle;
        }
        
        l2D.ShadowLayer = -1;
        l2D.EnableEvents = _useEvents;
        l2D.LightType = _lightType;

        return l2D;
    }
}

// Doxygen Stuff

/*! \mainpage Thank you for choosing 2DVLS!
 *      Below you can find some helpful links to tutorials and the API available to programmers via the Light2D class. You can alternativly search
 *      for these files by clicking 'Related Pages' or the 'Classes' tab.
 *      \tableofcontents
 *          \section sec1API API
 *              \link Light2D Light2D API \endlink \n
 *          \section sec2Tutorials Tutorials
 *              \link creatSoftShadows Creating Soft Shadows \endlink \n
 *          \section sec3MadeWith Samples/Screenshots
 *              \link promoImages In-Game Screenshots \endlink \n
 *              \link sampleScenes Sample Scenes \endlink \n
 */

/*! \page creatSoftShadows Creating Soft Shadows
 *      \image html http://www.reverieinteractive.com/2DVLS/PromoImages/SoftLights.png
 *      \tableofcontents
 *          \section sec1 Adding Assets
 *              1) NOTE: This only works for PRO users as it requires the use of image effects.\n
 *              2) Right-Click in your 'Project' pane and highlight 'Import Package'. \n
 *              3) Left-Click on 'Image Effects (Pro Only)' to import the required assets. \n
 *          \section sec2 Setting Up Layers
 *              1) Goto [Edit >> Project Settings >> Tags] \n
 *              2) Add new user layer to 'User Layer #' called 'Light' \n
 *          \section sec3 Setting Up Cameras
 *              1) Add a new camera to the scene \n
 *              2) Parent camera to the 'Main Camera' and center the main camera onto it. [GameObject >> Center On Children] \n
 *              3) Move 'Main Camera' to X: 0, Y: 0, Z: -10 \n
 *              4) Delete 'Flare Layer', 'GUILayer', and 'Audio Listener' components from the new camera as you will not need these components \n
 *              5) Set the 'Culling Mask' layers of the new camera to only include 'Light' \n
 *              6) Add the 'Blur' image effect to the camera [Component >> Image Effects >> Blur] \n
 *              7) Click on 'Main Camera' object \n
 *              8) Set the 'Clear Flags' to 'Depth Only' \n
 *              9) Set the Main Camera's culling mask to exclude the 'Light' layer \n
 *          \section sec4 Adding Lights/Cubes
 *              1) Add light [GameObjects >> Create Other >> Light2D >> Radial Light] \n
 *              2) Set lights layer to 'Light' \n
 *              3) Add cube and place in light to see the effect!. \n
*/

/*! \page promoImages Made with 2DVLS
 *      <br/>
 *      <h2 align="center"><a href="http://luminesca.com/">Luminesca (click to go to Luminesca.com)</a></h2>
 *      <img width="620px" src="http://www.reverieinteractive.com/2DVLS/PromoImages/Luminesca_02.png"><div align="center">"Luminesca 1"</div></img><p/>
 *      <img width="620px" src="http://www.reverieinteractive.com/2DVLS/PromoImages/Luminesca_03.png"><div align="center">"Luminesca 2"</div></img><p/>
 *      <img width="620px" src="http://www.reverieinteractive.com/2DVLS/PromoImages/Luminesca_04.png"><div align="center">"Luminesca 3"</div></img><p/>
 *      <img width="620px" src="http://www.reverieinteractive.com/2DVLS/PromoImages/Luminesca_05.png"><div align="center">"Luminesca 4"</div></img><p/>
 *      <br/>
 *      <h2 align="center"><a href="http://reverieinteractive.com/">2D Volumetric Lights</a></h2>
 *      <img width="620px" src="http://www.reverieinteractive.com/2DVLS/PromoImages/SoftLights.png"><div align="center">"Soft Shadows"</div></img>
 *      <br/>
*       <a href="mailto:jake@reverieinteractive.com">Would you like to display the games and work you have done using 2DVLS? Click here to email me at jake@reverieinteractive.com</a> 
*/

/*! \page sampleScenes Sample Scenes
 *      <br/>
 *      <h2 align="center"><a href="http://reverieinteractive.com/2DVLS/CreateLightsSoft/">2D Soft Lights</a></h2>
 *      <img width="400px" src="http://www.reverieinteractive.com/2DVLS/PromoImages/SoftLights.png"></img>
 *      <h2 align="center"><a href="http://reverieinteractive.com/2DVLS/EventsDemo/">Events Sample</a></h2>
 *      <img width="400px" src="http://www.reverieinteractive.com/2DVLS/PromoImages/eventsdemo.png"></img>
 *      <h2 align="center"><a href="http://reverieinteractive.com/2DVLS/LightRoom/">Light Room Sample</a></h2>
 *      <img width="400px" src="http://www.reverieinteractive.com/2DVLS/PromoImages/lightroom.png"></img>
 *      <h2 align="center"><a href="http://reverieinteractive.com/2DVLS/RapidSpawn/">Rapid Spawn Sample</a></h2>
 *      <img width="400px" src="http://www.reverieinteractive.com/2DVLS/PromoImages/rapidspawn.png"></img>
 *      <h2 align="center"><a href="http://reverieinteractive.com/2DVLS/Shadows/">Shadows Sample</a></h2>
 *      <img width="400px" src="http://www.reverieinteractive.com/2DVLS/PromoImages/shadows.png"></img>
 *      <h2 align="center"><a href="http://reverieinteractive.com/2DVLS/LotsOfLights/">Lots of Lights Sample</a></h2>
 *      <img width="400px" src="http://www.reverieinteractive.com/2DVLS/PromoImages/lotsoflights.png"></img>
 * 
*/
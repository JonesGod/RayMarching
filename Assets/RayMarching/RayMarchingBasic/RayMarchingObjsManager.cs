using System;
using UnityEngine;

public class RayMarchingObjsManager : MonoBehaviour
{
    private static RayMarchingObjsManager _instance;

    public static RayMarchingObjsManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<RayMarchingObjsManager>();
                if (_instance == null)
                {
                    GameObject singletonObject = new GameObject(nameof(RayMarchingObjsManager));
                    _instance = singletonObject.AddComponent<RayMarchingObjsManager>();

                    if (Application.isPlaying)
                        DontDestroyOnLoad(singletonObject);
                }
            }
            return _instance;
        }
        private set {}
    }
    
    public RayMarchingSphere[] RayMarchingSphereSphere;

    public Material RayMarchingMaterial;
    
    public GraphicsBuffer sphereBuffer;
    
    [SerializeField]
    public Vector4[] Spheres;
    
    public Transform[] SphereTransforms;

    
    private struct Sphere
    {
        public Vector3 position;
        public float radius;
        public Vector3 color;
    }
    
    public ComputeBuffer SphereBuffer;
    
    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            DestroyImmediate(gameObject);
        }
    }

    public void RefreshTransforms()
    {
        // Sphere[] spheres = new Sphere[RayMarchingSphereSphere.Length];
        // for ( int i = 0; i < spheres.Length; ++i )
        // {
        //     var color = RayMarchingSphereSphere[i].SphereBaseColor;
        //     spheres[i].color = new Vector3(color.r, color.g, color.b);
        //     spheres[i].position = RayMarchingSphereSphere[i].transform.position;
        //     spheres[i].radius = RayMarchingSphereSphere[i].transform.localScale.x;
        // }
        //
        // if ( sphereBuffer == null )
        // {
        //     sphereBuffer = new GraphicsBuffer( GraphicsBuffer.Target.Structured,spheres.Length, sizeof(float) * 7);
        // }
        // sphereBuffer.SetData(spheres);
        //RayMarchingMaterial.SetBuffer("_SphereBuffer", sphereBuffer);
        
        if ( Spheres == null || Spheres.Length != SphereTransforms.Length )
        {
            Spheres = new Vector4[SphereTransforms.Length];
        }
        for ( int i = 0; i < Spheres.Length; i++ )
        {
            var position = SphereTransforms[i].position;
            Spheres[i].x = position.x;
            Spheres[i].y = position.y;
            Spheres[i].z = position.z;
            Spheres[i].w = SphereTransforms[i].localScale.x;
        }
    }
}

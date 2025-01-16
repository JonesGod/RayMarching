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
                if (_instance != null)
                {
                    // GameObject singletonObject = new GameObject(nameof(RayMarchingObjsManager));
                    // _instance = singletonObject.AddComponent<RayMarchingObjsManager>();

                    if (Application.isPlaying)
                        DontDestroyOnLoad(_instance);
                }
            }
            return _instance;
        }
        private set {}
    }
    
    public RayMarchingSphere[] RayMarchingSpheres;

    public Material RayMarchingMaterial;
    
    public ReflectionProbe ReflectionProbe;
    
    GraphicsBuffer sphereBuffer;

    
    private struct Sphere
    {
        public Vector3 position;
        public float radius;
        public Vector3 color;
    }
    
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
        Sphere[] spheres = new Sphere[RayMarchingSpheres.Length];
        for ( int i = 0; i < spheres.Length; ++i )
        {
            var color = RayMarchingSpheres[i].SphereBaseColor;
            spheres[i].color = new Vector3(color.r, color.g, color.b);
            spheres[i].position = RayMarchingSpheres[i].transform.position;
            spheres[i].radius = RayMarchingSpheres[i].transform.localScale.x;
        }
        
        if ( sphereBuffer == null )
        {
            sphereBuffer = new GraphicsBuffer( GraphicsBuffer.Target.Structured,spheres.Length, sizeof(float) * 7);
        }
        sphereBuffer.SetData(spheres);
        RayMarchingMaterial.SetBuffer("_SphereBuffer", sphereBuffer);
        
        if ( ReflectionProbe != null )
        {
            RayMarchingMaterial.SetTexture("_Reflection_CubeMap", ReflectionProbe.texture);
        }
    }
}

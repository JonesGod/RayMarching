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
    
    [SerializeField]
    public Vector4[] Spheres;
    
    public Transform[] SphereTransforms;
    
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

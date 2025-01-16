using UnityEngine;

public class TestReflMono : MonoBehaviour
{
    public Material material;
    void Start()
    {
        material.SetTexture( "_Reflection_CubeMap", RayMarchingObjsManager.Instance.ReflectionProbe.texture );
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

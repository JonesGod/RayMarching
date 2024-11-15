using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

public class DrawSphereByCS : MonoBehaviour
{
    public Light DirectionalLight;
    
    public ComputeShader ComputeShader;

    public Vector3 SphereWorldPos;
    
    [SerializeField]
    int textureResolution;
    
    [SerializeField]
    float sphereRadius;
    
    RenderTexture renderTexture;
    private int kernelID;

    void OnEnable() 
    {
        RenderPipelineManager.endCameraRendering += DrawSphere;
    }

    void OnDisable() 
    {
        RenderPipelineManager.endCameraRendering -= DrawSphere;
    }

    private void Start()
    {
        if ( renderTexture == null )
        {
            renderTexture = new RenderTexture(textureResolution, textureResolution, 24, format: GraphicsFormat.R32G32B32A32_SFloat);
            renderTexture.filterMode = FilterMode.Bilinear;
            renderTexture.name = "_DrawSphereRT";
            renderTexture.enableRandomWrite = true;
            renderTexture.Create();
        }

        kernelID = ComputeShader.FindKernel( "DrawSphere" );
        ComputeShader.SetTexture( kernelID, "_Result", renderTexture );
        ComputeShader.SetFloat( "_SphereRadius", sphereRadius );
        ComputeShader.SetInt( "_TextureResolution", textureResolution );
        
        var mainCamera = Camera.main;
        ComputeShader.SetVector( "_CameraData", new Vector2( mainCamera.fieldOfView, mainCamera.aspect ) );
    }

    private void Update()
    {
        ComputeShader.SetVector( "_SphereWorldPos",transform.position );
        var lightDirection = DirectionalLight.transform.rotation * Vector3.back;
        ComputeShader.SetVector( "_LightDirection", lightDirection );
        ComputeShader.SetVector( "_CameraPosition", Camera.main.transform.position );
        
        uint x, y, z;
        ComputeShader.GetKernelThreadGroupSizes(kernelID, out x, out y, out z);
        ComputeShader.Dispatch(kernelID, textureResolution / (int)x, textureResolution / (int)y, 1);
    }

    private void DrawSphere( ScriptableRenderContext context, Camera renderCamera )
    {
        if ( renderTexture != null )
        {
            Graphics.Blit( renderTexture, renderCamera.targetTexture );
        }
    }
    
}

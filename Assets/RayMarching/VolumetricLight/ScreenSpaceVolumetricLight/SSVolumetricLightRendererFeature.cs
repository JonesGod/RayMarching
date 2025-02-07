using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class SSVolumetricLightRendererFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class SSVolumetricLightSettings
    {
        public RenderPassEvent RenderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        public Material SSVolumetricLightMaterial;
    }
    public SSVolumetricLightSettings settings = new SSVolumetricLightSettings ();
    SSVolumetricLightPass m_ScriptablePass;

    public override void Create()
    {
        m_ScriptablePass = new SSVolumetricLightPass(settings);
        m_ScriptablePass.renderPassEvent = settings.RenderPassEvent;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(m_ScriptablePass);
    }
    
    class SSVolumetricLightPass : ScriptableRenderPass
    {
        private class PassData
        {
            internal TextureHandle sourceHandle;
            internal TextureHandle tempCopy;
            internal Material material;
        }
        
        Material ssVolumetricLightMaterial;
        
        public SSVolumetricLightPass( SSVolumetricLightSettings settings )
        {
            ssVolumetricLightMaterial = settings.SSVolumetricLightMaterial;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameContext)
        {
            if ( ssVolumetricLightMaterial == null )
            {
                Debug.LogError( "SSVolumetricLightMaterial == null" );
                return;
            }

            // This adds a raster render pass to the graph, specifying the name and the data type that will be passed to the ExecutePass function.
            using (var builder = renderGraph.AddUnsafePass<PassData>(passName, out var passData))
            {
                UniversalCameraData cameraData = frameContext.Get<UniversalCameraData>();
                UniversalResourceData resourceData = frameContext.Get<UniversalResourceData>();

                // Prepare Temp Texture
                var desc = cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0;
                desc.colorFormat = RenderTextureFormat.ARGB32;
                Vector3 sunDirWS = RenderSettings.sun.transform.forward;
                Vector3 cameraPosWS = cameraData.camera.transform.position;
                Vector3 sunPosWS = cameraPosWS - sunDirWS;
                Vector3 sunPosVS = cameraData.camera.WorldToViewportPoint(sunPosWS);
                ssVolumetricLightMaterial.SetVector( "_ScreenLightPos", sunPosVS );

                var cameraDirWS = cameraData.camera.transform.forward;
                float lightAtten = Vector3.Dot(-sunDirWS, cameraDirWS);
                lightAtten = Mathf.Clamp(lightAtten, 0, 1);
                ssVolumetricLightMaterial.SetFloat( "_LightAttenuation", lightAtten );
                
                passData.sourceHandle = resourceData.activeColorTexture;
                passData.material = ssVolumetricLightMaterial;
                passData.tempCopy = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_TempCopy", true, FilterMode.Bilinear);
                
                builder.UseTexture ( passData.sourceHandle, AccessFlags.ReadWrite );
                builder.UseTexture ( passData.tempCopy, AccessFlags.ReadWrite );
                
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(false);

                // Assigns the ExecutePass function to the render pass delegate. This will be called by the render graph when executing the pass.
                builder.SetRenderFunc((PassData data, UnsafeGraphContext context) => ExecutePass(data, context));
            }
        }
        
        void ExecutePass(PassData data, UnsafeGraphContext context)
        {
            using (new ProfilingScope(context.cmd, profilingSampler))
            {
                var commandBuffer = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                //commandBuffer.Blit ( data.sourceHandle, data.tempCopy );
                //commandBuffer.Blit ( data.tempCopy, data.sourceHandle, data.material );
                
                Blitter.BlitCameraTexture(commandBuffer, data.sourceHandle, data.tempCopy, data.material, 0);
                Blitter.BlitCameraTexture(commandBuffer, data.tempCopy, data.sourceHandle);
                
            }
        }

    }
}

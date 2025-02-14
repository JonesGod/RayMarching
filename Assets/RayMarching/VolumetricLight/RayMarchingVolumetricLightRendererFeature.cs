using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class RayMarchingVolumetricLightRendererFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class RayMarchingVolumetricLightSettings
    {
        public RenderPassEvent RenderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        public Material volumetricLightMaterial;
        public int LoopTimes = 2;
        public float BlurRange = 1.5f;
    }
    
    public RayMarchingVolumetricLightSettings settings = new RayMarchingVolumetricLightSettings();

    RayMarchingVolumetricLightPass m_ScriptablePass;

    public override void Create()
    {
        m_ScriptablePass = new RayMarchingVolumetricLightPass(settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(m_ScriptablePass);
    }
    
    
    class RayMarchingVolumetricLightPass : ScriptableRenderPass
    {
        public RayMarchingVolumetricLightPass( RayMarchingVolumetricLightSettings settings )
        {
            volumetricLightMaterial = settings.volumetricLightMaterial;
            renderPassEvent = settings.RenderPassEvent;
            loopTimes = settings.LoopTimes;
            blurRange = settings.BlurRange;
        }
        
        int loopTimes;
        float blurRange;
        
        private class PassData
        {
            internal TextureHandle sourceHandle;
            internal TextureHandle lightShaftCopy;
            internal TextureHandle tempBlur0;
            internal TextureHandle tempBlur1;
            internal Material material;
            internal int loopTimes;
            internal float blurRange;
        }

        Material volumetricLightMaterial;

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if ( volumetricLightMaterial == null)
            {
                Debug.LogError("VolumetricLightMaterial == null");
                return;
            }

            using (var builder = renderGraph.AddUnsafePass<PassData>(passName, out var passData))
            {
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

                var desc = cameraData.cameraTargetDescriptor;
                desc.width /= 2;
                desc.height /= 2;
                desc.depthBufferBits = 0;
                desc.colorFormat = RenderTextureFormat.ARGB32;

                passData.sourceHandle = resourceData.activeColorTexture;
                passData.lightShaftCopy = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_LightShaftTexture", true, FilterMode.Bilinear);

                var blurDesc = desc;
                //blurDesc.width /= 2;
                //blurDesc.height /= 2;
                passData.tempBlur0 = UniversalRenderer.CreateRenderGraphTexture(renderGraph, blurDesc, "_TempBlurTexture0", true, FilterMode.Bilinear);
                passData.tempBlur1 = UniversalRenderer.CreateRenderGraphTexture(renderGraph, blurDesc, "_TempBlurTexture1", true, FilterMode.Bilinear);
                
                passData.material = volumetricLightMaterial;
                passData.loopTimes = loopTimes;
                passData.blurRange = blurRange;
                
                builder.UseTexture( passData.sourceHandle, AccessFlags.ReadWrite );
                builder.UseTexture( passData.lightShaftCopy, AccessFlags.ReadWrite );
                builder.UseTexture( passData.tempBlur0, AccessFlags.ReadWrite );
                builder.UseTexture( passData.tempBlur1, AccessFlags.ReadWrite );
                
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(false);

                builder.SetRenderFunc((PassData data, UnsafeGraphContext context) => ExecutePass(data, context));
            }
        }

        static void ExecutePass(PassData data, UnsafeGraphContext context)
        {
            var cmd = CommandBufferHelpers.GetNativeCommandBuffer( context.cmd );
            
            // Blitter.BlitCameraTexture( cmd, data.sourceHandle, data.lightShaftCopy, data.material, 0 );
            // Blitter.BlitCameraTexture( cmd, data.lightShaftCopy, data.sourceHandle );
            
            Blitter.BlitCameraTexture( cmd, data.sourceHandle, data.lightShaftCopy, data.material, 0 );
            Blitter.BlitCameraTexture( cmd, data.lightShaftCopy, data.tempBlur0 );
            for (int t = 1; t < data.loopTimes; t++) 
            {
                data.material.SetFloat("_BlurRange", data.blurRange);
                Blitter.BlitCameraTexture(cmd, data.tempBlur0, data.tempBlur1, data.material, 1);
                var temRT = data.tempBlur0;
                data.tempBlur0 = data.tempBlur1;
                data.tempBlur1 = temRT;
            }
            Blitter.BlitCameraTexture( cmd, data.tempBlur1, data.sourceHandle, data.material, 2 );
        }
    }
}

using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using ProfilingScope = UnityEngine.Rendering.ProfilingScope;

public class RayMarchingRendererFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class RayMarchingSettings
    {
        public RenderPassEvent RenderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        public Material Material = null;
    }
    public RayMarchingSettings RayMarchingSetting = new RayMarchingSettings ();
    RayMarchingPass rayMarchingPass;

    public override void Create()
    {
        rayMarchingPass = new RayMarchingPass( RayMarchingSetting );
        rayMarchingPass.renderPassEvent = RayMarchingSetting.RenderPassEvent;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(rayMarchingPass);
    }
    
    class RayMarchingPass : ScriptableRenderPass
    {
        Material rayMarchingMaterial;

        public RayMarchingPass( RayMarchingSettings setting )
        {
            rayMarchingMaterial = setting.Material;
        }
        
        private class PassData
        {
            internal TextureHandle sourceHandle;
            internal TextureHandle tempCopy;
            internal Material material;
        }

        // RecordRenderGraph is where the RenderGraph handle can be accessed, through which render passes can be added to the graph.
        // FrameData is a context container through which URP resources can be accessed and managed.
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            
            if ( rayMarchingMaterial == null )
            {
                Debug.LogError( "rayMarchingMaterial == null" );
                return;
            }
            
            // RasterPass ，UnsafePass ，ComputePass
            using (var builder = renderGraph.AddUnsafePass<PassData>("RayMarchingPass", out var passData))
            {
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData> ();
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData> ();
                
                if ( cameraData.isPreviewCamera )
                {
                    return;
                }

                var desc = cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0;
                desc.colorFormat = RenderTextureFormat.ARGB32;
                
                passData.sourceHandle = resourceData.activeColorTexture;
                passData.material = rayMarchingMaterial;
                passData.tempCopy = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_TempCopy", true, FilterMode.Bilinear);
                // Setup pass inputs and outputs through the builder interface. Eg:
                // TextureHandle destination = UniversalRenderer.CreateRenderGraphTexture(renderGraph, cameraData.cameraTargetDescriptor, "Destination Texture", false);
                // builder.CreateTransientTexture(in TextureDesc desc);
                // で、builderで作成する
                // renderGraph.ImportTexture(RTHandle rt);
                // などのRTHandleから作成する方法もある。外部管理してるRTHandle使う場合などはこちらのパターン
                builder.UseTexture ( passData.sourceHandle, AccessFlags.ReadWrite );
                builder.UseTexture( passData.tempCopy, AccessFlags.ReadWrite );
                
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(false);
                
                // Assigns the ExecutePass function to the render pass delegate. This will be called by the render graph when executing the pass.
                builder.SetRenderFunc((PassData data, UnsafeGraphContext context) =>  ExecutePass(data, context));
            }
        }

        void ExecutePass(PassData data, UnsafeGraphContext context)
        {
            using (new ProfilingScope(context.cmd, profilingSampler))
            {
                var commandBuffer = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

                Blitter.BlitCameraTexture ( commandBuffer, data.sourceHandle, data.tempCopy, data.material, 0 );
                Blitter.BlitCameraTexture ( commandBuffer, data.tempCopy, data.sourceHandle, 0, true);
            }
        }

    }
}

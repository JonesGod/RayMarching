using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
 
public class DrawObjectsWithOverrideMaterial : ScriptableRendererFeature
{
    [SerializeField] private RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
    [SerializeField] private LayerMask layerMask = -1;
    [SerializeField] private RenderQueueType renderQueueType;
    [SerializeField] private List<string> shaderTagList = new() { "UniversalForward" };
    public Material overrideMaterial;
    DrawObjectsPass drawObjectsPass;
 
    public override void Create()
    {
        drawObjectsPass = new DrawObjectsPass(overrideMaterial, layerMask, renderQueueType, shaderTagList);
        drawObjectsPass.renderPassEvent = renderPassEvent;
    }
 
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(drawObjectsPass);
    }

    class DrawObjectsPass : ScriptableRenderPass
    {
        private Material materialToUse;
        LayerMask layerMask;
        RenderQueueType renderQueueType;
        List<ShaderTagId> shaderTagIds = new();

        public DrawObjectsPass(Material overrideMaterial, LayerMask layerMask, RenderQueueType renderQueueType, List<string> shaderTagList)
        {
            this.layerMask = layerMask;
            this.renderQueueType = renderQueueType;
            foreach (var tag in shaderTagList)
            {
                shaderTagIds.Add(new ShaderTagId(tag));
            }
            // Set the pass's local copy of the override material 
            materialToUse = overrideMaterial;
        }
       
        private class PassData
        {
            // Create a field to store the list of objects to draw
            public RendererListHandle skyBoxRendererListHandle;
            public RendererListHandle rendererListHandle;
            internal TextureHandle colorHandle;
            internal TextureHandle depthHandle;
        }
 
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameContext)
        {
            using (var builder = renderGraph.AddUnsafePass<PassData>("Redraw objects", out var passData))
            {
                // Get the data needed to create the list of objects to draw
                UniversalRenderingData renderingData = frameContext.Get<UniversalRenderingData>();
                UniversalCameraData cameraData = frameContext.Get<UniversalCameraData>();
                UniversalLightData lightData = frameContext.Get<UniversalLightData>();
                SortingCriteria sortFlags = (renderQueueType == RenderQueueType.Transparent) ?
                    SortingCriteria.CommonTransparent : cameraData.defaultOpaqueSortFlags;
                RenderQueueRange renderQueueRange = (renderQueueType == RenderQueueType.Transparent) ?
                    RenderQueueRange.transparent : RenderQueueRange.opaque;;
                FilteringSettings filterSettings = new FilteringSettings(renderQueueRange, layerMask);
                
                // Create drawing settings
                DrawingSettings drawSettings = RenderingUtils.CreateDrawingSettings(shaderTagIds, renderingData, cameraData, lightData, sortFlags);
                if ( materialToUse != null )
                {
                    // Add the override material to the drawing settings
                    drawSettings.overrideMaterial = materialToUse;
                }

                // Create the list of objects to draw
                passData.skyBoxRendererListHandle = renderGraph.CreateSkyboxRendererList( cameraData.camera );
                var rendererListParameters = new RendererListParams(renderingData.cullResults, drawSettings, filterSettings);

                // Convert the list to a list handle that the render graph system can use
                passData.rendererListHandle = renderGraph.CreateRendererList(rendererListParameters);
                
                // Set the render target as the color and depth textures of the active camera texture
                UniversalResourceData resourceData = frameContext.Get<UniversalResourceData>();
                passData.colorHandle = resourceData.activeColorTexture;
                passData.depthHandle = resourceData.activeDepthTexture;
                builder.UseRendererList( passData.skyBoxRendererListHandle );
                builder.UseRendererList( passData.rendererListHandle );
                builder.UseTexture( passData.colorHandle, AccessFlags.ReadWrite );
                builder.UseTexture( passData.depthHandle, AccessFlags.ReadWrite );
                builder.SetRenderFunc( ( PassData data, UnsafeGraphContext context ) => ExecutePass( data, context ) );
            }
        }

        static void ExecutePass(PassData data, UnsafeGraphContext context)
        {
            context.cmd.SetRenderTarget(data.colorHandle, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store,
                data.depthHandle, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
            context.cmd.ClearRenderTarget(true, true, Color.black);
            // Draw the objects in the list
            context.cmd.DrawRendererList(data.skyBoxRendererListHandle);
            context.cmd.DrawRendererList(data.rendererListHandle);
        }

    }
 
}

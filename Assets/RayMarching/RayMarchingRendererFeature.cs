using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using Matrix4x4 = UnityEngine.Matrix4x4;
using ProfilingScope = UnityEngine.Rendering.ProfilingScope;
using Vector3 = UnityEngine.Vector3;

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
                Debug.LogError( "RayMarchingMaterial == null" );
                return;
            }
            
            if ( RayMarchingObjsManager.Instance != null )
            {
                RayMarchingObjsManager.Instance.RefreshTransforms();
                rayMarchingMaterial.SetVectorArray( "_Spheres", RayMarchingObjsManager.Instance.Spheres );
            }
            
            // RasterPass ，UnsafePass ，ComputePass
            using (var builder = renderGraph.AddUnsafePass<PassData>("RayMarchingPass", out var passData))
            {
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData> ();
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData> ();
                
                rayMarchingMaterial.SetMatrix( "_CameraRayMatrix", GetCameraFrustum(cameraData.camera ) );
                rayMarchingMaterial.SetMatrix("_CamToWorldMatrix", cameraData.camera.cameraToWorldMatrix);
                
                // if ( cameraData.isPreviewCamera )
                // {
                //     return;
                // }

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

                commandBuffer.Blit ( data.sourceHandle, data.tempCopy );
                commandBuffer.Blit ( data.tempCopy, data.sourceHandle, data.material );
            }
        }

        Matrix4x4 GetCameraFrustum( Camera camera )
        {
            Matrix4x4 matrix = Matrix4x4.identity;

            float tanHalfFOV = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            //ViewSpace
            Vector3 top = Vector3.up * tanHalfFOV;
            Vector3 right = Vector3.right * tanHalfFOV * camera.aspect;
            Vector3 TL = (-Vector3.forward + top - right).normalized;
            Vector3 TR = (-Vector3.forward + top + right).normalized;
            Vector3 BL = (-Vector3.forward - top - right).normalized;
            Vector3 BR = (-Vector3.forward - top + right).normalized;
            
            matrix.SetRow( 0, BL );
            matrix.SetRow( 1, BR );
            matrix.SetRow( 2, TL );
            matrix.SetRow( 3, TR );
            return matrix;
        }

    }
}

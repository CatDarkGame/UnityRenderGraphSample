using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace CatDarkGame.URPRenderPassGuide
{
    public class GrayScaleRenderPass : ScriptableRenderPass
    {
        public enum SubPassMode
        {
            UnsafePass = 0,
            RasterPass,
            FBRasterPass,
        }
        
        private static class ShaderPropertyID
        {
            internal static readonly int BlitTexture = Shader.PropertyToID("_BlitTexture");      
        }
        
        private static class PassID
        {
            internal const int CopyColor = 0;
            internal const int GrayScale = 1;
            internal const int CopyColor_FB = 2;
            internal const int GrayScale_FB = 3;
        }
        
        private class PassData
        {
            internal TextureHandle cameraColorTexture;
            internal TextureHandle copyColorTexture;
            internal Material material;
        }

        private static readonly ProfilingSampler k_ProfilingSampler = new ("GrayScale RenderPass");
        
        public SubPassMode subPassMode { get; set; }
        private RTHandle _copyColorHandle;
        private Material _material;
        
        public GrayScaleRenderPass(Material material, RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing)
        {
            base.renderPassEvent = renderPassEvent;
            _material = material;
        }
        
        public void Dispose()
        {
            _copyColorHandle?.Release();
            _copyColorHandle = null;
            _material = null;
        }

        [Obsolete]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (!_material || !renderingData.cameraData.postProcessEnabled) return;
            
            // Setup Descriptor
            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.width = renderingData.cameraData.cameraTargetDescriptor.width;  
            descriptor.height = renderingData.cameraData.cameraTargetDescriptor.height;
            descriptor.msaaSamples = 1;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            descriptor.depthBufferBits = 0;
            
            // Setup RenderTextureHandle
            RTHandle cameraColorHandle = renderingData.cameraData.renderer.cameraColorTargetHandle;
            RenderingUtils.ReAllocateIfNeeded(ref _copyColorHandle, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: $"{passName}-CopyColor");
            
            // Execute Render
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, k_ProfilingSampler))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                
                cmd.SetGlobalTexture(ShaderPropertyID.BlitTexture, cameraColorHandle);
                cmd.SetRenderTarget(_copyColorHandle, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store); 
                cmd.DrawProcedural(Matrix4x4.identity, _material, PassID.CopyColor, MeshTopology.Triangles, 3, 1);
                
                cmd.SetGlobalTexture(ShaderPropertyID.BlitTexture, _copyColorHandle);
                cmd.SetRenderTarget(cameraColorHandle, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                cmd.DrawProcedural(Matrix4x4.identity, _material, PassID.GrayScale, MeshTopology.Triangles, 3, 1);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourcesData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            if (!_material || !cameraData.postProcessEnabled) return;
            
            // Setup Descriptor
            TextureDesc descriptor = renderGraph.GetTextureDesc(resourcesData.cameraColor);
            descriptor.name = $"{passName}-CopyColor";
            descriptor.clearBuffer = false;
            descriptor.msaaSamples = MSAASamples.None;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            descriptor.depthBufferBits = 0;
            
            // Setup RenderTextureHandle
            TextureHandle cameraColorTexture = resourcesData.cameraColor;
            TextureHandle copyColorTexture = renderGraph.CreateTexture(descriptor);

            // Execute Render
            switch (subPassMode)
            {
                case SubPassMode.UnsafePass :
                    ExecuteUnsafePass(renderGraph, ref cameraColorTexture, ref copyColorTexture);
                    break;
                case SubPassMode.RasterPass :
                    ExecuteRasterPass(renderGraph, ref cameraColorTexture, ref copyColorTexture);
                    break;
                case SubPassMode.FBRasterPass :
                    ExecuteRasterPassFB(renderGraph, ref cameraColorTexture, ref copyColorTexture);
                    break;
            }
        }

        private void ExecuteUnsafePass(RenderGraph renderGraph, ref TextureHandle cameraColorTexture, ref TextureHandle copyColorTexture)
        {
            using (var builder = renderGraph.AddUnsafePass<PassData>($"{passName}", out var passData, k_ProfilingSampler))
            {
                passData.cameraColorTexture = cameraColorTexture;
                passData.copyColorTexture = copyColorTexture;
                passData.material = _material;
                
                builder.UseTexture(passData.cameraColorTexture, AccessFlags.ReadWrite);
                builder.UseTexture(passData.copyColorTexture, AccessFlags.ReadWrite);
                builder.AllowGlobalStateModification(false);
                builder.AllowPassCulling(true);
                builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) =>
                {
                    var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                    
                    cmd.SetRenderTarget(data.copyColorTexture, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                    cmd.SetGlobalTexture(ShaderPropertyID.BlitTexture, data.cameraColorTexture);
                    cmd.DrawProcedural(Matrix4x4.identity, data.material, PassID.CopyColor, MeshTopology.Triangles, 3, 1);
                    
                    cmd.SetRenderTarget(data.cameraColorTexture, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                    cmd.SetGlobalTexture(ShaderPropertyID.BlitTexture, data.copyColorTexture);
                    cmd.DrawProcedural(Matrix4x4.identity, data.material, PassID.GrayScale, MeshTopology.Triangles, 3, 1);
                });
            }
        }

        private void ExecuteRasterPass(RenderGraph renderGraph, ref TextureHandle cameraColorTexture, ref TextureHandle copyColorTexture)
        {
            using (var builder = renderGraph.AddRasterRenderPass<PassData>($"{passName}-CopyColor", out var passData, k_ProfilingSampler))
            {
                passData.cameraColorTexture = cameraColorTexture;
                passData.copyColorTexture = copyColorTexture;
                passData.material = _material;
                
                builder.UseTexture(passData.cameraColorTexture, AccessFlags.Read);
                builder.SetRenderAttachment(passData.copyColorTexture, 0, AccessFlags.WriteAll);
                builder.AllowGlobalStateModification(true);
                builder.AllowPassCulling(true);
                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    var cmd = context.cmd;
                    cmd.SetGlobalTexture(ShaderPropertyID.BlitTexture, data.cameraColorTexture);
                    cmd.DrawProcedural(Matrix4x4.identity, data.material, PassID.CopyColor, MeshTopology.Triangles, 3, 1);
                });
            }
           
            using (var builder = renderGraph.AddRasterRenderPass<PassData>($"{passName}-GrayScale", out var passData, k_ProfilingSampler))
            {
                passData.cameraColorTexture = cameraColorTexture;
                passData.copyColorTexture = copyColorTexture;
                passData.material = _material;
                
                builder.UseTexture(passData.copyColorTexture, AccessFlags.Read);
                builder.SetRenderAttachment(passData.cameraColorTexture, 0, AccessFlags.WriteAll);
                builder.AllowGlobalStateModification(true);
                builder.AllowPassCulling(true);
                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    var cmd = context.cmd;
                    cmd.SetGlobalTexture(ShaderPropertyID.BlitTexture, data.copyColorTexture);  
                    cmd.DrawProcedural(Matrix4x4.identity, data.material, PassID.GrayScale, MeshTopology.Triangles, 3, 1);
                });
            }
        }
        
        private void ExecuteRasterPassFB(RenderGraph renderGraph, ref TextureHandle cameraColorTexture, ref TextureHandle copyColorTexture)
        {
            using (var builder = renderGraph.AddRasterRenderPass<PassData>($"FB {passName}-CopyColor", out var passData, k_ProfilingSampler))
            {
                passData.cameraColorTexture = cameraColorTexture;
                passData.copyColorTexture = copyColorTexture;
                passData.material = _material;
                
                builder.SetInputAttachment(passData.cameraColorTexture, 0, AccessFlags.Read);
                builder.SetRenderAttachment(passData.copyColorTexture, 0, AccessFlags.WriteAll);
                builder.AllowGlobalStateModification(false);
                builder.AllowPassCulling(true);
                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    var cmd = context.cmd;
                    cmd.DrawProcedural(Matrix4x4.identity, data.material, PassID.CopyColor_FB, MeshTopology.Triangles, 3, 1);
                });
            }
           
            using (var builder = renderGraph.AddRasterRenderPass<PassData>($"FB {passName}-GrayScale", out var passData, k_ProfilingSampler))
            {
                passData.cameraColorTexture = cameraColorTexture;
                passData.copyColorTexture = copyColorTexture;
                passData.material = _material;
                
                builder.SetInputAttachment(passData.copyColorTexture, 0, AccessFlags.Read);
                builder.SetRenderAttachment(passData.cameraColorTexture, 0, AccessFlags.WriteAll);
                builder.AllowGlobalStateModification(false);
                builder.AllowPassCulling(true);
                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    var cmd = context.cmd;
                    cmd.DrawProcedural(Matrix4x4.identity, data.material, PassID.GrayScale_FB, MeshTopology.Triangles, 3, 1);
                });
            }
        }
    }
}




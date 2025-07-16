using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CatDarkGame.URPRenderPassGuide
{
    public class GrayScaleRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        [SerializeField] private Shader shader;
        [SerializeField] private GrayScaleRenderPass.SubPassMode subPassMode = GrayScaleRenderPass.SubPassMode.UnsafePass;
        private GrayScaleRenderPass _renderPass;
        private Material _material;
        
        public override void Create()
        {
            if (shader) _material = CoreUtils.CreateEngineMaterial(shader);
            if (!_material) return;
            _renderPass = new GrayScaleRenderPass(_material, renderPassEvent);
        }
        
        protected override void Dispose(bool disposing)
        {
            if(_material) CoreUtils.Destroy(_material);
            _material = null;
            _renderPass?.Dispose();
            _renderPass = null;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_renderPass == null) return;
            if (renderingData.cameraData.cameraType == CameraType.Preview || 
                renderingData.cameraData.cameraType == CameraType.Reflection)
                return;

            _renderPass.subPassMode = subPassMode;
            renderer.EnqueuePass(_renderPass);
        }
    }
}
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace GeometricFlight.Presentation
{
    /// <summary>URP renderer feature for the screen effects (menu glitch, arcade mode's fringing, CRT and vintage
    /// extras). Runs after post-processing, and only for the camera registered with <see cref="ScreenEffects"/>
    /// while an effect is on, so other cameras and the Scene view are untouched. The UI draws afterwards and is
    /// never affected.</summary>
    public sealed class ScreenEffectsFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader shader;

        private Material material;
        private Pass pass;

        public override void Create()
        {
            pass = new Pass
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing,
                requiresIntermediateTexture = true
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!ScreenEffects.ShouldRender(renderingData.cameraData.camera)) return;
            if (material == null)
            {
                if (shader == null) return;
                material = CoreUtils.CreateEngineMaterial(shader);
            }
            pass.Material = material;
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            material = null;
        }

        private sealed class Pass : ScriptableRenderPass
        {
            public Material Material;

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;

                TextureHandle source = resources.activeColorTexture;
                TextureDesc desc = renderGraph.GetTextureDesc(source);
                desc.name = "_ScreenEffects";
                desc.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(desc);
                renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, destination, Material, 0), "Screen Effects");
                resources.cameraColor = destination;
            }
        }
    }
}

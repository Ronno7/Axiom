using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace GeometricFlight.Presentation
{
    /// <summary>URP renderer feature for the menu's screen effects (glitch, palette fringing, CRT). Runs after
    /// post-processing, and only for the camera <see cref="MenuLook"/> registers while an effect is on, so gameplay,
    /// the title camera and the Scene view are untouched. The UI draws afterwards and is never affected.</summary>
    public sealed class MenuScreenFeature : ScriptableRendererFeature
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
            if (!MenuScreen.ShouldRender(renderingData.cameraData.camera)) return;
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
                desc.name = "_MenuScreen";
                desc.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(desc);
                renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, destination, Material, 0), "Menu Screen");
                resources.cameraColor = destination;
            }
        }
    }
}

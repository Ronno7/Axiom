using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace GeometricFlight.Presentation
{
    /// <summary>URP renderer feature for the screen effects (menu glitch, arcade mode's fringing, CRT and vintage
    /// extras). Runs after post-processing, and only for the camera registered with <see cref="ScreenEffects"/>
    /// while an effect is on, so other cameras and the Scene view are untouched. Normally the UI draws afterwards
    /// and is never affected; in Arcade Mode a second pass first lays the interface (rendered into its own texture
    /// by <see cref="ArcadeInterface"/>) into the post-processed picture with a phosphor glow, so the screen
    /// effects bend, scan and fringe it with everything else. It is added after URP's bloom and grade on purpose:
    /// those washed out text and shifted colour cues.</summary>
    public sealed class ScreenEffectsFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader shader;

        private Material material;
        private Pass effects, ui;

        public override void Create()
        {
            effects = new Pass(0, "Screen Effects")
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing,
                requiresIntermediateTexture = true
            };
            // Same event as the effects; enqueued first, so it runs first.
            ui = new Pass(1, "Screen Interface")
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing,
                requiresIntermediateTexture = true
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;
            bool composite = ScreenEffects.ShouldComposite(camera);
            bool render = ScreenEffects.ShouldRender(camera);
            if (!composite && !render) return;
            if (material == null)
            {
                if (shader == null) return;
                material = CoreUtils.CreateEngineMaterial(shader);
            }
            if (composite)
            {
                ui.Material = material;
                renderer.EnqueuePass(ui);
            }
            if (render)
            {
                effects.Material = material;
                renderer.EnqueuePass(effects);
            }
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            material = null;
        }

        /// <summary>Blits the camera colour through one shader pass into a new texture and makes that the camera colour.</summary>
        private sealed class Pass : ScriptableRenderPass
        {
            public Material Material;
            private readonly int shaderPass;
            private readonly string label;

            public Pass(int shaderPass, string label)
            {
                this.shaderPass = shaderPass;
                this.label = label;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;

                TextureHandle source = resources.activeColorTexture;
                TextureDesc desc = renderGraph.GetTextureDesc(source);
                desc.name = "_" + label.Replace(" ", "");
                desc.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(desc);
                renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, destination, Material, shaderPass), label);
                resources.cameraColor = destination;
            }
        }
    }
}

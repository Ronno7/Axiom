using GeometricFlight.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GeometricFlight.Tests
{
    public sealed class PaletteTests
    {
        [TestCase("_PlainColor", 0)]
        [TestCase("_HdrColor", 1)]
        [TestCase("_GammaColor", 2)]
        [TestCase("_GammaVector", 3)]
        public void MaterialPaletteColorReachesTheGpuWithoutDoubleConversion(string property, int mode)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null ||
                !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat) ||
                !SystemInfo.SupportsTextureFormat(TextureFormat.RGBAFloat))
                Assert.Ignore("Palette upload regression requires a graphics device with float render/readback support.");

            // Material.GetVector only reports stored values. A GPU readback is necessary: plain Color
            // and [Gamma] properties convert on upload even when assigned with Material.SetVector.
            const string source = @"Shader ""Hidden/Axiom/PaletteUploadTest""
            {
                Properties
                {
                    _PlainColor (""Plain"", Color) = (1,1,1,1)
                    [HDR] _HdrColor (""HDR"", Color) = (1,1,1,1)
                    [Gamma] _GammaColor (""Gamma color"", Color) = (1,1,1,1)
                    [Gamma] _GammaVector (""Gamma vector"", Vector) = (1,1,1,1)
                }
                SubShader
                {
                    Pass
                    {
                        ZTest Always Cull Off ZWrite Off
                        CGPROGRAM
                        #pragma vertex vert_img
                        #pragma fragment frag
                        #include ""UnityCG.cginc""
                        float4 _PlainColor, _HdrColor, _GammaColor, _GammaVector;
                        float _Mode;
                        float4 frag(v2f_img input) : SV_Target
                        {
                            if (_Mode < 0.5) return _PlainColor;
                            if (_Mode < 1.5) return _HdrColor;
                            if (_Mode < 2.5) return _GammaColor;
                            return _GammaVector;
                        }
                        ENDCG
                    }
                }
            }";

            Shader shader = null;
            Material material = null;
            RenderTexture target = null;
            Texture2D readback = null;
            RenderTexture previousTarget = RenderTexture.active;
            bool previousSrgbWrite = GL.sRGBWrite;
            try
            {
                shader = ShaderUtil.CreateShaderAsset(source);
                shader.hideFlags = HideFlags.HideAndDontSave;
                Assert.That(shader.isSupported, Is.True, "Palette probe shader must compile on the active device.");
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                target = new RenderTexture(8, 8, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear)
                    { hideFlags = HideFlags.HideAndDontSave };
                Assert.That(target.Create(), Is.True);
                readback = new Texture2D(8, 8, TextureFormat.RGBAFloat, false, true)
                    { hideFlags = HideFlags.HideAndDontSave };

                const float intensity = 1.3f;
                const float alpha = 0.37f;
                Color authored = AxiomPalette.Orange;
                material.SetVector(property, AxiomPalette.MaterialColor(material, property, authored, intensity, alpha));
                material.SetFloat("_Mode", mode);
                GL.sRGBWrite = false;
                Graphics.Blit(Texture2D.whiteTexture, target, material);
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, 8, 8), 0, 0);
                readback.Apply(false, false);
                Color actual = readback.GetPixel(4, 4);

                // Independent expected values: do not derive the assertion from either palette helper.
                bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
                float red = linear ? Mathf.GammaToLinearSpace(authored.r) : authored.r;
                float green = linear ? Mathf.GammaToLinearSpace(authored.g) : authored.g;
                float blue = linear ? Mathf.GammaToLinearSpace(authored.b) : authored.b;
                Assert.That(actual.r, Is.EqualTo(red * intensity).Within(0.002f), property + " GPU red");
                Assert.That(actual.g, Is.EqualTo(green * intensity).Within(0.002f), property + " GPU green");
                Assert.That(actual.b, Is.EqualTo(blue * intensity).Within(0.002f), property + " GPU blue");
                Assert.That(actual.a, Is.EqualTo(alpha).Within(0.0001f), "Emission must not scale alpha.");
            }
            finally
            {
                RenderTexture.active = previousTarget;
                GL.sRGBWrite = previousSrgbWrite;
                if (target != null) target.Release();
                Object.DestroyImmediate(readback);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(shader);
            }
        }

        [Test]
        public void RawHdrShaderColorMatchesUnityColorConversionWithoutScalingAlpha()
        {
            var block = new MaterialPropertyBlock();
            int ldrId = Shader.PropertyToID("_PaletteLdrTest");
            int hdrId = Shader.PropertyToID("_PaletteHdrTest");
            // Unity's LDR SetColor is the independent reference for the active project's color space.
            // HDR takes SetVector so conversion happens once and its boost never changes opacity.
            block.SetColor(ldrId, AxiomPalette.Cyan);
            block.SetVector(hdrId, AxiomPalette.ShaderColor(AxiomPalette.Cyan, 3.2f, 0.37f));
            Vector4 ldr = block.GetVector(ldrId);
            Vector4 hdr = block.GetVector(hdrId);
            Assert.That(hdr.x, Is.EqualTo(ldr.x * 3.2f).Within(0.0001f));
            Assert.That(hdr.y, Is.EqualTo(ldr.y * 3.2f).Within(0.0001f));
            Assert.That(hdr.z, Is.EqualTo(ldr.z * 3.2f).Within(0.0001f));
            Assert.That(hdr.z, Is.GreaterThan(1f), "HDR must not be clamped to UI range");
            Assert.That(hdr.w, Is.EqualTo(0.37f).Within(0.00001f));
        }

        [Test]
        public void FadingAnHdrPaletteColorDoesNotDimItsRgbOrClampIt()
        {
            Color bright = AxiomPalette.ShaderColor(AxiomPalette.Orange, 6f);
            Color faded = AxiomPalette.WithAlpha(bright, 0.12f);
            Assert.That(faded.r, Is.EqualTo(bright.r));
            Assert.That(faded.g, Is.EqualTo(bright.g));
            Assert.That(faded.b, Is.EqualTo(bright.b));
            Assert.That(faded.r, Is.GreaterThan(1f));
            Assert.That(faded.a, Is.EqualTo(0.12f));
            Assert.That(bright.a, Is.EqualTo(1f), "input is a value, not mutable shared state");
        }
    }
}

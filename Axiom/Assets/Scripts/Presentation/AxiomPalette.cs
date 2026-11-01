// Generated from ArtSource/Palette/palette.json. Run generate_palette.py; do not edit here.
using UnityEngine;
using UnityEngine.Rendering;

namespace GeometricFlight.Presentation
{
    /// <summary>Axiom's authored sRGB palette. Alpha and emission are separate from hue.</summary>
    public static class AxiomPalette
    {
        /// <summary>#060A0C: Sky, recesses and scrims.</summary>
        public static readonly Color Void = new Color32(6, 10, 12, 255);
        /// <summary>#0C141F: Dark architecture and craft bodies.</summary>
        public static readonly Color Structure = new Color32(12, 20, 31, 255);
        /// <summary>#102931: Raised panels and secondary planes.</summary>
        public static readonly Color Panel = new Color32(16, 41, 49, 255);
        /// <summary>#3A5362: Quiet outlines and inactive tracks.</summary>
        public static readonly Color Edge = new Color32(58, 83, 98, 255);
        /// <summary>#8198A6: Secondary readable text and labels.</summary>
        public static readonly Color Muted = new Color32(129, 152, 166, 255);
        /// <summary>#B7C9D1: Body text and neutral outlines.</summary>
        public static readonly Color Text = new Color32(183, 201, 209, 255);
        /// <summary>#E6FFFF: Title, focus fill and player core.</summary>
        public static readonly Color Light = new Color32(230, 255, 255, 255);
        /// <summary>#6FC3DF: Player systems, shield and cool energy.</summary>
        public static readonly Color Cyan = new Color32(111, 195, 223, 255);
        /// <summary>#DF740C: Program energy and selection accent.</summary>
        public static readonly Color Orange = new Color32(223, 116, 12, 255);
        /// <summary>#FF410D: Fault, damage and attack escalation.</summary>
        public static readonly Color Ember = new Color32(255, 65, 13, 255);
        /// <summary>#FFE64D: Reserved boss and authority signature.</summary>
        public static readonly Color Gold = new Color32(255, 230, 77, 255);
        /// <summary>#FFF2D9: Warm impact core and warning text.</summary>
        public static readonly Color Hot = new Color32(255, 242, 217, 255);
        /// <summary>#000000: Mask, shade and empty-data endpoint.</summary>
        public static readonly Color Black = new Color32(0, 0, 0, 255);
        /// <summary>#FFFFFF: Neutral multiplier and light endpoint.</summary>
        public static readonly Color White = new Color32(255, 255, 255, 255);
        public static readonly Color Clear = new Color32(0, 0, 0, 0);

        /// <summary>Preserves the supplied RGB, including HDR values; changes alpha only.</summary>
        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        /// <summary>For raw shader-color inputs: converts authored sRGB once, then scales RGB only.
        /// UI colors and Unity properties that already convert sRGB must use the tokens directly.</summary>
        public static Color ShaderColor(Color srgb, float intensity = 1f, float alpha = 1f)
        {
            Color color = QualitySettings.activeColorSpace == ColorSpace.Linear ? srgb.linear : srgb;
            return new Color(color.r * intensity, color.g * intensity, color.b * intensity, alpha);
        }

        /// <summary>Value for Material.SetVector, accounting for the property's GPU upload encoding.
        /// Plain Color and Gamma properties store sRGB; HDR Color and raw vectors store linear.
        /// This differs from MaterialPropertyBlock.SetVector, which always takes raw shader values.</summary>
        public static Color MaterialColor(Material material, string property, Color srgb, float intensity = 1f, float alpha = 1f)
        {
            Color color = ShaderColor(srgb, intensity, alpha);
            int index = material.shader.FindPropertyIndex(property);
            if (index < 0 || QualitySettings.activeColorSpace != ColorSpace.Linear) return color;
            ShaderPropertyFlags flags = material.shader.GetPropertyFlags(index);
            bool gammaUpload = (flags & ShaderPropertyFlags.Gamma) != 0
                || (material.shader.GetPropertyType(index) == ShaderPropertyType.Color
                    && (flags & ShaderPropertyFlags.HDR) == 0);
            return gammaUpload ? WithAlpha(color.gamma, alpha) : color;
        }
    }
}

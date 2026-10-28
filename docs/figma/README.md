# Axiom UI in Figma

`index.html` rebuilds the game UI (`Axiom/Assets/UI/*.uxml` + `Axiom.uss`) in HTML/CSS so it can be pulled into Figma as editable layers: real text, frames and vector buttons, not screenshots.

It has eight 1920×1080 screens (main menu, options, help, calibration, HUD, HUD alerts, paused, results) and a components sheet with colours, the type scale, button states, toggles, slider, throttle and HUD tags.

## Import (SVG, no plugins)

`svg/` holds one SVG per screen. Drag them onto a Figma canvas. Each comes in as a frame with editable text layers, rectangles and vector button outlines, grouped like the UXML hierarchy.

To regenerate them after editing `index.html`, open `index.html#svg` in any browser and use the download bar in the top right. The export is done by `svg-export.js`, which reads positions from the rendered page.

Tip: in Figma, select an imported group and press **Ctrl+Alt+G** (Frame selection), or right-click and choose **Ungroup**, to get frames you can turn into auto layout.

## Import (html.to.design plugin)

Alternatively, the free **html.to.design** plugin (by ‹div›RIOTS) imports the HTML directly, with auto-layout:

1. Install the **html.to.design** Chrome extension and the Figma plugin of the same name.
2. Open `docs/figma/index.html` in Chrome, click the extension and choose **Import to Figma**. It downloads a `.h2d` file.
3. In Figma, run **Plugins › html.to.design**, open the **File** tab and drop in the `.h2d` file.
4. Set the import viewport to **2080** wide so the 1920 frames aren't squeezed.

If the page is hosted (GitHub Pages serves `docs/`), you can paste its URL into the plugin's **URL** tab instead.

## Notes

- Font is **Inter**, which is Unity's default runtime font and comes with Figma.
- Backgrounds are flat stand-ins for the 3D scene. The **AXIOM** title is a placeholder for the 3D title render texture.
- SlantButton outlines use the same path as `SlantButton.Draw` (lean 18, point 22, stroke 2).
- This is a one-way copy. Changes made in Figma have to go back into `Axiom.uss` / UXML by hand. If the USS changes, update this page to match.

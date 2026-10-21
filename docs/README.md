# UI showcase page

The page at <https://ronno7.github.io/Axiom/>, served by GitHub Pages from this folder
(repository Settings → Pages → Deploy from a branch → `main`, `/docs`). Open `index.html` in a browser to preview.

## Updating it

- **Screenshots** live in `img/` as 1600×900 JPGs. Replace a file with the same name and the page picks it up.
- **Text** is plain HTML in `index.html`. Change the "Updated" date and build number in the header when you do.
- **Interactive parts** are HTML with data attributes; `showcase.js` only adds the behaviour:

| Part | Edit |
| --- | --- |
| Before/after slider | `data-sets='[["Tab name","before.jpg","after.jpg"]]'`, labels in `data-labels` |
| Layer stepper | One `<li data-img="...">` per step |
| Hotspot picture | `<li class="item" data-kind="..." data-rect="left,top,width,height">` in percent of the image; no `data-rect` = listed only |
| Cockpit LCD | One `<button data-top="..." data-bottom="...">` per state, 16 characters per row |
| Glitch chart | `data-frames` on the `svg.glitch` element |
| Screen flow | The Mermaid text inside `<pre class="mermaid">` ([syntax](https://mermaid.js.org/syntax/flowchart.html)) |

To find a hotspot's rectangle, open the screenshot in an image editor and divide the pixel box by 1600 (x) and 900 (y).

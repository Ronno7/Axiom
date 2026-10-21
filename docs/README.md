# UI showcase

A static, image-led tour of Axiom's interface, intended for GitHub Pages at
<https://ronno7.github.io/Axiom/>. No build step or JavaScript dependencies.

Open `index.html` locally, or run `python -m http.server 8765 --bind 127.0.0.1 --directory docs`
from the repository root and visit <http://127.0.0.1:8765/>. Publishing is a separate step.

## Editing

Keep headings and captions short. Put implementation detail in the expandable notes and let the
screenshots lead. Update the footer date when refreshing the page.

| Part | Where to edit |
| --- | --- |
| Copy, section order, links | `index.html` |
| Typography, layout, responsive rules | `showcase.css` |
| Screen and feedback galleries | Buttons with `data-img` and `data-caption` |
| Standard / Arcade comparisons | `data-sets` on `.compare`, with labels in `data-labels` |
| Layer sequence | One `<li data-img="...">` per step |
| Annotations | `.item` with `data-kind` and `data-rect="left,top,width,height"`, in percentages |
| Cockpit LCD | Buttons with `data-top` and `data-bottom`, exactly 16 characters per row |
| Screen flow | Plain HTML inside the expandable Screen flow section |
| Viewer and interactions | `showcase.js` |

Click a gallery image or an annotated image to enlarge it. Comparisons have a separate Expand button
so dragging the split stays unambiguous. The viewer provides previous/next, Fit image / Actual size,
and close controls; arrow keys move between gallery images and Escape closes the native dialog.
Actual-size images can be scrolled or dragged. Expanded comparisons keep their slider position when closed.

## Captures

Current game images in `img/` are 2560 × 1440 JPGs, captured from Unity's Game view in Play Mode.
The historical images remain intentionally older; the cockpit illustration is a concept render.
Replace an existing filename to update all its uses. Annotation coordinates are percentages of the
source image: divide the pixel box by the actual width and height, then multiply by 100.

Refresh the current menu, Options, glitch, render-layer sequence, flight, pause, hit/proximity feedback,
and both Arcade comparisons together when the game's presentation changes. Preserve the archive.
Capture Standard and Arcade from the same frozen camera and geometry; the evolution comparison uses
different camera positions and is labelled accordingly.

For captures, let the Game view finish rendering after each state change. If the editor is unfocused,
frames may stop updating; a temporary Play Mode run-in-background setting can help. Restore runtime
settings and leave Play Mode afterwards. Do not save staged scenes or change saved player preferences.
The layer sequence removes interface, bloom, grade and atmosphere progressively at runtime only.

The feedback images are staged UI examples (including a real shield hit held for capture), not evidence
of an end-to-end playthrough. Add the results panel from a natural completed/failed run with meaningful
stats. Keep planned cockpit effects distinguished from working integration.

## Verification

The refresh passed 22 DOM interaction checks covering galleries, viewer navigation/zoom/focus restoration,
comparison sliders, layers, annotations and LCD states. JavaScript syntax and asset references were checked.
DOM tests simulate the dialog API; they do not verify browser rendering or native modal behavior.

Layout was then checked in headless Chrome at 320, 390, 768 and 1440 px: no horizontal page overflow (only the
feedback table scrolls inside its own box) and no console errors. Still to check in a real browser before
publishing: dialog focus and Escape, image zoom and panning, touch comparisons, reduced motion, and fonts unavailable.

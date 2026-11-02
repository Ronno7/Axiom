# UI showcase

A static, image-led tour of Axiom's interface, served by GitHub Pages at
<https://ronno7.github.io/Axiom/>. No build step or JavaScript dependencies.

Open `index.html` locally, or run `python -m http.server 8765 --bind 127.0.0.1 --directory docs`
from the repository root and visit <http://127.0.0.1:8765/>. Pages serves this folder from the `dev` branch, so
pushing `dev` publishes it.

## Editing

Keep headings and captions short. Put implementation detail in the expandable notes and let the
screenshots lead. Update the footer date when refreshing the page.

The page runs in six numbered sections: Opening, In play, Look, Cockpit, Ship and History. Each section's `id`,
eyebrow (`01 / Opening`) and masthead link share one name. The masthead marks the section in view, and each section
ends with a "Next" link generated from the masthead, so adding a section means adding a `<section id>` and a matching
masthead link.

| Part | Where to edit |
| --- | --- |
| Copy, section order, links | `index.html` |
| Typography, layout, responsive rules | `showcase.css` |
| Route map | Buttons in `.flow-map` with `data-img` and `data-caption`; a `node-*` class places each one on the map |
| Standard / CRT comparisons | `data-sets` on `.compare`, with labels in `data-labels` |
| Layer and history steppers | One `<li data-img="...">` per step; `data-start` picks the opening step |
| Annotations | `.item` with `data-kind` and `data-rect="left,top,width,height"`, in percentages |
| Implemented / Planned | `.legend` with `.on` / `.off` markers; use the same two words everywhere |
| Viewer, navigation and interactions | `showcase.js` |

Click a gallery image or an annotated image to enlarge it. Comparisons have a separate Expand button
so dragging the split stays unambiguous. The viewer provides previous/next, Fit image / Actual size,
and close controls; arrow keys move between gallery images and Escape closes the native dialog.
Actual-size images can be scrolled or dragged. Expanded comparisons keep their slider position when closed.

## Captures

Game images in `img/` are 2560 × 1440 JPGs, captured from Unity's Game view in Play Mode with Standard display unless
named `-crt`. The cockpit illustration is a concept render, and the ship images are Cycles studio renders copied from
the [ship package](../Axiom/ArtSource/Ships/SweptDart/README.md). `history-*` images stay intentionally older, in
chronological order. Replace an existing filename to update all its uses. Annotation coordinates are percentages of
the source image.

Refresh the menu, Options, entry, rail, arena, pause, results, layers and both CRT comparisons together when the
game's presentation changes. Move the outgoing menu into the history stepper first. Capture Standard and CRT from the
same frozen camera and restore saved settings afterwards.

Films are rendered rather than screen recorded: `Time.captureFramerate = 60` with a screenshot saved each frame, encoded
with ffmpeg at 1920 × 1080 (the opening) or 1280 × 720 (the `clip-*` loops). They have no audio. The README's `Axiom/Docs/images/menu.gif` shows only the
menu, on purpose: the opening stays a surprise until someone chooses to watch it here.

The flight images come from a scripted run with a simulated cockpit, which hides the port name on the status line.
The shield hit is triggered for the capture, and the autopilot's accuracy on the results screen isn't a player's.

## Verification

Check layout at phone, tablet and desktop widths after a change, and the viewer, comparisons, steppers and annotations
in a real browser before publishing.

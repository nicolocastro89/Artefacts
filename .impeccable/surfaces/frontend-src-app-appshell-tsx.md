---
version: 1
slug: "frontend-src-app-appshell-tsx"
primary_target: "frontend/src/app/AppShell.tsx"
related_targets: ["frontend/src/pages/ArtefactListPage.tsx"]
---

# Surface: app shell + artefact list

Scope: the application shell (navigation, page frame) and the artefact list, the primary daily job (browse, search, sort, recognise, open). Mode: Operate. The detail, form, region and print screens inherit this world.

Audience/job: a small team managing a private art collection, at desks on large screens for long sessions, sometimes on phones. Must not feel precious: no gallery luxury, no wasted space.

## Direction contract

THESIS: The collection as it is really kept: racked, tagged, and pulled out when you need to look. It refuses both the white-sidebar blue-button admin template and the precious white-wall gallery.

OWN-WORLD: Powder-coated steel rail and a galvanised cool-grey ground; records sit on white plates. One enamel-blue action colour, only on things you can press. Rack-label yellow means "selected" and nothing else. Archivo throughout; its condensed width sets inventory tags, column labels and counts; numerals are tabular.

STORY: Staff scan hundreds of artefacts, recognise each by its fixed-scale thumbnail and inventory tag, pull one out to check it, then open or edit the record.

FIRST VIEWPORT: Steel rail on the left (Artefacts, Regions, with counts). The work area has the title and record count, search, a Table/Grid switch, and the primary "New artefact" action. Below that, a dense table with fixed-scale thumbnails. Selecting a row slides a rack panel out from the right, holding the image large under its yellow tie-on inventory tag, with key facts and Open/Edit.

FORM: Storage Rack, position 6 of 7 on the grounded list; seed:50cedb360ba4305e. Signature interaction: the pull-out rack (slides out from the right; arrow keys walk the racks; Esc slides it back). When the list re-sorts, rows travel to their new place.

FINISH: unreviewed and undocumented is unfinished; this build ends with the finish review, the verdict, DESIGN.md, and every shipping raster carrying its provenance

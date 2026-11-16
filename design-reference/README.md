# UI reference

`ui-reverse/` preserves the user-supplied UI mirror source. Its Vue/Gridsome build
is a reference, not the frontend runtime. Dependencies are not duplicated here;
the original package and lock files are retained.

React implementation and generated public content/media manifests live in
`Frontend/src/shared/brand/`. Image/video cache files are ignored by Git and served
from Azure after importing. See `docs/REACT_UI_MIGRATION.md` for routes, import
commands and verification details.

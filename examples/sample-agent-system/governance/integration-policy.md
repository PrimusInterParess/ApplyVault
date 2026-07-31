# Integration Policy — Sample Catalog API

1. Architect reconciles `READY` handoffs against the working tree, then
   integrates before declaring request complete. `STALE` handoffs are not
   integrated.
2. Backend changes must stay within delegated scope and approved/proposed contracts.
3. Parallel work is allowed only when paths and contracts do not conflict.
4. Incomplete validation yields `REQUEST COMPLETE WITH DOCUMENTED LIMITATIONS` or `REQUEST BLOCKED`.
5. Do not merge conflicting API shapes; escalate to conflict-resolution.
6. After integrate/approve, run Close: archive summary, delete
   `scratch/<task-id>/`, clear `handoffs/active/<task-id>/`. Cleanup is part of
   COMPLETE.

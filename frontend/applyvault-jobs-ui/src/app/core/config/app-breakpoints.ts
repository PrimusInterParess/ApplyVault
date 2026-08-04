/**
 * Viewport breakpoints mirrored from CSS `--app-bp-*` / SCSS `$app-bp-*`.
 * Use in matchMedia / layout JS so behavior stays aligned with feature SCSS.
 */
export const APP_BP_COMPACT_PX = 1080;
export const APP_BP_PHONE_PX = 640;

/** matchMedia query for list/detail compact workspace (≤ `--app-bp-compact`). */
export const APP_BP_COMPACT_MAX_MQ = `(max-width: ${APP_BP_COMPACT_PX}px)`;

/** matchMedia query for phone chrome (≤ `--app-bp-phone`). */
export const APP_BP_PHONE_MAX_MQ = `(max-width: ${APP_BP_PHONE_PX}px)`;

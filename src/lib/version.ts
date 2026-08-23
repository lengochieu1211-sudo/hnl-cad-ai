/** Canonical runtime version for renderer/server labels.
 * Package/build manifests are checked against this value by scripts/check-version-sync.mjs.
 */
export const HNL_APP_VERSION = "2.7.5" as const;
export const HNL_DISPLAY_VERSION = `v${HNL_APP_VERSION}` as const;
export const HNL_PROJECT_SCHEMA_VERSION = 2 as const;

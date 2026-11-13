/// <reference types="vite/client" />
import { manifests as entrypoints } from "./entrypoints/manifest.js";
import { manifests as dashboards } from "./dashboards/manifest.js";

// Optional add-ons live in src/demo/. Nothing imports it; the glob is resolved when vite builds, so with
// the folder deleted it matches nothing and the bundle builds without it.
const demo = Object.values(
  import.meta.glob<{ manifests: Array<UmbExtensionManifest> }>("./demo/manifest.ts", { eager: true }),
).flatMap((m) => m.manifests);

// Job of the bundle is to collate all the manifests from different parts of the extension and load other manifests
// We load this bundle from umbraco-package.json
export const manifests: Array<UmbExtensionManifest> = [
  ...entrypoints,
  ...dashboards,
  ...demo,
];

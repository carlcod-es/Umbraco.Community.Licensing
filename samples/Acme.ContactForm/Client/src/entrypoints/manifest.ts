export const manifests: Array<UmbExtensionManifest> = [
  {
    name: "Acme Contact Form Entrypoint",
    alias: "Acme.ContactForm.Entrypoint",
    type: "backofficeEntryPoint",
    js: () => import("./entrypoint.js"),
  },
];

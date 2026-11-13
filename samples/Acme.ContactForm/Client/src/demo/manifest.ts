import type { ManifestElement } from "@umbraco-cms/backoffice/extension-api";

// The licensing dashboard renders every extension of this type in its panel slot.
interface ManifestAcmeContactFormLicensePanel extends ManifestElement {
  type: "acmeContactFormLicensePanel";
}

declare global {
  interface UmbExtensionManifestMap {
    ManifestAcmeContactFormLicensePanel: ManifestAcmeContactFormLicensePanel;
  }
}

export const manifests: Array<UmbExtensionManifest> = [
  {
    type: "acmeContactFormLicensePanel",
    alias: "Acme.ContactForm.Demo.Picker",
    name: "Acme Contact Form Demo Key Picker",
    element: () => import("./demo-picker.element.js"),
  },
];

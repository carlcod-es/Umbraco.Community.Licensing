export const manifests: Array<UmbExtensionManifest> = [
  {
    name: "Acme Contact Form Dashboard",
    alias: "Acme.ContactForm.Dashboard",
    type: "dashboard",
    js: () => import("./dashboard.element.js"),
    meta: {
      label: "Example Dashboard",
      pathname: "example-dashboard",
    },
    conditions: [
      {
        alias: "Umb.Condition.SectionAlias",
        match: "Umb.Section.Content",
      },
    ],
  },
];

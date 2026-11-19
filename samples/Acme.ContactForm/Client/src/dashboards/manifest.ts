export const manifests: Array<UmbExtensionManifest> = [
  {
    name: "Acme Contact Form License Dashboard",
    alias: "Acme.ContactForm.Dashboard.License",
    type: "dashboard",
    js: () => import("./license-dashboard.element.js"),
    weight: 100,
    meta: {
      label: "Contact form license",
      pathname: "contact-form-license",
    },
    conditions: [
      {
        alias: "Umb.Condition.SectionAlias",
        match: "Umb.Section.Settings",
      },
    ],
  },
  {
    name: "Acme Contact Form Submissions Dashboard",
    alias: "Acme.ContactForm.Dashboard.Submissions",
    type: "dashboard",
    js: () => import("./submissions-dashboard.element.js"),
    weight: 100,
    meta: {
      label: "Contact form submissions",
      pathname: "contact-form-submissions",
    },
    conditions: [
      {
        alias: "Umb.Condition.SectionAlias",
        match: "Umb.Section.Content",
      },
    ],
  },
];

# ADR-0027: Sample backoffice API and client

- Status: Decided
- Date: 2026-11-13
- Source: `add-sample-package` design.md Q21

## Context

PDR-0068 gives administrators a Settings dashboard and editors a banner in a Content-section
screen; PDR-0069 adds a picker for Settings users when the demo setting is on. Umbraco 17's
backoffice is a web-component client calling the Management API. Vendors start packages from
the `umbraco-extension` template.

## Decision

- **Management API controllers** in the package, Umbraco 17 style (`[BackOfficeRoute]`,
  `[MapToApi("acme-contact-form")]`), with their own OpenAPI document. Authorization by
  Umbraco's section policies: Settings access for `license/status` and the demo endpoints,
  Content access for `submissions`. The demo endpoints answer 404 when the demo setting is off.
- **Client** from the 17 template: Lit, Vite and TypeScript, built to
  `wwwroot/App_Plugins/...` (gitignored, built before `dotnet build`), with the template's
  generated hey-api client, regenerated from the running site and committed. Two dashboards
  registered in TypeScript manifests with section conditions; the picker in `src/demo/`.
- **Wording on the server.** First actions and banner messages are produced by the package's
  C# and sent as text; the client lays them out with UUI elements.
- **Template patches**: the hey-api peer minimum and the 17 generator and `hey-api.ts` fixes
  recorded in Carl's package scaffold notes were checked at scaffold; none occurs on 17.8.1, so
  none is patched.

## Alternatives Considered

| Option | Why not |
|---|---|
| Hand-written `fetch` with the auth token | Copyable but diverges from the template vendors start from; types drift from the server |
| Razor-rendered backoffice view | Not how Umbraco 17 extends the backoffice |
| Wording in the client | Not covered by the .NET tests; editor and admin wording would be split across languages |
| One controller with mixed policies | Easier to get one action's policy wrong; separate controllers make the reflection test simple |

## Consequences

- Node 24 is needed to build the sample.
- Regenerating the client needs a running site; the generated files are committed.
- The client is checked by building and by Carl's observed checks, not by automated UI tests.

## Reversal Cost

Medium. The API and wording are independent of the client; replacing the generated client with
hand-written calls touches the client only.

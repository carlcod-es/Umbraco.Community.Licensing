# Development

How to build, test and run this repository on your own machine: the library
`Umbraco.Community.Licensing`, the vendor tool `ucl` (`Umbraco.Community.Licensing.Tool`) and the
sample in `samples/`.
Releasing is in [Releasing](releasing.md); using the published tool is in
[Vendor tool](vendor-tool.md).

The library has nothing to run on its own. You build and test it, run the tool, or reference it
from a site of your own.

## Prerequisites

- The [.NET 10 SDK](https://dotnet.microsoft.com/download). `global.json` pins 10.0.100 and rolls
  forward to a later feature band.
- [Node.js](https://nodejs.org) 20 or later, for the release tooling and its tests in
  `tools/release/`. The sample needs 24.13 or later (see [Sample](#sample)). With 24.13 or later
  installed, every command in this page works.
- Windows, macOS or Linux. Every command below works in any shell on each of them.

## Build and test

From the repository root:

```
dotnet build Umbraco.Community.Licensing.slnx
dotnet test --solution Umbraco.Community.Licensing.slnx
node --test tools/release/*.test.mjs
```

- Tests run on Microsoft.Testing.Platform (`global.json`), so `dotnet test` takes `--solution`.
- Warnings are errors (`Directory.Build.props`): a build with a warning fails.
- **Node.js glob.** Node.js 22 and later expand a quoted pattern themselves, so
  `node --test "tools/**/*.test.mjs"` (every test under `tools/`, as CI runs it) works there, and
  so with the Node.js 24 the sample needs. Node.js 20 does not: leave the pattern unquoted so the
  shell expands it, as above.

## Run the tool from source

```
dotnet run --project src/Umbraco.Community.Licensing.Tool
dotnet run --project src/Umbraco.Community.Licensing.Tool -- --version
```

Arguments for `ucl` go after `--`. Run it in a real terminal: `ucl` refuses to start when its input
or output is redirected ([Vendor tool](vendor-tool.md), Start ucl).

## Install a local build as `ucl`

The closest to what a vendor gets from nuget.org. `artifacts/` is ignored by git.

```
dotnet pack src/Umbraco.Community.Licensing.Tool -c Release -o artifacts/pack
dotnet tool install Umbraco.Community.Licensing.Tool --tool-path artifacts/ucl --add-source artifacts/pack --version <Version>
artifacts/ucl/ucl --version
```

- `<Version>` is the `Version` in `Version.props`, for example `0.1.0-beta.7`.
- `--tool-path` keeps the build out of your global tools. With `--global` instead, it replaces a
  `ucl` installed from nuget.org; remove it with
  `dotnet tool uninstall --global Umbraco.Community.Licensing.Tool`.
- A rebuild keeps the same version, and installing the same version again changes nothing.
  Uninstall first (`dotnet tool uninstall Umbraco.Community.Licensing.Tool --tool-path artifacts/ucl`),
  then install.
- The install also reads nuget.org for the tool's dependencies, so it needs the network or a
  package cache that already holds them.
- On Windows the command is `artifacts\ucl\ucl.exe`.

## Keep test runs away from your own installation

`ucl` uses the current folder when it holds `ucl.db`, otherwise `.ucl` in your home folder
([Vendor tool](vendor-tool.md), Where the installation is). To try a build without touching the
installation you issue real keys from, give it a folder of its own:

1. Make an empty folder outside the repository and start the tool in it (with `dotnet run
   --project <repository>/src/Umbraco.Community.Licensing.Tool`, or the installed `ucl`).
2. With no `.ucl` in your home folder, setup offers the current folder: choose it. With one, the
   session opens that installation. Choose **Settings > New installation in the current folder**
   and set up there.
3. Start the tool in that folder from then on. The session's first line, `Using <full path>`,
   says which installation it opened.

Delete the folder to start again.

## Use the library from a local site

- **Project reference.** Reference `src/Umbraco.Community.Licensing/Umbraco.Community.Licensing.csproj`
  from the site's project.
- **Local package.** Pack it and install from the pack folder:

  ```
  dotnet pack src/Umbraco.Community.Licensing -c Release -o artifacts/pack
  dotnet add <site project> package Umbraco.Community.Licensing --version <Version> --source <full path to artifacts/pack>
  ```

  A package of the same version stays in the NuGet cache, so after a rebuild the site keeps the
  old build. Delete `umbraco.community.licensing/<Version>` from the global packages folder
  (`dotnet nuget locals global-packages --list` shows where it is), then restore the site.

How to issue and check keys from code: [Vendor guide](vendor-guide.md) and
[Implementor guide](implementor-guide.md).

## Sample

`samples/` holds Acme Contact Form, a fictional Umbraco 17 package, and a demo site for it. It
has its own solution, `samples/Acme.ContactForm.slnx`, and the root solution does not include it.
It references the library as a project, so a library change that breaks it shows up at once.
It is never released. What it shows and what to copy: [`samples/README.md`](../samples/README.md).

It needs Node.js **24.13 or later** for its backoffice client and `samples/run-demo.mjs`, as well
as the .NET 10 SDK. Check with `node --version`. If you manage Node.js versions with a version
manager, switch to 24 before running these commands.

### Run the demo site

From the repository root:

```
node samples/run-demo.mjs
```

It installs and builds the client when needed, then starts the site on
<http://localhost:15329>. The demo administrator login and what to try are in
[`samples/README.md`](../samples/README.md#first-run).

### Build and test, as CI does

`.github/workflows/sample.yml` runs these on Ubuntu for any change under `samples/`, the library
project, `global.json` or `Directory.Build.props`. To run the same checks locally, from the
repository root:

```
cd samples/Acme.ContactForm/Client
npm ci
npm run build
cd ../../..
node --test samples/run-demo.test.mjs
dotnet build samples/Acme.ContactForm.slnx -c Release
dotnet test --solution samples/Acme.ContactForm.slnx -c Release --no-build
```

Two tests are skipped in a normal run. They are the explicit generators for the demo keys and
the demo signing keys ([`samples/README.md`](../samples/README.md#demo-keys)).

### Regenerate the backoffice API client

`samples/Acme.ContactForm/Client/src/api/` is generated from the site's OpenAPI document and
committed. CI does not regenerate it, so regenerate it after any change to a sample controller or
response type:

1. Start the site on its `http` profile, which serves <http://localhost:15329>:
   `dotnet run --project samples/Acme.ContactForm.Site --launch-profile http`.
2. In another terminal, in `samples/Acme.ContactForm/Client`, run `npm run generate-client`.

The generator reads
`http://localhost:15329/umbraco/swagger/acme-contact-form/swagger.json`, so it fails if the site
is not running or is on another port.

### Demo signing keys and secret scanning

`samples/demo-signing-keys/` commits two private keys on purpose. They are public and demo-only
([`samples/demo-signing-keys/README.md`](../samples/demo-signing-keys/README.md)). GitHub secret
scanning may raise alerts for them. Dismiss an alert as "used in tests" only when it is on
`samples/demo-signing-keys/demo.pem` or `samples/demo-signing-keys/untrusted.pem` and the file
still starts with the demo-only warning. Treat an alert on any other file as a real leak: stop
trusting that key's public key and rotate it ([Vendor guide](vendor-guide.md#rotate-a-signing-key)).
Never commit a real signing key.

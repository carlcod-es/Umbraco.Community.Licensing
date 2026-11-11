# Development

How to build, test and run this repository on your own machine: the library
`Umbraco.Community.Licensing` and the vendor tool `ucl` (`Umbraco.Community.Licensing.Tool`).
Releasing is in [Releasing](releasing.md); using the published tool is in
[Vendor tool](vendor-tool.md).

The library has nothing to run on its own. You build and test it, run the tool, or reference it
from a site of your own.

## Prerequisites

- The [.NET 10 SDK](https://dotnet.microsoft.com/download). `global.json` pins 10.0.100 and rolls
  forward to a later feature band.
- [Node.js](https://nodejs.org) 20 or later, for the release tooling and its tests in
  `tools/release/`.
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
  `node --test "tools/**/*.test.mjs"` (every test under `tools/`, as CI runs it) works there. Node.js
  20 does not: leave the pattern unquoted so the shell expands it, as above.

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

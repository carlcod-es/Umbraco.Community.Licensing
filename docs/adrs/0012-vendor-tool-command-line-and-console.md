# ADR-0012: Vendor tool command line and console

- Status: Decided
- Date: 2026-10-28
- Source: `add-vendor-tool` design.md Q19, Q33

## Context

PDR-0030: every action runs by hand with prompts, or from scripts with all inputs given and no
prompts. About 20 subcommands, each with options, help and validation. The passphrase must never
be a plain argument (PDR-0032).

## Decision

- **Parsing.** System.CommandLine 2.0 (stable), pinned exactly. Subcommands, `--help`,
  `--version`, option validation.
- **Prompts.** BCL `Console` only. Interactive when stdin is not redirected and `--no-input` is
  absent. Prompts and messages to stderr; results (key string, path, list) to stdout. Hidden
  passphrase read with `Console.ReadKey(intercept: true)`.
- **Passphrase sources**, first found: `--passphrase-stdin` (first line of stdin),
  `UCL_PASSPHRASE`, hidden prompt. `--passphrase-stdin` reads its line as UTF-8; an empty line or
  end of stdin is refused, with no fall-through to `UCL_PASSPHRASE`; with stdin at a terminal it is
  refused, since reading would echo the passphrase (Q33).
- **Exit codes.** 0 done, 1 refused, 2 usage error.
- **Testability.** Commands run through `ToolApp.Run(args, ToolEnvironment)`; the environment
  carries the console, clock, folders and variables.

## Alternatives Considered

| Option | Why not |
|---|---|
| Hand-written parsing | Help, validation and errors for every command by hand |
| Spectre.Console.Cli | Third party, brings a rendering library not needed |
| Spectre.Console for prompts and tables | One more dependency; FR-13 (Carl, 2026-10-28) |
| Passphrase as an option value | Visible in shell history and process lists |

## Consequences

- Plain-text prompts and tables. FR-13 can add a richer console later without changing commands.
- Dependency: System.CommandLine (Microsoft), approved by Carl 2026-10-28.

## Reversal Cost

Medium: parsing is confined to the command definitions; prompts sit behind `ToolConsole`.

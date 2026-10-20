# tpm-mac-multiselect

Multiselect BepInEx plugin for *The Planet of the Machines* on macOS (Unity IL2CPP).

## Contents

- `plugins/multiselect/` — Multiselect plugin (selection, placement hooks, build actions)
- `docs/` — multiselect + shared thin-host/multiselect specs, plans, gates

## Hard dependency

This plugin `HardDependency`s the thin Il2CppInterop Host shipped in **[tpm-mac-loader](https://github.com/Dywane/tpm-mac-loader)** (`plugins/mac-il2cpp-host`).

## Provenance

Initial import from monorepo worktree `TPM-muti-select` branch `track-a/tps-game-loaderrs` tip `3668dca`.
Probes and `docs/evidence/` binary dumps were excluded.

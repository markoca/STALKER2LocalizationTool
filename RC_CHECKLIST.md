# RC6 checklist

- [ ] `tools\` contains prebuilt `retoc.exe`, `repak.exe`, `UAssetGUI.exe`, `Mappings.usmap`, and `S2HOCMM.exe`.
- [ ] Publish performs no Git clone, download, Rust/cargo build, Python patch step, or retoc compilation.
- [ ] Stock `retoc.exe to-zen --help` runs successfully; `--source-package-map` is not required.
- [ ] MODS scans NewContent/OverrideContent aliases together and groups only by the complete 24-hex Zen chunk ID.
- [ ] A complete OverrideContent alias is preferred when available; incompatible/incomplete alias SID sets fail safely.
- [ ] Modular build verifies canonical output path, original complete chunk/FPackageId, and exact patched RawExport.
- [ ] All-in-One performs the same stock-retoc path/chunk/payload verification.
- [ ] Already-correct target-language database values do not create redundant physical overlays.
- [ ] GAME scans only pakchunk0 localization, builds from Editable JSON, verifies S2HOCMM round-trip, and uses repak V11 + path hash seed 1244705156.
- [ ] Publish output contains the application plus the complete project-local `tools\` bundle.
- [ ] Windows EXE starts without the RC4 custom app.manifest Side-by-Side failure.

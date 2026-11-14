# Design journal

Chronological record of ideas, options, rejected ideas, who decided, and what changed and why.

## 2026-11-13 · Phase 0 planning

**First reading.** The brief asks for a small, clean ASP.NET 10 API with tests, and for AI to be used visibly to both code and review. So the reviewers will judge the code quality and the engineering process in equal measure. The harness (rules, hooks, reviewer agents, evidence files) is therefore part of the deliverable, not overhead.

**Environment findings that changed the plan.**
- Only the .NET 7 SDK was installed. The brew cask installed SDK 11 (there is no 10 cask). Options were the official pkg, the brew formula, or container-only builds. **Alessandro installed the official .NET 10 pkg.**
- The GitHub repo is private. **Alessandro decided** to keep it private while building and make it public in Phase 11, after a history scan.
- Semgrep and Gitleaks were missing. **Alessandro approved** a brew install. The Microsoft Learn MCP was **not** added (Alessandro's choice), so API checks use WebFetch on learn.microsoft.com.

**Harness decisions (D1–D9, proposed by the agent, approved by Alessandro on 2026-11-14).**
- D1: an initial commit on `main`, then one branch per phase, because a PR needs a base branch.
- D2: block every `.env*` edit, including `.env.example`. Config goes through appsettings and compose environment variables instead.
- D3: block force push only (`--force`, `-f`), not every `--force`. Otherwise harmless commands like `dotnet new --force` would be blocked too.
- D4: whitespace-only format on each edit (~1 s). The full `dotnet format --verify-no-changes` runs in `verify.sh`.
- D5: `.slnx` solution, the .NET 10 default.
- D6: central package management, so package versions are audited in one file.
- D7: AI review workflows are created now and skip until the API key secret exists.
- D8: repo artefacts stay in normal English regardless of the chat style. `docs/PROGRESS.md` is the only progress tracker.
- D9: the official .NET 10 SDK pkg.

## 2026-11-14 · Phase 0 implementation

- **xUnit v3 packaging.** Checked the xUnit docs: `xunit.v3` 4.x defaults to Microsoft Testing Platform v2, and with the .NET 10 SDK the `global.json` `test.runner` setting replaces `TestingPlatformDotnetTestSupport`. Chose the plain `xunit.v3` package, with no `Microsoft.NET.Test.Sdk`, because the project is MTP-only.
- **Stop hook scope.** The first version ran verify whenever anything under `src/` or `tests/` changed, including nested `CLAUDE.md` files. Narrowed it to code and build files (`*.cs`, `*.csproj`, `*.props`, `*.slnx`, `global.json`), so doc edits never trigger a build.
- **Dangerous-bash hook rewrite.** The agent's first version had two bugs, both caught by its own hook tests (see the phase file): a plain `rm file.txt` exited with code 1, and flags were read from the whole command line. The rewrite parses each command segment separately.

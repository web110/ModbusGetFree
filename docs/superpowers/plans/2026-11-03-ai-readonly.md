# External AI Read-only Interface Implementation Plan
> Execute inline with executing-plans; user requested implementation and demo.
**Goal:** Expose current Modbus data to external AI and demonstrate it.
**Architecture:** UI snapshots -> loopback token-authenticated GET API -> Python MCP stdio adapter.
**Tech Stack:** Existing C# .NET Framework, Python standard library.
**Spec:** docs/superpowers/specs/2026-11-03-ai-readonly-design.md
## Global Constraints
Default off, loopback only, session token, read-only, no new device transactions. Preserve existing edits.
## Review Focus
Never-read/edited values, reconnect source confusion, errors after success, hostile HTTP requests, server lifecycle.
## Tasks
- [x] Add tests/AiInterfaceTests.cs: new window never_read; successful demo read fresh; modified raw values unverified; disconnect not fresh; HTTP token/method/origin/stop tests. Run before implementing to establish failure.
- [x] Add src/DataWindow.Ai.cs, src/AiReadServer.cs, src/MainWindow.Ai.cs; integrate read provenance in DataWindow.Execute and session identity in Bus.Open. Add UI menu and immutable timer publication.
- [x] Add ai/modbus_mcp.py and ai/test_mcp.py with initialization, tool listing/calls, invalid request and unavailable server checks. Add demo page and launcher. Document configuration and limitations.
- [x] Build and run new tests, existing regression, live demo using existing simulated RTU; capture evidence and review final diff. No commit/push requested.

## Verification ledger
- Build with warnings as errors passed.
- test.ps1: 14 suites passed, including 16 AI interface checks.
- Python MCP: 4 tests passed with live demo.
- Actual MCP subprocess initialized, listed tools and called both tools against running simulated RTU; 2 windows / 5 values, all fresh. Evidence: ai/demo-mcp-result.json.
- Browser clicked Read and verified 25/25.1/25.2 and 7/7.01. Screenshot: ai/demo-preview.png.
- Independent static review found retry-error freshness and frozen publication issues; both fixed with regression checks and re-reviewed.
- Existing language-menu position regression was diagnosed and fixed by inserting the AI menu before Language/Help.
- No real hardware or external AI account/model validation. No commit/push performed.

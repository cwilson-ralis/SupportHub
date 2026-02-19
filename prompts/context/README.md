# prompts/context/

This directory contains auto-generated context files produced by the orchestrator after each completed wave.

## Purpose
When multiple Claude Code agents work in parallel, they need to know what types, interfaces, and endpoints were created by other agents in prior waves. Context files bridge this gap by summarizing what was built and what's available for use.

## File Naming Convention
```
phase-{N}-wave-{M}.md    — Context snapshot after Phase N, Wave M
phase-{N}-complete.md    — Summary after entire Phase N is complete
```

## File Format
Each context file includes:
- **Completed** — Files created/modified in that wave
- **New Types Available** — Entity classes, enums, DTOs added
- **New Interfaces Available** — Service interfaces ready for implementation
- **New Endpoints** — API routes added
- **Notes for Next Wave** — Important context for subsequent work

## Who Writes These Files
Only the **orchestrator** writes to this directory. Sub-agents read these files but never modify them.

## Who Reads These Files
All sub-agents read context files from prior waves to understand:
- What types exist and their full property lists
- What interfaces are available for injection
- What endpoints exist for the UI to call
- Any constraints or decisions made in prior waves

## Lifecycle
- Files accumulate throughout the build
- They are not deleted between phases
- The `phase-{N}-complete.md` files serve as the authoritative summary for each phase

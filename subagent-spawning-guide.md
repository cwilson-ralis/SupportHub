# Sub-Agent Spawning Guide

This document explains how to spawn specialist sub-agents from the Orchestrator using Claude Code.

## Method: Using `claude` CLI with Prompts

The recommended approach is to use the Claude Code CLI to spawn sub-agents, passing them their system prompt and task-specific context.

### Basic Pattern

```bash
# Spawn a sub-agent with its system prompt + task
claude --print \
  "$(cat prompts/agent-backend.md)

---

## Phase 0 Conventions (Required Reading)
$(cat docs/Phase-0-Project-Overview.md)

---

## Your Task Assignment

### Phase 1, Wave 1: Create Core Entities

Create the following entities following the specifications in the phase document below. Output complete, compilable C# files.

$(cat docs/Phase-1-Foundation.md | sed -n '/## Task 1.2/,/## Task 1.4/p')

---

## Existing Code Context

### BaseEntity (already exists):
$(cat src/SupportHub.Core/Entities/BaseEntity.cs 2>/dev/null || echo 'Not yet created — you will create this.')
"
```

### Using --print vs Interactive

- **`--print`** — Non-interactive. Agent produces output, you review it. Best for well-defined tasks.
- **Without `--print`** — Interactive. Agent can ask clarifying questions. Better for complex/ambiguous tasks.

For most build tasks, `--print` is preferred because the task documents are detailed enough.

---

## Wave-by-Wave Examples

### Phase 1, Wave 1: Backend Agent — Entities & EF Config

```bash
claude --print \
  "$(cat prompts/agent-backend.md)

## Phase 0 Conventions
$(cat docs/Phase-0-Project-Overview.md)

## Your Task
Complete Tasks 1.2, 1.3, and 1.4 from Phase 1.
- Task 1.2: Create BaseEntity, all enums, Result<T>
- Task 1.3: Create all 12 entity classes
- Task 1.4: Create AppDbContext, all EF configurations, ICurrentUserService

Reference document:
$(cat docs/Phase-1-Foundation.md)

Output every file with its full path and complete content.
" > wave1-backend-output.md
```

After reviewing, apply the files to the repo.

### Phase 1, Wave 2: Service Agent — Implementations

```bash
claude --print \
  "$(cat prompts/agent-service.md)

## Phase 0 Conventions
$(cat docs/Phase-0-Project-Overview.md)

## Your Task
Complete Tasks 1.6 and 1.7 from Phase 1:
- Task 1.6: Implement CompanyService (CRUD for companies)
- Task 1.7: Implement UserService (user management, company assignments)

Reference document:
$(cat docs/Phase-1-Foundation.md)

## Dependencies (from Wave 1 — already in the codebase)

### Interfaces you are implementing:
$(cat src/SupportHub.Core/Interfaces/ICompanyService.cs)
$(cat src/SupportHub.Core/Interfaces/IUserService.cs)

### DTOs you are using:
$(cat src/SupportHub.Core/DTOs/CompanyDto.cs)
$(cat src/SupportHub.Core/DTOs/UserProfileDto.cs)

### Entities you are working with:
$(cat src/SupportHub.Core/Entities/Company.cs)
$(cat src/SupportHub.Core/Entities/UserProfile.cs)
$(cat src/SupportHub.Core/Entities/UserCompanyAssignment.cs)

### DbContext:
$(cat src/SupportHub.Infrastructure/Data/AppDbContext.cs)

Output every file with its full path and complete content.
" > wave2-service-output.md
```

### Phase 1, Wave 2: API Agent — Controllers (runs in parallel with Service Agent)

```bash
claude --print \
  "$(cat prompts/agent-api.md)

## Phase 0 Conventions
$(cat docs/Phase-0-Project-Overview.md)

## Your Task
Complete the API portions of Tasks 1.6 and 1.7 from Phase 1:
- Create CompaniesController (CRUD)
- Create UsersController (user management, company assignments)
- Configure Api/Program.cs with auth, versioning, Swagger

Reference document:
$(cat docs/Phase-1-Foundation.md)

## Dependencies (from Wave 1)
### Service interfaces your controllers will call:
$(cat src/SupportHub.Core/Interfaces/ICompanyService.cs)
$(cat src/SupportHub.Core/Interfaces/IUserService.cs)

### DTOs:
$(cat src/SupportHub.Core/DTOs/CompanyDto.cs)
$(cat src/SupportHub.Core/DTOs/UserProfileDto.cs)

Output every file with its full path and complete content.
" > wave2-api-output.md
```

### Phase 1, Wave 3: Test Agent

```bash
claude --print \
  "$(cat prompts/agent-test.md)

## Phase 0 Conventions
$(cat docs/Phase-0-Project-Overview.md)

## Your Task
Write unit tests for CompanyService and UserService.

## Code Under Test:
$(cat src/SupportHub.Infrastructure/Services/CompanyService.cs)
$(cat src/SupportHub.Infrastructure/Services/UserService.cs)

## Interfaces:
$(cat src/SupportHub.Core/Interfaces/ICompanyService.cs)
$(cat src/SupportHub.Core/Interfaces/IUserService.cs)

## Entities:
$(cat src/SupportHub.Core/Entities/Company.cs)
$(cat src/SupportHub.Core/Entities/UserProfile.cs)
$(cat src/SupportHub.Core/Entities/UserCompanyAssignment.cs)

## DbContext:
$(cat src/SupportHub.Infrastructure/Data/AppDbContext.cs)

Create TestDbContextFactory, FakeCurrentUserService, entity builders, and all test classes.
Output every file with its full path and complete content.
" > wave3-test-output.md
```

---

## Tips for Effective Sub-Agent Delegation

### 1. Give minimal but sufficient context
Don't dump the entire codebase. Give the agent:
- Its prompt file (role, conventions, patterns)
- Phase 0 conventions
- The specific task from the phase document
- Only the dependency files it actually needs

### 2. Be explicit about what files to create
List the files you expect as output. E.g., "Create TicketService.cs, InternalNoteService.cs, and their validators."

### 3. Review before integrating
Always read the agent's output before applying it to the repo. Check for:
- Correct namespaces and file paths
- Consistent naming with existing code
- No duplicate DI registrations
- No missing `using` statements

### 4. Update context files after each wave
After applying Wave 1 files, regenerate the context extracts in `prompts/context/` so Wave 2 agents have accurate dependency information.

```bash
# Example: extract current interfaces
find src/SupportHub.Core/Interfaces -name "*.cs" -exec cat {} \; > prompts/context/current-interfaces.md
```

### 5. Run build gates
After every wave:
```bash
dotnet build
dotnet test  # after Wave 3
```

### 6. Handle cross-agent issues
If Agent A's output doesn't compile because it needs something from Agent B:
1. Identify the specific incompatibility
2. Decide which agent needs to change
3. Re-run that agent with the corrected context
4. Do NOT manually fix it yourself (this creates drift from the agent's understanding)

---

## Parallelization Matrix

| Phase | Wave 1 (Sequential) | Wave 2 (Parallel) | Wave 3 (Parallel) |
|---|---|---|---|
| 1 | Backend | Service + API + Infra | UI + Test |
| 2 | Backend + UI (scaffold) | Service + API + Infra | UI (pages) + Test |
| 3 | Backend + Infra (Graph setup) | Infra (email services) | Service + UI + Test |
| 4 | Backend | Service + Infra | UI + API + Test |
| 5 | Backend | Service + API | UI + Test |
| 6 | Backend + Infra | Service + Infra + UI | Test (QA) |

Agents in the same wave can be spawned simultaneously. Wait for all agents in a wave to complete before starting the next wave.

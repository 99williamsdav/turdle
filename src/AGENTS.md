# Turdle Agent Context

## Purpose

Turdle is a real-time multiplayer word game platform. It combines:

- A live game server for Wordle-like rounds and lobbies.
- Optional AI-powered bots and avatar/image generation.
- Persistence for active and prewarmed rooms.
- A web client (Angular) that interacts with the server over SignalR.

The broader solution also includes a second game domain (HorseCopyPaste, inspired by Codenames), shared utilities, test projects, and small tooling utilities.

## Solution Map

- `Turdle/`: Main ASP.NET Core host (net8.0), SignalR hubs, game state orchestration, persistence wiring.
- `ChatGpt/`: AI integration clients for chat completions and image generation.
- `HorseCopyPaste/`: Separate game engine/domain.
- `Turdle.Utils/`: Shared helpers (extensions, logging context).
- `Turdle.Tests/` and `HorseCopyPaste.Tests/`: Unit/integration style tests.
- `Turdle.Tools/`: Offline scripts for resource generation and parsing.

## Runtime Architecture (Turdle)

### Entry and Composition

`Turdle/Program.cs` composes the app:

- Registers hubs (`GameHub`, `AdminHub`, `HomeHub`).
- Registers long-lived singleton services (`RoomManager`, `WordService`, bot and AI services).
- Binds config sections for ChatGpt settings, room buffering, and persistence.
- Enables CORS, compression, static files, MVC routes, and SignalR endpoints.

### Core Domain Flow

Main flow is client command -> hub method -> `RoomManager` -> `Room`.

- `HomeHub`: room discovery/creation surface.
- `GameHub`: player lifecycle, gameplay, chat, ready/start, admin-like per-room actions.
- `AdminHub`: higher-privilege operations (hard reset, global parameter updates, moderation actions).

`RoomManager` responsibilities:

- Acts as the process-level room registry.
- Maintains connection-to-room cache for reconnect/disconnect behavior.
- Prewarms rooms in the background using a bounded buffer.
- Persists and restores room snapshots via `IRoomStateRepository`.
- Broadcasts room summaries to home/lobby subscribers.

`Room` responsibilities:

- Owns round state, players, timers, bot registration, and chat state.
- Applies gameplay commands and emits masked/full state updates.
- Handles room-scoped side effects (avatar generation, typing events, persistence callback).
- Supports snapshot restore paths for resiliency.

### Data and Persistence

- Snapshot model: `RoomStateSnapshot` and nested snapshot DTOs.
- Storage adapter: `SqliteRoomStateRepository`.
- Persistence strategy: upsert full room payload JSON, with `is_buffered` marker for prewarmed rooms.
- Startup behavior: repository can reload buffered rooms and active rooms by code lookup.

This is effectively a lightweight eventless snapshot architecture (state is periodically overwritten as a whole aggregate snapshot).

### AI Integration

- `ChatGptClient` performs completion calls and tracks rough token spend in logs.
- `ImageGenerationClient` (in `ChatGpt/`) plus avatar services provide cached generated images for rooms and personalities.
- `BotFactory` creates bot types (dumb or ChatGPT personality bot).
- `ChatGptPersonalityBot` uses word constraints from `WordService` and personality prompts to choose words/chat.

### Frontend Boundary

- Angular client lives in `Turdle/ClientApp`.
- ASP.NET Core serves static assets and proxies SPA dev server in development.
- SignalR is the primary command/update channel between client and game state.

## Architectural Style Summary

- Host-centered modular monolith.
- Stateful in-memory aggregates (`Room`) coordinated by a singleton manager.
- Adapter boundary around persistence and AI providers.
- Real-time transport at the edge (SignalR hubs), not inside the core domain.

This is a practical architecture for game-lobby products where latency and push updates matter more than strict stateless scaling.

## Transferable Blueprint For Other Projects

When reusing this design in another project, preserve these boundaries:

1. Keep transport thin.
   - Hubs/controllers should validate, log, and delegate only.
2. Keep a single orchestration layer.
   - One manager/service owns aggregate lookup, lifecycle, caching, and broadcast coordination.
3. Keep aggregate state cohesive.
   - One aggregate (`Room` equivalent) owns invariants, timers, and command handling.
4. Keep external integrations behind adapters.
   - AI, storage, and media providers should be injectable ports.
5. Keep snapshot schema explicit.
   - Versionable DTOs prevent accidental breaking changes during restore.

## Suggested Improvements For Future Projects

These suggestions are based on patterns in this codebase and are portable to similar systems.

### 1) Introduce explicit domain/application layers

Current code mixes orchestration, domain state transitions, and some infrastructure concerns in `Room` and `RoomManager`. A clearer split helps scale complexity:

- `Domain`: pure game logic and invariants.
- `Application`: command handlers/use-cases.
- `Infrastructure`: SignalR, SQLite, OpenAI, file system.

### 2) Add cancellation and timeout policies for all AI/network calls

AI calls are async but not strongly policy-bound. Add:

- `CancellationToken` plumbing from hub request to provider.
- Retry/backoff with jitter for transient faults.
- Circuit breaker or fallback bot behavior.

### 3) Version room snapshots

Persisted JSON snapshots should include schema version and migration logic:

- `SnapshotVersion` field.
- Backward migration steps on load.
- Test fixtures for old versions.

### 4) Strengthen concurrency model

`lock`, timers, and concurrent collections are used heavily. For long-term robustness consider:

- Single-threaded actor/mailbox per room (command queue).
- Deterministic state mutation path.
- Reduced lock scope and timer race risk.

### 5) Normalize content safety and moderation

Bot personalities and chat prompts can drift into unsafe text. Add:

- Prompt-level guardrails and sanitization.
- Optional moderation API pass for generated text.
- Configurable content policy per deployment environment.

### 6) Improve observability

Add OpenTelemetry traces and metrics around:

- Command latency by hub method.
- Room count, prewarm queue depth, restore count.
- AI cost, timeout, and failure rates.

### 7) Make room lifecycle explicit

Define lifecycle states (buffered, active, idle, archived) and cleanup policies:

- Idle expiration.
- Snapshot retention policy.
- Background archival/deletion tasks.

### 8) Treat configuration as typed, validated contracts

At startup, validate strongly-typed options:

- Missing API keys.
- Invalid buffer size/delay.
- SQLite path/connectivity.

Fail fast on invalid production config.

### 9) Upgrade dependency and package hygiene

A few package versions are old relative to target framework. In future projects:

- Keep JSON/logging/data packages current.
- Automate dependency updates and security scanning in CI.

### 10) Expand test strategy beyond unit tests

Add focused integration and contract tests for:

- Hub command -> room state transitions.
- Snapshot save/restore compatibility.
- AI adapter behavior with deterministic fake providers.

## Quick Start For New Agents In This Repo

If you are an AI coding agent entering this project:

1. Start from `Turdle/Program.cs` to see all runtime dependencies.
2. Read `Turdle/Hubs/*.cs` to map client command surface.
3. Read `Turdle/RoomManager.cs` then `Turdle/Room.cs` for behavior.
4. Read `Turdle/Persistence/*.cs` for restore and durability semantics.
5. Read `ChatGpt/*.cs` before changing AI behavior.
6. Use test projects first when modifying game rules.

This order gives the fastest path from external API to internal invariants.
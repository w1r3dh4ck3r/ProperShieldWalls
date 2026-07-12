## Inject

This project uses a layered directory structure. Place new files in the correct layer.

<!-- Customize this file for your project. Example for Next.js: -->

<!-- 
src/
  app/          # Next.js routing only (page.tsx, layout.tsx, route.ts)
  frontend/     # Client components, hooks, UI logic
  server/       # Server-side business logic, services, repositories
  shared/       # Types and utilities used by both layers (no browser APIs, no DB)

Rules:
- Logic that touches the DB lives in src/server/services/*-repo.ts
- React components live in src/frontend/components/
- Shared types live in src/shared/domain/ or src/shared/types/
- src/utils/, src/components/, src/hooks/ at root are BLOCKED (layer-less)
-->

## Reference

<!-- Full architecture detail — not injected, human-read only. -->

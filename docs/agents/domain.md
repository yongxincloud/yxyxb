# Domain Docs

How the engineering skills should consume this repo's domain documentation when exploring the codebase.

## Before exploring, read these

- **`GLOSSARY.md`** at the repo root.
- **`docs/adr/`**: read ADRs that touch the area you're about to work in.
- Relevant files under `docs/superpowers/specs/` and `docs/superpowers/plans/` when you need design history.

If any of these files don't exist, **proceed silently**. Don't flag their absence; don't suggest creating them upfront. The `/domain-modeling` skill (reached via `/grill-with-docs` and `/improve-codebase-architecture`) creates them lazily when terms or decisions actually get resolved.

## File structure

This repo is single-context:

```
/
├── GLOSSARY.md
├── docs/adr/
│   └── 0001-object-storage-direct-https.md
├── docs/agents/                       ← agent configuration, not domain decisions
├── docs/superpowers/                  ← historical specs and plans
└── src/
    ├── YxyXb.Client/
    ├── YxyXb.Engine/
    ├── YxyXb.Core/
    ├── YxyXb.Api/
    └── ...
```

Do not create a `GLOSSARY-MAP.md` or per-project glossaries unless the user explicitly decides to move to multi-context documentation.

## Source of truth

When repository artifacts disagree, use this order:

1. Current code and executable behavior.
2. Accepted ADRs under `docs/adr/`.
3. `GLOSSARY.md` for domain vocabulary.
4. Historical specs and plans under `docs/superpowers/`.

Treat `docs/superpowers/` as context about past decisions, not as automatically current requirements.

## Use the glossary's vocabulary

When your output names a domain concept (in an issue title, a refactor proposal, a hypothesis, a test name), use the term as defined in `GLOSSARY.md`. Don't drift to synonyms the glossary explicitly avoids.

If the concept you need isn't in the glossary yet, that's a signal: either you're inventing language the project doesn't use (reconsider) or there's a real gap (note it for `/domain-modeling`).

## Flag ADR conflicts

If your output contradicts an existing ADR, surface it explicitly rather than silently overriding:

> _Contradicts ADR-0007 (event-sourced orders), but worth reopening because…_

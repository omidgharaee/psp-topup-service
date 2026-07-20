# GitFlow

This repository follows the **GitFlow** branching model, adapted for an
enterprise payment service.

---

## Branches

| Branch | Lifetime | Purpose |
| --- | --- | --- |
| `main` | permanent | Production-ready code. Every commit is tagged. |
| `develop` | permanent | Integration branch for the next release. |
| `feature/*` | ephemeral | One branch per feature, branched from `develop`. |
| `release/*` | ephemeral | Release stabilization, branched from `develop`. |
| `hotfix/*` | ephemeral | Production hotfixes, branched from `main`. |

```
main    ─────●────────────────────────●────────●────  (tags: v1.0.0)
              \                       /          \
develop ───────●────●────●────●───────●────●──────●────
                   \      \      \         \
feature/...         ●──────●      ●         ●
```

---

## Conventions

### Branch naming
- `feature/<short-slug>` — e.g. `feature/domain-topup`
- `release/<version>` — e.g. `release/1.0.0`
- `hotfix/<version>` — e.g. `hotfix/1.0.1`

### Commit message format (Conventional Commits)
```
<type>(<scope>): <subject>

<body>

<footer>
```
- **type**: `feat`, `fix`, `chore`, `build`, `ci`, `docs`, `refactor`, `test`, `perf`
- **scope**: the affected layer/feature — e.g. `domain`, `application`, `outbox`, `mci`
- **subject**: imperative, lowercase, ≤ 72 chars, no trailing period

Examples:
```
feat(domain): create topup aggregate
fix(outbox): mark message published on confirm
test(integration): add payment-consumer idempotency tests
docs: update sequence diagram for reverse flow
```

---

## Lifecycle of a feature

1. Branch from `develop`:
   ```bash
   git checkout develop
   git pull
   git checkout -b feature/domain-topup
   ```
2. Implement in small commits.
3. Merge to `develop` with `--no-ff` (pull-request style merge commit):
   ```bash
   git checkout develop
   git merge --no-ff feature/domain-topup
   git branch -d feature/domain-topup
   ```

## Lifecycle of a release

1. Branch from `develop`:
   ```bash
   git checkout -b release/1.0.0 develop
   ```
2. Bump versions, fix release-blocking issues only.
3. Merge to `main` (merge commit), tag, and merge back to `develop`:
   ```bash
   git checkout main
   git merge --no-ff release/1.0.0
   git tag -a v1.0.0 -m "Release 1.0.0"
   git checkout develop
   git merge --no-ff release/1.0.0
   ```

## Lifecycle of a hotfix

1. Branch from `main`:
   ```bash
   git checkout -b hotfix/1.0.1 main
   ```
2. Fix, bump patch version.
3. Merge to `main`, tag, and merge back to `develop`:
   ```bash
   git checkout main
   git merge --no-ff hotfix/1.0.1
   git tag -a v1.0.1 -m "Hotfix 1.0.1"
   git checkout develop
   git merge --no-ff hotfix/1.0.1
   ```

---

## CI mapping
- `build.yml` runs on `push`/`pull_request` to `main`, `develop`, `release/*`.
- `test.yml` runs on the same triggers and fans out to unit / architecture /
  integration jobs (integration uses Testcontainers).
- `publish.yml` runs on `v*.*.*` tags and publishes + creates a GitHub Release.

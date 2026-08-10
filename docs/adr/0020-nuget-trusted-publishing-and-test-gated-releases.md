# 0020. NuGet Trusted Publishing and Test-Gated Releases

## Status

Accepted

## Context

`.github/workflows/publish.yml` has existed and worked since `fbcfe26`: a tag push
(`v*`) triggers a job that packs `Dorn.Messaging.Contracts`, `Dorn.Messaging`,
`Dorn.SharedKernel`, `Dorn.Cli`, and `Dorn.Templates.WebApi`, smoke-tests the packed
CLI, and pushes to NuGet.org. Two gaps existed before this change:

1. **No test gate.** `publish.yml` never ran `dotnet test`. A tag push published
   packages regardless of whether the 2-OS build-and-test matrix in `ci.yml` would
   have passed.
2. **No defined ordering against `ci.yml`.** Both workflows triggered independently
   on the same tag push, with no relationship between them.

NuGet.org allows unlisting a bad package version, but not deleting it — a published
package with a broken build stays resolvable via pinned version references
indefinitely. The cost of a bad release is asymmetric: cheap to prevent, expensive
to undo.

## Decision

### Trusted Publishing over a stored API key

`publish.yml` already used `NuGet/login@v1` (OIDC-based Trusted Publishing) instead
of a stored `NUGET_API_KEY` secret. This ADR keeps that choice and does not revisit
it: no long-lived credential exists in the repo or in GitHub Secrets, and the OIDC
token is scoped to the single job run that requests it.

### Gate via `workflow_call`

The `build-and-test` job moves out of `ci.yml` into a reusable workflow,
`.github/workflows/build-test.yml` (`on: workflow_call`), unchanged in every step,
condition, and order. `publish.yml` gains a `test:` job that calls it, and the
`publish` job declares `needs: test`.

`workflow_call` was chosen over `workflow_run` (triggering `publish.yml` on
`ci.yml`'s `completed` event). `workflow_run` has three silent failure modes for a
tag-triggered release:

- The triggered workflow's **definition** is resolved from the **default branch**,
  not from the tag that fired the originating run — so a change to `publish.yml`
  on that tag would not be honored until it also existed on the default branch.
- Inside a `workflow_run` job, `github.ref_name` resolves to the **default branch**,
  not the tag — silently breaking `VERSION=${GITHUB_REF_NAME#v}`, the tag-derived
  version extraction the publish job depends on.
- `workflow_run` fires on completion of the **originating workflow for any trigger**
  (push, PR, schedule, ...), not just tag pushes — without an explicit tag filter
  reproduced in the second workflow, a plain branch push could trigger a publish
  attempt.

All three are silent: they do not fail loudly, they just produce a run that behaves
differently from what the tag pusher expects. `workflow_call` avoids all three
because the reusable job executes in the **caller's** context: `github.ref_name` is
the tag, the file used is the one on the tag's commit (via `uses: ./...`, resolved
from the checked-out ref), and there is no separate completion-based trigger to
filter.

### Passing = the full 2-OS matrix

`build-test.yml` keeps `fail-fast: false` and both `ubuntu-latest` and
`windows-latest` legs. `needs: test` on the `publish` job means GitHub Actions only
starts `publish` after every job produced by the `test` call — both matrix cells —
reports success. A single failing cell (either OS) blocks the push step.

### One authoritative run per tag

`ci.yml`'s `push` trigger adds `tags-ignore: ["v*"]` alongside an explicit
`branches: ["**"]`. Per GitHub's documented behavior, defining only
`tags`/`tags-ignore` on a `push` trigger disables the workflow for **all** branch
push events, not just tags — the `branches: ["**"]` companion is required to keep
ordinary branch pushes running `ci.yml` unaffected. With both keys set, a `v*` tag
push runs the matrix exactly once, through `publish.yml`'s `needs: test` call, and
`ci.yml` does not start a second, independent matrix run for the same tag.

OIDC/publish steps (`NuGet/login@v1`, `dotnet nuget push`) stay inside the
`publish` job in `publish.yml`, unmoved by this change — NuGet.org's Trusted
Publishing validates the OIDC token's `job_workflow_ref` claim, which resolves to
the file that owns the job, not the caller. Moving those steps into a reusable
workflow would change that claim and could invalidate the existing Trusted
Publishing policy. The workflow-level `permissions:` block on `publish.yml` is
left byte-identical, and no `environment:` key is added to the `publish` job — an
environment not present in NuGet.org's policy configuration would invalidate it.

### Alternatives considered

- **`workflow_run` on `ci.yml` completion.** Rejected: the three silent failure
  modes above make it unsuitable for a trigger that produces an irreversible
  publish.
- **Duplicate the matrix inline in `publish.yml`.** Rejected: forks the 2-OS matrix
  into two definitions that can drift apart over time.
- **Stored `NUGET_API_KEY` secret.** Rejected (pre-existing decision, reaffirmed):
  a long-lived credential is a larger blast radius than a per-run OIDC token.
- **`environment:` approval gate on the `publish` job.** Rejected: an environment
  not already present in the NuGet.org Trusted Publishing policy would invalidate
  that policy; adding manual approval would need coordinated policy changes on
  NuGet.org's side first.
- **Ubuntu-only gate.** Rejected: the whole point of the 2-OS matrix (ADR 0013) is
  catching platform-specific breakage; gating on one OS would silently reintroduce
  the risk this change closes.

## Consequences

- Tag-push CI time roughly doubles: a release tag now runs the full 2-OS matrix
  before publishing, where before it ran no tests at all.
- Check names change for nested reusable-workflow jobs (`<caller-job> /
  build-and-test (<os>)`). No required-status-check configuration exists on
  `develop` or `main` today (verified via `gh api`), so no branch protection
  update is needed as part of this change.
- `publish.yml`'s filename and the fact that its `publish` job (not a called
  workflow) owns the OIDC login step are now load-bearing for Trusted Publishing
  and must not be changed without first updating the NuGet.org policy.
- A red Windows (or Ubuntu) matrix cell now blocks a release. The fix is to
  correct the failure and re-tag — there is no override path, by design.

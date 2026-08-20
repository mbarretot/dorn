# 0020. NuGet Trusted Publishing and Test-Gated Releases

## Status

Accepted

## Context

Tag pushes could publish packages without waiting for the repository's Ubuntu and Windows tests. NuGet versions cannot be deleted after publication, so release prevention is cheaper than recovery.

## Decision

Use one tag-scoped, test-gated release path:

```text
v* tag
  -> publish.yml
    -> reusable build-test.yml
      -> Ubuntu + Windows pass
        -> NuGet OIDC login
          -> pack, smoke, publish
```

| Concern | Policy |
| --- | --- |
| Authentication | `NuGet/login@v1` Trusted Publishing through OIDC |
| Test gate | `.github/workflows/build-test.yml` via `workflow_call` |
| Publish ordering | `publish` declares `needs: test` |
| Tag ownership | `publish.yml` owns `v*`; `ci.yml` ignores those tags |
| Version | Derived from the caller's tag ref |

Keep OIDC login and push steps inside `.github/workflows/publish.yml`. NuGet validates the owning workflow in the token's `job_workflow_ref`; moving or renaming it can invalidate the trusted-publishing policy.

`ci.yml`'s `push` trigger is scoped to `branches: [main, develop]` (an explicit branches key is still required — GitHub disables normal branch pushes if `push` carries no branch filter at all). `tags-ignore: ["v*"]` was dropped as redundant: tag refs never match a `branches` filter, so `push: tags: v*` (publish.yml's own trigger) can never also satisfy `ci.yml`'s trigger regardless. `pull_request` (unfiltered by branch) remains the sole trigger for feature branches — narrowing `push` off them stopped every commit to an open PR from running the full matrix twice (once per event).

## Consequences

- Any red runner blocks publication with no override path.
- Release tags run the full matrix before packaging, increasing release time.
- Tag CI runs once through the publish caller instead of duplicating the matrix.
- Workflow identity and the location of OIDC steps are load-bearing release configuration.

## Alternatives

- **`workflow_run`:** rejected because workflow definitions and refs can resolve from the default branch, and non-tag completions require duplicated filtering.
- **Duplicate tests inside `publish.yml`:** rejected because matrices would drift.
- **Stored API key:** rejected due to long-lived credential blast radius.
- **Environment approval:** deferred until coordinated with the NuGet policy.
- **Ubuntu-only gate:** rejected because platform-specific failures must block release.

## Related

- [Engineering release flow](../../eng/README.md)
- [Reusable workflow](../../.github/workflows/build-test.yml)
- [Publish workflow](../../.github/workflows/publish.yml)

# Releasing

Releases are automatic. `.github/workflows/ci.yml` builds and tests every push and pull request. On a push to `main`
it also checks whether the package version is already on nuget.org, and if not:

1. pushes `EasySoapClient.<version>.nupkg` (and the `.snupkg` symbols) to nuget.org, and
2. creates a GitHub release `v<version>` with the packages attached and generated release notes.

## Publishing a new version

1. Bump `<Version>` in `EasySoapClient/EasySoapClient/EasySoapClient.csproj` (e.g. `3.0.1`, or `3.1.0-beta.1` for a prerelease).
2. Merge / push to `main`.

A push to `main` without a version bump only builds and tests. Pull requests never publish.

## One-time setup

Publishing uses nuget.org [Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing):
GitHub's OIDC token is exchanged for a temporary API key, so no API key is stored anywhere.

1. **nuget.org**: sign in as the package owner (`ThorChristiansen`) → your user name → **Trusted Publishing** → add a policy:
   - Repository Owner: `DrNoLife`
   - Repository: `Easy-Soap-Client`
   - Workflow File: `ci.yml` (file name only)
   - Environment: leave empty
2. Nothing to set up in GitHub: the nuget.org user name (`ThorChristiansen`) is in the workflow, and no API key is stored.

If the repository is private, nuget.org activates the policy only temporarily (7 days) until the first successful publish.

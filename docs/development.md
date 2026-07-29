# Development guide

## Prerequisites

- Git
- .NET 8 SDK or a newer compatible SDK
- Windows, macOS, or Linux for build and tests

`global.json` prefers SDK `8.0.413` and permits newer major SDKs. CI always installs .NET 8.

## Build and test

```sh
./scripts/verify.sh
```

The script performs a Release restore/build and test pass. Individual commands:

```sh
dotnet restore Blueprints.sln
dotnet build Blueprints.sln --configuration Release --no-restore
dotnet test Blueprints.Tests/Blueprints.Tests.csproj --configuration Release --no-build
```

Run the app:

```sh
./scripts/run-app.sh
```

Or:

```sh
dotnet run --project Blueprints.App/Blueprints.App.csproj
```

## Platform notes

- Windows is the most mature runtime target and uses DPAPI for private-key protection.
- macOS and Linux use the local AES-GCM protector.
- On Linux, Avalonia may require X11/XWayland. `scripts/diagnose-linux-display.sh` explains missing display state.
- Tests create isolated workspaces and must not read or write real application data.

## Repository workflow

- `develop` is the integration/default branch.
- `main` contains promoted release-ready states.
- Branch from `develop` using `feature/<slug>`, `fix/<slug>`, `docs/<slug>`, or `chore/<slug>`.
- Open pull requests into `develop`.
- Promotion pull requests move verified changes from `develop` to `main`.
- Releases are tagged from `main`.

## Code organization

- Keep the domain free of UI and provider dependencies.
- Put canonical persistence rules in Storage.
- Put key handling and signatures in Security.
- Put exchange/sync behavior in Collaboration.
- Keep hosted integrations outside signed project truth.
- Prefer immutable records for persisted documents.
- Add a regression test before or with every bug fix.

## Adding a persisted field

1. Decide whether it is signed project truth or local-only state.
2. Update the relevant record.
3. define compatibility behavior for older files.
4. update canonical serialization or workspace tests.
5. update [workspace format](workspace-format.md).
6. add migration logic when schema versions begin diverging.

## Troubleshooting

If SDK selection fails:

```sh
dotnet --list-sdks
dotnet --version
```

Install .NET 8 or a newer stable SDK. Do not edit a parent-directory `global.json`; this repository has its own pin.

If restore is slow on first use, let NuGet finish populating its cache. Subsequent builds should be much faster.

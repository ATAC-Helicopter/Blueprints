# SonarQube Cloud

Blueprints uses SonarQube Cloud as an additional code-quality and security-hotspot signal. It complements CodeQL, dependency review, NuGet vulnerability checks, tests, and human review; it does not replace them.

The repository workflow is intentionally disabled until the external SonarQube Cloud project is imported and its first baseline can be reviewed.

## One-time activation

1. Sign in to SonarQube Cloud with the GitHub account that administers `ATAC-Helicopter`.
2. Import `ATAC-Helicopter/Blueprints`.
3. Choose CI-based analysis and disable automatic analysis to avoid duplicate results.
4. Create a scoped analysis token.
5. Add the token as the repository Actions secret `SONAR_TOKEN`.
6. Add repository Actions variables:
   - `SONAR_ORGANIZATION`: the SonarQube Cloud organization key;
   - `SONAR_PROJECT_KEY`: the imported project key;
   - `SONAR_ENABLED`: `true`.
7. Run the **SonarQube** workflow manually.
8. Review every baseline reliability, security, maintainability, hotspot, and coverage finding.
9. Fix findings or record a justified disposition in SonarQube Cloud.
10. Require the `SonarQube analysis` status check on `develop` and `main` only after a clean successful run.

Do not store the token in Git, project files, logs, documentation, or signed Blueprints workspaces.

## Workflow behavior

- The scanner version is pinned in `.config/dotnet-tools.json`.
- Analysis uses the full Git history for new-code attribution.
- The workflow restores and builds the .NET 10 solution between SonarScanner begin/end steps.
- Tests emit Coverlet OpenCover reports from `Blueprints.Tests`.
- The job waits for the Sonar quality gate.
- Pull requests from forks are skipped because GitHub does not expose repository secrets to them.
- The workflow remains skipped while `SONAR_ENABLED` is not `true`.

## Local verification

The normal local verification does not require a Sonar token:

```sh
./scripts/verify.sh
```

Maintainers may reproduce analysis locally after exporting the three activation values, but must avoid shell history or diagnostic output that exposes `SONAR_TOKEN`.

## Response policy

- Treat new security findings and reviewed hotspots as release blockers until triaged.
- Do not weaken a quality profile merely to make a gate green.
- Do not classify a hotspot as safe without recording the relevant trust boundary and reasoning.
- Keep CodeQL enabled even when SonarQube Cloud is healthy.
- Never describe SonarQube output as a complete security audit.

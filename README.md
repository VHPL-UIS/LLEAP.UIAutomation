# LLEAP UI Test Automation

This repository contains automated end-to-end tests for Laerdal Learning
Application (LLEAP).

The tests can be started from GitHub without writing commands or changing
source code. They run on a dedicated Windows test machine and provide a simple
pass/fail summary, screenshots, detailed logs, and NUnit TRX reports.

## Automated tests

| Test | What it verifies |
| --- | --- |
| Virtual SimMan session | Starts a Healthy patient Virtual SimMan 3G session, changes patient settings, plays a voice, and ends the session. |
| Collect client log files | Opens the Help menu, collects the client logs, and verifies the generated log archive. |

When **All tests** is selected, the Virtual SimMan test runs first. The client
log test starts only after the first test has completed successfully.

## Run tests from GitHub

### Before starting

Confirm that:

- The Windows runner named or labelled `lleap-ui` is online.
- The self-hosted GitHub Actions runner is version 2.327.1 or newer.
- LLEAP is installed and configured on the runner.
- A Windows user is signed in and the desktop is unlocked.
- The GitHub Actions runner was started from an elevated PowerShell window.
- No person or other automated test is using LLEAP on that machine.

### Start a test run

1. Open this repository in GitHub.
2. Select the **Actions** tab.
3. Select **LLEAP UI Tests** in the left-hand list.
4. Select **Run workflow**.
5. Choose one of the following:

   - **All tests** — run both tests sequentially.
   - **Virtual SimMan session** — run only the session test.
   - **Collect client log files** — run only the client log test.

6. Select the green **Run workflow** button.
7. Open the new workflow run to follow its progress.

The manual **Run workflow** button is available when the workflow file is on
the repository's default branch. The user starting it must have write access
to the repository.

## Understand the result

At the top of the completed workflow run, the **LLEAP UI test results** summary
shows whether each test:

- Passed
- Failed
- Was cancelled
- Was not selected
- Was blocked because an earlier test failed

A green workflow result means every selected test passed. A red result means
at least one required preparation, build, test, summary, or upload step failed.

## Download screenshots and logs

The workflow saves its evidence even when a test fails.

1. Open the completed workflow run.
2. Scroll to **Artifacts**.
3. Download `lleap-ui-evidence-<run number>-<attempt number>`.
4. Extract the downloaded ZIP.

The artifact can contain:

- A TRX report for each selected test.
- Screenshots captured before and after each test step.
- A failure screenshot when available.
- Detailed Serilog execution logs.

Artifacts are retained for 14 days.

## Automatic execution

Every push to the `main` branch requests a complete run. The two tests execute
sequentially on the self-hosted LLEAP runner.

The `concurrency` setting prevents two workflow runs from controlling the
desktop at the same time. If another run is already active, the new run waits.

## Troubleshooting

| Symptom | Likely reason | Action |
| --- | --- | --- |
| Workflow remains queued | The `lleap-ui` runner is offline or its labels do not match. | Sign in to the Windows test machine and start the GitHub Actions runner. |
| Workflow fails during `Prepare test files` or `Prepare .NET 10` | The self-hosted runner is outdated. | Update the GitHub Actions runner to version 2.327.1 or newer. |
| Test reports that administrator access is required | The runner was not started with elevation. | Stop it and start `run.cmd` from an Administrator PowerShell window. |
| LLEAP window or control times out | The desktop is locked, an RDP session was disconnected, or another window covered/interrupted the application. | Unlock the interactive desktop, restore the session, close unrelated dialogs, and run again. |
| LLEAP executable is not found | LLEAP is not installed at the configured location. | Verify the installation and `Paths:SimulationHomeExePath` in `LLEAP.UITests/appsettings.json`. |
| Client log test cannot find the archive | The output path or path hint does not match the runner. | Check the test log and the client-log values under `Paths` in `LLEAP.UITests/appsettings.json`. |
| A test fails without an obvious reason | The detailed test evidence is needed. | Download the workflow artifact and inspect the final screenshot, Serilog log, and TRX report. |

## Run locally from PowerShell

Technical users can still run the complete suite directly:

```powershell
dotnet test LLEAP.UITests/LLEAP.UITests.csproj
```

Run the command from an elevated, interactive PowerShell session on a Windows
machine where LLEAP is installed.

## CI/CD design

The workflow in `.github/workflows/lleap-ui-tests.yml`:

1. Downloads the selected revision to the remote Windows runner.
2. Installs or selects .NET 10.
3. Restores and builds the test solution.
4. Runs the requested tests in a controlled sequence.
5. Publishes a readable summary and downloadable evidence.

This lets developers, business analysts, and manual testers execute the same
repeatable tests without editing C# or remembering command-line filters.
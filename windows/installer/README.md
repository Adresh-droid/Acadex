# Windows installer

The workflow `.github/workflows/windows-release.yml` builds, tests and publishes the native WPF application on Windows.

## CI validation

Pushing changes to `windows-native-client` runs restore, Release compilation, unit tests, and a self-contained win-x64 publish. The resulting app directory is uploaded as a short-lived Actions artifact.

## Creating Acadex-Setup.exe

After reviewing the branch and confirming the Windows CI run passes, create a version tag such as `windows-v0.1.0` on the approved commit. The workflow installs Inno Setup, compiles `Acadex.iss`, checks that `Acadex-Setup.exe` exists, uploads it as an artifact, and attaches it to a GitHub Release.

Do not tag an unvalidated commit or claim a release exists until the workflow completes successfully and the installer is listed as a release asset.

The installer is self-contained and is intended not to require the .NET SDK on the target PC. It leaves user-local settings/session data in LocalAppData on uninstall so that reinstall/upgrade doesn't casually destroy application state.

# Acadex Windows Client

Native C# WPF/MVVM desktop client for the existing Acadex service. It does not embed or reuse the website frontend.

## Current status

- Backend compatibility audit: `docs/windows-native-backend-compatibility.md`.
- The existing service authenticates JSON API calls with `Authorization: Bearer <token>`; `/api/auth/login` returns a 30-day server session token.
- Native desktop code is being developed independently of the web/PWA frontend.
- Installer and GitHub Release automation will be completed after a Windows Release build is confirmed.

## Backend URL

Default production API root: `https://acadex.onrender.com` is not hard-coded as the final truth. Set the base URL in app settings before release to the currently configured Acadex deployment URL. The desktop client allows overriding the API root for testing.

## Build

Requires Windows and the .NET 8 SDK to build. End users will get a self-contained Windows build through GitHub Actions when configured.

## Security

Never commit production credentials. Session token storage should use Windows DPAPI. All authorization and exam validation must remain server-side.

## Development

Open `windows/Acadex.Windows.sln` in Visual Studio, or run `dotnet build windows/Acadex.Windows.sln -c Release` on Windows with .NET 8 SDK.

See backend compatibility report before extending API workflows.
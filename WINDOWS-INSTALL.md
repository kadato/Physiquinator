# Physiquinator - Windows Installation Guide

## System Requirements

- **Windows 10** (version 1809 or later) or **Windows 11**
- **.NET 10 Desktop Runtime** (downloadable for free from Microsoft)
- **64-bit (x64) processor**

## Installation Options

### Option 1: Framework-Dependent (Recommended - 62 MB)

**Requirements:** .NET 10 Desktop Runtime must be installed

1. **Download .NET 10 Desktop Runtime**
   - Visit: https://dotnet.microsoft.com/download/dotnet/10.0
   - Download: ".NET Desktop Runtime 10.0.x (x64)"
   - Install the downloaded file (one-time, 5-minute setup)

2. **Download Physiquinator**
   - Download `Physiquinator-Windows.zip` from the [latest release](https://github.com/tothKarolyDavid/Physiquinator/releases/latest)
   - Extract the ZIP file to any folder
   - Run `Physiquinator.exe`

**Pros:**
- ✅ Smaller download (62 MB)
- ✅ Faster to extract and start
- ✅ Shares runtime with other .NET apps
- ✅ Easier to update

**Cons:**
- ❌ Requires one-time .NET 10 runtime installation

### Option 2: Portable with WindowsAppSDK (62 MB)

**Requirements:** .NET 10 Desktop Runtime

1. **Download Physiquinator-Portable**
   - Download `Physiquinator-Windows-Portable.zip` from the [latest release](https://github.com/tothKarolyDavid/Physiquinator/releases/latest)
   - Extract the ZIP file to any folder
   - Run `Physiquinator.exe`

**Pros:**
- ✅ Includes WindowsAppSDK runtime bundled
- ✅ Same size as Option 1

**Cons:**
- ❌ Still requires .NET 10 Desktop Runtime

> **Note:** Both options require .NET 10 Desktop Runtime because .NET MAUI uses the Mono runtime for cross-platform support. The runtime is free, small (~55 MB), and installs quickly.

## Why Can't I Get a Fully Standalone EXE?

.NET MAUI applications use the Mono runtime for cross-platform compatibility, which requires the .NET Desktop Runtime to be installed. This is the same for all .NET MAUI Windows apps. The runtime needs to be installed only once and works for all .NET 10 apps on your system.

## Troubleshooting

### "This application requires .NET Runtime"

**Solution:** Install .NET 10 Desktop Runtime
1. Visit: https://dotnet.microsoft.com/download/dotnet/10.0
2. Download "Desktop Runtime" (x64)
3. Install and restart the app

### "The application failed to start"

**Solution:** Install Visual C++ Redistributable
1. Visit: https://aka.ms/vs/17/release/vc_redist.x64.exe
2. Download and install
3. Restart your computer

### App doesn't start (no error)

**Solutions:**
1. Right-click `Physiquinator.exe` → Properties → Unblock → Apply
2. Run as Administrator (right-click → Run as administrator)
3. Check Windows Defender/Antivirus (it may be blocking the app)

### SmartScreen Warning

If you see "Windows protected your PC":
1. Click "More info"
2. Click "Run anyway"

This happens because the app isn't signed with a code signing certificate.

## Running from Source

If you prefer to run from source:

```powershell
# Clone the repository
git clone https://github.com/tothKarolyDavid/Physiquinator.git
cd Physiquinator

# Run the app
dotnet run --framework net10.0-windows10.0.19041.0
```

## Features

- 🏋️ Custom workout plan creation
- ⏱️ Smart rest timer with audio/vibration
- 📊 Real-time progress tracking
- 💾 Local SQLite storage (offline-first)
- 📤 Export/Import workout plans (JSON)
- 🎨 Modern dark theme UI

## Support

For issues or questions:
- [GitHub Issues](https://github.com/tothKarolyDavid/Physiquinator/issues)
- [Release Notes](https://github.com/tothKarolyDavid/Physiquinator/releases)

## Building Your Own

See [DOCKER.md](DOCKER.md) and [README.md](README.md) for build instructions.

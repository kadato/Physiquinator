# Install Physiquinator on Windows

## System requirements

- Windows 10 (version 1809 or later) or Windows 11 (x64)
- [.NET 11 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/11.0)

## Installation

1. Install the .NET 11 Desktop Runtime:
   - Download the installer: [.NET Desktop Runtime 11.0.x (x64)](https://dotnet.microsoft.com/download/dotnet/11.0).
   - Run the installer and follow the prompts.

2. Download and run Physiquinator:
   - Download `Physiquinator-Windows.zip` from the [latest GitHub release](https://github.com/kadato/Physiquinator/releases/latest).
   - Extract the ZIP archive to a folder of your choice.
   - Launch `Physiquinator.exe`.

## Troubleshooting

### "This application requires .NET Runtime"
The required desktop runtime is missing. Download and install [.NET 11 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/11.0), then restart the application.

### "The application failed to start"
The Visual C++ redistributable may be missing. Install the [Visual C++ Redistributable (x64)](https://aka.ms/vs/17/release/vc_redist.x64.exe) and restart your computer.

### Application does not launch
1. Right-click `Physiquinator.exe`, select **Properties**, check **Unblock** if present, and click **Apply**.
2. If corporate security policies apply, verify that Windows Defender or third-party endpoint protection allows the binary to execute.

### Windows SmartScreen prompt
If Windows displays "Windows protected your PC":
1. Click **More info**.
2. Click **Run anyway**.

This prompt appears because the release binary is not signed with a commercial certificate.

## Running from source

```powershell
git clone https://github.com/kadato/Physiquinator.git
cd Physiquinator
dotnet run --framework net11.0-windows10.0.19041.0
```

## Features

See [Features](README.md#features) in the README for the full list.

## Support

- [GitHub Issues](https://github.com/kadato/Physiquinator/issues)
- [Release Notes](https://github.com/kadato/Physiquinator/releases)



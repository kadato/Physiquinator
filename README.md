# Physiquinator

<div align="center">

![.NET MAUI](https://img.shields.io/badge/.NET_MAUI-10.0-512BD4?logo=dotnet)
![Blazor](https://img.shields.io/badge/Blazor-Hybrid-512BD4?logo=blazor)
![SQLite](https://img.shields.io/badge/SQLite-3-003B57?logo=sqlite)
![License](https://img.shields.io/github/license/tothKarolyDavid/Physiquinator)
![Platform](https://img.shields.io/badge/Platform-Android%20%7C%20Windows%20%7C%20iOS%20%7C%20macOS-blue)

A cross-platform workout tracking app built with .NET MAUI and Blazor Hybrid. Features rest timers, real-time progress tracking, and workout plan management.

[Download](#-download--install) • [Features](#-features) • [Tech Stack](#-tech-stack) • [Demo](#-demo) • [Getting Started](#-getting-started)

</div>

---

## 📥 Download & Install

> **Note**: Download links will be available once the repository is made public.

### Available Builds

**Latest Release**: [v1.0.2](https://github.com/tothKarolyDavid/Physiquinator/releases/latest) includes:

| Platform | Package | Size | Requirements |
|----------|---------|------|--------------|
| 🤖 Android | Physiquinator-Android.apk | 30 MB | Android 7.0+ |
| 🪟 Windows | Physiquinator-Windows.zip | 62 MB | .NET 10 Runtime |
| 🪟 Windows Portable | Physiquinator-Windows-Portable.zip | 62 MB | .NET 10 Runtime + WindowsAppSDK included |

> **Windows Users**: See [WINDOWS-INSTALL.md](WINDOWS-INSTALL.md) for detailed installation instructions and troubleshooting.

**Note:** Both Windows versions require .NET 10 Runtime. Fully self-contained builds (no runtime required) will be available once .NET 10 RTM is released.

---

**Option 1: Using Docker** (Recommended for Android - No Android SDK required)
```powershell
# Build Android APK
docker build -t physiquinator-android -f Dockerfile.android .
docker create --name temp physiquinator-android
docker cp temp:/app/output/com.companyname.physiquinator-Signed.apk ./Physiquinator.apk
docker rm temp
```

**Option 2: Build Windows Locally**
```powershell
# Build Windows application
dotnet publish Physiquinator.csproj `
  -f net10.0-windows10.0.19041.0 `
  -c Release `
  -p:WindowsPackageType=None `
  -p:SelfContained=false `
  -p:PublishTrimmed=false `
  -o ./artifacts/windows

# Run the app
./artifacts/windows/Physiquinator.exe
```

**Option 3: Using .NET SDK (All platforms)**
```bash
# Android (requires Android SDK)
dotnet build -t:Run -f net10.0-android

# Windows  
dotnet build -t:Run -f net10.0-windows10.0.19041.0

# iOS (Mac only, requires Xcode)
dotnet build -t:Run -f net10.0-ios
```

See [🐳 Docker Builds](#-docker-builds) or [🚀 Getting Started](#-getting-started) for detailed instructions.

#### Installation Instructions

**Android:**
1. Enable "Install from Unknown Sources" in **Settings** → **Security**
2. Transfer the APK to your device
3. Open the APK file and tap **Install**
4. Launch **Physiquinator** and start tracking! 🎉

**Windows:**
1. Extract the ZIP file
2. Run `Physiquinator.exe`
3. No installation required - runs standalone!
4. Note: Requires .NET 10 Runtime (or use published version with runtime included)

> **💡 Tip:** The app includes sample workout plans to get you started immediately!

---

## Features

### Workout Plan Management
- **Custom Workout Plans** - Design personalized routines with unlimited exercises
- **Flexible Configuration** - Set rest intervals and set counts per exercise
- **Quick Edit** - Modify plans anytime to match your progress
- **One-Tap Start** - Jump directly into workouts from the home screen

### Smart Rest Timer
- **Visual Countdown** - Large, easy-to-read timer display
- **Haptic Feedback** - Phone vibrates when rest time ends (mobile)
- **Audio Notifications** - Audible beep when rest is complete
- **Animation** - Green glow indicates rest completion
- **Full Control** - Pause, resume, reset, or skip rest periods

### Live Progress Tracking
- **Real-Time Updates** - See completed vs remaining sets instantly
- **Progress Bars** - Visual representation of workout completion
- **Exercise Grouping** - Completed and upcoming exercises clearly separated
- **Mobile-Optimized** - Upcoming exercises shown first on small screens

### Workout Completion Celebration
- **Animated Trophy** - Bouncing, rotating trophy icon
- **Victory Sound** - Ascending musical tones
- **Haptic Pattern** - Celebratory vibration sequence
- **Glowing Effect** - Pulsing green glow animation

### Data Management
- **Local SQLite Storage** - Fast, offline-first data persistence
- **Export to JSON** - Share plans across devices or back them up
- **Import Plans** - Load workout plans from JSON files
- **Bulk Export** - Export all plans at once for backup

---

## 🎬 Demo

> **📹 Demo Video**

### Screenshots

<!-- Add screenshots here once created -->
| Home Screen | Plan Editor | Workout in Progress |
|:----------:|:----------:|:-------------------:|
| ![Home](docs/screenshots/home.png) | ![Editor](docs/screenshots/editor.png) | ![Workout](docs/screenshots/workout.png) |

---

## Tech Stack

### Core Technologies
- **[.NET 10](https://dotnet.microsoft.com/)** - Latest .NET with performance improvements
- **[.NET MAUI](https://dotnet.microsoft.com/apps/maui)** - Cross-platform native UI framework
- **[Blazor Hybrid](https://learn.microsoft.com/aspnet/core/blazor/hybrid/)** - Rich web UI in native apps
- **[SQLite](https://www.sqlite.org/)** via [sqlite-net-pcl](https://github.com/praeclarum/sqlite-net) - Local database

### UI & Styling
- **[Bootstrap 5](https://getbootstrap.com/)** - Responsive design system
- **[Bootstrap Icons](https://icons.getbootstrap.com/)** - Icon library
- **Custom CSS Animations** - Smooth, modern UI effects
- **Dark Theme** - Eye-friendly design with gradient accents

### Features & APIs
- **MAUI Essentials** - File picker, share, vibration APIs
- **System.Timers** - Rest timer implementation
- **Web Audio API** - Browser-based notification sounds

### DevOps & CI/CD
- **GitHub Actions** - Automated build and release pipeline
- **Multi-Platform Builds** - Android APK and Windows executables
- **Automated Releases** - Tag-based release creation

---

## 🚀 Getting Started

### Prerequisites

- **[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)** (Preview)
- **[Visual Studio 2026](https://visualstudio.microsoft.com/)** with MAUI workload, or
- **[Visual Studio Code](https://code.visualstudio.com/)** with C# Dev Kit
- **Android SDK** - For Android development (via Visual Studio)
- **Xcode** - For iOS/macOS development (Mac only)

### Quick Start

```bash
# Clone the repository
git clone https://github.com/tothKarolyDavid/Physiquinator.git
cd Physiquinator

# Restore dependencies
dotnet restore

# Run on Windows
dotnet build -t:Run -f net10.0-windows10.0.19041.0

# Run on Android (device/emulator)
dotnet build -t:Run -f net10.0-android

# Run on iOS (Mac only)
dotnet build -t:Run -f net10.0-ios
```

### Build Configurations

#### Debug (Fast Development)
- Shared runtime for quick deployment
- No AOT compilation
- Fast wireless debugging on Android

#### Release (Optimized Performance)
- Full linking and trimming
- AOT compilation enabled
- App bundle (AAB) format for Android
- Self-contained Windows build

### 🐳 Docker Builds

Build Android APK using Docker (no Android SDK setup required):

```powershell
# Build and extract APK
docker build -t physiquinator-android -f Dockerfile.android .
docker create --name temp physiquinator-android
docker cp temp:/app/output/com.companyname.physiquinator-Signed.apk ./Physiquinator.apk
docker rm temp
```

**See [DOCKER.md](DOCKER.md) for complete Docker setup and troubleshooting.**

---

## 🎯 Key Design Decisions

### Why .NET MAUI + Blazor Hybrid?
- **Single Codebase** - Write once, run everywhere (Android, iOS, Windows, macOS)
- **Web Skills** - Use Blazor/HTML/CSS for UI while maintaining native performance
- **Native APIs** - Full access to platform features (vibration, file system, etc.)
- **Familiar Stack** - Leverages existing .NET and web development knowledge

### Why SQLite?
- **Offline-First** - Works without internet connection
- **Fast** - Excellent performance for local queries
- **Cross-Platform** - Same database file format across all platforms
- **Zero Configuration** - No server setup required

### Why Bootstrap?
- **Rapid Development** - Pre-built responsive components
- **Consistent Design** - Predictable UI patterns
- **Mobile-First** - Optimized for small screens by default

---
<div align="center">




</div>

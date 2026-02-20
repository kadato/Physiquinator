# Physiquinator

A cross-platform workout tracking app built with .NET MAUI and Blazor, featuring rest timers, progress tracking, and workout plan management.

<!-- ![Physiquinator Home Screen](docs/screenshots/home.png) -->

## Features

### Workout Plan Management
- **Create Custom Plans**: Design your own workout routines with multiple exercises
- **Flexible Configuration**: Set custom rest intervals and set counts per exercise
- **Edit Anytime**: Modify existing plans to match your progress
- **Quick Start**: Jump directly into your workout from the home screen

<!-- ![Plan Editor](docs/screenshots/plan-editor.png) -->

### Live Workout Sessions
- **Interactive Timer**: Visual rest timer with pause, resume, and skip controls
- **Real-time Progress**: Track completed sets with instant visual feedback
- **Set Completion**: Mark sets as done and move through your workout
- **Progress Bar**: See your workout progress at a glance

<!-- ![Workout Timer](docs/screenshots/workout-timer.gif) -->

### Data Persistence & Sync
- **Local SQLite Database**: All your workout plans saved locally
- **JSON Export/Import**: Share workout plans across devices
- **Cross-Platform Sync**: Export plans as JSON files and import on any device
- **Backup Support**: Export all plans for backup purposes

<!-- ![Export Import](docs/screenshots/export-import.png) -->

## Tech Stack

- **Framework**: [.NET 10](https://dotnet.microsoft.com/)
- **UI**: [.NET MAUI](https://dotnet.microsoft.com/apps/maui) with [Blazor WebView](https://learn.microsoft.com/aspnet/core/blazor/hybrid/)
- **Database**: [SQLite](https://www.sqlite.org/) via [sqlite-net-pcl](https://github.com/praeclarum/sqlite-net)
- **Styling**: [Bootstrap 5](https://getbootstrap.com/) + [Bootstrap Icons](https://icons.getbootstrap.com/)

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Visual Studio 2026](https://visualstudio.microsoft.com/) or [Visual Studio Code](https://code.visualstudio.com/)
- For Android: Android SDK (installed via Visual Studio)
- For iOS/macOS: Xcode (Mac required)

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/tothKarolyDavid/Physiquinator.git
   cd Physiquinator
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore
   ```

3. **Build the project**
   ```bash
   dotnet build
   ```

4. **Run on your platform**

   **Windows:**
   ```bash
   dotnet build -t:Run -f net10.0-windows10.0.19041.0
   ```

   **Android:**
   ```bash
   dotnet build -t:Run -f net10.0-android
   ```

   **iOS (Mac only):**
   ```bash
   dotnet build -t:Run -f net10.0-ios
   ```

   **macOS (Mac only):**
   ```bash
   dotnet build -t:Run -f net10.0-maccatalyst
   ```

## Configuration

The app uses SQLite for local storage. The database is automatically created at:
- **Windows**: `%LOCALAPPDATA%\Physiquinator\physiquinator.db3`
- **Android**: `/data/data/com.companyname.physiquinator/files/physiquinator.db3`
- **iOS/macOS**: `~/Library/Application Support/physiquinator.db3`

# Docker Build Setup

This directory contains Docker configurations for building Physiquinator in isolated, reproducible environments.

## Available Dockerfiles

### 1. `Dockerfile.android` - .NET 9 Android Build (STABLE)
✅ **Recommended for production releases**

Builds Android APK using .NET 9.0.100 (stable release) with guaranteed package availability.

```bash
# Build the Docker image
docker build -f Dockerfile.android -t physiquinator-android .

# Run the build
docker run --rm -v $(pwd)/artifacts:/app/artifacts physiquinator-android

# Or use docker-compose
docker-compose up android-build
```

### 2. `Dockerfile.net10-experimental` - .NET 10 Preview (EXPERIMENTAL)
⚠️ **NOT WORKING YET** - Runtime packages unavailable

Attempts to build with .NET 10 preview. Will fail until Microsoft publishes runtime packages.

```bash
# Try experimental .NET 10 build (will likely fail)
docker build -f Dockerfile.net10-experimental -t physiquinator-net10 .
```

## GitHub Actions Integration

The `docker-release.yml` workflow uses Docker for Android builds:

- ✅ Isolated build environment
- ✅ Pinned .NET SDK version (9.0.100)
- ✅ Reproducible builds
- ✅ Consistent across all runners

## Why Docker?

### Problems Solved:
1. **SDK Version Drift** - GitHub Actions may install newer SDK versions without runtime packages
2. **Environment Consistency** - Same build environment everywhere (local, CI/CD)
3. **Android SDK Management** - All Android dependencies included
4. **NuGet Source Control** - Explicit control over package sources

### Advantages:
- ✅ Reproducible builds
- ✅ No reliance on GitHub Actions SDK versions
- ✅ Easy to test locally
- ✅ Can upgrade .NET versions independently

### Trade-offs:
- ⚠️ Longer initial build time (Docker image pull)
- ⚠️ Larger cache usage
- ⚠️ Slightly more complex setup

## Local Testing

Test the Docker build locally before pushing:

```bash
# Build Android APK
docker build -f Dockerfile.android -t physiquinator-android .
docker run --rm -v $(pwd)/artifacts:/app/artifacts physiquinator-android

# Check output
ls -l artifacts/android/
```

## Future: .NET 10 Support

Once .NET 10 runtime packages are available:

1. Update `Dockerfile.net10-experimental` with correct package sources
2. Test locally: `docker build -f Dockerfile.net10-experimental .`
3. If successful, rename to `Dockerfile.android` (replace stable)
4. Update `docker-release.yml` to use .NET 10
5. Update `global.json` to specify .NET 10 SDK

## Notes

- **Windows builds** still use native GitHub Actions runners (Docker Windows containers have limitations)
- **Android builds** benefit most from Docker isolation
- **iOS/macOS builds** require macOS runners (not in scope for Docker on Linux)

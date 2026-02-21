# Docker Build Setup

This directory contains Docker configurations for building the Physiquinator .NET MAUI application in containerized environments.

## Prerequisites

- **Docker Desktop** installed and running
  - Download: https://www.docker.com/products/docker-desktop
  - On Windows: Docker Desktop must be started
- **8GB RAM minimum** (16GB recommended for Android builds)
- **20GB free disk space**

## Files

- **`Dockerfile.android`** - Builds Android APK

## Quick Start

### Build Android APK

```powershell
# Build the Docker image
docker build -t physiquinator-android -f Dockerfile.android .
```

The built APK will be in `/app/output` inside the container.

### Extract APK from Container

```powershell
# Build the image
docker build -t physiquinator-android -f Dockerfile.android .

# Create temporary container and extract APK
docker create --name temp physiquinator-android
docker cp temp:/app/output/com.companyname.physiquinator-Signed.apk ./Physiquinator.apk
docker rm temp
```

## Build Times

- **Android build**: ~10-15 minutes (first build), ~2-8 seconds (cached)

## Build Outputs

### Android APK Location
After successful build, the APK is located at:
- `/app/output/com.companyname.physiquinator-Signed.apk` (~31MB)

## Troubleshooting

### "Docker daemon is not running"

**Solution**: Start Docker Desktop and wait for it to fully initialize.

### "Not enough memory"

**Solution**: Increase Docker memory allocation:
1. Open Docker Desktop
2. Settings → Resources → Memory
3. Allocate at least 8GB

### "No space left on device"

**Solution**: Clean up Docker resources:
```powershell
docker system prune -a
```

### Build fails with Android SDK errors

**Solution**: Clear cache and rebuild:
```powershell
docker build --no-cache -t physiquinator-android -f Dockerfile.android .
```

## CI/CD Integration

These Dockerfiles are designed to work with GitHub Actions workflows.

Example workflow step:
```yaml
- name: Build Android APK
  run: |
    docker build -t physiquinator-android -f Dockerfile.android .
    docker create --name temp physiquinator-android
    docker cp temp:/app/output/com.companyname.physiquinator-Signed.apk ./Physiquinator.apk
    docker rm temp
```

## Technical Details

### Dockerfile.android

- **Base Image**: `mcr.microsoft.com/dotnet/sdk:10.0`
- **Android SDK**: Version 35-36 (Android 15)
- **Build Tools**: 35.0.0, 36.0.0
- **Java**: OpenJDK 17
- **Output**: APK (unsigned, for testing/distribution)

## Best Practices

1. **Test builds locally** before pushing to CI/CD
2. **Use cache** for faster iterations
3. **Monitor resources** during builds (CPU, memory, disk)

## Performance Tips

- **Multi-stage builds**: Used to minimize final image size
- **Layer caching**: Dependencies are restored in separate layer
- **Docker BuildKit**: Enable for faster builds:
  ```powershell
  $env:DOCKER_BUILDKIT=1
  ```

## Support

For issues:
1. Check Docker Desktop is running
2. Review error messages during build
3. Try rebuilding without cache: `docker build --no-cache ...`
4. Check [GitHub Issues](https://github.com/tothKarolyDavid/Physiquinator/issues)

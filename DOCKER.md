# Build the Android APK in Docker
 
Docker configuration for building the Physiquinator Android APK in a container.
 
## Prerequisites
 
- [Docker Desktop](https://www.docker.com/products/docker-desktop) installed and running
- 8 GB RAM minimum (16 GB recommended)
- 20 GB free disk space
 
## Build instructions
 
### 1. Build the Docker image
 
```powershell
docker build -t physiquinator-android -f Dockerfile.android .
```
 
The compiled APK will be located at `/app/output/com.companyname.physiquinator-Signed.apk` inside the container image.
 
### 2. Extract the APK artifact
 
```powershell
docker create --name temp physiquinator-android
docker cp temp:/app/output/com.companyname.physiquinator-Signed.apk ./Physiquinator.apk
docker rm temp
```
 
## CI integration
 
Example GitHub Actions step:
 
```yaml
- name: Build Android APK
  run: |
    docker build -t physiquinator-android -f Dockerfile.android .
    docker create --name temp physiquinator-android
    docker cp temp:/app/output/com.companyname.physiquinator-Signed.apk ./Physiquinator.apk
    docker rm temp
```
 
## Image specifications
 
- **Base image:** `mcr.microsoft.com/dotnet/sdk:11.0`
- **Android SDK:** API 35-36 (Android 15)
- **Build Tools:** 35.0.0, 36.0.0
- **JDK:** OpenJDK 17
- **Output:** Unsigned/Signed APK for testing and distribution
 
## Troubleshooting
 
### Daemon not running
Ensure Docker Desktop is started before running build commands.
 
### Out of memory
Increase the memory allocated to Docker Desktop to at least 8 GB under Settings, Resources, Memory.
 
### Disk space exhaustion
Prune dangling containers and build cache:
```powershell
docker system prune -a
```
 
### Stale cache build failures
Rebuild without cached layers:
```powershell
docker build --no-cache -t physiquinator-android -f Dockerfile.android .
```


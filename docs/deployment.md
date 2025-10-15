# PhotoBoothX Deployment Guide

## 🚀 Automated CI/CD Pipeline

### Overview
PhotoBoothX uses GitHub Actions for automated building, testing, and release creation. When you push to the `production` branch, it automatically:

1. **Builds** the application
2. **Runs tests** (if available)
3. **Creates installer** using Inno Setup
4. **Publishes release** on GitHub with download links

### Triggering a Release

#### Option 1: Push to Production Branch
```bash
# Switch to production branch
git checkout production

# Merge your changes
git merge main

# Push to trigger CI/CD
git push origin production
```

#### Option 2: Manual Release
1. Go to GitHub → Actions
2. Run "Build and Deploy PhotoBoothX" workflow manually
3. Select the branch you want to build from

### Version Management
- Update version in `PhotoBooth/PhotoBooth.csproj`
- The CI/CD pipeline automatically uses this version for:
  - Installer filename: `PhotoBoothX-Setup-{version}.exe`
  - GitHub release tag: `v{version}`
  - Application metadata

## 📦 Local Building and Testing

### Prerequisites
1. **Visual Studio 2022** or **VS Code** with C# extension
2. **.NET 8.0 SDK**
3. **Inno Setup 6** (for installer creation)
   - Download: https://www.jrsoftware.org/isinfo.php
   - Install to default location: `C:\Program Files (x86)\Inno Setup 6\`

### Quick Build Commands

#### Build Application Only
```powershell
.\build.ps1
```

#### Build with Custom Version
```powershell
.\build.ps1 -Version "1.2.0"
```

#### Build without Installer
```powershell
.\build.ps1 -SkipInstaller
```

#### Open Output Directory After Build
```powershell
.\build.ps1 -OpenOutput
```

### Manual Build Steps
```powershell
# 1. Restore packages
dotnet restore PhotoBooth/PhotoBooth.csproj

# 2. Build application
dotnet build PhotoBooth/PhotoBooth.csproj --configuration Release

# 3. Publish for deployment
dotnet publish PhotoBooth/PhotoBooth.csproj --configuration Release --output "PhotoBooth/bin/Release/net8.0-windows/win-x64/publish" --self-contained true --runtime win-x64 /p:PublishSingleFile=true

# 4. Create installer (requires Inno Setup)
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer/PhotoBoothX.iss
```

## 📋 Client Installation Instructions

### System Requirements
- **Operating System**: Windows 10/11 (64-bit)
- **RAM**: 4GB minimum, 8GB recommended
- **Storage**: 500MB for application + space for photos/database
- **USB Ports**: For camera, Arduino, printer
- **Administrator Access**: Required for installation

### Installation Steps for Client

1. **Download Installer**
   - Go to: `https://github.com/[your-username]/PhotoBoothX/releases/latest`
   - Download `PhotoBoothX-Setup-{version}.exe`

2. **Run Installation**
   - Right-click installer → "Run as Administrator"
   - Follow installation wizard
   - ✅ Check "Launch PhotoBoothX automatically when Windows starts" (recommended)
   - ❌ Uncheck "Create desktop icon" (optional for kiosks)

3. **Post-Installation**
   - Application installs to: `C:\Program Files\PhotoBoothX\`
   - Database creates in: `%APPDATA%\PhotoboothX\`
   - Templates folder: `C:\Program Files\PhotoBoothX\Templates\` (user-updatable)

4. **First Launch**
   - Application launches automatically after installation
   - **Default admin credentials**:
     - **Master Admin**: Username: `admin`, Password: `admin123`
     - **User Admin**: Username: `user`, Password: `user123`
   - **⚠️ IMPORTANT**: You will be forced to change these passwords on first login!

### Kiosk Mode Setup
The installer automatically configures:
- ✅ **Auto-launch** on Windows startup
- ✅ **Full-screen** application mode
- ✅ **Database** in AppData (survives reinstalls)
- ✅ **Templates** with update permissions

## 🔧 Troubleshooting Installation

### Common Issues

#### "Application failed to start"
- **Cause**: .NET 8.0 not installed
- **Solution**: Installer includes .NET runtime, but if issues persist:
  ```
  Download: https://dotnet.microsoft.com/download/dotnet/8.0
  Install: "ASP.NET Core Runtime 8.0.x (Windows x64)"
  ```

#### "Access Denied" during installation
- **Cause**: Insufficient permissions
- **Solution**: Right-click installer → "Run as Administrator"

#### "Templates not updating"
- **Cause**: Folder permissions
- **Solution**: 
  ```
  Navigate to: C:\Program Files\PhotoBoothX\Templates\
  Right-click → Properties → Security → Edit
  Give "Users" group "Modify" permissions
  ```

#### Application won't auto-start
- **Cause**: Registry entry missing
- **Solution**: Re-run installer or manually add to startup:
  ```
  Win+R → shell:startup
  Create shortcut to: C:\Program Files\PhotoBoothX\PhotoBooth.exe
  ```

### Log Locations
- **Application Logs**: `%APPDATA%\PhotoboothX\logs\`
- **Database**: `%APPDATA%\PhotoboothX\photobooth.db`
- **Installation Log**: `%TEMP%\Setup Log {timestamp}.txt`

## 🔄 Updates and Maintenance

### Automatic Updates (Future Feature)
- Application will check for updates on startup
- Prompts admin for update approval
- Downloads and installs new version automatically

### Manual Updates
1. Download new installer from GitHub releases
2. Run installer (automatically detects existing installation)
3. Database and settings are preserved
4. Templates are updated but custom ones preserved

### Backup Recommendations
```powershell
# Backup database and settings
Copy-Item "$env:APPDATA\PhotoboothX" "C:\PhotoBoothX-Backup-$(Get-Date -Format 'yyyy-MM-dd')" -Recurse

# Backup custom templates
Copy-Item "C:\Program Files\PhotoBoothX\Templates" "C:\Templates-Backup-$(Get-Date -Format 'yyyy-MM-dd')" -Recurse
```

## 📞 Support and Monitoring

### Remote Access Setup
- Client gets access to GitHub releases page
- Download links are always available at: `/releases/latest`
- Release notes include installation instructions and changelog

### Fleet Management (Future)
- Web dashboard for monitoring multiple kiosks
- Remote configuration updates
- Sales data aggregation
- Health monitoring and alerts

## 🔐 Security Considerations

### Production Deployment
- ✅ Change default admin passwords immediately
- ✅ Use strong passwords for admin accounts
- ✅ Limit physical access to kiosk PC
- ✅ Configure Windows auto-lock after inactivity
- ✅ Disable unnecessary Windows features (future enhancement)

### Network Security
- Application works offline (no network required for basic operation)
- Future web features will require HTTPS and secure authentication
- Database is local SQLite (no network exposure)

---

## 📋 Quick Reference

### File Locations
| Component | Location |
|-----------|----------|
| Application | `C:\Program Files\PhotoBoothX\` |
| Database | `%APPDATA%\PhotoboothX\photobooth.db` |
| Templates | `C:\Program Files\PhotoBoothX\Templates\` |
| Logs | `%APPDATA%\PhotoboothX\logs\` |

### Default Credentials
| Account | Username | Password | Access Level |
|---------|----------|----------|--------------|
| Master Admin | `admin` | `admin123` | Full access |
| User Admin | `user` | `user123` | View-only + volume |

**Security**: You will be forced to change these passwords immediately on first login.

### Build Commands
| Command | Purpose |
|---------|---------|
| `.\build.ps1` | Full build with installer |
| `.\build.ps1 -SkipInstaller` | Build app only |
| `.\build.ps1 -Version "1.2.0"` | Build with specific version |
| `.\build.ps1 -OpenOutput` | Build and open output folder |

---

**🤖 Ready for deployment!** Your client can now download and install PhotoBoothX from GitHub releases. 
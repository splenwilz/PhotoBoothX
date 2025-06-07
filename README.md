# PhotoBoothX - Professional Kiosk Application

## 🚀 Quick Start for Client

### Download Latest Release
1. Go to: **[Releases Page](../../releases/latest)**
2. Download `PhotoBoothX-Setup-{version}.exe`
3. Run as Administrator
4. Follow installation wizard
5. Application auto-launches and starts on Windows boot

### Default Login Credentials
- **Master Admin**: `admin` / `admin123` (full access)
- **User Admin**: `user` / `user123` (view-only + volume)
- **⚠️ Change passwords immediately after installation!**

## 🏗️ Development & Deployment

### Automated CI/CD Pipeline
- **Push to `production` branch** → Automatic build, test, and release
- **GitHub Releases** → Client downloads installer automatically
- **Version Management** → Update `PhotoBooth/PhotoBooth.csproj` version

### Local Development
```powershell
# Build application only
.\build.ps1 -SkipInstaller

# Build with installer (requires Inno Setup)
.\build.ps1

# Build specific version
.\build.ps1 -Version "1.2.0"
```

### Prerequisites for Local Building
- Visual Studio 2022 or VS Code
- .NET 8.0 SDK
- Inno Setup 6 (for installer creation)

## 📋 Current Implementation Status

### ✅ Completed Features
- **Complete UI Framework**: Welcome, Product Selection, Template Selection, Admin Dashboard
- **Database System**: SQLite with comprehensive schema for sales, templates, settings
- **Admin Backend**: Two-level access, sales reports, template management, settings
- **Professional Installer**: Auto-launch, proper permissions, kiosk-friendly
- **CI/CD Pipeline**: Automated building and releases on GitHub

### 🔄 Next Phase (Hardware Integration)
- Camera Service (Logitech C920)
- Printer Service (DNP RX1hs)
- Arduino Service (LED control, payment detection)
- Photo Capture Flow (Payment → Camera → Review → Print)

## 📁 Project Structure
```
PhotoBoothX/
├── PhotoBooth/              # Main WPF application
│   ├── Services/            # Database, License, Notification services
│   ├── Models/              # Data models and DTOs
│   ├── Controls/            # Custom UI controls
│   ├── Templates/           # Photo templates (user-updatable)
│   └── *.xaml/.cs          # UI screens and logic
├── installer/               # Inno Setup installer configuration
├── .github/workflows/       # CI/CD pipeline
├── build.ps1               # Local build script
└── DEPLOYMENT.md           # Detailed deployment guide
```

## 🔧 System Requirements
- **OS**: Windows 10/11 (64-bit)
- **RAM**: 4GB minimum, 8GB recommended
- **Storage**: 500MB + space for photos/database
- **Hardware**: USB ports for camera, Arduino, printer
- **Installation**: Administrator privileges required

## 📞 Support & Documentation
- **Deployment Guide**: [DEPLOYMENT.md](DEPLOYMENT.md)
- **Latest Releases**: [GitHub Releases](../../releases)
- **Issues & Support**: [GitHub Issues](../../issues)

---

**Ready for client deployment!** 🎉

Your client can now download and install PhotoBoothX from GitHub releases while you continue development on hardware integration.
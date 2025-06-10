# Development Setup Guide

This guide will help you set up a local development environment for PhotoBoothX.

## 🛠️ Prerequisites

### Required Software
- **Visual Studio 2022** (Community, Professional, or Enterprise)
  - Workloads: `.NET desktop development`, `Universal Windows Platform development`
- **.NET 8.0 SDK** (included with Visual Studio 2022)
- **Git for Windows** (with Git Bash)
- **Windows 10/11 SDK** (latest version)

### Optional but Recommended
- **Inno Setup 6** - For creating installers locally
- **SQLite Browser** - For database inspection and debugging
- **Windows Terminal** - Better PowerShell experience
- **Arduino IDE** - For hardware prototyping and testing

## 🚀 Initial Setup

### 1. Clone the Repository
```powershell
# Clone the repository
git clone https://github.com/splenwilz/PhotoBoothX.git
cd PhotoBoothX

# Verify the structure
dir
```

### 2. Open in Visual Studio
```powershell
# Open the solution in Visual Studio
start PhotoBooth.sln
```

### 3. Restore NuGet Packages
In Visual Studio:
1. Right-click on Solution in Solution Explorer
2. Select "Restore NuGet Packages"
3. Wait for all packages to be restored

### 4. Build the Application
```powershell
# Command line build
dotnet build PhotoBooth/PhotoBooth.csproj --configuration Debug

# Or use Visual Studio: Build > Build Solution (Ctrl+Shift+B)
```

## 🔧 Development Workflow

### Local Building and Testing

#### Quick Build (Application Only)
```powershell
# Build without installer creation
.\build.ps1 -SkipInstaller
```

#### Full Build (With Installer)
```powershell
# Requires Inno Setup to be installed
.\build.ps1

# Build specific version
.\build.ps1 -Version "1.2.0"
```

#### Debug in Visual Studio
1. Set `PhotoBooth` as startup project
2. Press F5 to start debugging
3. Application will launch in windowed mode for development

### Database Development

#### SQLite Database Location
- **Development**: `PhotoBooth/bin/Debug/net8.0-windows/photobooth.db`
- **Production**: `%APPDATA%/PhotoBoothX/photobooth.db`

#### Database Tools
```powershell
# Install SQLite Browser via Chocolatey
choco install sqlitebrowser

# Or download from: https://sqlitebrowser.org/
```

### Template Development
- Templates are stored in `PhotoBooth/Templates/`
- Use 300 DPI resolution for print quality
- Support formats: PNG, JPG, PDF
- Test templates in the Template Selection screen

## 🧪 Testing

### Unit Tests
```powershell
# Run all tests
dotnet test Photobooth.Tests/Photobooth.Tests.csproj

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Manual Testing Checklist
- [ ] Application launches correctly
- [ ] Navigation between screens works
- [ ] Admin login with default credentials
- [ ] Database operations (sales, templates, settings)
- [ ] Template selection and preview
- [ ] Settings persistence
- [ ] Window management (fullscreen/windowed)

## 🔄 CI/CD Integration

### GitHub Actions Workflow
The CI/CD pipeline automatically:
1. Builds the application on push to `production` branch
2. Runs tests (if present)
3. Creates installer with Inno Setup
4. Publishes GitHub release with installer

### Local CI/CD Testing
```powershell
# Test the build process locally
.\build.ps1 -Version "test-1.0.0"

# Verify installer creation
dir dist/
```

## 📁 Project Structure

```
PhotoBoothX/
├── PhotoBooth/                    # Main WPF Application
│   ├── App.xaml(.cs)             # Application entry point
│   ├── MainWindow.xaml(.cs)      # Main window and navigation
│   ├── Services/                 # Business logic services
│   │   ├── DatabaseService.cs    # SQLite database operations
│   │   ├── LicenseService.cs     # License validation
│   │   └── NotificationService.cs # UI notifications
│   ├── Models/                   # Data models and DTOs
│   │   ├── Product.cs           # Product/pricing models
│   │   ├── Sale.cs              # Sales transaction models
│   │   └── Template.cs          # Photo template models
│   ├── Controls/                 # Custom WPF controls
│   │   ├── AdminPanel.xaml      # Admin interface
│   │   ├── ProductSelection.xaml # Product selection UI
│   │   └── TemplateSelection.xaml # Template selection UI
│   ├── Templates/               # Photo templates (user-updatable)
│   ├── Styles/                  # WPF styles and themes
│   └── photobooth.db           # SQLite database (created at runtime)
├── installer/                   # Inno Setup installer
│   └── PhotoBoothX.iss         # Installer script
├── .github/workflows/          # GitHub Actions CI/CD
│   └── deploy.yml              # Build and release workflow
├── docs/                       # Developer documentation
├── build.ps1                   # Local build script
└── README.md                   # User-focused documentation
```

## 🐛 Debugging and Troubleshooting

### Common Issues

#### Build Errors
```powershell
# Clean and rebuild
dotnet clean
dotnet build --configuration Debug
```

#### Database Issues
```powershell
# Delete database to reset
Remove-Item PhotoBooth/bin/Debug/net8.0-windows/photobooth.db
```

#### NuGet Package Issues
```powershell
# Clear NuGet cache
dotnet nuget locals all --clear
dotnet restore
```

### Debugging Tips
1. **Use Visual Studio Debugger**: Set breakpoints in service classes
2. **Check Output Window**: View build and debug messages
3. **Enable Native Code Debugging**: For hardware integration issues
4. **Use Exception Helper**: Review exception details in VS

### Performance Monitoring
- Monitor memory usage during photo operations
- Check UI thread responsiveness
- Profile database queries for optimization
- Test with multiple template loading

## 🔧 Hardware Development

### Camera Testing (Future)
```csharp
// Camera service will be implemented here
// Use UWP Camera APIs or DirectShow
```

### Printer Testing (Future)
```csharp
// Printer service for DNP RX1hs
// Use manufacturer SDKs or Windows Print API
```

### Arduino Integration (Future)
```csharp
// Serial communication for LED control
// Payment system pulse detection
```

## 📦 Release Process

### Version Management
1. Update version in `PhotoBooth/PhotoBooth.csproj`
2. Update changelog/release notes
3. Test installer creation locally
4. Push to `production` branch for automatic release

### Manual Release Steps
```powershell
# 1. Update version
# Edit PhotoBooth/PhotoBooth.csproj

# 2. Build and test
.\build.ps1 -Version "1.2.0"

# 3. Test installer
.\dist\PhotoBoothX-Setup-1.2.0.exe

# 4. Commit and push to production
git add .
git commit -m "Release version 1.2.0"
git push origin production
```

## 📞 Getting Help

- **GitHub Issues**: Report bugs and request features
- **GitHub Discussions**: Ask questions and share ideas
- **Code Review**: Submit pull requests for contributions
- **Documentation**: Update docs with improvements

---

**Ready to contribute?** Check out the [Contributing Guidelines](contributing.md) next. 
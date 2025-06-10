# PhotoBoothX Developer Documentation

This directory contains all developer-focused documentation for PhotoBoothX.

## 📚 Documentation Index

### Development Guides
- **[Development Setup](development.md)** - Local development environment, building, and testing
- **[Project Structure](project-structure.md)** - Codebase organization and architecture
- **[Deployment Guide](deployment.md)** - CI/CD pipeline and release process
- **[Contributing](contributing.md)** - Guidelines for contributors and code standards

### Technical References
- **[API Reference](api-reference.md)** - Service interfaces and data models
- **[Database Schema](database-schema.md)** - SQLite database structure and relationships
- **[Hardware Integration](hardware-integration.md)** - Camera, printer, and Arduino setup
- **[Configuration](configuration.md)** - Settings, templates, and customization

### Deployment & Operations
- **[CI/CD Pipeline](ci-cd.md)** - Automated building and releases
- **[Installer Configuration](installer.md)** - Inno Setup and distribution
- **[Release Notes](release-notes.md)** - Version history and roadmap
- **[Troubleshooting](troubleshooting.md)** - Common issues and solutions

## 🚀 Quick Start for Developers

```powershell
# Clone repository
git clone https://github.com/splenwilz/PhotoBoothX.git
cd PhotoBoothX

# Build application only
.\build.ps1 -SkipInstaller

# Build with installer (requires Inno Setup)
.\build.ps1

# Open in Visual Studio
start PhotoBooth.sln
```

## 📋 Current Status

### ✅ Completed (70% Complete)
- Complete UI Framework with WPF/XAML
- SQLite database with comprehensive schema
- Two-level admin system with authentication
- Professional installer with kiosk configuration
- CI/CD pipeline with GitHub Actions
- Sales tracking and reporting system

### 🔄 In Progress (Hardware Integration Phase)
- Camera Service (Logitech C920)
- Printer Service (DNP RX1hs) 
- Arduino Service (LED control, payment detection)
- Complete photo capture workflow

### 🎯 Architecture Goals
- **Reliability**: 24/7 kiosk operation without intervention
- **Performance**: Fast photo capture and printing
- **Maintainability**: Easy troubleshooting and updates
- **Flexibility**: Configurability for different venues

## 🔧 Development Environment

### Prerequisites
- Visual Studio 2022 or VS Code with C# extension
- .NET 8.0 SDK
- Git for version control
- Inno Setup 6 (for installer creation)

### Recommended Tools
- SQLite Browser for database inspection
- Postman or similar for API testing
- Windows SDK for hardware integration
- Arduino IDE for hardware prototyping

## 📞 Developer Support

- **Issues**: Use GitHub Issues for bug reports and feature requests
- **Discussions**: GitHub Discussions for architecture and design questions  
- **Pull Requests**: Follow the contributing guidelines
- **Code Review**: All changes require review before merging

---

**Ready to contribute to PhotoBoothX?** Start with the [Development Setup](development.md) guide. 
# PhotoBoothX

A professional photobooth kiosk application built with C#/WPF for Windows, featuring enterprise-grade security, offline operation, and comprehensive hardware integration.

## Overview

PhotoBoothX is a production-ready kiosk application designed for unattended operation in commercial environments. The codebase emphasizes security, reliability, and maintainability with a modular architecture, comprehensive test coverage, and automated CI/CD deployment.

## Technology Stack

### Core Technologies
- **.NET 8.0** - Modern C# runtime with performance optimizations
- **WPF (Windows Presentation Foundation)** - Rich desktop UI framework
- **SQLite** - Embedded database for local data persistence
- **xUnit** - Unit and integration testing framework

### Security & Cryptography
- **PBKDF2-SHA256** - Password hashing and key derivation
- **HMAC-SHA256** - Master password generation and validation
- **Windows DPAPI** - Secure credential storage with machine-specific encryption
- **Rate Limiting** - Brute-force attack prevention

### Build & Deployment
- **GitHub Actions** - Automated CI/CD pipeline
- **Inno Setup** - Professional Windows installer creation
- **PowerShell** - Build automation and deployment scripts

## Architecture

### Project Structure
```text
PhotoBooth/                  # Main WPF application
├── Services/               # Business logic and core services
│   ├── DatabaseService.cs  # SQLite data access layer
│   ├── MasterPasswordService.cs
│   ├── TemplateManager.cs
│   └── ...
├── Models/                 # Data models and entities
├── Controls/              # Reusable WPF controls
├── Converters/            # XAML value converters
├── Configuration/         # Application configuration
└── Assets/               # Images, icons, sounds

Photobooth.Tests/         # Test project (xUnit)
├── Services/             # Service unit tests
├── Integration/          # Integration tests
└── Mocks/               # Test doubles and mocks

installer/                # Inno Setup installer configuration
docs/                    # Technical documentation
website/                 # Next.js support tool for master password generation
```

### Key Design Patterns
- **Service Layer Pattern** - Business logic encapsulation
- **Repository Pattern** - Data access abstraction
- **MVVM (Model-View-ViewModel)** - UI separation of concerns
- **Dependency Injection** - Loose coupling and testability

## Development Setup

### Prerequisites
- **Visual Studio 2022** (17.8 or later) or **Rider 2024.1+**
- **.NET 8.0 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Windows 10/11** - Required for WPF development
- **Node.js 20+** (LTS) - Required to run the Next.js support tool in `website/`
- **Git** - Version control

### Building from Source

```powershell
# Clone the repository
git clone https://github.com/splenwilz/PhotoBoothX.git
cd PhotoBoothX

# Restore NuGet packages
dotnet restore PhotoBooth/PhotoBooth.csproj

# Build the application
dotnet build PhotoBooth/PhotoBooth.csproj --configuration Debug

# Run the application
dotnet run --project PhotoBooth/PhotoBooth.csproj
```

### Running Tests

```powershell
# Run all tests
dotnet test Photobooth.Tests/Photobooth.Tests.csproj

# Run with coverage
dotnet test Photobooth.Tests/Photobooth.Tests.csproj `
  --collect:"XPlat Code Coverage" `
  --settings:PhotoBooth/coverlet.runsettings

# Generate coverage report
dotnet tool install --global dotnet-reportgenerator-globaltool
reportgenerator -reports:TestResults/**/coverage.cobertura.xml `
  -targetdir:TestResults/CoverageReport -reporttypes:Html
```

**Current Test Coverage**: 92.4% (63 tests)

## Security Features

### Authentication & Authorization
- **Multi-level admin system** - Master and User access levels
- **PIN-based password recovery** - Offline password reset capability
- **Master password system** - Enterprise support access with HMAC-based OTP
- **Rate limiting** - 5 attempts with 1-minute lockout
- **Audit logging** - All security events tracked

### Data Protection
- **Windows DPAPI encryption** - Machine-specific credential storage
- **Embedded database schema** - Prevents tampering with schema files
- **Foreign key constraints** - Data integrity enforcement
- **Parameterized queries** - SQL injection prevention

### Build Security
- **Debug symbols removed** - Release builds exclude PDB files
- **Config file auto-deletion** - Sensitive files removed after initialization
- **Secret injection via CI/CD** - No secrets in source code
- **Signed installers** - (Planned) Code signing for authenticity

## CI/CD Pipeline

### Automated Workflows
The project uses GitHub Actions for continuous integration and deployment:

- **Test Branch** - Pre-release testing (`v{version}-test`)
- **Master/Main Branch** - Staging releases (`v{version}-staging`)
- **Production Branch** - Stable releases (`v{version}`)

### Build Process
1. **Checkout code** - Clone repository and submodules
2. **Restore dependencies** - NuGet package restoration
3. **Build application** - Release configuration with optimizations
4. **Run tests** - Execute test suite with coverage reporting
5. **Inject secrets** - Add master password config (if available)
6. **Create installer** - Build Windows installer with Inno Setup
7. **Create release** - Upload artifacts to GitHub Releases
8. **Notify** - Deployment status notifications

See [`.github/workflows/deploy.yml`](.github/workflows/deploy.yml) for full pipeline configuration.

## Database Schema

PhotoBoothX uses SQLite for local data persistence:

- **AdminUsers** - User accounts and authentication
- **Settings** - Application configuration
- **Templates** - Photo layout templates
- **Transactions** - Sales and payment records
- **UsedMasterPasswords** - Replay attack prevention

Schema is embedded as a resource in the application DLL for security. See [`PhotoBooth/Database_Schema.sql`](PhotoBooth/Database_Schema.sql) for complete schema.

## Contributing

We welcome contributions! Please see our [Contributing Guide](docs/contributing.md) for:
- Code style guidelines
- Commit message conventions
- Pull request process
- Development workflow
- Testing requirements

### Quick Contribution Guide
1. Fork the repository
2. Create a feature branch (`git checkout -b feature/your-feature`)
3. Write tests for your changes
4. Ensure all tests pass
5. Commit with descriptive messages (`git commit -m 'Add feature: short summary'`)
6. Push to your fork (`git push origin feature/your-feature`)
7. Open a Pull Request

## Documentation

### Technical Documentation
- [Project Structure](docs/project-structure.md) - Detailed codebase organization
- [Development Guide](docs/development.md) - Development environment setup
- [Deployment Guide](docs/deployment.md) - Release and deployment process
- [Security Hardening](docs/SECURITY_HARDENING.md) - Security measures and best practices
- [Release Notes](docs/release-notes.md) - Version history and changelog

### API Documentation
- Service layer interfaces are documented with XML comments
- Database schema includes inline documentation
- XAML controls include usage examples

## License

This project is proprietary software. See [LICENSE.txt](LICENSE.txt) for details.

## Support

- **Bug Reports**: [GitHub Issues](../../issues)
- **Feature Requests**: [GitHub Discussions](../../discussions)
- **Security Issues**: See [SECURITY.md](SECURITY.md) for responsible disclosure

---

### Built with .NET 8.0 | Maintained by the PhotoBoothX Team
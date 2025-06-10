# Project Structure and Architecture

This document provides a detailed overview of the PhotoBoothX codebase structure and architectural decisions.

## 📁 Repository Structure

```
PhotoBoothX/
├── 📂 PhotoBooth/                 # Main WPF Application
│   ├── 📄 App.xaml               # Application definition and startup
│   ├── 📄 App.xaml.cs            # Application lifecycle and global settings
│   ├── 📄 MainWindow.xaml        # Main application window
│   ├── 📄 MainWindow.xaml.cs     # Navigation and window management
│   ├── 📄 PhotoBooth.csproj      # Project file with dependencies and build settings
│   │
│   ├── 📂 Services/               # Business Logic Services
│   │   ├── 📄 DatabaseService.cs # SQLite database operations and queries
│   │   ├── 📄 LicenseService.cs  # License validation and management
│   │   └── 📄 NotificationService.cs # UI notifications and messaging
│   │
│   ├── 📂 Models/                 # Data Models and DTOs
│   │   ├── 📄 Product.cs         # Product definitions and pricing
│   │   ├── 📄 Sale.cs            # Sales transaction models
│   │   ├── 📄 Template.cs        # Photo template definitions
│   │   ├── 📄 User.cs            # User account and authentication
│   │   └── 📄 Setting.cs         # Application configuration settings
│   │
│   ├── 📂 Controls/               # Custom WPF User Controls
│   │   ├── 📄 AdminPanel.xaml    # Administration interface
│   │   ├── 📄 ProductSelection.xaml # Product selection screen
│   │   ├── 📄 TemplateSelection.xaml # Template selection screen
│   │   ├── 📄 WelcomeScreen.xaml # Welcome/splash screen
│   │   └── 📄 PaymentScreen.xaml # Payment processing screen
│   │
│   ├── 📂 Templates/              # Photo Templates (User-Updatable)
│   │   ├── 📄 basic-template.png # Default template examples
│   │   ├── 📄 wedding-template.png
│   │   └── 📄 corporate-template.png
│   │
│   ├── 📂 Styles/                 # WPF Styles and Themes
│   │   ├── 📄 MainStyles.xaml    # Primary application styles
│   │   ├── 📄 ButtonStyles.xaml  # Button styling and animations
│   │   └── 📄 TouchStyles.xaml   # Touch-friendly control styles
│   │
│   └── 📂 bin/Debug/              # Build output directory
│       └── 📄 photobooth.db      # SQLite database (created at runtime)
│
├── 📂 Photobooth.Tests/           # Unit Tests Project
│   ├── 📄 Photobooth.Tests.csproj # Test project configuration
│   ├── 📄 DatabaseServiceTests.cs # Database service unit tests
│   ├── 📄 LicenseServiceTests.cs  # License validation tests
│   └── 📄 ModelTests.cs           # Data model validation tests
│
├── 📂 installer/                  # Inno Setup Installer Configuration
│   └── 📄 PhotoBoothX.iss        # Installer script with kiosk configuration
│
├── 📂 .github/workflows/          # GitHub Actions CI/CD
│   └── 📄 deploy.yml             # Automated build and release workflow
│
├── 📂 docs/                       # Developer Documentation
│   ├── 📄 README.md              # Documentation index
│   ├── 📄 development.md         # Development setup guide
│   ├── 📄 project-structure.md   # This file
│   └── 📄 deployment.md          # Deployment and release guide
│
├── 📄 build.ps1                   # Local build script for development
├── 📄 README.md                   # User-focused project documentation
├── 📄 .gitignore                  # Git ignore rules
├── 📄 .gitattributes              # Git file handling attributes
├── 📄 LICENSE.txt                 # Software license
└── 📄 EULA.txt                    # End-user license agreement
```

## 🏗️ Architecture Overview

### Design Patterns

#### MVVM (Model-View-ViewModel) - Selectively Applied
- **Models**: Data classes in `Models/` folder
- **Views**: XAML files in `Controls/` folder
- **ViewModels**: Integrated into code-behind for simplicity and reliability
- **Rationale**: Kiosk applications prioritize reliability over strict patterns

#### Service Layer Pattern
- **Services**: Business logic abstracted into service classes
- **Dependency Injection**: Services injected where needed
- **Separation of Concerns**: UI, business logic, and data access separated

#### Repository Pattern (Simplified)
- **DatabaseService**: Acts as repository for all data operations
- **Single Responsibility**: Each service handles one concern
- **Testability**: Services can be mocked for unit testing

### Component Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    Presentation Layer                   │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐     │
│  │   XAML UI   │  │   Controls  │  │   Styles    │     │
│  │   (Views)   │  │ (UserCtrl)  │  │  (Themes)   │     │
│  └─────────────┘  └─────────────┘  └─────────────┘     │
└─────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────┐
│                    Business Layer                       │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐     │
│  │  Database   │  │   License   │  │Notification │     │
│  │   Service   │  │   Service   │  │   Service   │     │
│  └─────────────┘  └─────────────┘  └─────────────┘     │
└─────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────┐
│                     Data Layer                          │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐     │
│  │   SQLite    │  │   Models    │  │   Config    │     │
│  │  Database   │  │  (Entities) │  │   Files     │     │
│  └─────────────┘  └─────────────┘  └─────────────┘     │
└─────────────────────────────────────────────────────────┘
```

## 🔧 Key Components

### 1. Main Application (App.xaml/MainWindow.xaml)

#### App.xaml
- Application entry point and global resources
- Startup event handling
- Global exception handling
- Application-wide styles and themes

#### MainWindow.xaml
- Primary window container
- Navigation framework
- Kiosk mode window management
- Screen switching logic

### 2. Services Layer

#### DatabaseService.cs
```csharp
public class DatabaseService
{
    // SQLite connection management
    // CRUD operations for all entities
    // Database initialization and schema creation
    // Transaction management
    // Data validation and integrity
}
```

#### LicenseService.cs
```csharp
public class LicenseService
{
    // License key validation
    // Hardware fingerprinting
    // Expiration checking
    // Feature flag management
}
```

#### NotificationService.cs
```csharp
public class NotificationService
{
    // UI notification system
    // Toast messages
    // Error reporting
    // Status updates
}
```

### 3. Models (Data Layer)

#### Core Entities
- **Product**: Pricing and product definitions
- **Sale**: Transaction records and payment tracking
- **Template**: Photo template metadata and file references
- **User**: Authentication and role management
- **Setting**: Application configuration and preferences

#### Data Transfer Objects (DTOs)
- Simplified models for UI binding
- Validation attributes
- Property change notification

### 4. Controls (Presentation Layer)

#### Screen-Based Navigation
- **WelcomeScreen**: Initial customer interaction
- **ProductSelection**: Choose pricing and options
- **TemplateSelection**: Choose photo layout
- **PaymentScreen**: Process payment (future hardware)
- **AdminPanel**: Administrative interface

#### Touch-Optimized Design
- Large buttons (minimum 80px)
- High contrast colors
- Clear visual feedback
- No hover states (touch-only)

## 🗄️ Database Design

### SQLite Schema
```sql
-- Core Tables
CREATE TABLE Products (
    Id INTEGER PRIMARY KEY,
    Name TEXT NOT NULL,
    Price DECIMAL(10,2) NOT NULL,
    IsActive BOOLEAN DEFAULT 1
);

CREATE TABLE Sales (
    Id INTEGER PRIMARY KEY,
    ProductId INTEGER REFERENCES Products(Id),
    Amount DECIMAL(10,2) NOT NULL,
    Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
    TemplateId INTEGER REFERENCES Templates(Id)
);

CREATE TABLE Templates (
    Id INTEGER PRIMARY KEY,
    Name TEXT NOT NULL,
    FilePath TEXT NOT NULL,
    IsActive BOOLEAN DEFAULT 1,
    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE Users (
    Id INTEGER PRIMARY KEY,
    Username TEXT UNIQUE NOT NULL,
    PasswordHash TEXT NOT NULL,
    Role TEXT NOT NULL, -- 'Admin', 'User'
    IsActive BOOLEAN DEFAULT 1
);

CREATE TABLE Settings (
    Key TEXT PRIMARY KEY,
    Value TEXT NOT NULL,
    Description TEXT
);
```

### Database Locations
- **Development**: `PhotoBooth/bin/Debug/net8.0-windows/photobooth.db`
- **Production**: `%APPDATA%/PhotoBoothX/photobooth.db`
- **Rationale**: AppData survives application reinstalls

## 🎨 UI/UX Architecture

### WPF Control Hierarchy
```
MainWindow
├── Grid (Main Container)
│   ├── ContentPresenter (Screen Container)
│   │   └── UserControl (Current Screen)
│   │       ├── WelcomeScreen
│   │       ├── ProductSelection
│   │       ├── TemplateSelection
│   │       └── AdminPanel
│   └── StatusBar (Optional)
```

### Style System
- **MainStyles.xaml**: Core application styling
- **ButtonStyles.xaml**: Touch-optimized button styles
- **TouchStyles.xaml**: Kiosk-specific UI elements
- **Color Scheme**: High contrast for visibility
- **Typography**: Large, readable fonts

### Responsive Design Considerations
- **Fixed Resolution**: Designed for 1920x1080 kiosk displays
- **Scaling**: DPI-aware for different screen densities
- **Touch Targets**: Minimum 80px for finger interaction
- **Visual Feedback**: Clear pressed/selected states

## 🔐 Security Architecture

### Authentication System
- **Two-Level Access**: Admin (full) and User (limited)
- **Password Hashing**: BCrypt for secure storage
- **Session Management**: In-memory session tracking
- **Auto-Logout**: Timeout after inactivity

### Data Protection
- **Database Encryption**: SQLite encryption (future)
- **Sensitive Data**: Passwords hashed, no plain text storage
- **Access Control**: Role-based feature access
- **Audit Trail**: Transaction logging for compliance

## 🔌 Hardware Integration Architecture (Future)

### Service Abstraction Pattern
```csharp
// Planned hardware services
public interface ICameraService
{
    Task<byte[]> CapturePhotoAsync();
    Task StartPreviewAsync();
    Task StopPreviewAsync();
}

public interface IPrinterService
{
    Task<bool> PrintPhotoAsync(byte[] imageData);
    Task<PrinterStatus> GetStatusAsync();
    Task<bool> IsReadyAsync();
}

public interface IArduinoService
{
    Task SendLEDCommand(LEDPattern pattern);
    Task<bool> ReadPaymentPulse();
    Task<bool> TestConnection();
}
```

### Hardware Abstraction Benefits
- **Testability**: Mock hardware for development
- **Flexibility**: Support multiple hardware vendors
- **Reliability**: Graceful degradation when hardware fails
- **Maintainability**: Hardware changes don't affect UI

## 📦 Build and Deployment Architecture

### Build Process
1. **Source Code**: Git repository with branch-based workflow
2. **Compilation**: .NET 8.0 self-contained deployment
3. **Packaging**: Inno Setup creates Windows installer
4. **Distribution**: GitHub Releases for client downloads

### CI/CD Pipeline
- **Trigger**: Push to `production` branch
- **Build**: GitHub Actions Windows runner
- **Test**: Automated unit tests (when present)
- **Package**: Create installer with Inno Setup
- **Deploy**: Publish GitHub release automatically

### Installation Architecture
```
C:\Program Files\PhotoBoothX\        # Application files
├── PhotoBooth.exe                   # Main executable
├── Templates\                       # User-updatable templates
└── Dependencies\                    # .NET runtime and libraries

%APPDATA%\PhotoBoothX\              # User data directory
├── photobooth.db                   # SQLite database
├── Logs\                           # Application logs
└── Settings\                       # Configuration files
```

## 🔄 Future Extensibility

### Planned Enhancements
- **Hardware Services**: Camera, Printer, Arduino integration
- **Cloud Sync**: Remote monitoring and template updates
- **Multi-Language**: Internationalization support
- **Advanced Analytics**: Detailed reporting and insights
- **Fleet Management**: Multi-kiosk administration

### Architecture Considerations
- **Modular Design**: Components can be enhanced independently
- **Service Interfaces**: Hardware abstraction for flexibility
- **Configuration-Driven**: Minimize code changes for customization
- **Backward Compatibility**: Database schema versioning

---

**Next**: Learn about the [Development Workflow](development.md) or [Deployment Process](deployment.md). 
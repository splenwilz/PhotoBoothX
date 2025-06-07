# PhotoBooth Test Suite

This test suite follows industry-standard organization patterns for maintainability and scalability.

## Test Organization

### 📁 `/Services/`
Unit tests for business logic and service classes:
- **AuthenticationServiceTests.cs** - Admin authentication, user management, and access control
- **DatabaseServiceTests.cs** - Database operations, CRUD functionality, and data integrity  
- **NotificationServiceTests.cs** - Notification system and UI feedback mechanisms

### 📁 `/Controls/`
Unit tests for custom UI controls and components:
- **ConfirmationDialogTests.cs** - Dialog functionality and user interaction validation

### 📁 `/Screens/`
Unit tests for application screens and views:
- **AdminDashboardScreenTests.cs** - Dashboard functionality and administrative features

### 📁 `/Integration/`
Integration tests that verify component interactions (placeholder for future tests):
- *Currently empty - reserved for future integration test scenarios*

## Test Coverage

- **61 Total Tests** ✅ All Passing
- **Authentication & Security**: User creation, login validation, access levels
- **Database Operations**: CRUD operations, settings management, data integrity
- **UI Controls**: Dialog behavior, confirmation workflows
- **Notification System**: Toast notifications, error handling, user feedback
- **Admin Dashboard**: Business settings, user management, system configuration

## Code Coverage Analysis

### 📊 Overall Coverage: **12.8%**
- **Covered Lines**: 521 / 4,052 coverable lines
- **Branch Coverage**: 12% (141 / 1,173 branches)
- **Method Coverage**: 11% (57 / 514 methods)

### 🎯 Well-Tested Components (High Coverage):
- **DatabaseResult Models**: 100% ✅
- **ProductConfiguration**: 100% ✅  
- **ProductInfo**: 100% ✅
- **ProductSelectedEventArgs**: 100% ✅
- **AdminUser Model**: 81.8% ✅
- **Setting Model**: 77.7% ✅
- **BusinessInfo Model**: 57.1% ✅
- **DatabaseService**: 47.5% 🟡

### 🔍 Areas Needing Test Coverage (0% Coverage):
- **UI Screens**: AdminDashboardScreen, AdminLoginScreen, WelcomeScreen
- **UI Controls**: ConfirmationDialog, NotificationToast
- **Models**: Customer, PrintJob, Transaction, Template (and related)
- **Application Entry**: App, MainWindow

### 📋 Coverage Priority Recommendations:
1. **High Priority**: UI Controls (ConfirmationDialog, NotificationToast) - Core functionality
2. **Medium Priority**: Screen logic extraction to testable services
3. **Low Priority**: Model classes (mostly data containers)

## Running Tests

```bash
# Run all tests
dotnet test

# Run tests with verbose output
dotnet test --verbosity normal

# Run tests with code coverage
dotnet test --collect:"XPlat Code Coverage" --results-directory:"./TestResults"

# Start 
start CoverageReport/index.html

# Generate coverage report
reportgenerator -reports:"TestResults/*/coverage.cobertura.xml" -targetdir:"CoverageReport" -reporttypes:Html

# Run tests for specific category
dotnet test --filter "TestCategory=Services"
dotnet test --filter "TestCategory=Controls"
dotnet test --filter "TestCategory=Screens"
```

## Test Conventions

- **Naming**: `[ComponentName]Tests.cs`
- **Namespaces**: `Photobooth.Tests.[Category]`
- **Methods**: `[MethodName]_[Scenario]_[ExpectedResult]()`
- **Arrange-Act-Assert**: Clear test structure with meaningful assertions

## Notes

- All tests are designed to run without creating actual UI dialogs
- Database tests use temporary files to avoid connection sharing issues
- Tests focus on business logic validation rather than UI automation
- No external dependencies required - all tests are self-contained
- **Coverage reports are generated in `CoverageReport/` folder** - Open `index.html` for detailed analysis 
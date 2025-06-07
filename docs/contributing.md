# Contributing to PhotoBoothX

Thank you for your interest in contributing to PhotoBoothX! This guide will help you get started with contributing to our professional photobooth software.

## 🤝 How to Contribute

### Types of Contributions
- **Bug Reports**: Help us identify and fix issues
- **Feature Requests**: Suggest new functionality
- **Code Contributions**: Submit pull requests with improvements
- **Documentation**: Improve guides and documentation
- **Testing**: Help test new features and releases

## 🐛 Reporting Issues

### Bug Reports
When reporting bugs, please include:

1. **Clear Title**: Descriptive summary of the issue
2. **Environment**: 
   - Windows version
   - .NET version
   - PhotoBoothX version
   - Hardware configuration (if relevant)
3. **Steps to Reproduce**: Detailed steps to recreate the issue
4. **Expected Behavior**: What should happen
5. **Actual Behavior**: What actually happens
6. **Screenshots**: If applicable
7. **Logs**: Include relevant log files or console output

#### Bug Report Template
```markdown
**Bug Description**
A clear description of what the bug is.

**Environment**
- OS: Windows 10/11
- PhotoBoothX Version: 1.0.0
- Hardware: Camera/Printer model (if relevant)

**Steps to Reproduce**
1. Go to '...'
2. Click on '....'
3. Scroll down to '....'
4. See error

**Expected Behavior**
What you expected to happen.

**Actual Behavior**
What actually happened.

**Screenshots**
If applicable, add screenshots to help explain your problem.

**Additional Context**
Any other context about the problem here.
```

### Feature Requests
For new features, please include:
- **Use Case**: Why is this feature needed?
- **Description**: What should the feature do?
- **Mockups**: Visual representations if applicable
- **Priority**: How important is this feature?

## 💻 Code Contributions

### Development Workflow

#### 1. Fork and Clone
```bash
# Fork the repository on GitHub, then clone your fork
git clone https://github.com/splenwilz/PhotoBoothX.git
cd PhotoBoothX

# Add upstream remote
git remote add upstream https://github.com/splenwilz/PhotoBoothX.git
```

#### 2. Create a Branch
```bash
# Create a feature branch
git checkout -b feature/your-feature-name

# Or bug fix branch
git checkout -b bugfix/issue-description
```

#### 3. Make Changes
- Follow the coding standards (see below)
- Write meaningful commit messages
- Test your changes thoroughly
- Update documentation if needed

#### 4. Commit Changes
```bash
# Stage your changes
git add .

# Commit with descriptive message
git commit -m "Add feature: describe what you implemented"
```

#### 5. Push and Create Pull Request
```bash
# Push to your fork
git push origin feature/your-feature-name

# Create pull request on GitHub
```

### Pull Request Guidelines

#### Before Submitting
- [ ] Code follows project coding standards
- [ ] All tests pass (if applicable)
- [ ] Documentation updated (if needed)
- [ ] No merge conflicts with main branch
- [ ] Descriptive commit messages
- [ ] Pull request description explains changes

#### Pull Request Template
```markdown
**Description**
Brief description of changes made.

**Type of Change**
- [ ] Bug fix (non-breaking change that fixes an issue)
- [ ] New feature (non-breaking change that adds functionality)
- [ ] Breaking change (fix or feature that would cause existing functionality to not work as expected)
- [ ] Documentation update

**Testing**
- [ ] I have performed self-review of my own code
- [ ] I have tested the changes locally
- [ ] My changes generate no new warnings
- [ ] I have added tests that prove my fix is effective or that my feature works

**Screenshots** (if applicable)
Add screenshots to demonstrate the changes.

**Additional Notes**
Any additional information about the changes.
```

## 📝 Coding Standards

### C# Code Style

#### Naming Conventions
```csharp
// Classes and Methods: PascalCase
public class DatabaseService
{
    public async Task<List<Product>> GetProductsAsync()
    {
        // Implementation
    }
}

// Private fields: camelCase with underscore
private readonly DatabaseService _databaseService;
private bool _isInitialized;

// Local variables and parameters: camelCase
public void ProcessOrder(int orderId, string customerName)
{
    var orderDetails = GetOrderDetails(orderId);
    // Implementation
}

// Constants: PascalCase
public const string DefaultConnectionString = "Data Source=photobooth.db";
```

#### Code Organization
```csharp
// File structure
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
// ... other using statements

namespace PhotoBooth.Services
{
    /// <summary>
    /// Service for managing database operations
    /// </summary>
    public class DatabaseService : IDisposable
    {
        // Fields
        private readonly string _connectionString;
        private SQLiteConnection _connection;

        // Constructors
        public DatabaseService(string connectionString)
        {
            _connectionString = connectionString;
        }

        // Properties
        public bool IsConnected { get; private set; }

        // Public methods
        public async Task InitializeAsync()
        {
            // Implementation
        }

        // Private methods
        private void EnsureConnection()
        {
            // Implementation
        }

        // Dispose pattern
        public void Dispose()
        {
            _connection?.Dispose();
        }
    }
}
```

#### Error Handling
```csharp
// Use proper exception handling
public async Task<Product> GetProductAsync(int id)
{
    try
    {
        // Database operation
        return await _databaseService.GetProductAsync(id);
    }
    catch (InvalidOperationException ex)
    {
        _logger.LogError(ex, "Failed to retrieve product {ProductId}", id);
        throw new ApplicationException($"Product {id} not found", ex);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error retrieving product {ProductId}", id);
        throw;
    }
}
```

### XAML Guidelines

#### Naming Conventions
```xml
<!-- Controls: PascalCase with descriptive names -->
<Button x:Name="StartPhotoSessionButton"
        Content="Start Photo Session"
        Click="StartPhotoSessionButton_Click" />

<!-- Resources: PascalCase -->
<Style x:Key="TouchButtonStyle" TargetType="Button">
    <!-- Style definition -->
</Style>
```

#### Code Organization
```xml
<!-- File structure -->
<UserControl x:Class="PhotoBooth.Controls.WelcomeScreen"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    
    <!-- Resources -->
    <UserControl.Resources>
        <!-- Styles and resources -->
    </UserControl.Resources>
    
    <!-- Main content -->
    <Grid>
        <!-- UI elements with proper naming and structure -->
    </Grid>
</UserControl>
```

### Documentation Standards

#### XML Documentation
```csharp
/// <summary>
/// Represents a photo template with layout and metadata
/// </summary>
public class Template
{
    /// <summary>
    /// Gets or sets the unique identifier for the template
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// Loads a template from the specified file path
    /// </summary>
    /// <param name="filePath">Path to the template file</param>
    /// <returns>A task that represents the asynchronous load operation</returns>
    /// <exception cref="FileNotFoundException">Thrown when template file is not found</exception>
    public async Task LoadFromFileAsync(string filePath)
    {
        // Implementation
    }
}
```

## 🧪 Testing Guidelines

### Unit Testing
```csharp
[Test]
public async Task GetProductAsync_ValidId_ReturnsProduct()
{
    // Arrange
    var service = new DatabaseService(TestConnectionString);
    var expectedProduct = new Product { Id = 1, Name = "Test Product" };
    
    // Act
    var result = await service.GetProductAsync(1);
    
    // Assert
    Assert.IsNotNull(result);
    Assert.AreEqual(expectedProduct.Name, result.Name);
}

[Test]
public async Task GetProductAsync_InvalidId_ThrowsException()
{
    // Arrange
    var service = new DatabaseService(TestConnectionString);
    
    // Act & Assert
    Assert.ThrowsAsync<InvalidOperationException>(
        async () => await service.GetProductAsync(-1));
}
```

### Integration Testing
- Test complete workflows (welcome → product selection → template selection)
- Verify database operations work correctly
- Test UI navigation and state management

### Manual Testing Checklist
- [ ] Application starts correctly
- [ ] All screens load without errors
- [ ] Navigation works between screens
- [ ] Admin panel authentication works
- [ ] Database operations complete successfully
- [ ] Settings persist correctly
- [ ] Application handles edge cases gracefully

## 📋 Code Review Process

### Review Criteria
1. **Functionality**: Does the code work as intended?
2. **Code Quality**: Is the code clean, readable, and maintainable?
3. **Performance**: Are there any performance concerns?
4. **Security**: Are there any security vulnerabilities?
5. **Testing**: Are tests adequate and passing?
6. **Documentation**: Is the code properly documented?

### Review Checklist
- [ ] Code follows project standards
- [ ] No hardcoded values or magic numbers
- [ ] Proper error handling implemented
- [ ] Resource disposal handled correctly
- [ ] Thread safety considered where needed
- [ ] Performance implications reviewed
- [ ] Security best practices followed

## 🏷️ Version Control

### Branch Naming Convention
- `feature/description` - New features
- `bugfix/issue-description` - Bug fixes
- `hotfix/critical-issue` - Critical production fixes
- `docs/update-description` - Documentation updates

### Commit Message Format
```
type(scope): short description

Optional longer description explaining the change.

Fixes #123
```

Types:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation only
- `style`: Formatting changes
- `refactor`: Code restructuring
- `test`: Adding tests
- `chore`: Maintenance tasks

Examples:
```
feat(database): add product search functionality

Implements full-text search across product names and descriptions.
Includes pagination support for large result sets.

Fixes #45
```

## 🚀 Release Process

### Version Numbering
We use [Semantic Versioning](https://semver.org/):
- `MAJOR.MINOR.PATCH` (e.g., 1.2.3)
- **MAJOR**: Breaking changes
- **MINOR**: New features, backward compatible
- **PATCH**: Bug fixes, backward compatible

### Release Workflow
1. Feature development on feature branches
2. Merge to `main` for testing
3. Merge to `production` for release
4. Automated CI/CD creates release
5. GitHub release with installer

## 📞 Getting Help

### Communication Channels
- **GitHub Issues**: Technical questions and bug reports
- **GitHub Discussions**: General questions and ideas
- **Pull Request Comments**: Code-specific discussions

### Before Asking for Help
1. Check existing issues and documentation
2. Search previous discussions
3. Provide clear reproduction steps
4. Include relevant code snippets
5. Specify your environment details

---

## 🎯 Current Priorities

### High Priority
- Hardware integration (Camera, Printer, Arduino)
- Photo capture and printing workflow
- Payment system integration
- Advanced error handling and logging

### Medium Priority
- Template management improvements
- Enhanced reporting and analytics
- Multi-language support
- Cloud synchronization features

### Low Priority
- UI/UX enhancements
- Advanced kiosk management
- Fleet management features
- Performance optimizations

---

**Thank you for contributing to PhotoBoothX!** 🎉

Your contributions help make PhotoBoothX the best photobooth software for businesses worldwide. 
# PhotoBooth Application Documentation

## Developer Documentation

### Database & Data Management
- **[Database Migration System](./DatabaseMigrations.md)** - Complete guide to the incremental migration system for schema changes
  - How migrations work
  - Adding new migrations
  - Best practices and examples
  - Troubleshooting guide

### Quick Links

#### Database Migration Quick Reference
- **Current Schema Version**: 1
- **Adding Migration**: Update `GetExpectedSchemaVersion()` → Add to `ApplyIncrementalMigrations()` → Implement `ApplyMigrationVX()`
- **Database Location**: `%APPDATA%/PhotoboothX/photobooth.db`
- **Reset Database**: Delete database file, restart app

#### Key Files
- `Services/DatabaseService.cs` - Main database service and migration logic
- `Database_Schema.sql` - Initial database schema for new installations
- `Models/` - Entity classes that map to database tables

## Architecture Overview

```
PhotoBooth Application
├── UI (WPF)
│   ├── Windows/         # Main application windows
│   ├── Views/           # User interface screens
│   └── Controls/        # Reusable UI components
├── Services/
│   ├── DatabaseService  # Database operations & migrations
│   ├── AuthService      # User authentication
│   └── SettingsService  # Application settings
├── Models/              # Data entities
└── Database_Schema.sql  # Initial database structure
```

## Getting Started

1. **Clone Repository**
2. **Build Solution** - Database will be created automatically on first run
3. **Default Login**: 
   - **Secure Setup**: Random passwords generated during installation
- **Credentials**: Located in `Desktop\PhotoBoothX-Setup-Credentials` (auto-deletes after setup)

## Development Guidelines

### Database Changes
- Always follow the [migration system](./DatabaseMigrations.md)
- Test migrations with existing data
- Use transactions for complex changes
- Document breaking changes

### Code Style
- Follow C# conventions
- Use async/await for database operations
- Implement proper error handling
- Add logging for important operations

### Testing
- Test new installations (delete database file)
- Test migrations from previous versions
- Verify data preservation during upgrades

---

For specific technical details, see the individual documentation files linked above. 
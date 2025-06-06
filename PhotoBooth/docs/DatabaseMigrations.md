# Database Migration System Documentation

## Table of Contents
- [Overview](#overview)
- [Architecture](#architecture)
- [Version Tracking](#version-tracking)
- [How Migrations Work](#how-migrations-work)
- [Adding New Migrations](#adding-new-migrations)
- [Best Practices](#best-practices)
- [Examples](#examples)
- [Troubleshooting](#troubleshooting)
- [Database Location](#database-location)
- [Development Workflow](#development-workflow)

## Overview

The PhotoBooth application uses an **incremental migration system** to manage database schema changes over time. This system ensures that:

- ✅ New installations get the latest schema
- ✅ Existing installations are automatically updated
- ✅ No data is lost during schema changes
- ✅ Migrations are applied in the correct order
- ✅ Failed migrations don't leave the database in an inconsistent state

### Key Benefits

- **Safe**: Each migration is tracked to prevent double-application
- **Incremental**: Only applies necessary changes between versions
- **Rollback-Safe**: Designed to handle partial failures gracefully
- **Development-Friendly**: Easy to add new migrations as features evolve
- **Production-Ready**: Handles complex multi-version upgrades automatically

## Architecture

### Core Components

```
DatabaseService.cs
├── InitializeAsync()           # Entry point for database setup
├── ApplyMigrations()          # Orchestrates the migration process
├── GetDatabaseVersionAsync()  # Reads current schema version
├── SetDatabaseVersionAsync()  # Updates version tracking
├── GetExpectedSchemaVersion() # Returns target version (update this!)
├── ApplyIncrementalMigrations() # Applies version-specific migrations
└── ApplyMigrationVX()         # Individual migration methods
```

### Database Tables

#### DatabaseVersion Table
```sql
CREATE TABLE DatabaseVersion (
    Id INTEGER PRIMARY KEY,
    Version INTEGER NOT NULL,
    UpdatedAt TEXT NOT NULL
);
```

**Purpose**: Tracks the current schema version of the database.
- `Version`: Current schema version number
- `UpdatedAt`: Timestamp when version was applied

## Version Tracking

### How Versions Work

- **Version 1**: Initial schema (current baseline)
- **Version 2**: First incremental change (future)
- **Version 3**: Second incremental change (future)
- And so on...

### Version States

| Database State | Version Value | Migration Action |
|---------------|---------------|------------------|
| New Install | N/A | Create with latest schema, set to current version |
| Up to Date | Matches expected | No action needed |
| Behind | Less than expected | Apply incremental migrations |
| Ahead | Greater than expected | Log warning, continue (dev scenario) |

## How Migrations Work

### Migration Flow Diagram

```
Application Start
        ↓
    InitializeAsync()
        ↓
    Check if DB exists
    ├─ No → Create new with latest schema
    └─ Yes → ApplyMigrations()
            ↓
        Get current version
            ↓
        Get expected version
            ↓
        Compare versions
        ├─ Same → Continue (up to date)
        └─ Different → Apply incremental migrations
                ↓
            Update version tracking
                ↓
            Continue with app startup
```

### Code Flow

```csharp
public async Task<DatabaseResult> InitializeAsync()
{
    // 1. Check if database exists
    if (databaseExists)
    {
        // 2. Apply any pending migrations
        await ApplyMigrations(connection);
    }
    else
    {
        // 3. Create new database with latest schema
        await ExecuteSchemaScript();
        await CreateDefaultData();
    }
}

private async Task ApplyMigrations(SqliteConnection connection)
{
    // 4. Get current version from DatabaseVersion table
    var currentVersion = await GetDatabaseVersionAsync(connection);
    
    // 5. Get expected version from code
    var expectedVersion = GetExpectedSchemaVersion();
    
    // 6. Apply incremental migrations if needed
    if (currentVersion < expectedVersion)
    {
        await ApplyIncrementalMigrations(connection, currentVersion, expectedVersion);
        await SetDatabaseVersionAsync(connection, expectedVersion);
    }
}
```

## Adding New Migrations

### Step 1: Update Expected Version

In `DatabaseService.cs`, update the expected version:

```csharp
private int GetExpectedSchemaVersion()
{
    return 2; // Increment this number for each schema change
}
```

### Step 2: Add Migration Logic

Add your migration logic to `ApplyIncrementalMigrations()`:

```csharp
private async Task ApplyIncrementalMigrations(SqliteConnection connection, int fromVersion, int toVersion)
{
    Console.WriteLine($"ApplyIncrementalMigrations: Migrating from v{fromVersion} to v{toVersion}");
    
    // Existing migrations
    if (fromVersion < 2 && toVersion >= 2)
    {
        await ApplyMigrationV2(connection);
    }
    
    // Add your new migration here
    if (fromVersion < 3 && toVersion >= 3)
    {
        await ApplyMigrationV3(connection);
    }
    
    Console.WriteLine("ApplyIncrementalMigrations: All migrations applied successfully");
}
```

### Step 3: Implement Migration Method

Create a dedicated method for your migration:

```csharp
private async Task ApplyMigrationV3(SqliteConnection connection)
{
    try
    {
        Console.WriteLine("Applying migration v3: [Description of changes]");
        
        // Use a transaction for safety
        using var transaction = connection.BeginTransaction();
        
        try
        {
            // Your migration SQL here
            var alterQuery = "ALTER TABLE Users ADD COLUMN LastLoginIP TEXT;";
            using var cmd = new SqliteCommand(alterQuery, connection, transaction);
            await cmd.ExecuteNonQueryAsync();
            
            // More changes if needed
            var updateQuery = "UPDATE Users SET LastLoginIP = '0.0.0.0' WHERE LastLoginIP IS NULL;";
            using var updateCmd = new SqliteCommand(updateQuery, connection, transaction);
            await updateCmd.ExecuteNonQueryAsync();
            
            await transaction.CommitAsync();
            Console.WriteLine("Migration v3 completed successfully");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Migration v3 failed: {ex.Message}");
        throw;
    }
}
```

## Best Practices

### Migration Design

1. **Always use transactions** for complex migrations
2. **Test migrations with sample data** before releasing
3. **Make migrations reversible** when possible
4. **Keep migrations focused** - one logical change per version
5. **Document breaking changes** in migration comments

### Code Organization

```csharp
// ✅ Good: Clear version progression
if (fromVersion < 2 && toVersion >= 2) await ApplyMigrationV2(connection);
if (fromVersion < 3 && toVersion >= 3) await ApplyMigrationV3(connection);
if (fromVersion < 4 && toVersion >= 4) await ApplyMigrationV4(connection);

// ❌ Bad: Hard to follow, version gaps
if (fromVersion == 1) await ApplyMultipleChanges(connection);
if (fromVersion <= 5) await ApplyV6Changes(connection);
```

### Error Handling

```csharp
private async Task ApplyMigrationV2(SqliteConnection connection)
{
    try
    {
        Console.WriteLine("Applying migration v2: Adding Theme support");
        
        using var transaction = connection.BeginTransaction();
        try
        {
            // Migration logic here
            await transaction.CommitAsync();
            Console.WriteLine("Migration v2 completed successfully");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Console.WriteLine($"Migration v2 transaction failed: {ex.Message}");
            throw;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Migration v2 failed: {ex.Message}");
        // Log to system for debugging
        await LogSystemEventAsync(LogLevel.Error, "Migration", $"Migration v2 failed: {ex.Message}");
        throw;
    }
}
```

### Data Preservation

```csharp
// ✅ Good: Preserve existing data
ALTER TABLE Users ADD COLUMN Theme TEXT DEFAULT 'Default';

// ✅ Good: Migrate existing data
UPDATE Users SET Theme = 'Classic' WHERE AccessLevel = 'Master';

// ❌ Bad: Could lose data
DROP TABLE Users;
CREATE TABLE Users (...);
```

## Examples

### Example 1: Adding a New Column

**Scenario**: Add a `Theme` preference to the Settings table.

```csharp
// Step 1: Update expected version
private int GetExpectedSchemaVersion()
{
    return 2; // Was 1, now 2
}

// Step 2: Add migration logic
private async Task ApplyIncrementalMigrations(SqliteConnection connection, int fromVersion, int toVersion)
{
    if (fromVersion < 2 && toVersion >= 2)
    {
        await ApplyMigrationV2(connection);
    }
}

// Step 3: Implement migration
private async Task ApplyMigrationV2(SqliteConnection connection)
{
    Console.WriteLine("Applying migration v2: Adding Theme support to Settings");
    
    using var transaction = connection.BeginTransaction();
    try
    {
        // Add new column with default value
        var alterQuery = "ALTER TABLE Settings ADD COLUMN Theme TEXT DEFAULT 'Default';";
        using var cmd = new SqliteCommand(alterQuery, connection, transaction);
        await cmd.ExecuteNonQueryAsync();
        
        // Insert default theme settings
        var insertQuery = @"
            INSERT INTO Settings (Id, Category, Key, Value, DataType, Description, IsUserEditable, UpdatedAt)
            VALUES (@id, 'UI', 'DefaultTheme', 'Classic', 'String', 'Default UI theme', 1, @updatedAt)";
        
        using var insertCmd = new SqliteCommand(insertQuery, connection, transaction);
        insertCmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
        insertCmd.Parameters.AddWithValue("@updatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        await insertCmd.ExecuteNonQueryAsync();
        
        await transaction.CommitAsync();
        Console.WriteLine("Migration v2: Theme support added successfully");
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        Console.WriteLine($"Migration v2 failed: {ex.Message}");
        throw;
    }
}
```

### Example 2: Creating a New Table

**Scenario**: Add user activity logging.

```csharp
// Update to version 3
private int GetExpectedSchemaVersion()
{
    return 3;
}

private async Task ApplyMigrationV3(SqliteConnection connection)
{
    Console.WriteLine("Applying migration v3: Adding UserActivity table");
    
    using var transaction = connection.BeginTransaction();
    try
    {
        var createTableQuery = @"
            CREATE TABLE UserActivity (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                UserId TEXT NOT NULL,
                ActivityType TEXT NOT NULL,
                ActivityData TEXT,
                IPAddress TEXT,
                UserAgent TEXT,
                CreatedAt TEXT NOT NULL,
                FOREIGN KEY (UserId) REFERENCES AdminUsers(UserId)
            );";
        
        using var cmd = new SqliteCommand(createTableQuery, connection, transaction);
        await cmd.ExecuteNonQueryAsync();
        
        // Create index for performance
        var indexQuery = "CREATE INDEX idx_useractivity_userid_created ON UserActivity(UserId, CreatedAt);";
        using var indexCmd = new SqliteCommand(indexQuery, connection, transaction);
        await indexCmd.ExecuteNonQueryAsync();
        
        await transaction.CommitAsync();
        Console.WriteLine("Migration v3: UserActivity table created successfully");
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        throw;
    }
}
```

### Example 3: Data Transformation

**Scenario**: Split `DisplayName` into `FirstName` and `LastName`.

```csharp
private async Task ApplyMigrationV4(SqliteConnection connection)
{
    Console.WriteLine("Applying migration v4: Splitting DisplayName into FirstName/LastName");
    
    using var transaction = connection.BeginTransaction();
    try
    {
        // Add new columns
        var alterQuery1 = "ALTER TABLE AdminUsers ADD COLUMN FirstName TEXT;";
        var alterQuery2 = "ALTER TABLE AdminUsers ADD COLUMN LastName TEXT;";
        
        using var cmd1 = new SqliteCommand(alterQuery1, connection, transaction);
        await cmd1.ExecuteNonQueryAsync();
        
        using var cmd2 = new SqliteCommand(alterQuery2, connection, transaction);
        await cmd2.ExecuteNonQueryAsync();
        
        // Migrate existing data
        var selectQuery = "SELECT UserId, DisplayName FROM AdminUsers WHERE DisplayName IS NOT NULL;";
        using var selectCmd = new SqliteCommand(selectQuery, connection, transaction);
        using var reader = await selectCmd.ExecuteReaderAsync();
        
        var updates = new List<(string UserId, string FirstName, string LastName)>();
        while (await reader.ReadAsync())
        {
            var userId = reader.GetString("UserId");
            var displayName = reader.GetString("DisplayName");
            
            // Split display name (handle edge cases)
            var parts = displayName.Split(' ', 2);
            var firstName = parts[0];
            var lastName = parts.Length > 1 ? parts[1] : "";
            
            updates.Add((userId, firstName, lastName));
        }
        reader.Close();
        
        // Apply updates
        foreach (var (userId, firstName, lastName) in updates)
        {
            var updateQuery = @"
                UPDATE AdminUsers 
                SET FirstName = @firstName, LastName = @lastName 
                WHERE UserId = @userId";
            
            using var updateCmd = new SqliteCommand(updateQuery, connection, transaction);
            updateCmd.Parameters.AddWithValue("@firstName", firstName);
            updateCmd.Parameters.AddWithValue("@lastName", lastName);
            updateCmd.Parameters.AddWithValue("@userId", userId);
            await updateCmd.ExecuteNonQueryAsync();
        }
        
        await transaction.CommitAsync();
        Console.WriteLine($"Migration v4: Split {updates.Count} display names successfully");
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        throw;
    }
}
```

## Troubleshooting

### Common Issues

#### Migration Fails Halfway Through

**Symptoms**: Application crashes during migration, database left in inconsistent state.

**Solution**:
1. Check console logs for specific error
2. Manually delete the database file to start fresh
3. Fix the migration code and restart

**Prevention**:
- Always use transactions in migration methods
- Test migrations thoroughly with sample data

#### Version Number Conflicts

**Symptoms**: Multiple developers increment version to same number.

**Solution**:
1. Coordinate version numbers in team
2. Use sequential numbering
3. Consider date-based versioning (YYYYMMDD format)

**Prevention**:
- Document version assignments in team chat/wiki
- Use feature branches for schema changes

#### Migration Skipped or Applied Twice

**Symptoms**: Migration logic runs but doesn't achieve expected result.

**Solution**:
1. Check `DatabaseVersion` table contents:
   ```sql
   SELECT * FROM DatabaseVersion ORDER BY Id DESC;
   ```
2. Verify version comparison logic
3. Check if migration is idempotent (safe to run multiple times)

### Debug Migration Issues

#### Enable Detailed Logging

Add more logging to migration methods:

```csharp
private async Task ApplyMigrationV2(SqliteConnection connection)
{
    Console.WriteLine("=== MIGRATION V2 START ===");
    Console.WriteLine($"Connection State: {connection.State}");
    
    try
    {
        // ... migration logic ...
        Console.WriteLine("Migration V2: Step 1 completed");
        // ... more steps ...
        Console.WriteLine("Migration V2: Step 2 completed");
        
        Console.WriteLine("=== MIGRATION V2 SUCCESS ===");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"=== MIGRATION V2 FAILED ===");
        Console.WriteLine($"Error: {ex.Message}");
        Console.WriteLine($"Stack: {ex.StackTrace}");
        throw;
    }
}
```

#### Check Database State

Useful SQL queries for debugging:

```sql
-- Check current version
SELECT * FROM DatabaseVersion ORDER BY Id DESC LIMIT 1;

-- Check table structure
PRAGMA table_info(AdminUsers);

-- Check for specific columns
SELECT sql FROM sqlite_master WHERE type='table' AND name='AdminUsers';

-- List all tables
SELECT name FROM sqlite_master WHERE type='table';
```

#### Manual Recovery

If migrations fail and leave database in bad state:

1. **Back up data** (if possible):
   ```sql
   SELECT * FROM AdminUsers;
   SELECT * FROM Settings;
   -- Export important data
   ```

2. **Delete database file**:
   - Location: `%APPDATA%/PhotoboothX/photobooth.db`
   - This forces fresh creation on next startup

3. **Restart application** - it will create clean database with latest schema

## Database Location

### Production Location
```
%APPDATA%/PhotoboothX/photobooth.db
```
**Example**: `C:\Users\YourName\AppData\Roaming\PhotoboothX\photobooth.db`

### Why This Location?
- ✅ Survives application updates
- ✅ Follows Windows conventions
- ✅ User-specific data isolation
- ✅ Automatic backup by Windows

### Development Override
```csharp
// In development, you can specify custom location
var dbService = new DatabaseService("./dev-database.db");
```

## Development Workflow

### Making Schema Changes

1. **Plan the change** - document what you're adding/changing
2. **Update expected version** in `GetExpectedSchemaVersion()`
3. **Add migration logic** in `ApplyIncrementalMigrations()`
4. **Implement migration method** with proper error handling
5. **Test with existing data** - create test database with old schema
6. **Test edge cases** - empty tables, null values, etc.
7. **Update documentation** if needed

### Testing Migrations

#### Test Scenario 1: New Installation
1. Delete database file
2. Start application
3. Verify fresh database created with latest schema

#### Test Scenario 2: Upgrade from Previous Version
1. Create database with old schema (manually set version to previous)
2. Start application
3. Verify migration runs and updates to latest version
4. Check that existing data is preserved

#### Test Scenario 3: Multiple Version Gap
1. Create database with very old schema (version 1)
2. Set expected version to 4 (skipping 2, 3)
3. Start application
4. Verify all intermediate migrations run in order

### Team Coordination

- **Version Numbers**: Assign in team meetings or chat
- **Breaking Changes**: Document and communicate early
- **Testing**: Cross-test on different environments
- **Rollback Plan**: Consider how to revert if needed

---

## Quick Reference

### Current Version Management
```csharp
// To check current schema version expected by code:
private int GetExpectedSchemaVersion() { return 1; }

// To add new migration:
// 1. Increment return value above
// 2. Add if statement in ApplyIncrementalMigrations()
// 3. Implement ApplyMigrationVX() method
```

### Migration Template
```csharp
private async Task ApplyMigrationVX(SqliteConnection connection)
{
    Console.WriteLine("Applying migration vX: [Description]");
    
    using var transaction = connection.BeginTransaction();
    try
    {
        // Your SQL changes here
        var query = "ALTER TABLE ...";
        using var cmd = new SqliteCommand(query, connection, transaction);
        await cmd.ExecuteNonQueryAsync();
        
        await transaction.CommitAsync();
        Console.WriteLine("Migration vX completed successfully");
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        Console.WriteLine($"Migration vX failed: {ex.Message}");
        throw;
    }
}
```

This migration system provides a robust foundation for evolving the PhotoBooth application's database schema safely and incrementally over time. 
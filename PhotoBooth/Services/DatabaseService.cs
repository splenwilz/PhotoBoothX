using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Threading.Tasks;
using System.Globalization;
using Photobooth.Models;
using System.Security.Cryptography;
using System.Text;

namespace Photobooth.Services
{
    public interface IDatabaseService
    {
        Task<DatabaseResult> InitializeAsync();
        Task<DatabaseResult<List<T>>> GetAllAsync<T>() where T : class, new();
        Task<DatabaseResult<T?>> GetByIdAsync<T>(int id) where T : class, new();
        Task<DatabaseResult<T?>> GetByUserIdAsync<T>(string userId) where T : class, new();
        Task<DatabaseResult<int>> InsertAsync<T>(T entity) where T : class;
        Task<DatabaseResult> UpdateAsync<T>(T entity) where T : class;
        Task<DatabaseResult> DeleteAsync<T>(int id) where T : class;
        
        // Specialized methods
        Task<DatabaseResult<AdminUser?>> AuthenticateAsync(string username, string password);
        Task<DatabaseResult<bool>> IsUsingSetupCredentialsAsync(string username, string password);
        Task<DatabaseResult> UpdateAdminPasswordAsync(AdminAccessLevel accessLevel, string newPassword, string? updatedBy = null);
        Task<DatabaseResult> UpdateUserPasswordByUserIdAsync(string userId, string newPassword, string? updatedBy = null);
        Task<DatabaseResult> CreateAdminUserAsync(AdminUser user, string password, string? createdBy = null);
        Task<DatabaseResult> DeleteAdminUserAsync(string userId, string? deletedBy = null);
        Task<DatabaseResult<List<SalesOverviewDto>>> GetSalesOverviewAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<DatabaseResult<DailySalesSummary?>> GetDailySalesAsync(DateTime date);
        Task<DatabaseResult<List<PopularTemplateDto>>> GetPopularTemplatesAsync(int limit = 10);
        Task<DatabaseResult<List<HardwareStatusDto>>> GetHardwareStatusAsync();
        Task<DatabaseResult<PrintSupply?>> GetPrintSupplyAsync(SupplyType supplyType);
        Task<DatabaseResult> UpdatePrintSupplyAsync(SupplyType supplyType, int newCount);
        
        // Product management methods
        Task<DatabaseResult<List<Product>>> GetProductsAsync();
        Task<DatabaseResult<List<ProductCategory>>> GetProductCategoriesAsync();
        Task<DatabaseResult> UpdateProductStatusAsync(int productId, bool isActive);
        Task<DatabaseResult> UpdateProductPriceAsync(int productId, decimal price);
        Task<DatabaseResult<List<Setting>>> GetSettingsByCategoryAsync(string category);
        Task<DatabaseResult<T?>> GetSettingValueAsync<T>(string category, string key);
        Task<DatabaseResult> SetSettingValueAsync<T>(string category, string key, T value, string? updatedBy = null);
        Task<DatabaseResult> LogSystemEventAsync(LogLevel level, string category, string message, string? details = null, string? userId = null);
        Task<DatabaseResult> CleanupProductSettingsAsync();
    }

    public class DatabaseService : IDatabaseService
    {
        private readonly string _connectionString;
        private readonly string _databasePath;

        public DatabaseService(string? databasePath = null)
        {
            // Use AppData for production safety - data survives application updates and follows Windows conventions
            // For development, you can pass a custom path to constructor if needed
            _databasePath = databasePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PhotoboothX", "photobooth.db");
            _connectionString = $"Data Source={_databasePath}";
        }

        public async Task<DatabaseResult> InitializeAsync()
        {
            try
            {
                // Log initialization start
                LoggingService.Application.Information("Database initialization starting",
                    ("DatabasePath", _databasePath),
                    ("ConnectionString", _connectionString.Replace(_databasePath, "[PATH]")));
                
                // Ensure directory exists
                var directory = Path.GetDirectoryName(_databasePath);
                Console.WriteLine($"Database directory: {directory}");
                
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Console.WriteLine("Creating database directory...");
                    Directory.CreateDirectory(directory);
                    Console.WriteLine($"Directory created successfully: {directory}");
                }
                else if (!string.IsNullOrEmpty(directory))
                {
                    Console.WriteLine($"Directory already exists: {directory}");
                }

                Console.WriteLine("Opening database connection...");
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                Console.WriteLine("Database connection opened successfully");

                // Check if database is already initialized
                var checkQuery = "SELECT name FROM sqlite_master WHERE type='table' AND name='AdminUsers';";
                using var checkCommand = new SqliteCommand(checkQuery, connection);
                var tableExists = await checkCommand.ExecuteScalarAsync();
                
                Console.WriteLine($"AdminUsers table exists: {tableExists != null}");
                
                if (tableExists != null)
                {
                    // Database already initialized - check for and apply migrations
                    Console.WriteLine("Database already initialized, applying migrations...");
                    await ApplyMigrations(connection);
                    Console.WriteLine("Database initialization completed (existing database)");
                    return DatabaseResult.SuccessResult();
                }

                // Database needs initialization - read and execute schema
                var schemaPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database_Schema.sql");
                Console.WriteLine($"Schema file path: {schemaPath}");
                
                if (!File.Exists(schemaPath))
                {
                    var errorMsg = $"Database schema file not found at: {schemaPath}";
                    Console.WriteLine(errorMsg);
                    return DatabaseResult.ErrorResult(errorMsg);
                }

                Console.WriteLine("Reading schema file...");
                var schemaScript = await File.ReadAllTextAsync(schemaPath);
                Console.WriteLine($"Schema file read successfully. Length: {schemaScript.Length} characters");

                // Split and execute commands
                var commands = schemaScript.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                Console.WriteLine($"Found {commands.Length} SQL commands to execute");
                
                using var transaction = connection.BeginTransaction();
                try
                {
                    int commandIndex = 0;
                    foreach (var commandText in commands)
                    {
                        commandIndex++;
                        Console.WriteLine($"Processing command {commandIndex}/{commands.Length}");
                        
                        // Clean up the command - remove comments and whitespace
                        var lines = commandText.Split('\n');
                        var cleanLines = new List<string>();
                        
                        foreach (var line in lines)
                        {
                            var trimmedLine = line.Trim();
                            // Skip empty lines
                            if (string.IsNullOrEmpty(trimmedLine))
                                continue;
                                
                            // Skip full comment lines
                            if (trimmedLine.StartsWith("--"))
                                continue;
                                
                            // Remove inline comments
                            var commentIndex = trimmedLine.IndexOf("--");
                            if (commentIndex >= 0)
                            {
                                trimmedLine = trimmedLine.Substring(0, commentIndex).Trim();
                            }
                            
                            // Add non-empty lines
                            if (!string.IsNullOrEmpty(trimmedLine))
                            {
                                cleanLines.Add(trimmedLine);
                            }
                        }
                        
                        if (cleanLines.Count == 0)
                        {
                            Console.WriteLine($"Command {commandIndex} is empty after cleaning, skipping");
                            continue;
                        }
                        
                        var trimmedCommand = string.Join(" ", cleanLines);
                        Console.WriteLine($"Executing command {commandIndex}: {trimmedCommand.Substring(0, Math.Min(100, trimmedCommand.Length))}...");

                        using var command = new SqliteCommand(trimmedCommand, connection, transaction);
                        await command.ExecuteNonQueryAsync();
                        Console.WriteLine($"Command {commandIndex} executed successfully");
                    }

                    Console.WriteLine("Committing transaction...");
                    await transaction.CommitAsync();
                    Console.WriteLine("Transaction committed successfully");
                    
                    // Create default admin users (only for new database)
                    Console.WriteLine("Creating default admin users...");
                    await CreateDefaultAdminUserDirect(connection);
                    Console.WriteLine("Default admin users created successfully");
                    
                    // Create default system settings (only for new database)
                    Console.WriteLine("Creating default system settings...");
                    await CreateDefaultSettingsDirect(connection);
                    Console.WriteLine("Default system settings created successfully");
                    
                    Console.WriteLine("Database initialization completed successfully");
                    return DatabaseResult.SuccessResult();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Transaction error: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                var errorMsg = $"Database initialization failed: {ex.Message}";
                Console.WriteLine(errorMsg);
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return DatabaseResult.ErrorResult(errorMsg, ex);
            }
        }

        private async Task ApplyMigrations(SqliteConnection connection)
        {
            Console.WriteLine("ApplyMigrations: Checking database schema version...");
            
            try
            {
                // Get current schema version
                var currentVersion = await GetDatabaseVersionAsync(connection);
                var expectedVersion = GetExpectedSchemaVersion();
                
                Console.WriteLine($"ApplyMigrations: Current DB version: {currentVersion}, Expected: {expectedVersion}");
                
                if (currentVersion == expectedVersion)
                {
                    Console.WriteLine("ApplyMigrations: Database schema is up to date");
                    return;
                }
                
                // Apply incremental migrations for version differences
                ApplyIncrementalMigrations(connection, currentVersion, expectedVersion);
                
                // Update database version
                await SetDatabaseVersionAsync(connection, expectedVersion);
                Console.WriteLine($"ApplyMigrations: Database updated to version {expectedVersion}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ApplyMigrations: Error during migration: {ex.Message}");
                // For now, continue - in the future could implement rollback logic
            }
        }
        
        private async Task<int> GetDatabaseVersionAsync(SqliteConnection connection)
        {
            try
            {
                // Check if version table exists
                var checkTableQuery = "SELECT name FROM sqlite_master WHERE type='table' AND name='DatabaseVersion';";
                using var checkCmd = new SqliteCommand(checkTableQuery, connection);
                var tableExists = await checkCmd.ExecuteScalarAsync();
                
                if (tableExists == null)
                {
                    // Create version table
                    var createTableQuery = @"
                        CREATE TABLE DatabaseVersion (
                            Id INTEGER PRIMARY KEY,
                            Version INTEGER NOT NULL,
                            UpdatedAt TEXT NOT NULL
                        );";
                    using var createCmd = new SqliteCommand(createTableQuery, connection);
                    await createCmd.ExecuteNonQueryAsync();
                    
                    // Insert initial version
                    var insertQuery = "INSERT INTO DatabaseVersion (Version, UpdatedAt) VALUES (1, @updatedAt);";
                    using var insertCmd = new SqliteCommand(insertQuery, connection);
                    insertCmd.Parameters.AddWithValue("@updatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    await insertCmd.ExecuteNonQueryAsync();
                    
                    return 1;
                }
                
                // Get current version
                var versionQuery = "SELECT Version FROM DatabaseVersion ORDER BY Id DESC LIMIT 1;";
                using var versionCmd = new SqliteCommand(versionQuery, connection);
                var result = await versionCmd.ExecuteScalarAsync();
                return result != null ? Convert.ToInt32(result) : 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetDatabaseVersionAsync error: {ex.Message}");
                return 1; // Default to version 1
            }
        }
        
        private async Task SetDatabaseVersionAsync(SqliteConnection connection, int version)
        {
            try
            {
                var query = "INSERT INTO DatabaseVersion (Version, UpdatedAt) VALUES (@version, @updatedAt);";
                using var cmd = new SqliteCommand(query, connection);
                cmd.Parameters.AddWithValue("@version", version);
                cmd.Parameters.AddWithValue("@updatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SetDatabaseVersionAsync error: {ex.Message}");
            }
        }
        
        private int GetExpectedSchemaVersion()
        {
            // Current schema version - increment this when making schema changes
            return 1;
        }
        
        private void ApplyIncrementalMigrations(SqliteConnection connection, int fromVersion, int toVersion)
        {
            Console.WriteLine($"ApplyIncrementalMigrations: Migrating from v{fromVersion} to v{toVersion}");
            
            // No migrations needed during development - database will be recreated with latest schema
            Console.WriteLine("ApplyIncrementalMigrations: No migrations required during development");
        }



        public async Task<DatabaseResult<List<T>>> GetAllAsync<T>() where T : class, new()
        {
            try
            {
                var tableName = GetTableName<T>();
                var query = $"SELECT * FROM {tableName}";

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                using var command = new SqliteCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                var results = new List<T>();
                while (await reader.ReadAsync())
                {
                    var entity = MapReaderToEntity<T>(reader);
                    results.Add(entity);
                }

                return DatabaseResult<List<T>>.SuccessResult(results);
            }
            catch (Exception ex)
            {
                return DatabaseResult<List<T>>.ErrorResult($"Failed to get all {typeof(T).Name}: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult<T?>> GetByIdAsync<T>(int id) where T : class, new()
        {
            try
            {
                var tableName = GetTableName<T>();
                
                // AdminUser table doesn't have an integer Id field anymore
                if (typeof(T) == typeof(AdminUser))
                {
                    return DatabaseResult<T?>.ErrorResult($"AdminUser table should be queried by UserId string, not integer Id. Use GetByUserIdAsync instead.");
                }
                
                var query = $"SELECT * FROM {tableName} WHERE Id = @id";

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@id", id);
                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    var entity = MapReaderToEntity<T>(reader);
                    return DatabaseResult<T?>.SuccessResult(entity);
                }

                return DatabaseResult<T?>.SuccessResult(null);
            }
            catch (Exception ex)
            {
                return DatabaseResult<T?>.ErrorResult($"Failed to get {typeof(T).Name} by id: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult<T?>> GetByUserIdAsync<T>(string userId) where T : class, new()
        {
            try
            {
                var tableName = GetTableName<T>();
                var query = $"SELECT * FROM {tableName} WHERE UserId = @userId";

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@userId", userId);
                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    var entity = MapReaderToEntity<T>(reader);
                    return DatabaseResult<T?>.SuccessResult(entity);
                }

                return DatabaseResult<T?>.SuccessResult(null);
            }
            catch (Exception ex)
            {
                return DatabaseResult<T?>.ErrorResult($"Failed to get {typeof(T).Name} by userId: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult<int>> InsertAsync<T>(T entity) where T : class
        {
            try
            {
                var tableName = GetTableName<T>();
                var properties = typeof(T).GetProperties();
                var columns = new List<string>();
                var parameters = new List<string>();
                var values = new List<object?>();

                foreach (var prop in properties)
                {
                    if (prop.Name == "Id") continue; // Skip auto-increment ID

                    columns.Add(prop.Name);
                    parameters.Add($"@{prop.Name}");
                    
                    var value = prop.GetValue(entity);
                    if (value is Enum enumValue)
                    {
                        values.Add(enumValue.ToString());
                    }
                    else if (value is DateTime dateTime)
                    {
                        values.Add(dateTime.ToString("yyyy-MM-dd HH:mm:ss"));
                    }
                    else
                    {
                        values.Add(value);
                    }
                }

                var query = $"INSERT INTO {tableName} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", parameters)}); SELECT last_insert_rowid();";

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                using var command = new SqliteCommand(query, connection);

                for (int i = 0; i < parameters.Count; i++)
                {
                    command.Parameters.AddWithValue(parameters[i], values[i] ?? DBNull.Value);
                }

                var newId = Convert.ToInt32(await command.ExecuteScalarAsync());
                return DatabaseResult<int>.SuccessResult(newId);
            }
            catch (Exception ex)
            {
                return DatabaseResult<int>.ErrorResult($"Failed to insert {typeof(T).Name}: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult> UpdateAsync<T>(T entity) where T : class
        {
            try
            {
                var tableName = GetTableName<T>();
                var properties = typeof(T).GetProperties();
                var setParts = new List<string>();
                var values = new List<object?>();
                object? idValue = null;

                foreach (var prop in properties)
                {
                    var value = prop.GetValue(entity);
                    
                    if (prop.Name == "Id")
                    {
                        idValue = value;
                        continue;
                    }

                    setParts.Add($"{prop.Name} = @{prop.Name}");
                    
                    if (value is Enum enumValue)
                    {
                        values.Add(enumValue.ToString());
                    }
                    else if (value is DateTime dateTime)
                    {
                        values.Add(dateTime.ToString("yyyy-MM-dd HH:mm:ss"));
                    }
                    else
                    {
                        values.Add(value);
                    }
                }

                var query = $"UPDATE {tableName} SET {string.Join(", ", setParts)} WHERE Id = @Id";

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                using var command = new SqliteCommand(query, connection);

                command.Parameters.AddWithValue("@Id", idValue);
                for (int i = 0; i < setParts.Count; i++)
                {
                    var paramName = setParts[i].Split('=')[0].Trim() + "Value";
                    command.Parameters.AddWithValue($"@{setParts[i].Split('=')[0].Trim()}", values[i] ?? DBNull.Value);
                }

                await command.ExecuteNonQueryAsync();
                return DatabaseResult.SuccessResult();
            }
            catch (Exception ex)
            {
                return DatabaseResult.ErrorResult($"Failed to update {typeof(T).Name}: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult> DeleteAsync<T>(int id) where T : class
        {
            try
            {
                var tableName = GetTableName<T>();
                var query = $"DELETE FROM {tableName} WHERE Id = @id";

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@id", id);

                await command.ExecuteNonQueryAsync();
                return DatabaseResult.SuccessResult();
            }
            catch (Exception ex)
            {
                return DatabaseResult.ErrorResult($"Failed to delete {typeof(T).Name}: {ex.Message}", ex);
            }
        }

        // Specialized Methods

        public async Task<DatabaseResult<AdminUser?>> AuthenticateAsync(string username, string password)
        {
            try
            {
                var query = "SELECT * FROM AdminUsers WHERE Username = @username AND IsActive = 1";
                
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@username", username);
                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    var user = MapReaderToEntity<AdminUser>(reader);
                    var storedHash = reader["PasswordHash"].ToString();
                    
                    // For now, simple hash comparison (you should use proper password hashing)
                    var inputHash = HashPassword(password);
                    
                    if (storedHash == inputHash || storedHash == password + "_hash") // Temporary for demo
                    {
                        // Update last login - do this asynchronously without blocking
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                using var updateConnection = new SqliteConnection(_connectionString);
                                await updateConnection.OpenAsync();
                                var updateQuery = "UPDATE AdminUsers SET LastLoginAt = @now WHERE UserId = @userId";
                                using var updateCommand = new SqliteCommand(updateQuery, updateConnection);
                                updateCommand.Parameters.AddWithValue("@now", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                updateCommand.Parameters.AddWithValue("@userId", user.UserId);
                                await updateCommand.ExecuteNonQueryAsync();
                            }
                            catch
                            {
                                // Ignore errors in background login update
                            }
                        });

                        return DatabaseResult<AdminUser?>.SuccessResult(user);
                    }
                }

                return DatabaseResult<AdminUser?>.SuccessResult(null);
            }
            catch (Exception ex)
            {
                return DatabaseResult<AdminUser?>.ErrorResult($"Authentication failed: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult<bool>> IsUsingSetupCredentialsAsync(string username, string password)
        {
            try
            {
                // Check if this is a setup credentials login by verifying:
                // 1. User was created during database initialization (has null CreatedBy)
                // 2. Password matches the stored hash (indicating they haven't changed it yet)
                // This approach is generic and works for any username, not just "admin" or "user"

                var query = "SELECT PasswordHash, CreatedBy FROM AdminUsers WHERE Username = @username AND IsActive = 1";
                
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@username", username);
                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    var storedHash = reader["PasswordHash"].ToString();
                    var createdBy = reader["CreatedBy"];
                    
                    // Check if this user was created during setup (CreatedBy is null)
                    var isSetupUser = createdBy == DBNull.Value || createdBy == null;
                    
                    // Check if password matches (indicating they're still using the original setup password)
                    // Support both new hash format and legacy demo format for backward compatibility
                    var inputHash = HashPassword(password);
                    var passwordMatches = storedHash == inputHash || storedHash == password + "_hash";
                    
                    // It's setup credentials if it's a setup user AND password still matches original
                    return DatabaseResult<bool>.SuccessResult(isSetupUser && passwordMatches);
                }

                return DatabaseResult<bool>.SuccessResult(false);
            }
            catch (Exception ex)
            {
                return DatabaseResult<bool>.ErrorResult($"Failed to check setup credentials: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult> UpdateAdminPasswordAsync(AdminAccessLevel accessLevel, string newPassword, string? updatedBy = null)
        {
            try
            {
                var query = "UPDATE AdminUsers SET PasswordHash = @newPassword WHERE AccessLevel = @accessLevel";

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@newPassword", HashPassword(newPassword));
                command.Parameters.AddWithValue("@accessLevel", accessLevel.ToString());

                await command.ExecuteNonQueryAsync();
                await LogSystemEventAsync(LogLevel.Info, "AdminUsers", $"Admin password updated for {accessLevel}", null, updatedBy);
                
                return DatabaseResult.SuccessResult();
            }
            catch (Exception ex)
            {
                return DatabaseResult.ErrorResult($"Failed to update admin password: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult> UpdateUserPasswordByUserIdAsync(string userId, string newPassword, string? updatedBy = null)
        {
            try
            {
                // Update password AND mark as no longer setup credentials by setting CreatedBy and UpdatedBy
                var query = @"UPDATE AdminUsers 
                             SET PasswordHash = @newPassword, 
                                 UpdatedAt = @updatedAt,
                                 UpdatedBy = @updatedBy,
                                 CreatedBy = COALESCE(CreatedBy, @updatedBy)
                             WHERE UserId = @userId";

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@newPassword", HashPassword(newPassword));
                command.Parameters.AddWithValue("@userId", userId);
                command.Parameters.AddWithValue("@updatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                command.Parameters.AddWithValue("@updatedBy", updatedBy ?? userId); // Self-update if no updatedBy specified

                await command.ExecuteNonQueryAsync();
                
                await LogSystemEventAsync(LogLevel.Info, "AdminUsers", $"User password updated for userId: {userId} - setup credentials converted to user-managed");
                
                return DatabaseResult.SuccessResult();
            }
            catch (Exception ex)
            {
                return DatabaseResult.ErrorResult($"Failed to update user password: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult> CreateAdminUserAsync(AdminUser user, string password, string? createdBy = null)
        {
            try
            {
                Console.WriteLine($"CreateAdminUserAsync: Creating user '{user.Username}' with UserId='{user.UserId}', CreatedBy='{createdBy}'");
                
                // Check if the createdBy user exists (if specified)
                if (!string.IsNullOrEmpty(createdBy))
                {
                    var checkQuery = "SELECT COUNT(*) FROM AdminUsers WHERE UserId = @createdBy";
                    using var checkConnection = new SqliteConnection(_connectionString);
                    await checkConnection.OpenAsync();
                    using var checkCommand = new SqliteCommand(checkQuery, checkConnection);
                    checkCommand.Parameters.AddWithValue("@createdBy", createdBy);
                    var count = Convert.ToInt32(await checkCommand.ExecuteScalarAsync());
                    Console.WriteLine($"CreateAdminUserAsync: CreatedBy user '{createdBy}' exists: {count > 0}");
                    
                    if (count == 0)
                    {
                        Console.WriteLine($"CreateAdminUserAsync: Warning - CreatedBy user '{createdBy}' not found, setting to NULL");
                        createdBy = null;
                    }
                }
                
                var query = "INSERT INTO AdminUsers (UserId, Username, DisplayName, PasswordHash, AccessLevel, IsActive, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy) VALUES (@userId, @username, @displayName, @passwordHash, @accessLevel, @isActive, @createdAt, @updatedAt, @createdBy, @updatedBy)";

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@userId", user.UserId);
                command.Parameters.AddWithValue("@username", user.Username);
                command.Parameters.AddWithValue("@displayName", user.DisplayName);
                command.Parameters.AddWithValue("@passwordHash", HashPassword(password));
                command.Parameters.AddWithValue("@accessLevel", user.AccessLevel.ToString());
                command.Parameters.AddWithValue("@isActive", user.IsActive);
                command.Parameters.AddWithValue("@createdAt", user.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                command.Parameters.AddWithValue("@updatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                command.Parameters.AddWithValue("@createdBy", (object?)createdBy ?? DBNull.Value);
                command.Parameters.AddWithValue("@updatedBy", (object?)createdBy ?? DBNull.Value);

                Console.WriteLine($"CreateAdminUserAsync: Executing query with parameters - UserId={user.UserId}, Username={user.Username}, CreatedBy={createdBy}");
                await command.ExecuteNonQueryAsync();
                Console.WriteLine($"CreateAdminUserAsync: User '{user.Username}' created successfully");
                
                await LogSystemEventAsync(LogLevel.Info, "AdminUsers", $"Admin user {user.Username} created");
                
                return DatabaseResult.SuccessResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CreateAdminUserAsync error: {ex}");
                return DatabaseResult.ErrorResult($"Failed to create admin user: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult<List<SalesOverviewDto>>> GetSalesOverviewAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var query = @"
                    SELECT 
                        DATE(t.CreatedAt) as SaleDate,
                        pc.Name as ProductCategory,
                        COUNT(*) as TransactionCount,
                        SUM(t.TotalPrice) as Revenue,
                        COALESCE(SUM(pj.Copies), 0) as TotalCopies,
                        COALESCE(SUM(pj.PrintsUsed), 0) as PrintsUsed
                    FROM Transactions t
                    JOIN Products p ON t.ProductId = p.Id
                    JOIN ProductCategories pc ON p.CategoryId = pc.Id
                    LEFT JOIN PrintJobs pj ON t.Id = pj.TransactionId
                    WHERE t.PaymentStatus = 'Completed'";

                if (startDate.HasValue)
                    query += " AND DATE(t.CreatedAt) >= @startDate";
                if (endDate.HasValue)
                    query += " AND DATE(t.CreatedAt) <= @endDate";

                query += @"
                    GROUP BY DATE(t.CreatedAt), pc.Name
                    ORDER BY SaleDate DESC, pc.Name";

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                using var command = new SqliteCommand(query, connection);

                if (startDate.HasValue)
                    command.Parameters.AddWithValue("@startDate", startDate.Value.ToString("yyyy-MM-dd"));
                if (endDate.HasValue)
                    command.Parameters.AddWithValue("@endDate", endDate.Value.ToString("yyyy-MM-dd"));

                using var reader = await command.ExecuteReaderAsync();
                var results = new List<SalesOverviewDto>();

                while (await reader.ReadAsync())
                {
                    results.Add(new SalesOverviewDto
                    {
                        SaleDate = reader["SaleDate"].ToString() ?? "",
                        ProductCategory = reader["ProductCategory"].ToString() ?? "",
                        TransactionCount = Convert.ToInt32(reader["TransactionCount"]),
                        Revenue = Convert.ToDecimal(reader["Revenue"]),
                        TotalCopies = Convert.ToInt32(reader["TotalCopies"]),
                        PrintsUsed = Convert.ToInt32(reader["PrintsUsed"])
                    });
                }

                return DatabaseResult<List<SalesOverviewDto>>.SuccessResult(results);
            }
            catch (Exception ex)
            {
                return DatabaseResult<List<SalesOverviewDto>>.ErrorResult($"Failed to get sales overview: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult<DailySalesSummary?>> GetDailySalesAsync(DateTime date)
        {
            try
            {
                var dateString = date.ToString("yyyy-MM-dd");
                var query = "SELECT * FROM DailySalesSummary WHERE Date = @date";

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@date", dateString);
                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    var summary = MapReaderToEntity<DailySalesSummary>(reader);
                    return DatabaseResult<DailySalesSummary?>.SuccessResult(summary);
                }

                return DatabaseResult<DailySalesSummary?>.SuccessResult(null);
            }
            catch (Exception ex)
            {
                return DatabaseResult<DailySalesSummary?>.ErrorResult($"Failed to get daily sales: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult<List<PopularTemplateDto>>> GetPopularTemplatesAsync(int limit = 10)
        {
            try
            {
                var query = @"
                    SELECT 
                        t.Name as TemplateName,
                        tc.Name as Category,
                        COALESCE(COUNT(tr.Id), 0) as TimesUsed,
                        COALESCE(SUM(tr.TotalPrice), 0) as Revenue,
                        MAX(tr.CreatedAt) as LastUsed
                    FROM Templates t
                    JOIN TemplateCategories tc ON t.CategoryId = tc.Id
                    LEFT JOIN Transactions tr ON t.Id = tr.TemplateId
                    WHERE t.IsActive = 1
                    GROUP BY t.Id, t.Name, tc.Name
                    ORDER BY TimesUsed DESC
                    LIMIT @limit";

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@limit", limit);
                using var reader = await command.ExecuteReaderAsync();

                var results = new List<PopularTemplateDto>();
                while (await reader.ReadAsync())
                {
                    results.Add(new PopularTemplateDto
                    {
                        TemplateName = reader["TemplateName"].ToString() ?? "",
                        Category = reader["Category"].ToString() ?? "",
                        TimesUsed = Convert.ToInt32(reader["TimesUsed"]),
                        Revenue = Convert.ToDecimal(reader["Revenue"]),
                        LastUsed = reader["LastUsed"] != DBNull.Value ? DateTime.Parse(reader["LastUsed"].ToString()!) : null
                    });
                }

                return DatabaseResult<List<PopularTemplateDto>>.SuccessResult(results);
            }
            catch (Exception ex)
            {
                return DatabaseResult<List<PopularTemplateDto>>.ErrorResult($"Failed to get popular templates: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult<List<HardwareStatusDto>>> GetHardwareStatusAsync()
        {
            try
            {
                var query = @"
                    SELECT 
                        ComponentName,
                        Status,
                        CASE 
                            WHEN Status = 'Online' THEN '🟢'
                            WHEN Status = 'Offline' THEN '🔴'
                            WHEN Status = 'Error' THEN '🟠'
                            ELSE '🟡'
                        END as StatusIcon,
                        ErrorCode,
                        LastCheckAt
                    FROM HardwareStatus
                    ORDER BY ComponentName";

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                using var command = new SqliteCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                var results = new List<HardwareStatusDto>();
                while (await reader.ReadAsync())
                {
                    results.Add(new HardwareStatusDto
                    {
                        ComponentName = reader["ComponentName"].ToString() ?? "",
                        Status = Enum.Parse<HardwareStatus>(reader["Status"].ToString() ?? "Offline"),
                        StatusIcon = reader["StatusIcon"].ToString() ?? "",
                        ErrorCode = reader["ErrorCode"]?.ToString(),
                        LastCheckAt = DateTime.Parse(reader["LastCheckAt"].ToString()!)
                    });
                }

                return DatabaseResult<List<HardwareStatusDto>>.SuccessResult(results);
            }
            catch (Exception ex)
            {
                return DatabaseResult<List<HardwareStatusDto>>.ErrorResult($"Failed to get hardware status: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult<PrintSupply?>> GetPrintSupplyAsync(SupplyType supplyType)
        {
            try
            {
                var query = "SELECT * FROM PrintSupplies WHERE SupplyType = @supplyType ORDER BY InstalledAt DESC LIMIT 1";

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@supplyType", supplyType.ToString());
                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    var supply = MapReaderToEntity<PrintSupply>(reader);
                    return DatabaseResult<PrintSupply?>.SuccessResult(supply);
                }

                return DatabaseResult<PrintSupply?>.SuccessResult(null);
            }
            catch (Exception ex)
            {
                return DatabaseResult<PrintSupply?>.ErrorResult($"Failed to get print supply: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult> UpdatePrintSupplyAsync(SupplyType supplyType, int newCount)
        {
            try
            {
                var query = "UPDATE PrintSupplies SET CurrentCount = @newCount WHERE SupplyType = @supplyType";

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@newCount", newCount);
                command.Parameters.AddWithValue("@supplyType", supplyType.ToString());

                await command.ExecuteNonQueryAsync();
                await LogSystemEventAsync(LogLevel.Info, "Supplies", $"{supplyType} supply updated to {newCount}");
                
                return DatabaseResult.SuccessResult();
            }
            catch (Exception ex)
            {
                return DatabaseResult.ErrorResult($"Failed to update print supply: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult<List<Product>>> GetProductsAsync()
        {
            try
            {
                var query = @"
                    SELECT p.*, pc.Name as CategoryName 
                    FROM Products p 
                    LEFT JOIN ProductCategories pc ON p.CategoryId = pc.Id 
                    ORDER BY p.SortOrder, p.Name";

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                using var command = new SqliteCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                var products = new List<Product>();
                while (await reader.ReadAsync())
                {
                    var product = MapReaderToEntity<Product>(reader);
                    
                    // Map ProductType from database string to enum
                    if (reader["ProductType"] != DBNull.Value)
                    {
                        var productTypeStr = reader["ProductType"].ToString();
                        if (Enum.TryParse<ProductType>(productTypeStr, out var productType))
                        {
                            product.ProductType = productType;
                        }
                    }
                    
                    // Map category name if available
                    if (reader["CategoryName"] != DBNull.Value)
                    {
                        product.Category = new ProductCategory 
                        { 
                            Id = product.CategoryId, 
                            Name = reader["CategoryName"].ToString() ?? "" 
                        };
                    }
                    products.Add(product);
                }

                return DatabaseResult<List<Product>>.SuccessResult(products);
            }
            catch (Exception ex)
            {
                return DatabaseResult<List<Product>>.ErrorResult($"Failed to get products: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult<List<ProductCategory>>> GetProductCategoriesAsync()
        {
            try
            {
                return await GetAllAsync<ProductCategory>();
            }
            catch (Exception ex)
            {
                return DatabaseResult<List<ProductCategory>>.ErrorResult($"Failed to get product categories: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult> UpdateProductStatusAsync(int productId, bool isActive)
        {
            try
            {
                var query = "UPDATE Products SET IsActive = @isActive, UpdatedAt = @updatedAt WHERE Id = @id";

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@isActive", isActive);
                command.Parameters.AddWithValue("@updatedAt", DateTime.Now);
                command.Parameters.AddWithValue("@id", productId);

                var rowsAffected = await command.ExecuteNonQueryAsync();
                if (rowsAffected > 0)
                {
                    await LogSystemEventAsync(LogLevel.Info, "Products", $"Product {productId} status updated to {isActive}");
                    return DatabaseResult.SuccessResult();
                }
                else
                {
                    return DatabaseResult.ErrorResult("Product not found");
                }
            }
            catch (Exception ex)
            {
                return DatabaseResult.ErrorResult($"Failed to update product status: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult> UpdateProductPriceAsync(int productId, decimal price)
        {
            try
            {
                var query = "UPDATE Products SET Price = @price, UpdatedAt = @updatedAt WHERE Id = @id";

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@price", price);
                command.Parameters.AddWithValue("@updatedAt", DateTime.Now);
                command.Parameters.AddWithValue("@id", productId);

                var rowsAffected = await command.ExecuteNonQueryAsync();
                if (rowsAffected > 0)
                {
                    await LogSystemEventAsync(LogLevel.Info, "Products", $"Product {productId} price updated to ${price:F2}");
                    return DatabaseResult.SuccessResult();
                }
                else
                {
                    return DatabaseResult.ErrorResult("Product not found");
                }
            }
            catch (Exception ex)
            {
                return DatabaseResult.ErrorResult($"Failed to update product price: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult<List<Setting>>> GetSettingsByCategoryAsync(string category)
        {
            try
            {
                var query = "SELECT * FROM Settings WHERE Category = @category ORDER BY Key";

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@category", category);
                using var reader = await command.ExecuteReaderAsync();

                var results = new List<Setting>();
                while (await reader.ReadAsync())
                {
                    results.Add(MapReaderToEntity<Setting>(reader));
                }

                return DatabaseResult<List<Setting>>.SuccessResult(results);
            }
            catch (Exception ex)
            {
                return DatabaseResult<List<Setting>>.ErrorResult($"Failed to get settings: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult<T?>> GetSettingValueAsync<T>(string category, string key)
        {
            try
            {
                var query = "SELECT Value, DataType FROM Settings WHERE Category = @category AND Key = @key";

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@category", category);
                command.Parameters.AddWithValue("@key", key);
                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    var value = reader["Value"].ToString();
                    var dataType = reader["DataType"].ToString();

                    if (string.IsNullOrEmpty(value))
                        return DatabaseResult<T?>.SuccessResult(default(T));

                    var convertedValue = ConvertSettingValue<T>(value, dataType);
                    return DatabaseResult<T?>.SuccessResult(convertedValue);
                }

                return DatabaseResult<T?>.SuccessResult(default(T));
            }
            catch (Exception ex)
            {
                return DatabaseResult<T?>.ErrorResult($"Failed to get setting value: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult> SetSettingValueAsync<T>(string category, string key, T value, string? updatedBy = null)
        {
            try
            {
                Console.WriteLine($"SetSettingValueAsync: {category}.{key} = {value}, updatedBy = '{updatedBy}'");
                
                var stringValue = value?.ToString() ?? "";
                var dataType = GetDataTypeString<T>();

                Console.WriteLine($"SetSettingValueAsync: stringValue = '{stringValue}', dataType = '{dataType}'");

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                // First try to update the existing setting
                var updateQuery = @"
                    UPDATE Settings 
                    SET Value = @value, DataType = @dataType, UpdatedAt = @updatedAt, UpdatedBy = @updatedBy
                    WHERE Category = @category AND Key = @key";

                using var updateCommand = new SqliteCommand(updateQuery, connection);
                updateCommand.Parameters.AddWithValue("@category", category);
                updateCommand.Parameters.AddWithValue("@key", key);
                updateCommand.Parameters.AddWithValue("@value", stringValue);
                updateCommand.Parameters.AddWithValue("@dataType", dataType);
                updateCommand.Parameters.AddWithValue("@updatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                updateCommand.Parameters.AddWithValue("@updatedBy", (object?)updatedBy ?? DBNull.Value);

                var rowsAffected = await updateCommand.ExecuteNonQueryAsync();
                Console.WriteLine($"SetSettingValueAsync: UPDATE affected {rowsAffected} rows");

                // If no rows were updated, insert a new setting with UUID
                if (rowsAffected == 0)
                {
                    var insertQuery = @"
                        INSERT INTO Settings (Id, Category, Key, Value, DataType, IsUserEditable, UpdatedAt, UpdatedBy)
                        VALUES (@id, @category, @key, @value, @dataType, 1, @updatedAt, @updatedBy)";

                    using var insertCommand = new SqliteCommand(insertQuery, connection);
                    insertCommand.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
                    insertCommand.Parameters.AddWithValue("@category", category);
                    insertCommand.Parameters.AddWithValue("@key", key);
                    insertCommand.Parameters.AddWithValue("@value", stringValue);
                    insertCommand.Parameters.AddWithValue("@dataType", dataType);
                    insertCommand.Parameters.AddWithValue("@updatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    insertCommand.Parameters.AddWithValue("@updatedBy", (object?)updatedBy ?? DBNull.Value);

                    var insertedRows = await insertCommand.ExecuteNonQueryAsync();
                    Console.WriteLine($"SetSettingValueAsync: INSERT affected {insertedRows} rows");
                }

                // Log the setting change
                await LogSystemEventAsync(LogLevel.Info, "Settings", 
                    $"Setting updated: {category}.{key} = {value}", 
                    $"Updated by user: {updatedBy ?? "Unknown"}", 
                    updatedBy);

                Console.WriteLine($"SetSettingValueAsync: Successfully saved {category}.{key}");
                return DatabaseResult.SuccessResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SetSettingValueAsync error: {ex}");
                return DatabaseResult.ErrorResult($"Failed to set setting value: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult> LogSystemEventAsync(LogLevel level, string category, string message, string? details = null, string? userId = null)
        {
            try
            {
                var query = @"
                    INSERT INTO SystemLogs (LogLevel, Category, Message, Details, UserId, CreatedAt)
                    VALUES (@level, @category, @message, @details, @userId, @createdAt)";

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@level", level.ToString());
                command.Parameters.AddWithValue("@category", category);
                command.Parameters.AddWithValue("@message", message);
                command.Parameters.AddWithValue("@details", (object?)details ?? DBNull.Value);
                command.Parameters.AddWithValue("@userId", (object?)userId ?? DBNull.Value);
                command.Parameters.AddWithValue("@createdAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                await command.ExecuteNonQueryAsync();
                return DatabaseResult.SuccessResult();
            }
            catch (Exception ex)
            {
                // Don't throw here to avoid infinite loops
                return DatabaseResult.ErrorResult($"Failed to log system event: {ex.Message}", ex);
            }
        }

        // Helper Methods

        private string GetTableName<T>()
        {
            var typeName = typeof(T).Name;
            return typeName switch
            {
                nameof(AdminUser) => "AdminUsers",
                nameof(ProductCategory) => "ProductCategories",
                nameof(Product) => "Products",
                nameof(TemplateCategory) => "TemplateCategories",
                nameof(Template) => "Templates",
                nameof(SeasonalSchedule) => "SeasonalSchedules",
                nameof(Transaction) => "Transactions",
                nameof(TransactionPhoto) => "TransactionPhotos",
                nameof(PrintJob) => "PrintJobs",
                nameof(Setting) => "Settings",
                nameof(BusinessInfo) => "BusinessInfo",
                nameof(HardwareStatusModel) => "HardwareStatus",
                nameof(PrintSupply) => "PrintSupplies",
                nameof(SupplyUsageHistory) => "SupplyUsageHistory",
                nameof(SystemLog) => "SystemLogs",
                nameof(SystemError) => "SystemErrors",
                nameof(DailySalesSummary) => "DailySalesSummary",
                nameof(TemplateUsageStat) => "TemplateUsageStats",
                nameof(Customer) => "Customers",
                _ => typeName + "s"
            };
        }

        private T MapReaderToEntity<T>(System.Data.Common.DbDataReader reader) where T : class, new()
        {
            var entity = new T();
            var properties = typeof(T).GetProperties();

            foreach (var prop in properties)
            {
                try
                {
                    var ordinal = reader.GetOrdinal(prop.Name);
                    if (reader.IsDBNull(ordinal)) continue;

                    var value = reader.GetValue(ordinal);
                    
                    if (prop.PropertyType.IsEnum)
                    {
                        var enumValue = Enum.Parse(prop.PropertyType, value.ToString()!);
                        prop.SetValue(entity, enumValue);
                    }
                    else if (prop.PropertyType == typeof(DateTime) || prop.PropertyType == typeof(DateTime?))
                    {
                        var dateValue = DateTime.Parse(value.ToString()!);
                        prop.SetValue(entity, dateValue);
                    }
                    else if (prop.PropertyType == typeof(decimal) || prop.PropertyType == typeof(decimal?))
                    {
                        var decimalValue = Convert.ToDecimal(value);
                        prop.SetValue(entity, decimalValue);
                    }
                    else if (prop.PropertyType == typeof(bool) || prop.PropertyType == typeof(bool?))
                    {
                        var boolValue = Convert.ToBoolean(value);
                        prop.SetValue(entity, boolValue);
                    }
                    else
                    {
                        prop.SetValue(entity, Convert.ChangeType(value, prop.PropertyType));
                    }
                }
                catch
                {
                    // Skip properties that don't exist in the result set
                }
            }

            return entity;
        }

        private T? ConvertSettingValue<T>(string value, string? dataType)
        {
            return dataType switch
            {
                "Boolean" => (T)(object)bool.Parse(value),
                "Integer" => (T)(object)int.Parse(value),
                "Decimal" => (T)(object)decimal.Parse(value, CultureInfo.InvariantCulture),
                "String" => (T)(object)value,
                _ => (T)(object)value
            };
        }

        private string GetDataTypeString<T>()
        {
            var type = typeof(T);
            if (type == typeof(bool) || type == typeof(bool?))
                return "Boolean";
            if (type == typeof(int) || type == typeof(int?))
                return "Integer";
            if (type == typeof(decimal) || type == typeof(decimal?))
                return "Decimal";
            return "String";
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password + "PhotoboothSalt"));
            return Convert.ToBase64String(hashedBytes);
        }

        /// <summary>
        /// Generates a cryptographically secure random password
        /// </summary>
        private string GenerateSecurePassword(int length = 16)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            var result = new char[length];
            var buffer = new byte[4];

            for (int i = 0; i < length; i++)
            {
                rng.GetBytes(buffer);
                var randomValue = BitConverter.ToUInt32(buffer, 0);
                result[i] = chars[(int)(randomValue % chars.Length)];
            }

            return new string(result);
        }

        private async Task CreateDefaultAdminUserDirect(SqliteConnection connection)
        {
            try
            {
                // Generate secure random passwords
                var masterPassword = GenerateSecurePassword(16);
                var userPassword = GenerateSecurePassword(16);

                // Create default master admin with secure password
                var masterAdmin = new AdminUser
                {
                    UserId = Guid.NewGuid().ToString(),
                    Username = "admin",
                    DisplayName = "Master Administrator", 
                    PasswordHash = HashPassword(masterPassword),
                    AccessLevel = Models.AdminAccessLevel.Master,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    CreatedBy = null
                };

                // Create default user admin with secure password
                var userAdmin = new AdminUser
                {
                    UserId = Guid.NewGuid().ToString(),
                    Username = "user",
                    DisplayName = "User Administrator",
                    PasswordHash = HashPassword(userPassword), 
                    AccessLevel = Models.AdminAccessLevel.User,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    CreatedBy = null
                };

                // Insert directly using the existing connection (UserId is now primary key)
                var insertQuery = @"
                    INSERT INTO AdminUsers (UserId, Username, DisplayName, PasswordHash, AccessLevel, IsActive, CreatedAt)
                    VALUES (@userId, @username, @displayName, @passwordHash, @accessLevel, @isActive, @createdAt)";

                // Insert master admin
                using var masterCommand = new SqliteCommand(insertQuery, connection);
                masterCommand.Parameters.AddWithValue("@userId", masterAdmin.UserId);
                masterCommand.Parameters.AddWithValue("@username", masterAdmin.Username);
                masterCommand.Parameters.AddWithValue("@displayName", masterAdmin.DisplayName);
                masterCommand.Parameters.AddWithValue("@passwordHash", masterAdmin.PasswordHash);
                masterCommand.Parameters.AddWithValue("@accessLevel", masterAdmin.AccessLevel.ToString());
                masterCommand.Parameters.AddWithValue("@isActive", masterAdmin.IsActive);
                masterCommand.Parameters.AddWithValue("@createdAt", masterAdmin.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                await masterCommand.ExecuteNonQueryAsync();

                // Insert user admin
                using var userCommand = new SqliteCommand(insertQuery, connection);
                userCommand.Parameters.AddWithValue("@userId", userAdmin.UserId);
                userCommand.Parameters.AddWithValue("@username", userAdmin.Username);
                userCommand.Parameters.AddWithValue("@displayName", userAdmin.DisplayName);
                userCommand.Parameters.AddWithValue("@passwordHash", userAdmin.PasswordHash);
                userCommand.Parameters.AddWithValue("@accessLevel", userAdmin.AccessLevel.ToString());
                userCommand.Parameters.AddWithValue("@isActive", userAdmin.IsActive);
                userCommand.Parameters.AddWithValue("@createdAt", userAdmin.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                await userCommand.ExecuteNonQueryAsync();

                // Write secure credentials to a protected file for first-time setup
                await WriteInitialCredentialsSecurely(masterPassword, userPassword);

                Console.WriteLine("🔒 Default admin users created with secure random passwords");
                Console.WriteLine("📋 Check the setup credentials file for initial login information");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating default admin users: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Writes initial credentials to a secure file for first-time setup
        /// </summary>
        private async Task WriteInitialCredentialsSecurely(string masterPassword, string userPassword)
        {
            try
            {
                // Create setup folder on Desktop - highly visible and clearly temporary
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var setupDir = Path.Combine(desktopPath, "PhotoBoothX-Setup-Credentials");
                var credentialsFile = Path.Combine(setupDir, "LOGIN-CREDENTIALS.txt");
                var readmeFile = Path.Combine(setupDir, "README-DELETE-AFTER-SETUP.txt");

                Directory.CreateDirectory(setupDir);

                var credentialsContent = $@"PhotoBoothX - Initial Setup Credentials
Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}

⚠️  IMPORTANT SECURITY NOTICE:
- These are ONE-TIME setup credentials
- CHANGE THESE PASSWORDS immediately after first login
- DELETE this file after completing setup
- Keep these credentials secure until setup is complete

Master Administrator Account:
  Username: admin
  Password: {masterPassword}
  Access: Full admin panel access

Operator Account:
  Username: user  
  Password: {userPassword}
  Access: Limited access (reports, volume control)

Setup Instructions:
1. Start PhotoBoothX application
2. Tap 5 times in top-left corner of welcome screen
3. Login with credentials above
4. Immediately change both passwords
5. Application will auto-delete this folder after successful setup

Security Best Practices:
- Use strong passwords (12+ characters, mixed case, numbers, symbols)
- Enable password rotation reminders
- Limit operator account permissions
- Regularly backup admin settings

⚠️  This folder will be automatically deleted after first successful admin login.
";

                // Create README file explaining the folder purpose
                var readmeContent = @"📋 PhotoBoothX Setup Credentials Folder

🎯 PURPOSE:
This folder contains your one-time setup credentials for PhotoBoothX.

📁 CONTENTS:
- LOGIN-CREDENTIALS.txt - Your admin usernames and passwords

⚠️  IMPORTANT:
- This folder is TEMPORARY and will be automatically deleted
- Complete your admin setup BEFORE this folder disappears
- If you need the credentials again, check before the folder auto-deletes

🔧 SETUP STEPS:
1. Open PhotoBoothX application
2. Tap 5 times in top-left corner of welcome screen  
3. Use credentials from LOGIN-CREDENTIALS.txt
4. Change passwords immediately after login
5. This folder deletes itself after successful setup

🗑️  SAFE TO DELETE:
If setup is complete, you can safely delete this entire folder manually.
It contains no important application files.

✅ Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                await File.WriteAllTextAsync(credentialsFile, credentialsContent);
                await File.WriteAllTextAsync(readmeFile, readmeContent);

                Console.WriteLine($"📋 Setup credentials folder created on Desktop: {setupDir}");
                Console.WriteLine("🎯 Folder is clearly labeled and will auto-delete after setup");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not write credentials file: {ex.Message}");
                Console.WriteLine($"Master admin password: {masterPassword}");
                Console.WriteLine($"User admin password: {userPassword}");
                Console.WriteLine("Please note these passwords for initial setup!");
            }
        }

        /// <summary>
        /// Cleans up the setup credentials folder after successful admin login
        /// </summary>
        public static void CleanupSetupCredentials()
        {
            try
            {
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var setupDir = Path.Combine(desktopPath, "PhotoBoothX-Setup-Credentials");

                if (Directory.Exists(setupDir))
                {
                    Directory.Delete(setupDir, true);
                    Console.WriteLine("🗑️ Setup credentials folder automatically deleted from Desktop");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Note: Could not auto-delete setup folder: {ex.Message}");
                Console.WriteLine("You can safely delete the PhotoBoothX-Setup-Credentials folder from Desktop manually.");
            }
        }

        /// <summary>
        /// Asynchronously cleans up the setup credentials folder after successful admin login
        /// </summary>
        public static async Task CleanupSetupCredentialsAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    var setupDir = Path.Combine(desktopPath, "PhotoBoothX-Setup-Credentials");

                    if (Directory.Exists(setupDir))
                    {
                        Directory.Delete(setupDir, true);
                        Console.WriteLine("🗑️ Setup credentials folder automatically deleted from Desktop");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Note: Could not auto-delete setup folder: {ex.Message}");
                    Console.WriteLine("You can safely delete the PhotoBoothX-Setup-Credentials folder from Desktop manually.");
                }
            });
        }

        private async Task CreateDefaultSettingsDirect(SqliteConnection connection)
        {
            try
            {
                Console.WriteLine("CreateDefaultSettingsDirect: Starting default settings creation...");
                
                // NOTE: Product pricing and enabled states removed - now managed exclusively in Products table
                var defaultSettings = new[]
                {
                    new { Id = Guid.NewGuid().ToString(), Category = "System", Key = "Volume", Value = "75", DataType = "Integer", Description = "Audio volume level (0-100)" },
                    new { Id = Guid.NewGuid().ToString(), Category = "System", Key = "LightsEnabled", Value = "true", DataType = "Boolean", Description = "Enable camera flash lights" },
                    new { Id = Guid.NewGuid().ToString(), Category = "Payment", Key = "PulsesPerCredit", Value = "1", DataType = "Integer", Description = "Arduino pulses required per $1 credit" },
                    new { Id = Guid.NewGuid().ToString(), Category = "System", Key = "Mode", Value = "Coin", DataType = "String", Description = "Operating mode: Coin or Free" },
                    new { Id = Guid.NewGuid().ToString(), Category = "RFID", Key = "Enabled", Value = "true", DataType = "Boolean", Description = "Enable RFID roll detection" },
                    new { Id = Guid.NewGuid().ToString(), Category = "Seasonal", Key = "AutoTemplates", Value = "true", DataType = "Boolean", Description = "Automatically enable/disable seasonal templates" }
                };

                var insertQuery = @"
                    INSERT INTO Settings (Id, Category, Key, Value, DataType, Description, IsUserEditable, UpdatedAt)
                    VALUES (@id, @category, @key, @value, @dataType, @description, @isUserEditable, @updatedAt)";

                foreach (var setting in defaultSettings)
                {
                    using var command = new SqliteCommand(insertQuery, connection);
                    command.Parameters.AddWithValue("@id", setting.Id);
                    command.Parameters.AddWithValue("@category", setting.Category);
                    command.Parameters.AddWithValue("@key", setting.Key);
                    command.Parameters.AddWithValue("@value", setting.Value);
                    command.Parameters.AddWithValue("@dataType", setting.DataType);
                    command.Parameters.AddWithValue("@description", setting.Description);
                    command.Parameters.AddWithValue("@isUserEditable", 1);
                    command.Parameters.AddWithValue("@updatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    
                    await command.ExecuteNonQueryAsync();
                    Console.WriteLine($"CreateDefaultSettingsDirect: Created setting {setting.Category}.{setting.Key}");
                }
                
                Console.WriteLine("CreateDefaultSettingsDirect: All default settings created successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CreateDefaultSettingsDirect error: {ex.Message}");
                throw;
            }
        }

        public async Task<DatabaseResult> DeleteAdminUserAsync(string userId, string? deletedBy = null)
        {
            try
            {
                Console.WriteLine($"DeleteAdminUserAsync: Attempting to delete user '{userId}', deleted by '{deletedBy}'");
                
                // First check if the user exists and get their username for logging
                var userResult = await GetByUserIdAsync<AdminUser>(userId);
                if (!userResult.Success || userResult.Data == null)
                {
                    Console.WriteLine($"DeleteAdminUserAsync: User '{userId}' not found");
                    return DatabaseResult.ErrorResult($"User with ID '{userId}' not found");
                }
                
                var username = userResult.Data.Username;
                Console.WriteLine($"DeleteAdminUserAsync: Found user '{username}' to delete");

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                
                // Temporarily disable foreign key constraints to work around migration issues
                Console.WriteLine("DeleteAdminUserAsync: Disabling foreign key constraints temporarily");
                using var disableFkCmd = new SqliteCommand("PRAGMA foreign_keys = OFF;", connection);
                await disableFkCmd.ExecuteNonQueryAsync();

                var query = "DELETE FROM AdminUsers WHERE UserId = @userId";
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@userId", userId);

                var rowsAffected = await command.ExecuteNonQueryAsync();
                Console.WriteLine($"DeleteAdminUserAsync: {rowsAffected} rows affected");
                
                // Re-enable foreign key constraints
                Console.WriteLine("DeleteAdminUserAsync: Re-enabling foreign key constraints");
                using var enableFkCmd = new SqliteCommand("PRAGMA foreign_keys = ON;", connection);
                await enableFkCmd.ExecuteNonQueryAsync();
                
                if (rowsAffected > 0)
                {
                    await LogSystemEventAsync(LogLevel.Info, "AdminUsers", $"Admin user '{username}' ({userId}) deleted", $"Deleted by: {deletedBy ?? "Unknown"}", deletedBy);
                    Console.WriteLine($"DeleteAdminUserAsync: User '{username}' deleted successfully");
                    return DatabaseResult.SuccessResult();
                }
                else
                {
                    Console.WriteLine($"DeleteAdminUserAsync: No rows affected, user may have already been deleted");
                    return DatabaseResult.ErrorResult("User not found or already deleted");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DeleteAdminUserAsync error: {ex}");
                return DatabaseResult.ErrorResult($"Failed to delete admin user: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult> CleanupProductSettingsAsync()
        {
            try
            {
                Console.WriteLine("CleanupProductSettingsAsync: Starting cleanup of redundant product settings...");
                
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                // List of product-related settings to remove from Settings table
                var productSettingsToRemove = new[]
                {
                    // Pricing category - now handled by Products table
                    new { Category = "Pricing", Key = "StripPrice" },
                    new { Category = "Pricing", Key = "Photo4x6Price" },
                    new { Category = "Pricing", Key = "SmartphonePrice" },
                    
                    // Products category - now handled by Products table
                    new { Category = "Products", Key = "StripEnabled" },
                    new { Category = "Products", Key = "Photo4x6Enabled" },
                    new { Category = "Products", Key = "PhonePrintsEnabled" }
                };

                int totalRemoved = 0;
                
                var deleteQuery = "DELETE FROM Settings WHERE Category = @category AND Key = @key";

                foreach (var setting in productSettingsToRemove)
                {
                    using var command = new SqliteCommand(deleteQuery, connection);
                    command.Parameters.AddWithValue("@category", setting.Category);
                    command.Parameters.AddWithValue("@key", setting.Key);
                    
                    var rowsAffected = await command.ExecuteNonQueryAsync();
                    if (rowsAffected > 0)
                    {
                        Console.WriteLine($"CleanupProductSettingsAsync: Removed {setting.Category}.{setting.Key} ({rowsAffected} rows)");
                        totalRemoved += rowsAffected;
                    }
                    else
                    {
                        Console.WriteLine($"CleanupProductSettingsAsync: Setting {setting.Category}.{setting.Key} not found (already cleaned)");
                    }
                }

                Console.WriteLine($"CleanupProductSettingsAsync: Cleanup completed. Removed {totalRemoved} total redundant settings.");
                
                // Log the cleanup action
                await LogSystemEventAsync(LogLevel.Info, "Database", 
                    $"Product settings cleanup completed", 
                    $"Removed {totalRemoved} redundant product-related settings from Settings table. Products now managed exclusively in Products table.");

                return DatabaseResult.SuccessResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CleanupProductSettingsAsync error: {ex}");
                return DatabaseResult.ErrorResult($"Failed to cleanup product settings: {ex.Message}", ex);
            }
        }


    }
} 
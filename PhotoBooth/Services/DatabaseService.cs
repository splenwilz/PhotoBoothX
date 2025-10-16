using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
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
        Task<DatabaseResult<List<Template>>> GetAllTemplatesAsync(bool showAllSeasons = false);
        Task<DatabaseResult<List<Template>>> GetTemplatesByTypeAsync(TemplateType templateType, bool showAllSeasons = false);
        Task<DatabaseResult<Template>> CreateTemplateAsync(Template template);
        Task<DatabaseResult<Template>> UpdateTemplateAsync(int templateId, string? name = null, bool? isActive = null, decimal? price = null, int? categoryId = null, string? description = null, int? sortOrder = null, int? photoCount = null, TemplateType? templateType = null);
        Task<DatabaseResult> BulkUpdateTemplateCategoryAsync(List<int> templateIds, int categoryId);
        Task<DatabaseResult> UpdateTemplatePathsAsync(int templateId, string folderPath, string templatePath, string previewPath);
        Task<DatabaseResult> UpdateTemplateFileSizeAsync(int templateId, long fileSize);
        Task<DatabaseResult> DeleteTemplateAsync(int templateId);
        Task<DatabaseResult<List<TemplateCategory>>> GetTemplateCategoriesAsync();
        Task<DatabaseResult<List<TemplateCategory>>> GetAllTemplateCategoriesAsync();
        Task<DatabaseResult<TemplateCategory>> CreateTemplateCategoryAsync(string name, string description = "", 
            bool isSeasonalCategory = false, string? seasonStartDate = null, string? seasonEndDate = null, int seasonalPriority = 0, bool isPremium = false);
        Task<DatabaseResult<TemplateCategory>> UpdateTemplateCategoryAsync(int categoryId, string name, string description = "",
            bool isSeasonalCategory = false, string? seasonStartDate = null, string? seasonEndDate = null, int seasonalPriority = 0, bool isPremium = false);
        Task<DatabaseResult> UpdateTemplateCategoryStatusAsync(int categoryId, bool isActive);
        Task<DatabaseResult> DeleteTemplateCategoryAsync(int categoryId);
        Task<DatabaseResult<List<Template>>> GetTemplatesByCategoryAsync(int categoryId);
        Task<DatabaseResult<List<TemplateLayout>>> GetTemplateLayoutsAsync();
        Task<DatabaseResult<TemplateLayout?>> GetTemplateLayoutAsync(string layoutId);
        Task<DatabaseResult<TemplateLayout?>> GetTemplateLayoutByKeyAsync(string layoutKey);
        Task<DatabaseResult<List<TemplatePhotoArea>>> GetTemplatePhotoAreasAsync(string layoutId);
        Task<DatabaseResult<Dictionary<string, List<TemplatePhotoArea>>>> GetPhotoAreasByLayoutIdsAsync(List<string> layoutIds);
        Task<DatabaseResult<List<HardwareStatusDto>>> GetHardwareStatusAsync();
        Task<DatabaseResult<PrintSupply?>> GetPrintSupplyAsync(SupplyType supplyType);
        Task<DatabaseResult> UpdatePrintSupplyAsync(SupplyType supplyType, int newCount);
        
        // Camera settings methods
        Task<DatabaseResult<List<CameraSettings>>> GetAllCameraSettingsAsync();
        Task<DatabaseResult<CameraSettings?>> GetCameraSettingAsync(string settingName);
        Task<DatabaseResult> SaveCameraSettingAsync(string settingName, int settingValue);
        Task<DatabaseResult> SaveAllCameraSettingsAsync(int brightness, int zoom, int contrast);
        
        // Product management methods
        Task<DatabaseResult<List<Product>>> GetProductsAsync();
        Task<DatabaseResult<List<ProductCategory>>> GetProductCategoriesAsync();
        Task<DatabaseResult> UpdateProductStatusAsync(int productId, bool isActive);
        Task<DatabaseResult> UpdateProductPriceAsync(int productId, decimal price);
        Task<DatabaseResult> UpdateProductAsync(int productId, bool? isActive = null, decimal? price = null, 
            bool? useCustomExtraCopyPricing = null, decimal? extraCopy1Price = null, decimal? extraCopy2Price = null, 
            decimal? extraCopy4BasePrice = null, decimal? extraCopyAdditionalPrice = null,
            // Simplified product-specific extra copy pricing
            decimal? stripsExtraCopyPrice = null, decimal? stripsMultipleCopyDiscount = null,
            decimal? photo4x6ExtraCopyPrice = null, decimal? photo4x6MultipleCopyDiscount = null,
            decimal? smartphoneExtraCopyPrice = null, decimal? smartphoneMultipleCopyDiscount = null);
        Task<DatabaseResult> UpdateProductAsync(int productId, ProductUpdateRequest request);
        Task<DatabaseResult<List<Setting>>> GetSettingsByCategoryAsync(string category);
        Task<DatabaseResult<T?>> GetSettingValueAsync<T>(string category, string key);
        Task<DatabaseResult> SetSettingValueAsync<T>(string category, string key, T value, string? updatedBy = null);
        Task<DatabaseResult> CleanupProductSettingsAsync();
        Task<DatabaseResult<SystemDateStatus>> GetSystemDateStatusAsync();
        
        // Credit transaction management methods
        Task<DatabaseResult<int>> InsertCreditTransactionAsync(CreditTransaction transaction);
        Task<DatabaseResult<List<CreditTransaction>>> GetCreditTransactionsAsync(int limit = 50);
        Task<DatabaseResult> DeleteOldCreditTransactionsAsync(int keepCount = 100);
        
        // Transaction management methods
        Task<DatabaseResult<List<Transaction>>> GetRecentTransactionsAsync(int limit);
        
        // Template pricing methods
        Task<DatabaseResult> UpdateTemplatePricingAsync();
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

        /// <summary>
        /// Create and open a SQLite connection with foreign keys enabled
        /// </summary>
        private async Task<SqliteConnection> CreateConnectionAsync()
        {
            var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            
            // Enable foreign key constraints for this connection
            using var fkCmd = new SqliteCommand("PRAGMA foreign_keys = ON;", connection);
            await fkCmd.ExecuteNonQueryAsync();
            
            return connection;
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

                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using var connection = await CreateConnectionAsync();

                // Check if database is already initialized
#if DEBUG
                Console.WriteLine("=== CHECKING IF DATABASE EXISTS ===");
#endif
                var checkQuery = "SELECT name FROM sqlite_master WHERE type='table' AND name='AdminUsers';";
                using var checkCommand = new SqliteCommand(checkQuery, connection);
                var tableExists = await checkCommand.ExecuteScalarAsync();
                
#if DEBUG
                Console.WriteLine($"Table 'AdminUsers' exists: {tableExists != null}");
                Console.WriteLine($"Database path: {_databasePath}");
#endif

                if (tableExists != null)
                {
                    // Database already initialized - check for and apply migrations
                    await ApplyMigrations(connection);

                    // Update template pricing for any templates with $0 price
                    await UpdateTemplatePricingAsync();

                    return DatabaseResult.SuccessResult();
                }

                // Database needs initialization - read and execute schema
#if DEBUG
                Console.WriteLine("=== DATABASE IS NEW - INITIALIZING SCHEMA ===");
#endif
                var schemaPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database_Schema.sql");

#if DEBUG
                Console.WriteLine($"Schema path: {schemaPath}");
#endif
                if (!File.Exists(schemaPath))
                {
#if DEBUG
                    Console.WriteLine("=== SCHEMA FILE NOT FOUND ===");
#endif
                    var errorMsg = $"Database schema file not found at: {schemaPath}";
                    return DatabaseResult.ErrorResult(errorMsg);
                }
#if DEBUG
                Console.WriteLine("=== SCHEMA FILE FOUND, READING CONTENT ===");
#endif

                var schemaScript = await File.ReadAllTextAsync(schemaPath);
#if DEBUG
                Console.WriteLine($"Schema script length: {schemaScript.Length} characters");
#endif

                // Split and execute commands
                var commands = schemaScript.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
#if DEBUG
                Console.WriteLine($"Found {commands.Length} SQL commands to execute");
#endif

                using var transaction = connection.BeginTransaction();
                try
                {
#if DEBUG
                    Console.WriteLine("=== STARTING TRANSACTION ===");
#endif
                    int commandIndex = 0;
                    foreach (var commandText in commands)
                    {
                        commandIndex++;
                        LoggingService.Application.Information("Executing database command",
                            ("CommandIndex", commandIndex),
                            ("CommandLength", commands.Length));

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
                            LoggingService.Application.Information("Skipping empty command",
                                ("CommandIndex", commandIndex));
                            continue;
                        }
                        
                        var trimmedCommand = string.Join(" ", cleanLines);
                        LoggingService.Application.Information("Executing",
                            ("Command", trimmedCommand.Substring(0, Math.Min(50, trimmedCommand.Length))));

                        using var command = new SqliteCommand(trimmedCommand, connection, transaction);
                        await command.ExecuteNonQueryAsync();
                        LoggingService.Application.Information("Command executed successfully",
                            ("CommandIndex", commandIndex));
                    }

                    LoggingService.Application.Information("Committing transaction...");
                    await transaction.CommitAsync();
                    LoggingService.Application.Information("Transaction committed successfully");

                    // Create default admin users (only for new database)
#if DEBUG
                    Console.WriteLine("=== STARTING ADMIN USER CREATION ===");
#endif
                    LoggingService.Application.Information("Creating default admin users...");
                    await CreateDefaultAdminUserDirect(connection);
                    LoggingService.Application.Information("Default admin users created");
#if DEBUG
                    Console.WriteLine("=== ADMIN USER CREATION COMPLETED ===");
#endif

                    // Create default system settings (only for new database)
                    LoggingService.Application.Information("Creating default settings...");
                    await CreateDefaultSettingsDirect(connection);
                    LoggingService.Application.Information("Default settings created");

                    LoggingService.Application.Information("Database initialization completed successfully!");
#if DEBUG
                    Console.WriteLine("=== DATABASE INITIALIZATION COMPLETED SUCCESSFULLY ===");
#endif
                }
                catch (Exception ex)
                {
#if DEBUG
                    Console.WriteLine("=== DATABASE INITIALIZATION ERROR ===");
                    Console.WriteLine($"Error type: {ex.GetType().Name}");
                    Console.WriteLine($"Error message: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
#endif
                    LoggingService.Application.Error("Database initialization error", ex,
                        ("ErrorMessage", ex.Message),
                        ("StackTrace", ex.StackTrace ?? "No stack trace available"));
                    LoggingService.Application.Error("Rolling back transaction");
                    await transaction.RollbackAsync();
                    LoggingService.Application.Error("Transaction rolled back");
                    throw;
                }
                
                // Update template pricing for any templates with $0 price (runs for new databases)
                await UpdateTemplatePricingAsync();
                
                return DatabaseResult.SuccessResult();
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.WriteLine("=== OUTER CATCH: DATABASE INITIALIZATION FAILED ===");
                Console.WriteLine($"Outer error type: {ex.GetType().Name}");
                Console.WriteLine($"Outer error message: {ex.Message}");
                Console.WriteLine($"Outer stack trace: {ex.StackTrace}");
#endif
                var errorMsg = $"Database initialization failed: {ex.Message}";
                LoggingService.Application.Error("Database initialization failed", ex,
                    ("ErrorMessage", ex.Message));
                return DatabaseResult.ErrorResult(errorMsg, ex);
            }
        }

        private async Task ApplyMigrations(SqliteConnection connection)
        {

            try
            {
                // Get current schema version
                var currentVersion = await GetDatabaseVersionAsync(connection);
                var expectedVersion = GetExpectedSchemaVersion();

                if (currentVersion == expectedVersion)
                {

                    return;
                }
                
                // Apply incremental migrations for version differences
                await ApplyIncrementalMigrations(connection, currentVersion, expectedVersion);
                
                // Update database version
                await SetDatabaseVersionAsync(connection, expectedVersion);

            }
            catch
            {

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
            catch
            {

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
            catch
            {

            }
        }
        
        private int GetExpectedSchemaVersion()
        {
            return 1; // Keep at version 1 during development - we recreate DB instead of migrating
        }
        
        /// <summary>
        /// Apply incremental database migrations (production only)
        /// During development phase: No migrations - schema changes go directly into Database_Schema.sql
        /// After v1.0 release: Implement proper migrations for production database updates
        /// </summary>
        private async Task ApplyIncrementalMigrations(SqliteConnection connection, int fromVersion, int toVersion)
        {
            // Development phase: No migrations needed - just recreate DB with updated schema
            // Production phase (post v1.0): Implement version-specific migrations here
            await Task.CompletedTask;
        }
        


        public async Task<DatabaseResult<List<T>>> GetAllAsync<T>() where T : class, new()
        {
            try
            {
                var tableName = GetTableName<T>();
                var query = $"SELECT * FROM {tableName}";

                using var connection = await CreateConnectionAsync();
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

                using var connection = await CreateConnectionAsync();
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

                using var connection = await CreateConnectionAsync();
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
                    
                    // Skip navigation properties (complex objects that are not primitives, strings, enums, or DateTime)
                    if (IsNavigationProperty(prop.PropertyType)) continue;

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

                // Debug logging for Transaction insertions (minimal)
                if (typeof(T) == typeof(Transaction))
                {
                    Console.WriteLine($"--- DATABASE DEBUG: Inserting Transaction with {parameters.Count} parameters ---");
                }

                using var connection = await CreateConnectionAsync();
                using var command = new SqliteCommand(query, connection);

                for (int i = 0; i < parameters.Count; i++)
                {
                    command.Parameters.AddWithValue(parameters[i], values[i] ?? DBNull.Value);
                }

                var newId = Convert.ToInt32(await command.ExecuteScalarAsync());
                
                if (typeof(T) == typeof(Transaction))
                {
                    Console.WriteLine($"--- DATABASE DEBUG: Transaction inserted with ID: {newId} ---");
                }
                
                return DatabaseResult<int>.SuccessResult(newId);
            }
            catch (Exception ex)
            {
                if (typeof(T) == typeof(Transaction))
                {
                    Console.WriteLine($"--- DATABASE DEBUG: Transaction insertion failed: {ex.Message} ---");
                }
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

                    // Skip navigation properties (complex objects that are not primitives, strings, enums, or DateTime)
                    if (IsNavigationProperty(prop.PropertyType)) continue;

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

                // Debug logging for Transaction updates
                if (typeof(T) == typeof(Transaction))
                {
                    Console.WriteLine($"--- DATABASE DEBUG: Updating Transaction with {setParts.Count} fields ---");
                    Console.WriteLine($"Query: {query}");
                }

                using var connection = await CreateConnectionAsync();
                using var command = new SqliteCommand(query, connection);

                command.Parameters.AddWithValue("@Id", idValue);
                for (int i = 0; i < setParts.Count; i++)
                {
                    var paramName = setParts[i].Split('=')[0].Trim() + "Value";
                    command.Parameters.AddWithValue($"@{setParts[i].Split('=')[0].Trim()}", values[i] ?? DBNull.Value);
                }

                await command.ExecuteNonQueryAsync();
                
                if (typeof(T) == typeof(Transaction))
                {
                    Console.WriteLine($"--- DATABASE DEBUG: Transaction update completed successfully ---");
                }
                
                return DatabaseResult.SuccessResult();
            }
            catch (Exception ex)
            {
                if (typeof(T) == typeof(Transaction))
                {
                    Console.WriteLine($"--- DATABASE DEBUG: Transaction update failed: {ex.Message} ---");
                }
                return DatabaseResult.ErrorResult($"Failed to update {typeof(T).Name}: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult> DeleteAsync<T>(int id) where T : class
        {
            try
            {
                var tableName = GetTableName<T>();
                var query = $"DELETE FROM {tableName} WHERE Id = @id";

                using var connection = await CreateConnectionAsync();
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
                
                using var connection = await CreateConnectionAsync();
                
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@username", username);
                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    var user = MapReaderToEntity<AdminUser>(reader);
                    var storedHash = reader["PasswordHash"].ToString();
                    
                    // Compare password using secure hash-only approach
                    var inputHash = HashPassword(password);
                    
                    if (storedHash == inputHash)
                    {
                        // Update last login - do this asynchronously without blocking
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                using var updateConnection = await CreateConnectionAsync();
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
                
                using var connection = await CreateConnectionAsync();
                
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
                    // Use secure hash-only comparison
                    var inputHash = HashPassword(password);
                    var passwordMatches = storedHash == inputHash;
                    
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

                using var connection = await CreateConnectionAsync();
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@newPassword", HashPassword(newPassword));
                command.Parameters.AddWithValue("@accessLevel", accessLevel.ToString());

                await command.ExecuteNonQueryAsync();
                
                // Use file-based logging instead of database logging
                LoggingService.Application.Information("Admin password updated",
                    ("AccessLevel", accessLevel.ToString()),
                    ("UpdatedBy", updatedBy ?? "Unknown"));
                
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

                using var connection = await CreateConnectionAsync();
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@newPassword", HashPassword(newPassword));
                command.Parameters.AddWithValue("@userId", userId);
                command.Parameters.AddWithValue("@updatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                command.Parameters.AddWithValue("@updatedBy", updatedBy ?? userId); // Self-update if no updatedBy specified

                await command.ExecuteNonQueryAsync();
                
                // Use file-based logging instead of database logging
                LoggingService.Application.Information("User password updated - setup credentials converted",
                    ("UserId", userId),
                    ("UpdatedBy", updatedBy ?? "Self"));
                
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

                var query = @"
                    INSERT INTO AdminUsers (UserId, Username, DisplayName, PasswordHash, AccessLevel, IsActive, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
                    VALUES (@userId, @username, @displayName, @passwordHash, @accessLevel, @isActive, @createdAt, @updatedAt, @createdBy, @updatedBy)";

                using var connection = await CreateConnectionAsync();
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

                await command.ExecuteNonQueryAsync();

                // Use file-based logging instead of database logging
                LoggingService.Application.Information("Admin user created",
                    ("Username", user.Username),
                    ("AccessLevel", user.AccessLevel.ToString()),
                    ("CreatedBy", createdBy ?? "System"));
                
                return DatabaseResult.SuccessResult();
            }
            catch (Exception ex)
            {

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
                        COUNT(DISTINCT t.Id) as TransactionCount,
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

                using var connection = await CreateConnectionAsync();
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
                Console.WriteLine($"--- SALES DEBUG: Getting daily sales for {dateString} ---");
                
                var query = @"
                    SELECT 
                        @date as Date,
                        COALESCE(SUM(t.TotalPrice), 0) as TotalRevenue,
                        COALESCE(COUNT(DISTINCT t.Id), 0) as TotalTransactions,
                        COALESCE(SUM(CASE WHEN pc.Name = 'Strips' THEN 1 ELSE 0 END), 0) as StripSales,
                        COALESCE(SUM(CASE WHEN pc.Name = '4x6' THEN 1 ELSE 0 END), 0) as Photo4x6Sales,
                        COALESCE(SUM(CASE WHEN pc.Name = 'Smartphone' THEN 1 ELSE 0 END), 0) as SmartphonePrintSales,
                        COALESCE(SUM(CASE WHEN t.PaymentMethod = 'Cash' THEN t.TotalPrice ELSE 0 END), 0) as CashPayments,
                        COALESCE(SUM(CASE WHEN t.PaymentMethod = 'Credit' THEN t.TotalPrice ELSE 0 END), 0) as CreditPayments,
                        COALESCE(SUM(CASE WHEN t.PaymentMethod = 'Free' THEN 1 ELSE 0 END), 0) as FreeTransactions,
                        COALESCE(SUM(pj.PrintsUsed), 0) as PrintsUsed,
                        datetime('now') as CreatedAt,
                        datetime('now') as UpdatedAt
                    FROM (SELECT @date as QueryDate) dates
                    LEFT JOIN Transactions t ON DATE(t.CreatedAt) = @date AND t.PaymentStatus = 'Completed'
                    LEFT JOIN Products p ON t.ProductId = p.Id
                    LEFT JOIN ProductCategories pc ON p.CategoryId = pc.Id
                    LEFT JOIN PrintJobs pj ON t.Id = pj.TransactionId
                    GROUP BY dates.QueryDate";

                // First, let's debug what transactions exist
                var debugQuery = "SELECT Id, TransactionCode, CreatedAt, PaymentStatus, TotalPrice FROM Transactions ORDER BY CreatedAt DESC LIMIT 5";
                using var connection = await CreateConnectionAsync();
                
                using var debugCommand = new SqliteCommand(debugQuery, connection);
                using var debugReader = await debugCommand.ExecuteReaderAsync();
                Console.WriteLine($"--- SALES DEBUG: Recent transactions ---");
                while (await debugReader.ReadAsync())
                {
                    Console.WriteLine($"ID: {debugReader["Id"]}, Code: {debugReader["TransactionCode"]}, CreatedAt: {debugReader["CreatedAt"]}, Status: {debugReader["PaymentStatus"]}, Amount: ${debugReader["TotalPrice"]}");
                }
                debugReader.Close();
                
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@date", dateString);
                Console.WriteLine($"--- SALES DEBUG: Executing query with date parameter: {dateString} ---");
                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    var totalRevenue = Convert.ToDecimal(reader["TotalRevenue"]);
                    var totalTransactions = Convert.ToInt32(reader["TotalTransactions"]);
                    
                    Console.WriteLine($"--- SALES DEBUG: Query result - Revenue: ${totalRevenue}, Transactions: {totalTransactions} ---");
                    
                    var summary = new DailySalesSummary
                    {
                        Date = reader["Date"].ToString() ?? dateString,
                        TotalRevenue = totalRevenue,
                        TotalTransactions = totalTransactions,
                        StripSales = Convert.ToInt32(reader["StripSales"]),
                        Photo4x6Sales = Convert.ToInt32(reader["Photo4x6Sales"]),
                        SmartphonePrintSales = Convert.ToInt32(reader["SmartphonePrintSales"]),
                        CashPayments = Convert.ToDecimal(reader["CashPayments"]),
                        CreditPayments = Convert.ToDecimal(reader["CreditPayments"]),
                        FreeTransactions = Convert.ToInt32(reader["FreeTransactions"]),
                        PrintsUsed = Convert.ToInt32(reader["PrintsUsed"]),
                        CreatedAt = DateTime.Parse(reader["CreatedAt"].ToString() ?? DateTime.Now.ToString()),
                        UpdatedAt = DateTime.Parse(reader["UpdatedAt"].ToString() ?? DateTime.Now.ToString())
                    };
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

                using var connection = await CreateConnectionAsync();
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

        public async Task<DatabaseResult<List<Template>>> GetAllTemplatesAsync(bool showAllSeasons = false)
        {
            try
            {
                var templates = new List<Template>();
                
                var query = @"
                    SELECT 
                        t.Id, t.Name, t.CategoryId, t.LayoutId,
                        t.FolderPath, t.TemplatePath, t.PreviewPath,
                        t.IsActive, t.Price, t.SortOrder, t.FileSize,
                        t.Description, t.UploadedAt, t.UploadedBy, t.TemplateType,
                        tc.Name as CategoryName, tc.IsSeasonalCategory, tc.SeasonStartDate, tc.SeasonEndDate, tc.SeasonalPriority, tc.IsPremium, tc.SortOrder as CategorySortOrder,
                        tl.Name as LayoutName, tl.LayoutKey, tl.Width, tl.Height, tl.PhotoCount, tl.ProductCategoryId,
                        pc.Name as ProductCategoryName,
                        -- Calculate effective priority for seasonal ordering
                        CASE 
                            WHEN tc.IsSeasonalCategory = 1 AND (
                                -- Normal seasons (start <= end): check if current date is within range
                                (tc.SeasonStartDate <= tc.SeasonEndDate AND strftime('%m-%d', 'now') BETWEEN tc.SeasonStartDate AND tc.SeasonEndDate)
                                OR
                                -- Cross-year seasons (start > end): check if current date is in either part of the range
                                (tc.SeasonStartDate > tc.SeasonEndDate AND (strftime('%m-%d', 'now') >= tc.SeasonStartDate OR strftime('%m-%d', 'now') <= tc.SeasonEndDate))
                            ) THEN tc.SeasonalPriority
                            ELSE 0
                        END as EffectivePriority
                    FROM Templates t
                    LEFT JOIN TemplateCategories tc ON t.CategoryId = tc.Id
                    LEFT JOIN TemplateLayouts tl ON t.LayoutId = tl.Id
                    LEFT JOIN ProductCategories pc ON tl.ProductCategoryId = pc.Id
                    ORDER BY 
                        -- First: Active seasonal categories by their priority (highest first)
                        CASE 
                            WHEN tc.IsSeasonalCategory = 1 AND (
                                -- Normal seasons (start <= end): check if current date is within range
                                (tc.SeasonStartDate <= tc.SeasonEndDate AND strftime('%m-%d', 'now') BETWEEN tc.SeasonStartDate AND tc.SeasonEndDate)
                                OR
                                -- Cross-year seasons (start > end): check if current date is in either part of the range
                                (tc.SeasonStartDate > tc.SeasonEndDate AND (strftime('%m-%d', 'now') >= tc.SeasonStartDate OR strftime('%m-%d', 'now') <= tc.SeasonEndDate))
                            ) THEN tc.SeasonalPriority
                            ELSE 0
                        END DESC,
                        -- Then: Regular category sort order
                        tc.SortOrder ASC,
                        -- Finally: Template sort order within category
                        t.SortOrder ASC";

                            using (var connection = await CreateConnectionAsync())
            {
                    using (var command = new SqliteCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var template = new Template
                                {
                                    Id = GetIntValue(reader, "Id"),
                                    Name = GetStringValue(reader, "Name"),
                                    CategoryId = GetIntValue(reader, "CategoryId"),
                                    LayoutId = GetStringValue(reader, "LayoutId"),
                                    FolderPath = GetStringValue(reader, "FolderPath"),
                                    TemplatePath = GetStringValue(reader, "TemplatePath"),
                                    PreviewPath = GetStringValue(reader, "PreviewPath"),
                                    IsActive = GetBoolValue(reader, "IsActive"),
                                    Price = (decimal)GetDoubleValue(reader, "Price"),
                                    SortOrder = GetIntValue(reader, "SortOrder"),
                                    FileSize = GetLongValue(reader, "FileSize"),
                                    Description = GetStringValue(reader, "Description"),
                                    UploadedAt = DateTime.Parse(GetStringValue(reader, "UploadedAt")),
                                    UploadedBy = GetStringValue(reader, "UploadedBy"),
                                    TemplateType = (TemplateType)GetIntValue(reader, "TemplateType", (int)TemplateType.Strip)
                                };

                                // Set CategoryName for display (used by UI)
                                template.CategoryName = reader["CategoryName"].ToString() ?? "";

                                // Set category information if available
                                if (reader["CategoryName"] != DBNull.Value)
                                {
                                    template.Category = new TemplateCategory 
                                    { 
                                        Id = template.CategoryId, 
                                        Name = reader["CategoryName"].ToString() ?? "",
                                        IsSeasonalCategory = GetBoolValue(reader, "IsSeasonalCategory", false),
                                        SeasonStartDate = GetStringValue(reader, "SeasonStartDate"),
                                        SeasonEndDate = GetStringValue(reader, "SeasonEndDate"),
                                        SeasonalPriority = GetIntValue(reader, "SeasonalPriority", 0),
                                        SortOrder = GetIntValue(reader, "CategorySortOrder", 0)
                                    };
                                }

                                // Set layout information if available
                                if (reader["LayoutName"] != DBNull.Value)
                                {
                                    template.Layout = new TemplateLayout
                                    {
                                        Id = template.LayoutId,
                                        Name = reader["LayoutName"].ToString() ?? "",
                                        LayoutKey = GetStringValue(reader, "LayoutKey"),
                                        Width = GetIntValue(reader, "Width"),
                                        Height = GetIntValue(reader, "Height"),
                                        PhotoCount = GetIntValue(reader, "PhotoCount"),
                                        ProductCategoryId = GetIntValue(reader, "ProductCategoryId")
                                    };
                                }

                                templates.Add(template);
                            }
                        }
                    }
                }

                // Load all photo areas for templates with layouts in a single query (N+1 optimization)
                var layoutIds = templates.Where(t => t.Layout != null).Select(t => t.LayoutId).Distinct().ToList();
                if (layoutIds.Any())
                {
                    var photoAreasResult = await GetPhotoAreasByLayoutIdsAsync(layoutIds);
                    if (photoAreasResult.Success && photoAreasResult.Data != null)
                    {
                        // Map photo areas to templates
                        foreach (var template in templates)
                        {
                            if (template.Layout != null && photoAreasResult.Data.ContainsKey(template.LayoutId))
                            {
                                template.Layout.PhotoAreas = photoAreasResult.Data[template.LayoutId];
                            }
                        }
                    }
                }

                // Apply seasonal filtering: only keep templates from categories that are currently in season (or non-seasonal)
                // Skip filtering if showAllSeasons is true (admin wants to see all templates regardless of season)
                if (showAllSeasons)
                {
                    // Return all templates without seasonal filtering
                    return DatabaseResult<List<Template>>.SuccessResult(templates);
                }
                else
                {
                    // Apply normal seasonal filtering
                    var filteredTemplates = new List<Template>();
                    foreach (var template in templates)
                    {
                        if (template.Category == null || !template.Category.IsSeasonalCategory || template.Category.IsCurrentlyInSeason)
                        {
                            filteredTemplates.Add(template);
                        }
                    }
                    return DatabaseResult<List<Template>>.SuccessResult(filteredTemplates);
                }
            }
            catch (Exception ex)
            {
                return DatabaseResult<List<Template>>.ErrorResult($"Failed to get templates: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult<List<Template>>> GetTemplatesByTypeAsync(TemplateType templateType, bool showAllSeasons = false)
        {
            try
            {
                var templates = new List<Template>();
                
                var query = @"
                    SELECT 
                        t.Id, t.Name, t.CategoryId, t.LayoutId,
                        t.FolderPath, t.TemplatePath, t.PreviewPath,
                        t.IsActive, t.Price, t.SortOrder, t.FileSize,
                        t.Description, t.UploadedAt, t.UploadedBy, t.TemplateType,
                        tc.Name as CategoryName, tc.IsSeasonalCategory, tc.SeasonStartDate, tc.SeasonEndDate, tc.SeasonalPriority, tc.IsPremium, tc.SortOrder as CategorySortOrder,
                        tl.Name as LayoutName, tl.LayoutKey, tl.Width, tl.Height, tl.PhotoCount, tl.ProductCategoryId,
                        pc.Name as ProductCategoryName,
                        -- Calculate effective priority for seasonal ordering
                        CASE 
                            WHEN tc.IsSeasonalCategory = 1 AND (
                                -- Normal seasons (start <= end): check if current date is within range
                                (tc.SeasonStartDate <= tc.SeasonEndDate AND strftime('%m-%d', 'now') BETWEEN tc.SeasonStartDate AND tc.SeasonEndDate)
                                OR
                                -- Cross-year seasons (start > end): check if current date is in either part of the range
                                (tc.SeasonStartDate > tc.SeasonEndDate AND (strftime('%m-%d', 'now') >= tc.SeasonStartDate OR strftime('%m-%d', 'now') <= tc.SeasonEndDate))
                            ) THEN tc.SeasonalPriority
                            ELSE 0
                        END as EffectivePriority
                    FROM Templates t
                    LEFT JOIN TemplateCategories tc ON t.CategoryId = tc.Id
                    LEFT JOIN TemplateLayouts tl ON t.LayoutId = tl.Id
                    LEFT JOIN ProductCategories pc ON tl.ProductCategoryId = pc.Id
                    WHERE t.TemplateType = @TemplateType AND t.IsActive = 1
                    ORDER BY 
                        -- First: Active seasonal categories by their priority (highest first)
                        CASE 
                            WHEN tc.IsSeasonalCategory = 1 AND (
                                -- Normal seasons (start <= end): check if current date is within range
                                (tc.SeasonStartDate <= tc.SeasonEndDate AND strftime('%m-%d', 'now') BETWEEN tc.SeasonStartDate AND tc.SeasonEndDate)
                                OR
                                -- Cross-year seasons (start > end): check if current date is in either part of the range
                                (tc.SeasonStartDate > tc.SeasonEndDate AND (strftime('%m-%d', 'now') >= tc.SeasonStartDate OR strftime('%m-%d', 'now') <= tc.SeasonEndDate))
                            ) THEN tc.SeasonalPriority
                            ELSE 0
                        END DESC,
                        -- Then: Regular category sort order
                        tc.SortOrder ASC,
                        -- Finally: Template sort order within category
                        t.SortOrder ASC";

                            using (var connection = await CreateConnectionAsync())
            {
                    using (var command = new SqliteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@TemplateType", (int)templateType);
                        
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var template = new Template
                                {
                                    Id = GetIntValue(reader, "Id"),
                                    Name = GetStringValue(reader, "Name"),
                                    CategoryId = GetIntValue(reader, "CategoryId"),
                                    LayoutId = GetStringValue(reader, "LayoutId"),
                                    FolderPath = GetStringValue(reader, "FolderPath"),
                                    TemplatePath = GetStringValue(reader, "TemplatePath"),
                                    PreviewPath = GetStringValue(reader, "PreviewPath"),
                                    IsActive = GetBoolValue(reader, "IsActive"),
                                    Price = (decimal)GetDoubleValue(reader, "Price"),
                                    SortOrder = GetIntValue(reader, "SortOrder"),
                                    FileSize = GetLongValue(reader, "FileSize"),
                                    Description = GetStringValue(reader, "Description"),
                                    UploadedAt = DateTime.Parse(GetStringValue(reader, "UploadedAt")),
                                    UploadedBy = GetStringValue(reader, "UploadedBy"),
                                    TemplateType = (TemplateType)GetIntValue(reader, "TemplateType", (int)TemplateType.Strip)
                                };

                                // Set CategoryName for display (used by UI)
                                template.CategoryName = reader["CategoryName"].ToString() ?? "";

                                // Set category information if available
                                if (reader["CategoryName"] != DBNull.Value)
                                {
                                    template.Category = new TemplateCategory 
                                    { 
                                        Id = template.CategoryId, 
                                        Name = reader["CategoryName"].ToString() ?? "",
                                        IsSeasonalCategory = GetBoolValue(reader, "IsSeasonalCategory", false),
                                        SeasonStartDate = GetStringValue(reader, "SeasonStartDate"),
                                        SeasonEndDate = GetStringValue(reader, "SeasonEndDate"),
                                        SeasonalPriority = GetIntValue(reader, "SeasonalPriority", 0),
                                        SortOrder = GetIntValue(reader, "CategorySortOrder", 0)
                                    };
                                }

                                // Set layout information if available
                                if (reader["LayoutName"] != DBNull.Value)
                                {
                                    template.Layout = new TemplateLayout
                                    {
                                        Id = template.LayoutId,
                                        Name = reader["LayoutName"].ToString() ?? "",
                                        LayoutKey = GetStringValue(reader, "LayoutKey"),
                                        Width = GetIntValue(reader, "Width"),
                                        Height = GetIntValue(reader, "Height"),
                                        PhotoCount = GetIntValue(reader, "PhotoCount"),
                                        ProductCategoryId = GetIntValue(reader, "ProductCategoryId")
                                    };
                                }

                                templates.Add(template);
                            }
                        }
                    }
                }

                // Load all photo areas for templates with layouts in a single query (N+1 optimization)
                var layoutIds = templates.Where(t => t.Layout != null).Select(t => t.LayoutId).Distinct().ToList();
                if (layoutIds.Any())
                {
                    var photoAreasResult = await GetPhotoAreasByLayoutIdsAsync(layoutIds);
                    if (photoAreasResult.Success && photoAreasResult.Data != null)
                    {
                        // Map photo areas to templates
                        foreach (var template in templates)
                        {
                            if (template.Layout != null && photoAreasResult.Data.ContainsKey(template.LayoutId))
                            {
                                template.Layout.PhotoAreas = photoAreasResult.Data[template.LayoutId];
                            }
                        }
                    }
                }

                // Apply seasonal filtering: only keep templates from categories that are currently in season (or non-seasonal)
                // Skip filtering if showAllSeasons is true (admin wants to see all templates regardless of season)
                if (showAllSeasons)
                {
                    // Return all templates without seasonal filtering
                    return DatabaseResult<List<Template>>.SuccessResult(templates);
                }
                else
                {
                    // Apply normal seasonal filtering
                    var filteredTemplates = new List<Template>();
                    foreach (var template in templates)
                    {
                        if (template.Category == null || !template.Category.IsSeasonalCategory || template.Category.IsCurrentlyInSeason)
                        {
                            filteredTemplates.Add(template);
                        }
                    }
                    return DatabaseResult<List<Template>>.SuccessResult(filteredTemplates);
                }
            }
            catch (Exception ex)
            {
                return DatabaseResult<List<Template>>.ErrorResult($"Failed to get templates by type: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult<Template>> CreateTemplateAsync(Template template)
        {
            try
            {
                // Set default price based on template type if no price is specified
                if (template.Price == 0)
                {
                    template.Price = GetDefaultTemplatePrice(template.TemplateType);
                }

                var query = @"
                    INSERT INTO Templates (Name, CategoryId, LayoutId,
                                         FolderPath, TemplatePath, PreviewPath,
                                         IsActive, Price, SortOrder, FileSize,
                                         Description, UploadedAt, UploadedBy, TemplateType)
                    VALUES (@Name, @CategoryId, @LayoutId,
                            @FolderPath, @TemplatePath, @PreviewPath,
                            @IsActive, @Price, @SortOrder, @FileSize,
                            @Description, @UploadedAt, @UploadedBy, @TemplateType);
                    SELECT last_insert_rowid();";

                using var connection = await CreateConnectionAsync();
                using var command = new SqliteCommand(query, connection);
                
                command.Parameters.AddWithValue("@Name", template.Name);
                command.Parameters.AddWithValue("@CategoryId", template.CategoryId);
                command.Parameters.AddWithValue("@LayoutId", template.LayoutId);
                command.Parameters.AddWithValue("@FolderPath", template.FolderPath);
                command.Parameters.AddWithValue("@TemplatePath", template.TemplatePath);
                command.Parameters.AddWithValue("@PreviewPath", template.PreviewPath);
                command.Parameters.AddWithValue("@IsActive", template.IsActive);
                command.Parameters.AddWithValue("@Price", template.Price);
                command.Parameters.AddWithValue("@SortOrder", template.SortOrder);
                command.Parameters.AddWithValue("@FileSize", template.FileSize);
                command.Parameters.AddWithValue("@Description", template.Description);
                command.Parameters.AddWithValue("@UploadedAt", template.UploadedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                command.Parameters.AddWithValue("@UploadedBy", template.UploadedBy ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@TemplateType", (int)template.TemplateType);

                var newId = await command.ExecuteScalarAsync();
                template.Id = Convert.ToInt32(newId);

                return DatabaseResult<Template>.SuccessResult(template);
            }
            catch (Exception ex)
            {
                return DatabaseResult<Template>.ErrorResult($"Failed to create template: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult<Template>> UpdateTemplateAsync(int templateId, string? name = null, bool? isActive = null, decimal? price = null, int? categoryId = null, string? description = null, int? sortOrder = null, int? photoCount = null, TemplateType? templateType = null)
        {
            try
            {
                var updates = new List<string>();
                var parameters = new List<(string name, object value)>();

                if (name != null)
                {
                    updates.Add("Name = @Name");
                    parameters.Add(("@Name", name));
                }

                if (isActive.HasValue)
                {
                    updates.Add("IsActive = @IsActive");
                    parameters.Add(("@IsActive", isActive.Value));
                }

                if (price.HasValue)
                {
                    updates.Add("Price = @Price");
                    parameters.Add(("@Price", price.Value));
                }

                if (categoryId.HasValue)
                {
                    updates.Add("CategoryId = @CategoryId");
                    parameters.Add(("@CategoryId", categoryId.Value));
                }

                if (description != null)
                {
                    updates.Add("Description = @Description");
                    parameters.Add(("@Description", description));
                }

                if (sortOrder.HasValue)
                {
                    updates.Add("SortOrder = @SortOrder");
                    parameters.Add(("@SortOrder", sortOrder.Value));
                }

                if (templateType.HasValue)
                {
                    updates.Add("TemplateType = @TemplateType");
                    parameters.Add(("@TemplateType", (int)templateType.Value));
                }

                // PhotoCount is now computed from layout, no longer stored in template table

                if (!updates.Any())
                {
                    return DatabaseResult<Template>.ErrorResult("No fields provided to update");
                }

                var query = $@"
                    UPDATE Templates 
                    SET {string.Join(", ", updates)}
                    WHERE Id = @Id;
                    
                    SELECT * FROM Templates WHERE Id = @Id;";

                using var connection = await CreateConnectionAsync();
                using var command = new SqliteCommand(query, connection);
                
                command.Parameters.AddWithValue("@Id", templateId);
                foreach (var param in parameters)
                {
                    command.Parameters.AddWithValue(param.name, param.value);
                }

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    // Just return success - the caller should reload the template from GetAllTemplatesAsync 
                    // which includes the proper layout information
                    return DatabaseResult<Template>.SuccessResult(new Template { Id = templateId });
                }

                return DatabaseResult<Template>.ErrorResult("Template not found after update");
            }
            catch (Exception ex)
            {
                return DatabaseResult<Template>.ErrorResult($"Failed to update template: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Bulk update category for multiple templates
        /// </summary>
        public async Task<DatabaseResult> BulkUpdateTemplateCategoryAsync(List<int> templateIds, int categoryId)
        {
            try
            {
                if (!templateIds.Any())
                {
                    return DatabaseResult.ErrorResult("No template IDs provided");
                }

                var placeholders = string.Join(",", templateIds.Select((_, i) => $"@id{i}"));
                var query = $@"
                    UPDATE Templates 
                    SET CategoryId = @CategoryId
                    WHERE Id IN ({placeholders})";

                using var connection = await CreateConnectionAsync();
                using var command = new SqliteCommand(query, connection);
                
                command.Parameters.AddWithValue("@CategoryId", categoryId);
                for (int i = 0; i < templateIds.Count; i++)
                {
                    command.Parameters.AddWithValue($"@id{i}", templateIds[i]);
                }

                var rowsAffected = await command.ExecuteNonQueryAsync();
                
                if (rowsAffected > 0)
                {
                    return DatabaseResult.SuccessResult();
                }
                else
                {
                    return DatabaseResult.ErrorResult("No templates were updated");
                }
            }
            catch (Exception ex)
            {
                return DatabaseResult.ErrorResult($"Failed to update template categories: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Update template file paths - used for synchronization with file system
        /// </summary>
        public async Task<DatabaseResult> UpdateTemplatePathsAsync(int templateId, string folderPath, string templatePath, string previewPath)
        {
            try
            {
                var query = @"
                    UPDATE Templates 
                    SET FolderPath = @FolderPath, TemplatePath = @TemplatePath, PreviewPath = @PreviewPath
                    WHERE Id = @Id";

                using var connection = await CreateConnectionAsync();
                using var command = new SqliteCommand(query, connection);
                
                command.Parameters.AddWithValue("@Id", templateId);
                command.Parameters.AddWithValue("@FolderPath", folderPath);
                command.Parameters.AddWithValue("@TemplatePath", templatePath);
                command.Parameters.AddWithValue("@PreviewPath", previewPath);

                await command.ExecuteNonQueryAsync();
                    return DatabaseResult.SuccessResult();
                }
            catch (Exception ex)
            {
                return DatabaseResult.ErrorResult($"Failed to update template paths: {ex.Message}", ex);
            }
        }
        
        public async Task<DatabaseResult> UpdateTemplateFileSizeAsync(int templateId, long fileSize)
        {
            try
            {
                var query = @"
                    UPDATE Templates 
                    SET FileSize = @FileSize
                    WHERE Id = @Id";

                using var connection = await CreateConnectionAsync();
                using var command = new SqliteCommand(query, connection);
                
                command.Parameters.AddWithValue("@Id", templateId);
                command.Parameters.AddWithValue("@FileSize", fileSize);

                await command.ExecuteNonQueryAsync();
                return DatabaseResult.SuccessResult();
            }
            catch (Exception ex)
            {
                return DatabaseResult.ErrorResult($"Failed to update template file size: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult> DeleteTemplateAsync(int templateId)
        {
            try
            {
                var query = "DELETE FROM Templates WHERE Id = @Id";

                using var connection = await CreateConnectionAsync();
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@Id", templateId);

                var rowsAffected = await command.ExecuteNonQueryAsync();
                
                if (rowsAffected > 0)
                {
                    return DatabaseResult.SuccessResult();
                }
                else
                {
                    return DatabaseResult.ErrorResult("Template not found");
                }
            }
            catch (Exception ex)
            {
                return DatabaseResult.ErrorResult($"Failed to delete template: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult<List<TemplateCategory>>> GetTemplateCategoriesAsync()
        {
            try
            {
                var categories = new List<TemplateCategory>();
                
                var query = @"
                    SELECT Id, Name, Description, IsActive, SortOrder, 
                           IsSeasonalCategory, SeasonStartDate, SeasonEndDate, SeasonalPriority, CreatedAt
                    FROM TemplateCategories
                    WHERE IsActive = 1
                    ORDER BY 
                        CASE 
                            WHEN IsSeasonalCategory = 1 THEN SeasonalPriority 
                            ELSE 0 
                        END DESC,
                        SortOrder, Name";

                using var connection = await CreateConnectionAsync();
                using var command = new SqliteCommand(query, connection);
                
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    categories.Add(new TemplateCategory
                    {
                        Id = Convert.ToInt32(reader["Id"]),
                        Name = reader["Name"].ToString() ?? "",
                        Description = reader["Description"]?.ToString(),
                        IsActive = Convert.ToBoolean(reader["IsActive"]),
                        IsPremium = GetBoolValue(reader, "IsPremium", false),
                        SortOrder = Convert.ToInt32(reader["SortOrder"]),
                        IsSeasonalCategory = Convert.ToBoolean(reader["IsSeasonalCategory"]),
                        SeasonStartDate = reader["SeasonStartDate"]?.ToString(),
                        SeasonEndDate = reader["SeasonEndDate"]?.ToString(),
                        SeasonalPriority = Convert.ToInt32(reader["SeasonalPriority"]),
                        CreatedAt = Convert.ToDateTime(reader["CreatedAt"])
                    });
                }

                // Filter seasonal categories to only show those currently in season, plus all non-seasonal
                var filteredCategories = new List<TemplateCategory>();
                foreach (var category in categories)
                {
                    if (!category.IsSeasonalCategory || category.IsCurrentlyInSeason)
                    {
                        filteredCategories.Add(category);
                    }
                }

                return DatabaseResult<List<TemplateCategory>>.SuccessResult(filteredCategories);
            }
            catch (Exception ex)
            {
                return DatabaseResult<List<TemplateCategory>>.ErrorResult($"Failed to get template categories: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult<List<TemplateCategory>>> GetAllTemplateCategoriesAsync()
        {
            try
            {
                var categories = new List<TemplateCategory>();
                
                var query = @"
                    SELECT Id, Name, Description, IsActive, IsPremium, SortOrder, 
                           IsSeasonalCategory, SeasonStartDate, SeasonEndDate, SeasonalPriority, CreatedAt
                    FROM TemplateCategories
                    ORDER BY 
                        CASE 
                            WHEN IsSeasonalCategory = 1 THEN SeasonalPriority 
                            ELSE 0 
                        END DESC,
                        SortOrder, Name";

                using var connection = await CreateConnectionAsync();
                using var command = new SqliteCommand(query, connection);
                
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    categories.Add(new TemplateCategory
                    {
                        Id = Convert.ToInt32(reader["Id"]),
                        Name = reader["Name"].ToString() ?? "",
                        Description = reader["Description"]?.ToString(),
                        IsActive = Convert.ToBoolean(reader["IsActive"]),
                        IsPremium = GetBoolValue(reader, "IsPremium", false),
                        SortOrder = Convert.ToInt32(reader["SortOrder"]),
                        IsSeasonalCategory = Convert.ToBoolean(reader["IsSeasonalCategory"]),
                        SeasonStartDate = reader["SeasonStartDate"]?.ToString(),
                        SeasonEndDate = reader["SeasonEndDate"]?.ToString(),
                        SeasonalPriority = Convert.ToInt32(reader["SeasonalPriority"]),
                        CreatedAt = Convert.ToDateTime(reader["CreatedAt"])
                    });
                }

                return DatabaseResult<List<TemplateCategory>>.SuccessResult(categories);
            }
            catch (Exception ex)
            {
                return DatabaseResult<List<TemplateCategory>>.ErrorResult($"Failed to get all template categories: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult<TemplateCategory>> CreateTemplateCategoryAsync(string name, string description = "", 
            bool isSeasonalCategory = false, string? seasonStartDate = null, string? seasonEndDate = null, int seasonalPriority = 0, bool isPremium = false)
        {
            try
            {
                // Validate seasonal dates if provided
                if (isSeasonalCategory)
                {
                    try
                    {
                        seasonStartDate = TemplateCategory.ValidateAndFormatSeasonalDate(seasonStartDate);
                        seasonEndDate = TemplateCategory.ValidateAndFormatSeasonalDate(seasonEndDate);
                    }
                    catch (ArgumentException ex)
                    {
                        return DatabaseResult<TemplateCategory>.ErrorResult(ex.Message);
                    }
                }
                var query = @"
                    INSERT INTO TemplateCategories (Name, Description, IsActive, IsPremium, SortOrder, 
                                                    IsSeasonalCategory, SeasonStartDate, SeasonEndDate, SeasonalPriority, CreatedAt)
                    VALUES (@Name, @Description, 1, @IsPremium, 0, @IsSeasonalCategory, @SeasonStartDate, @SeasonEndDate, @SeasonalPriority, @CreatedAt);
                    SELECT * FROM TemplateCategories WHERE Id = last_insert_rowid();";

                using var connection = await CreateConnectionAsync();
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@Name", name);
                command.Parameters.AddWithValue("@Description", description);
                command.Parameters.AddWithValue("@IsPremium", isPremium);
                command.Parameters.AddWithValue("@IsSeasonalCategory", isSeasonalCategory);
                command.Parameters.AddWithValue("@SeasonStartDate", seasonStartDate ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@SeasonEndDate", seasonEndDate ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@SeasonalPriority", seasonalPriority);
                command.Parameters.AddWithValue("@CreatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    // Manual mapping for performance
                    var category = new TemplateCategory
                    {
                        Id = Convert.ToInt32(reader["Id"]),
                        Name = reader["Name"].ToString() ?? "",
                        Description = reader["Description"]?.ToString() ?? "",
                        IsActive = Convert.ToBoolean(reader["IsActive"]),
                        IsPremium = GetBoolValue(reader, "IsPremium", false),
                        SortOrder = Convert.ToInt32(reader["SortOrder"]),
                        IsSeasonalCategory = Convert.ToBoolean(reader["IsSeasonalCategory"]),
                        SeasonStartDate = reader["SeasonStartDate"]?.ToString(),
                        SeasonEndDate = reader["SeasonEndDate"]?.ToString(),
                        SeasonalPriority = Convert.ToInt32(reader["SeasonalPriority"]),
                        CreatedAt = Convert.ToDateTime(reader["CreatedAt"])
                    };

                    // Use file-based logging instead of database logging
                    LoggingService.Application.Information("Template category created",
                        ("Name", name),
                        ("Description", description),
                        ("CategoryId", category.Id));

                    return DatabaseResult<TemplateCategory>.SuccessResult(category);
                }

                return DatabaseResult<TemplateCategory>.ErrorResult("Failed to create template category");
            }
            catch (Exception ex)
            {
                return DatabaseResult<TemplateCategory>.ErrorResult($"Failed to create template category: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult<TemplateCategory>> UpdateTemplateCategoryAsync(int categoryId, string name, string description = "",
            bool isSeasonalCategory = false, string? seasonStartDate = null, string? seasonEndDate = null, int seasonalPriority = 0, bool isPremium = false)
        {
            try
            {
                // Validate seasonal dates if provided
                if (isSeasonalCategory)
                {
                    try
                    {
                        seasonStartDate = TemplateCategory.ValidateAndFormatSeasonalDate(seasonStartDate);
                        seasonEndDate = TemplateCategory.ValidateAndFormatSeasonalDate(seasonEndDate);
                    }
                    catch (ArgumentException ex)
                    {
                        return DatabaseResult<TemplateCategory>.ErrorResult(ex.Message);
                    }
                }
                var query = @"
                    UPDATE TemplateCategories 
                    SET Name = @name, Description = @description, IsPremium = @isPremium,
                        IsSeasonalCategory = @isSeasonalCategory, SeasonStartDate = @seasonStartDate, SeasonEndDate = @seasonEndDate, SeasonalPriority = @seasonalPriority
                    WHERE Id = @id";

                using var connection = await CreateConnectionAsync();
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@name", name);
                command.Parameters.AddWithValue("@description", description);
                command.Parameters.AddWithValue("@isPremium", isPremium);
                command.Parameters.AddWithValue("@id", categoryId);
                command.Parameters.AddWithValue("@isSeasonalCategory", isSeasonalCategory);
                command.Parameters.AddWithValue("@seasonStartDate", seasonStartDate ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@seasonEndDate", seasonEndDate ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@seasonalPriority", seasonalPriority);

                var rowsAffected = await command.ExecuteNonQueryAsync();
                if (rowsAffected == 0)
                {
                    return DatabaseResult<TemplateCategory>.ErrorResult("Template category not found");
                }

                // Get the updated category
                var selectQuery = @"
                    SELECT Id, Name, Description, IsActive, IsPremium, SortOrder, CreatedAt, 
                           IsSeasonalCategory, SeasonStartDate, SeasonEndDate, SeasonalPriority
                    FROM TemplateCategories 
                    WHERE Id = @id";

                using var selectCommand = new SqliteCommand(selectQuery, connection);
                selectCommand.Parameters.AddWithValue("@id", categoryId);
                using var reader = await selectCommand.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    // Manual mapping for performance (avoid slow reflection)
                    var category = new TemplateCategory
                    {
                        Id = Convert.ToInt32(reader["Id"]),
                        Name = reader["Name"].ToString() ?? "",
                        Description = reader["Description"]?.ToString() ?? "",
                        IsActive = Convert.ToBoolean(reader["IsActive"]),
                        IsPremium = GetBoolValue(reader, "IsPremium", false),
                        SortOrder = Convert.ToInt32(reader["SortOrder"]),
                        CreatedAt = Convert.ToDateTime(reader["CreatedAt"]),
                        IsSeasonalCategory = GetBoolValue(reader, "IsSeasonalCategory", false),
                        SeasonStartDate = GetStringValue(reader, "SeasonStartDate"),
                        SeasonEndDate = GetStringValue(reader, "SeasonEndDate"),
                        SeasonalPriority = GetIntValue(reader, "SeasonalPriority", 0)
                    };

                    // Use file-based logging instead of database logging
                    LoggingService.Application.Information("Template category updated",
                        ("CategoryId", categoryId),
                        ("Name", name),
                        ("Description", description));
                    
                    return DatabaseResult<TemplateCategory>.SuccessResult(category);
                }

                return DatabaseResult<TemplateCategory>.ErrorResult("Failed to retrieve updated category");
            }
            catch (Exception ex)
            {
                return DatabaseResult<TemplateCategory>.ErrorResult($"Failed to update template category: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult> UpdateTemplateCategoryStatusAsync(int categoryId, bool isActive)
        {
            try
            {


                using var connection = await CreateConnectionAsync();
                using var transaction = connection.BeginTransaction();

                try
                {
                    // Update the category status
                    var categoryQuery = @"
                        UPDATE TemplateCategories 
                        SET IsActive = @isActive 
                        WHERE Id = @id";

                    using var categoryCommand = new SqliteCommand(categoryQuery, connection, transaction);
                    categoryCommand.Parameters.AddWithValue("@isActive", isActive);
                    categoryCommand.Parameters.AddWithValue("@id", categoryId);

                    var categoryRowsAffected = await categoryCommand.ExecuteNonQueryAsync();

                    if (categoryRowsAffected == 0)
                    {

                        transaction.Rollback();
                        return DatabaseResult.ErrorResult("Template category not found");
                    }

                    // Check how many templates are in this category before updating
                    var countQuery = @"SELECT COUNT(*) FROM Templates WHERE CategoryId = @categoryId";
                    using var countCommand = new SqliteCommand(countQuery, connection, transaction);
                    countCommand.Parameters.AddWithValue("@categoryId", categoryId);
                    var templateCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());

                    // Update all templates in this category to match the category status
                    var templatesQuery = @"
                        UPDATE Templates 
                        SET IsActive = @isActive 
                        WHERE CategoryId = @categoryId";

                    using var templatesCommand = new SqliteCommand(templatesQuery, connection, transaction);
                    templatesCommand.Parameters.AddWithValue("@isActive", isActive);
                    templatesCommand.Parameters.AddWithValue("@categoryId", categoryId);

                    var templateRowsAffected = await templatesCommand.ExecuteNonQueryAsync();

                    // Commit the transaction
                    transaction.Commit();

                    // Use file-based logging instead of database logging
                    LoggingService.Application.Information("Template category status updated",
                        ("CategoryId", categoryId),
                        ("IsActive", isActive),
                        ("TemplatesAffected", templateRowsAffected));
                        
                    return DatabaseResult.SuccessResult();
                }
                catch
                {

                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {

                return DatabaseResult.ErrorResult($"Failed to update template category status: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult> DeleteTemplateCategoryAsync(int categoryId)
        {
            try
            {
                var query = "DELETE FROM TemplateCategories WHERE Id = @id";

                using var connection = await CreateConnectionAsync();
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@id", categoryId);

                var rowsAffected = await command.ExecuteNonQueryAsync();
                if (rowsAffected == 0)
                {
                    return DatabaseResult.ErrorResult("Template category not found");
                }

                // Use file-based logging instead of database logging
                LoggingService.Application.Information("Template category deleted",
                    ("CategoryId", categoryId));
                    
                return DatabaseResult.SuccessResult();
            }
            catch (Exception ex)
            {
                return DatabaseResult.ErrorResult($"Failed to delete template category: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult<List<Template>>> GetTemplatesByCategoryAsync(int categoryId)
        {
            try
            {
                var query = @"
                    SELECT t.*, tc.Name as CategoryName 
                    FROM Templates t 
                    LEFT JOIN TemplateCategories tc ON t.CategoryId = tc.Id 
                    WHERE t.CategoryId = @categoryId 
                    ORDER BY t.Name";

                using var connection = await CreateConnectionAsync();
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@categoryId", categoryId);
                using var reader = await command.ExecuteReaderAsync();

                var templates = new List<Template>();
                while (await reader.ReadAsync())
                {
                    var template = MapReaderToEntity<Template>(reader);
                    if (reader["CategoryName"] != DBNull.Value)
                    {
                        template.Category = new TemplateCategory 
                        { 
                            Id = template.CategoryId, 
                            Name = reader["CategoryName"].ToString() ?? "" 
                        };
                    }
                    templates.Add(template);
                }

                return DatabaseResult<List<Template>>.SuccessResult(templates);
            }
            catch (Exception ex)
            {
                return DatabaseResult<List<Template>>.ErrorResult($"Failed to get templates by category: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult<List<TemplateLayout>>> GetTemplateLayoutsAsync()
        {
            try
            {
                var layouts = new List<TemplateLayout>();
                
                var query = @"
                    SELECT tl.*, pc.Name as ProductCategoryName
                    FROM TemplateLayouts tl
                    LEFT JOIN ProductCategories pc ON tl.ProductCategoryId = pc.Id
                    WHERE tl.IsActive = 1
                    ORDER BY tl.SortOrder, tl.Name";

                using var connection = await CreateConnectionAsync();
                using var command = new SqliteCommand(query, connection);
                
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var layout = new TemplateLayout
                    {
                        Id = GetStringValue(reader, "Id"),
                        LayoutKey = GetStringValue(reader, "LayoutKey"),
                        Name = GetStringValue(reader, "Name"),
                        Description = GetStringValue(reader, "Description"),
                        Width = GetIntValue(reader, "Width"),
                        Height = GetIntValue(reader, "Height"),
                        PhotoCount = GetIntValue(reader, "PhotoCount"),
                        ProductCategoryId = GetIntValue(reader, "ProductCategoryId"),
                        IsActive = GetBoolValue(reader, "IsActive"),
                        SortOrder = GetIntValue(reader, "SortOrder"),
                        CreatedAt = Convert.ToDateTime(reader["CreatedAt"])
                    };

                    // Set product category if available
                    if (reader["ProductCategoryName"] != DBNull.Value)
                    {
                        layout.ProductCategory = new ProductCategory
                        {
                            Id = layout.ProductCategoryId,
                            Name = GetStringValue(reader, "ProductCategoryName")
                        };
                    }

                    layouts.Add(layout);
                }

                return DatabaseResult<List<TemplateLayout>>.SuccessResult(layouts);
            }
            catch (Exception ex)
            {
                return DatabaseResult<List<TemplateLayout>>.ErrorResult($"Failed to get template layouts: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult<TemplateLayout?>> GetTemplateLayoutAsync(string layoutId)
        {
            try
            {
                var query = @"
                    SELECT tl.*, pc.Name as ProductCategoryName
                    FROM TemplateLayouts tl
                    LEFT JOIN ProductCategories pc ON tl.ProductCategoryId = pc.Id
                    WHERE tl.Id = @layoutId";

                using var connection = await CreateConnectionAsync();
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@layoutId", layoutId);
                
                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var layout = new TemplateLayout
                    {
                        Id = GetStringValue(reader, "Id"),
                        LayoutKey = GetStringValue(reader, "LayoutKey"),
                        Name = GetStringValue(reader, "Name"),
                        Description = GetStringValue(reader, "Description"),
                        Width = GetIntValue(reader, "Width"),
                        Height = GetIntValue(reader, "Height"),
                        PhotoCount = GetIntValue(reader, "PhotoCount"),
                        ProductCategoryId = GetIntValue(reader, "ProductCategoryId"),
                        IsActive = GetBoolValue(reader, "IsActive"),
                        SortOrder = GetIntValue(reader, "SortOrder"),
                        CreatedAt = Convert.ToDateTime(reader["CreatedAt"])
                    };

                    // Set product category if available
                    if (reader["ProductCategoryName"] != DBNull.Value)
                    {
                        layout.ProductCategory = new ProductCategory
                        {
                            Id = layout.ProductCategoryId,
                            Name = GetStringValue(reader, "ProductCategoryName")
                        };
                    }

                    // Load photo areas
                    var photoAreasResult = await GetTemplatePhotoAreasAsync(layoutId);
                    if (photoAreasResult.Success && photoAreasResult.Data != null)
                    {
                        layout.PhotoAreas = photoAreasResult.Data;
                    }

                    return DatabaseResult<TemplateLayout?>.SuccessResult(layout);
                }

                return DatabaseResult<TemplateLayout?>.SuccessResult(null);
            }
            catch (Exception ex)
            {
                return DatabaseResult<TemplateLayout?>.ErrorResult($"Failed to get template layout: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult<TemplateLayout?>> GetTemplateLayoutByKeyAsync(string layoutKey)
        {
            try
            {
                var query = @"
                    SELECT tl.*, pc.Name as ProductCategoryName
                    FROM TemplateLayouts tl
                    LEFT JOIN ProductCategories pc ON tl.ProductCategoryId = pc.Id
                    WHERE tl.LayoutKey = @layoutKey";

                using var connection = await CreateConnectionAsync();
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@layoutKey", layoutKey);
                
                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var layout = new TemplateLayout
                    {
                        Id = GetStringValue(reader, "Id"),
                        LayoutKey = GetStringValue(reader, "LayoutKey"),
                        Name = GetStringValue(reader, "Name"),
                        Description = GetStringValue(reader, "Description"),
                        Width = GetIntValue(reader, "Width"),
                        Height = GetIntValue(reader, "Height"),
                        PhotoCount = GetIntValue(reader, "PhotoCount"),
                        ProductCategoryId = GetIntValue(reader, "ProductCategoryId"),
                        IsActive = GetBoolValue(reader, "IsActive"),
                        SortOrder = GetIntValue(reader, "SortOrder"),
                        CreatedAt = Convert.ToDateTime(reader["CreatedAt"])
                    };

                    // Set product category if available
                    if (reader["ProductCategoryName"] != DBNull.Value)
                    {
                        layout.ProductCategory = new ProductCategory
                        {
                            Id = layout.ProductCategoryId,
                            Name = GetStringValue(reader, "ProductCategoryName")
                        };
                    }

                    // Load photo areas
                    var photoAreasResult = await GetTemplatePhotoAreasAsync(layout.Id);
                    if (photoAreasResult.Success && photoAreasResult.Data != null)
                    {
                        layout.PhotoAreas = photoAreasResult.Data;
                    }

                    return DatabaseResult<TemplateLayout?>.SuccessResult(layout);
                }

                return DatabaseResult<TemplateLayout?>.SuccessResult(null);
            }
            catch (Exception ex)
            {
                return DatabaseResult<TemplateLayout?>.ErrorResult($"Failed to get template layout: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult<List<TemplatePhotoArea>>> GetTemplatePhotoAreasAsync(string layoutId)
        {
            try
            {
                var photoAreas = new List<TemplatePhotoArea>();
                
                var query = @"
                    SELECT Id, LayoutId, PhotoIndex, X, Y, Width, Height, Rotation, BorderRadius, ShapeType
                    FROM TemplatePhotoAreas
                    WHERE LayoutId = @layoutId
                    ORDER BY PhotoIndex";

                using var connection = await CreateConnectionAsync();
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@layoutId", layoutId);
                
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var shapeTypeString = GetStringValue(reader, "ShapeType") ?? "Rectangle";
                    var shapeType = Enum.TryParse<ShapeType>(shapeTypeString, out var parsedShapeType) ? parsedShapeType : ShapeType.Rectangle;
                    
                    photoAreas.Add(new TemplatePhotoArea
                    {
                        Id = GetIntValue(reader, "Id"),
                        LayoutId = GetStringValue(reader, "LayoutId"),
                        PhotoIndex = GetIntValue(reader, "PhotoIndex"),
                        X = GetIntValue(reader, "X"),
                        Y = GetIntValue(reader, "Y"),
                        Width = GetIntValue(reader, "Width"),
                        Height = GetIntValue(reader, "Height"),
                        Rotation = Convert.ToDouble(reader["Rotation"]),
                        BorderRadius = GetIntValue(reader, "BorderRadius"),
                        ShapeType = shapeType
                    });
                }

                return DatabaseResult<List<TemplatePhotoArea>>.SuccessResult(photoAreas);
            }
            catch (Exception ex)
            {
                return DatabaseResult<List<TemplatePhotoArea>>.ErrorResult($"Failed to get template photo areas: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult<Dictionary<string, List<TemplatePhotoArea>>>> GetPhotoAreasByLayoutIdsAsync(List<string> layoutIds)
        {
            try
            {
                if (!layoutIds.Any())
                {
                    return DatabaseResult<Dictionary<string, List<TemplatePhotoArea>>>.SuccessResult(new Dictionary<string, List<TemplatePhotoArea>>());
                }

                var photoAreasByLayout = new Dictionary<string, List<TemplatePhotoArea>>();
                
                // Create parameter placeholders for IN clause
                var placeholders = string.Join(",", layoutIds.Select((_, i) => $"@layoutId{i}"));
                var query = $@"
                    SELECT Id, LayoutId, PhotoIndex, X, Y, Width, Height, Rotation, BorderRadius, ShapeType
                    FROM TemplatePhotoAreas
                    WHERE LayoutId IN ({placeholders})
                    ORDER BY LayoutId, PhotoIndex";

                using var connection = await CreateConnectionAsync();
                using var command = new SqliteCommand(query, connection);
                
                // Add parameters for each layout ID
                for (int i = 0; i < layoutIds.Count; i++)
                {
                    command.Parameters.AddWithValue($"@layoutId{i}", layoutIds[i]);
                }
                
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var shapeTypeString = GetStringValue(reader, "ShapeType") ?? "Rectangle";
                    var shapeType = Enum.TryParse<ShapeType>(shapeTypeString, out var parsedShapeType) ? parsedShapeType : ShapeType.Rectangle;
                    
                    var photoArea = new TemplatePhotoArea
                    {
                        Id = GetIntValue(reader, "Id"),
                        LayoutId = GetStringValue(reader, "LayoutId"),
                        PhotoIndex = GetIntValue(reader, "PhotoIndex"),
                        X = GetIntValue(reader, "X"),
                        Y = GetIntValue(reader, "Y"),
                        Width = GetIntValue(reader, "Width"),
                        Height = GetIntValue(reader, "Height"),
                        Rotation = Convert.ToDouble(reader["Rotation"]),
                        BorderRadius = GetIntValue(reader, "BorderRadius"),
                        ShapeType = shapeType
                    };

                    if (!photoAreasByLayout.ContainsKey(photoArea.LayoutId))
                    {
                        photoAreasByLayout[photoArea.LayoutId] = new List<TemplatePhotoArea>();
                    }
                    photoAreasByLayout[photoArea.LayoutId].Add(photoArea);
                }

                return DatabaseResult<Dictionary<string, List<TemplatePhotoArea>>>.SuccessResult(photoAreasByLayout);
            }
            catch (Exception ex)
            {
                return DatabaseResult<Dictionary<string, List<TemplatePhotoArea>>>.ErrorResult($"Failed to get photo areas by layout IDs: {ex.Message}", ex);
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

                using var connection = await CreateConnectionAsync();
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

                using var connection = await CreateConnectionAsync();
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

                using var connection = await CreateConnectionAsync();
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@newCount", newCount);
                command.Parameters.AddWithValue("@supplyType", supplyType.ToString());

                await command.ExecuteNonQueryAsync();
                
                // Use file-based logging instead of database logging
                LoggingService.Hardware.Information("Supplies", "Supply count updated",
                    ("SupplyType", supplyType.ToString()),
                    ("NewCount", newCount));
                
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

                using var connection = await CreateConnectionAsync();
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

                using var connection = await CreateConnectionAsync();
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@isActive", isActive);
                command.Parameters.AddWithValue("@updatedAt", DateTime.Now);
                command.Parameters.AddWithValue("@id", productId);

                var rowsAffected = await command.ExecuteNonQueryAsync();
                if (rowsAffected > 0)
                {
                    // Use file-based logging instead of database logging
                    LoggingService.Application.Information("Product status updated",
                        ("ProductId", productId),
                        ("IsActive", isActive));
                        
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

                using var connection = await CreateConnectionAsync();
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@price", price);
                command.Parameters.AddWithValue("@updatedAt", DateTime.Now);
                command.Parameters.AddWithValue("@id", productId);

                var rowsAffected = await command.ExecuteNonQueryAsync();
                if (rowsAffected > 0)
                {
                    // Use file-based logging instead of database logging
                    LoggingService.Application.Information("Product price updated",
                        ("ProductId", productId),
                        ("NewPrice", price));
                        
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

        public async Task<DatabaseResult> UpdateProductAsync(int productId, bool? isActive = null, decimal? price = null, 
            bool? useCustomExtraCopyPricing = null, decimal? extraCopy1Price = null, decimal? extraCopy2Price = null, 
            decimal? extraCopy4BasePrice = null, decimal? extraCopyAdditionalPrice = null,
            // Simplified product-specific extra copy pricing
            decimal? stripsExtraCopyPrice = null, decimal? stripsMultipleCopyDiscount = null,
            decimal? photo4x6ExtraCopyPrice = null, decimal? photo4x6MultipleCopyDiscount = null,
            decimal? smartphoneExtraCopyPrice = null, decimal? smartphoneMultipleCopyDiscount = null)
        {
            // Create request object and delegate to the new overload
            var request = new ProductUpdateRequest
            {
                IsActive = isActive,
                Price = price,
                UseCustomExtraCopyPricing = useCustomExtraCopyPricing,
                ExtraCopy1Price = extraCopy1Price,
                ExtraCopy2Price = extraCopy2Price,
                ExtraCopy4BasePrice = extraCopy4BasePrice,
                ExtraCopyAdditionalPrice = extraCopyAdditionalPrice,
                StripsExtraCopyPrice = stripsExtraCopyPrice,
                StripsMultipleCopyDiscount = stripsMultipleCopyDiscount,
                Photo4x6ExtraCopyPrice = photo4x6ExtraCopyPrice,
                Photo4x6MultipleCopyDiscount = photo4x6MultipleCopyDiscount,
                SmartphoneExtraCopyPrice = smartphoneExtraCopyPrice,
                SmartphoneMultipleCopyDiscount = smartphoneMultipleCopyDiscount
            };

            return await UpdateProductAsync(productId, request);
        }

        public async Task<DatabaseResult> UpdateProductAsync(int productId, ProductUpdateRequest request)
        {
            // Validate that at least one parameter is provided
            if (!request.HasAnyValue())
            {
                return DatabaseResult.ErrorResult("At least one field must be provided for update");
            }

            try
            {
                using var connection = await CreateConnectionAsync();
                
                // Use transaction for atomic operation
                using var transaction = connection.BeginTransaction();
                try
                {
                    // Build dynamic query based on provided parameters
                    var setParts = new List<string>();
                    var parameters = new List<(string name, object value)>();
                    var logMessages = new List<string>();

                    if (request.IsActive.HasValue)
                    {
                        setParts.Add("IsActive = @isActive");
                        parameters.Add(("@isActive", request.IsActive.Value));
                        logMessages.Add($"status = {request.IsActive.Value}");
                    }

                    if (request.Price.HasValue)
                    {
                        setParts.Add("Price = @price");
                        parameters.Add(("@price", request.Price.Value));
                        logMessages.Add($"price = ${request.Price.Value:F2}");
                    }

                    if (request.UseCustomExtraCopyPricing.HasValue)
                    {
                        setParts.Add("UseCustomExtraCopyPricing = @useCustomExtraCopyPricing");
                        parameters.Add(("@useCustomExtraCopyPricing", request.UseCustomExtraCopyPricing.Value));
                        logMessages.Add($"useCustomExtraCopyPricing = {request.UseCustomExtraCopyPricing.Value}");
                    }

                    if (request.ExtraCopy1Price.HasValue)
                    {
                        setParts.Add("ExtraCopy1Price = @extraCopy1Price");
                        parameters.Add(("@extraCopy1Price", request.ExtraCopy1Price.Value));
                        logMessages.Add($"extraCopy1Price = ${request.ExtraCopy1Price.Value:F2}");
                    }

                    if (request.ExtraCopy2Price.HasValue)
                    {
                        setParts.Add("ExtraCopy2Price = @extraCopy2Price");
                        parameters.Add(("@extraCopy2Price", request.ExtraCopy2Price.Value));
                        logMessages.Add($"extraCopy2Price = ${request.ExtraCopy2Price.Value:F2}");
                    }

                    if (request.ExtraCopy4BasePrice.HasValue)
                    {
                        setParts.Add("ExtraCopy4BasePrice = @extraCopy4BasePrice");
                        parameters.Add(("@extraCopy4BasePrice", request.ExtraCopy4BasePrice.Value));
                        logMessages.Add($"extraCopy4BasePrice = ${request.ExtraCopy4BasePrice.Value:F2}");
                    }

                    if (request.ExtraCopyAdditionalPrice.HasValue)
                    {
                        setParts.Add("ExtraCopyAdditionalPrice = @extraCopyAdditionalPrice");
                        parameters.Add(("@extraCopyAdditionalPrice", request.ExtraCopyAdditionalPrice.Value));
                        logMessages.Add($"extraCopyAdditionalPrice = ${request.ExtraCopyAdditionalPrice.Value:F2}");
                    }

                    // Simplified product-specific extra copy pricing
                    if (request.StripsExtraCopyPrice.HasValue)
                    {
                        setParts.Add("StripsExtraCopyPrice = @stripsExtraCopyPrice");
                        parameters.Add(("@stripsExtraCopyPrice", request.StripsExtraCopyPrice.Value));
                        logMessages.Add($"stripsExtraCopyPrice = ${request.StripsExtraCopyPrice.Value:F2}");
                    }

                    if (request.StripsMultipleCopyDiscount.HasValue)
                    {
                        setParts.Add("StripsMultipleCopyDiscount = @stripsMultipleCopyDiscount");
                        parameters.Add(("@stripsMultipleCopyDiscount", request.StripsMultipleCopyDiscount.Value));
                        logMessages.Add($"stripsMultipleCopyDiscount = ${request.StripsMultipleCopyDiscount.Value:F0}%");
                    }

                    if (request.Photo4x6ExtraCopyPrice.HasValue)
                    {
                        setParts.Add("Photo4x6ExtraCopyPrice = @photo4x6ExtraCopyPrice");
                        parameters.Add(("@photo4x6ExtraCopyPrice", request.Photo4x6ExtraCopyPrice.Value));
                        logMessages.Add($"photo4x6ExtraCopyPrice = ${request.Photo4x6ExtraCopyPrice.Value:F2}");
                    }

                    if (request.Photo4x6MultipleCopyDiscount.HasValue)
                    {
                        setParts.Add("Photo4x6MultipleCopyDiscount = @photo4x6MultipleCopyDiscount");
                        parameters.Add(("@photo4x6MultipleCopyDiscount", request.Photo4x6MultipleCopyDiscount.Value));
                        logMessages.Add($"photo4x6MultipleCopyDiscount = ${request.Photo4x6MultipleCopyDiscount.Value:F0}%");
                    }

                    if (request.SmartphoneExtraCopyPrice.HasValue)
                    {
                        setParts.Add("SmartphoneExtraCopyPrice = @smartphoneExtraCopyPrice");
                        parameters.Add(("@smartphoneExtraCopyPrice", request.SmartphoneExtraCopyPrice.Value));
                        logMessages.Add($"smartphoneExtraCopyPrice = ${request.SmartphoneExtraCopyPrice.Value:F2}");
                    }

                    if (request.SmartphoneMultipleCopyDiscount.HasValue)
                    {
                        setParts.Add("SmartphoneMultipleCopyDiscount = @smartphoneMultipleCopyDiscount");
                        parameters.Add(("@smartphoneMultipleCopyDiscount", request.SmartphoneMultipleCopyDiscount.Value));
                        logMessages.Add($"smartphoneMultipleCopyDiscount = ${request.SmartphoneMultipleCopyDiscount.Value:F0}%");
                    }

                    setParts.Add("UpdatedAt = @updatedAt");
                    parameters.Add(("@updatedAt", DateTime.Now));

                    var query = $"UPDATE Products SET {string.Join(", ", setParts)} WHERE Id = @id";

                    using var command = new SqliteCommand(query, connection, transaction);
                    
                    // Add all parameters
                    command.Parameters.AddWithValue("@id", productId);
                    foreach (var (name, value) in parameters)
                    {
                        command.Parameters.AddWithValue(name, value);
                    }

                    var rowsAffected = await command.ExecuteNonQueryAsync();
                    if (rowsAffected == 0)
                    {
                        await transaction.RollbackAsync();
                        return DatabaseResult.ErrorResult("Product not found");
                    }

                    // Commit the transaction
                    await transaction.CommitAsync();

                    // Use file-based logging instead of database logging
                    var logMessage = $"Product {productId} updated: {string.Join(", ", logMessages)}";
                    LoggingService.Application.Information("Product updated",
                        ("ProductId", productId),
                        ("Changes", string.Join(", ", logMessages)));

                    return DatabaseResult.SuccessResult();
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                return DatabaseResult.ErrorResult($"Failed to update product: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult<List<Setting>>> GetSettingsByCategoryAsync(string category)
        {
            try
            {
                var query = "SELECT * FROM Settings WHERE Category = @category ORDER BY Key";

                using var connection = await CreateConnectionAsync();
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

                using var connection = await CreateConnectionAsync();
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

                var stringValue = value?.ToString() ?? "";
                var dataType = GetDataTypeString<T>();

                // Check if setting exists
                var checkQuery = "SELECT COUNT(*) FROM Settings WHERE Category = @category AND Key = @key";
                using var connection = await CreateConnectionAsync();

                using var checkCommand = new SqliteCommand(checkQuery, connection);
                checkCommand.Parameters.AddWithValue("@category", category);
                checkCommand.Parameters.AddWithValue("@key", key);
                
                var exists = Convert.ToInt32(await checkCommand.ExecuteScalarAsync()) > 0;

                if (exists)
                {
                    // Update existing setting
                var updateQuery = @"
                    UPDATE Settings 
                    SET Value = @value, DataType = @dataType, UpdatedAt = @updatedAt, UpdatedBy = @updatedBy
                    WHERE Category = @category AND Key = @key";

                using var updateCommand = new SqliteCommand(updateQuery, connection);
                updateCommand.Parameters.AddWithValue("@value", stringValue);
                updateCommand.Parameters.AddWithValue("@dataType", dataType);
                    updateCommand.Parameters.AddWithValue("@category", category);
                    updateCommand.Parameters.AddWithValue("@key", key);
                updateCommand.Parameters.AddWithValue("@updatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                updateCommand.Parameters.AddWithValue("@updatedBy", (object?)updatedBy ?? DBNull.Value);

                    var updatedRows = await updateCommand.ExecuteNonQueryAsync();

                }
                else
                {
                    // Insert new setting
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

                }

                // Use file-based logging instead of database logging
                LoggingService.Application.Information("Setting updated",
                    ("Category", category),
                    ("Key", key),
                    ("Value", stringValue),
                    ("UpdatedBy", updatedBy ?? "Unknown"));

                return DatabaseResult.SuccessResult();
            }
            catch (Exception ex)
            {

                return DatabaseResult.ErrorResult($"Failed to set setting value: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult> CleanupProductSettingsAsync()
        {
            try
            {
                // Remove settings for products that no longer exist
                var query = @"
                    DELETE FROM Settings 
                    WHERE Category = 'Product' 
                    AND Key NOT IN (
                        SELECT 'Product_' || Id || '_IsActive' FROM Products
                        UNION
                        SELECT 'Product_' || Id || '_Price' FROM Products
                    )";

                using var connection = await CreateConnectionAsync();
                using var command = new SqliteCommand(query, connection);
                
                var deletedCount = await command.ExecuteNonQueryAsync();
                
                return DatabaseResult.SuccessResult();
            }
            catch (Exception ex)
            {
                return DatabaseResult.ErrorResult($"Failed to cleanup product settings: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get system date status and seasonal information for verification
        /// </summary>
        public async Task<DatabaseResult<SystemDateStatus>> GetSystemDateStatusAsync()
        {
            try
            {
                var status = new SystemDateStatus
                {
                    CurrentSystemDate = DateTime.Now,
                    CurrentSystemDateString = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    CurrentDateForSeason = $"{DateTime.Now.Month:D2}-{DateTime.Now.Day:D2}",
                    TimeZone = TimeZoneInfo.Local.DisplayName
                };

                // Get all seasonal categories and check their status
                var categoriesResult = await GetAllTemplateCategoriesAsync();
                if (categoriesResult.Success && categoriesResult.Data != null)
                {
                    var seasonalCategories = categoriesResult.Data.Where(c => c.IsSeasonalCategory).ToList();
                    
                    foreach (var category in seasonalCategories)
                    {
                        var seasonStatus = new SeasonStatus
                        {
                            CategoryName = category.Name,
                            SeasonStartDate = category.SeasonStartDate ?? "",
                            SeasonEndDate = category.SeasonEndDate ?? "",
                            SeasonalPriority = category.SeasonalPriority,
                            IsCurrentlyActive = category.IsCurrentlyInSeason,
                            SpansYears = !string.IsNullOrEmpty(category.SeasonStartDate) && !string.IsNullOrEmpty(category.SeasonEndDate) && 
                                       string.Compare(category.SeasonStartDate, category.SeasonEndDate) > 0
                        };
                        
                        status.SeasonalCategories.Add(seasonStatus);
                    }

                    // Sort by priority (active seasons first, then by priority)
                    status.SeasonalCategories = status.SeasonalCategories
                        .OrderByDescending(s => s.IsCurrentlyActive)
                        .ThenByDescending(s => s.SeasonalPriority)
                        .ToList();

                    status.ActiveSeasonsCount = status.SeasonalCategories.Count(s => s.IsCurrentlyActive);
                }

                return DatabaseResult<SystemDateStatus>.SuccessResult(status);
            }
            catch (Exception ex)
            {
                return DatabaseResult<SystemDateStatus>.ErrorResult($"Failed to get system date status: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Insert a new credit transaction record
        /// </summary>
        public async Task<DatabaseResult<int>> InsertCreditTransactionAsync(CreditTransaction transaction)
        {
            try
            {
                var query = @"
                    INSERT INTO CreditTransactions (Amount, TransactionType, Description, BalanceAfter, CreatedAt, CreatedBy, RelatedTransactionId)
                    VALUES (@amount, @transactionType, @description, @balanceAfter, @createdAt, @createdBy, @relatedTransactionId);
                    SELECT last_insert_rowid();";

                using var connection = await CreateConnectionAsync();
                using var command = new SqliteCommand(query, connection);
                
                command.Parameters.AddWithValue("@amount", transaction.Amount);
                command.Parameters.AddWithValue("@transactionType", transaction.TransactionType.ToString());
                command.Parameters.AddWithValue("@description", transaction.Description);
                command.Parameters.AddWithValue("@balanceAfter", transaction.BalanceAfter);
                command.Parameters.AddWithValue("@createdAt", transaction.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                command.Parameters.AddWithValue("@createdBy", (object?)transaction.CreatedBy ?? DBNull.Value);
                command.Parameters.AddWithValue("@relatedTransactionId", (object?)transaction.RelatedTransactionId ?? DBNull.Value);

                var newId = Convert.ToInt32(await command.ExecuteScalarAsync());
                return DatabaseResult<int>.SuccessResult(newId);
            }
            catch (Exception ex)
            {
                return DatabaseResult<int>.ErrorResult($"Failed to insert credit transaction: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get credit transactions ordered by most recent first
        /// </summary>
        public async Task<DatabaseResult<List<CreditTransaction>>> GetCreditTransactionsAsync(int limit = 50)
        {
            try
            {
                var query = @"
                    SELECT * FROM CreditTransactions 
                    ORDER BY CreatedAt DESC 
                    LIMIT @limit";

                using var connection = await CreateConnectionAsync();
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@limit", limit);
                using var reader = await command.ExecuteReaderAsync();

                var transactions = new List<CreditTransaction>();
                while (await reader.ReadAsync())
                {
                    var transaction = new CreditTransaction
                    {
                        Id = GetIntValue(reader, "Id"),
                        Amount = GetDecimalValue(reader, "Amount"),
                        TransactionType = Enum.Parse<CreditTransactionType>(GetStringValue(reader, "TransactionType")),
                        Description = GetStringValue(reader, "Description"),
                        BalanceAfter = GetDecimalValue(reader, "BalanceAfter"),
                        CreatedAt = DateTime.Parse(GetStringValue(reader, "CreatedAt")),
                        CreatedBy = IsDBNullValue(reader, "CreatedBy") ? null : GetStringValue(reader, "CreatedBy"),
                        RelatedTransactionId = IsDBNullValue(reader, "RelatedTransactionId") ? null : GetIntValue(reader, "RelatedTransactionId")
                    };
                    transactions.Add(transaction);
                }

                return DatabaseResult<List<CreditTransaction>>.SuccessResult(transactions);
            }
            catch (Exception ex)
            {
                return DatabaseResult<List<CreditTransaction>>.ErrorResult($"Failed to get credit transactions: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Delete old credit transactions, keeping only the most recent ones
        /// </summary>
        public async Task<DatabaseResult> DeleteOldCreditTransactionsAsync(int keepCount = 100)
        {
            try
            {
                var query = @"
                    DELETE FROM CreditTransactions 
                    WHERE Id NOT IN (
                        SELECT Id FROM CreditTransactions 
                        ORDER BY CreatedAt DESC 
                        LIMIT @keepCount
                    )";

                using var connection = await CreateConnectionAsync();
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@keepCount", keepCount);
                
                var deletedCount = await command.ExecuteNonQueryAsync();
                
                LoggingService.Application.Information("Old credit transactions cleaned up",
                    ("DeletedCount", deletedCount),
                    ("KeepCount", keepCount));

                return DatabaseResult.SuccessResult();
            }
            catch (Exception ex)
            {
                return DatabaseResult.ErrorResult($"Failed to cleanup old credit transactions: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get recent transactions ordered by most recent first with database-level limiting
        /// </summary>
        public async Task<DatabaseResult<List<Transaction>>> GetRecentTransactionsAsync(int limit)
        {
            try
            {
                var query = @"
                    SELECT * FROM Transactions 
                    ORDER BY CreatedAt DESC 
                    LIMIT @limit";

                using var connection = await CreateConnectionAsync();
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@limit", limit);
                using var reader = await command.ExecuteReaderAsync();

                var transactions = new List<Transaction>();
                while (await reader.ReadAsync())
                {
                    var transaction = new Transaction
                    {
                        Id = GetIntValue(reader, "Id"),
                        TransactionCode = IsDBNullValue(reader, "TransactionCode") ? string.Empty : GetStringValue(reader, "TransactionCode"),
                        ProductId = GetIntValue(reader, "ProductId"),
                        TemplateId = IsDBNullValue(reader, "TemplateId") ? null : GetIntValue(reader, "TemplateId"),
                        Quantity = GetIntValue(reader, "Quantity"),
                        BasePrice = GetDecimalValue(reader, "BasePrice"),
                        TotalPrice = GetDecimalValue(reader, "TotalPrice"),
                        PaymentStatus = Enum.Parse<PaymentStatus>(GetStringValue(reader, "PaymentStatus")),
                        PaymentMethod = Enum.Parse<PaymentMethod>(GetStringValue(reader, "PaymentMethod")),
                        CustomerEmail = IsDBNullValue(reader, "CustomerEmail") ? null : GetStringValue(reader, "CustomerEmail"),
                        SessionId = IsDBNullValue(reader, "SessionId") ? null : GetStringValue(reader, "SessionId"),
                        CreatedAt = DateTime.Parse(GetStringValue(reader, "CreatedAt")),
                        CompletedAt = IsDBNullValue(reader, "CompletedAt") ? null : DateTime.Parse(GetStringValue(reader, "CompletedAt")),
                        Notes = IsDBNullValue(reader, "Notes") ? null : GetStringValue(reader, "Notes")
                    };
                    transactions.Add(transaction);
                }

                return DatabaseResult<List<Transaction>>.SuccessResult(transactions);
            }
            catch (Exception ex)
            {
                return DatabaseResult<List<Transaction>>.ErrorResult($"Failed to get recent transactions: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Update template pricing for existing templates that have $0 price
        /// </summary>
        public async Task<DatabaseResult> UpdateTemplatePricingAsync()
        {
            try
            {
                var query = @"
                    UPDATE Templates 
                    SET Price = CASE 
                        WHEN TemplateType = 0 THEN 5.00  -- Strip = $5.00
                        WHEN TemplateType = 1 THEN 3.00  -- Photo4x6 = $3.00
                        ELSE 5.00  -- Default to strip price
                    END
                    WHERE Price = 0";

                using var connection = await CreateConnectionAsync();
                using var command = new SqliteCommand(query, connection);
                
                var updatedCount = await command.ExecuteNonQueryAsync();
                
                LoggingService.Application.Information("Template pricing updated",
                    ("UpdatedTemplates", updatedCount));

                return DatabaseResult.SuccessResult();
            }
            catch (Exception ex)
            {
                return DatabaseResult.ErrorResult($"Failed to update template pricing: {ex.Message}", ex);
            }
        }

        public async Task<DatabaseResult> DeleteAdminUserAsync(string userId, string? deletedBy = null)
        {
            try
            {

                // First check if the user exists and get their username for logging
                var userResult = await GetByUserIdAsync<AdminUser>(userId);
                if (!userResult.Success || userResult.Data == null)
                {

                    return DatabaseResult.ErrorResult($"User with ID '{userId}' not found");
                }
                
                var username = userResult.Data.Username;

                using var connection = await CreateConnectionAsync();
                using var transaction = connection.BeginTransaction();

                try
                {
                    // Properly handle foreign key relationships instead of disabling constraints
                    
                    // 1. Update Templates.UploadedBy to NULL where it references this user
                    var updateTemplatesCmd = new SqliteCommand(
                        "UPDATE Templates SET UploadedBy = NULL WHERE UploadedBy = @userId", 
                        connection, transaction);
                    updateTemplatesCmd.Parameters.AddWithValue("@userId", userId);
                    await updateTemplatesCmd.ExecuteNonQueryAsync();
                    
                    // 2. Update Settings.UpdatedBy to NULL where it references this user
                    var updateSettingsCmd = new SqliteCommand(
                        "UPDATE Settings SET UpdatedBy = NULL WHERE UpdatedBy = @userId", 
                        connection, transaction);
                    updateSettingsCmd.Parameters.AddWithValue("@userId", userId);
                    await updateSettingsCmd.ExecuteNonQueryAsync();
                    
                    // 3. Update BusinessInfo.UpdatedBy to NULL where it references this user
                    var updateBusinessInfoCmd = new SqliteCommand(
                        "UPDATE BusinessInfo SET UpdatedBy = NULL WHERE UpdatedBy = @userId", 
                        connection, transaction);
                    updateBusinessInfoCmd.Parameters.AddWithValue("@userId", userId);
                    await updateBusinessInfoCmd.ExecuteNonQueryAsync();
                    
                    // 4. Update SystemErrors.ResolvedBy to NULL where it references this user
                    var updateErrorsCmd = new SqliteCommand(
                        "UPDATE SystemErrors SET ResolvedBy = NULL WHERE ResolvedBy = @userId", 
                        connection, transaction);
                    updateErrorsCmd.Parameters.AddWithValue("@userId", userId);
                    await updateErrorsCmd.ExecuteNonQueryAsync();
                    
                    // 5. Update AdminUsers self-references (CreatedBy, UpdatedBy) to NULL
                    var updateAdminUsersCmd = new SqliteCommand(
                        "UPDATE AdminUsers SET CreatedBy = NULL, UpdatedBy = NULL WHERE CreatedBy = @userId OR UpdatedBy = @userId", 
                        connection, transaction);
                    updateAdminUsersCmd.Parameters.AddWithValue("@userId", userId);
                    await updateAdminUsersCmd.ExecuteNonQueryAsync();
                    
                    // 6. Now safely delete the admin user
                    var deleteCmd = new SqliteCommand("DELETE FROM AdminUsers WHERE UserId = @userId", connection, transaction);
                    deleteCmd.Parameters.AddWithValue("@userId", userId);
                    var rowsAffected = await deleteCmd.ExecuteNonQueryAsync();
                    
                    // Commit the transaction
                    transaction.Commit();
                    
                    if (rowsAffected > 0)
                    {
                        // Use file-based logging instead of database logging
                        LoggingService.Application.Information("Admin user deleted",
                            ("Username", username),
                            ("UserId", userId),
                            ("DeletedBy", deletedBy ?? "Unknown"));

                        return DatabaseResult.SuccessResult();
                    }
                    else
                    {
                        return DatabaseResult.ErrorResult("User not found or already deleted");
                    }
                }
                catch
                {
                    // Rollback transaction on error
                    transaction.Rollback();
                    throw; // Re-throw to be caught by outer catch block
                }
            }
            catch (Exception ex)
            {

                return DatabaseResult.ErrorResult($"Failed to delete admin user: {ex.Message}", ex);
            }
        }

        // Helper Methods

        /// <summary>
        /// Determines if a property type is a navigation property that should be skipped during database insertion
        /// </summary>
        private bool IsNavigationProperty(Type propertyType)
        {
            // Handle nullable types
            var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
            
            // Skip these basic types that ARE database columns
            if (underlyingType.IsPrimitive ||
                underlyingType == typeof(string) ||
                underlyingType == typeof(decimal) ||
                underlyingType == typeof(DateTime) ||
                underlyingType == typeof(Guid) ||
                underlyingType.IsEnum)
            {
                return false;
            }
            
            // Everything else is likely a navigation property (complex objects, lists, etc.)
            return true;
        }

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
                nameof(CreditTransaction) => "CreditTransactions",
                nameof(Setting) => "Settings",
                nameof(BusinessInfo) => "BusinessInfo",
                nameof(HardwareStatusModel) => "HardwareStatus",
                nameof(PrintSupply) => "PrintSupplies",
                nameof(SupplyUsageHistory) => "SupplyUsageHistory",
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

        /// <summary>
        /// Get default price for template based on template type
        /// </summary>
        private decimal GetDefaultTemplatePrice(TemplateType templateType)
        {
            return templateType switch
            {
                TemplateType.Strip => 5.00m,        // Strip templates = $5.00
                TemplateType.Photo4x6 => 3.00m,     // 4x6 templates = $3.00
                _ => 5.00m  // Default to strip price for unknown types
            };
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password + "PhotoboothSalt"));
            return Convert.ToBase64String(hashedBytes);
        }

        private string GetStringValue(System.Data.Common.DbDataReader reader, string columnName, string defaultValue = "")
        {
            try
            {
                var ordinal = reader.GetOrdinal(columnName);
                return reader.IsDBNull(ordinal) ? defaultValue : reader.GetString(ordinal);
            }
            catch
            {
                return defaultValue;
            }
        }

        private int GetIntValue(System.Data.Common.DbDataReader reader, string columnName, int defaultValue = 0)
        {
            try
            {
                var ordinal = reader.GetOrdinal(columnName);
                return reader.IsDBNull(ordinal) ? defaultValue : reader.GetInt32(ordinal);
            }
            catch
            {
                return defaultValue;
            }
        }

        private long GetLongValue(System.Data.Common.DbDataReader reader, string columnName, long defaultValue = 0)
        {
            try
            {
                var ordinal = reader.GetOrdinal(columnName);
                return reader.IsDBNull(ordinal) ? defaultValue : reader.GetInt64(ordinal);
            }
            catch
            {
                return defaultValue;
            }
        }

        private bool GetBoolValue(System.Data.Common.DbDataReader reader, string columnName, bool defaultValue = false)
        {
            try
            {
                var ordinal = reader.GetOrdinal(columnName);
                return reader.IsDBNull(ordinal) ? defaultValue : reader.GetBoolean(ordinal);
            }
            catch
            {
                return defaultValue;
            }
        }

        private double GetDoubleValue(System.Data.Common.DbDataReader reader, string columnName, double defaultValue = 0.0)
        {
            try
            {
                var ordinal = reader.GetOrdinal(columnName);
                return reader.IsDBNull(ordinal) ? defaultValue : reader.GetDouble(ordinal);
            }
            catch
            {
                return defaultValue;
            }
        }

        private decimal GetDecimalValue(System.Data.Common.DbDataReader reader, string columnName, decimal defaultValue = 0m)
        {
            try
            {
                var ordinal = reader.GetOrdinal(columnName);
                return reader.IsDBNull(ordinal) ? defaultValue : reader.GetDecimal(ordinal);
            }
            catch
            {
                return defaultValue;
            }
        }

        private bool IsDBNullValue(System.Data.Common.DbDataReader reader, string columnName)
        {
            try
            {
                var ordinal = reader.GetOrdinal(columnName);
                return reader.IsDBNull(ordinal);
            }
            catch
            {
                return true; // Assume null if column doesn't exist
            }
        }

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
                // Use fixed default passwords - users will be forced to change on first login
                var masterPassword = "admin123";
                var userPassword = "user123";

                // Create default master admin with fixed password
                var masterAdmin = new AdminUser
                {
                    UserId = Guid.NewGuid().ToString(),
                    Username = "admin",
                    DisplayName = "Master Administrator", 
                    PasswordHash = HashPassword(masterPassword),
                    AccessLevel = Models.AdminAccessLevel.Master,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    CreatedBy = null  // Null CreatedBy indicates this is a setup account that needs password change
                };

                // Create default user admin with fixed password
                var userAdmin = new AdminUser
                {
                    UserId = Guid.NewGuid().ToString(),
                    Username = "user",
                    DisplayName = "User Administrator",
                    PasswordHash = HashPassword(userPassword), 
                    AccessLevel = Models.AdminAccessLevel.User,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    CreatedBy = null  // Null CreatedBy indicates this is a setup account that needs password change
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

                // Log that default credentials are being used
                LoggingService.Application.Information("Default admin accounts created with fixed passwords",
                    ("MasterUsername", "admin"),
                    ("MasterPassword", "admin123"),
                    ("UserUsername", "user"),
                    ("UserPassword", "user123"),
                    ("SecurityNote", "Users will be forced to change passwords on first login"));
                
                Console.WriteLine("=== Default credentials set: admin/admin123, user/user123 ===");
                Console.WriteLine("=== Users will be forced to change passwords on first login ===");


            }
            catch
            {

                throw;
            }
        }

        private async Task WriteInitialCredentialsSecurely(string masterPassword, string userPassword)
        {
            try
            {
                Console.WriteLine("=== CREATING INITIAL CREDENTIALS ===");
                // Create credentials file on Desktop for easy user access
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var credentialsFile = Path.Combine(desktopPath, "PhotoBoothX-LOGIN-CREDENTIALS.txt");
                
                Console.WriteLine($"Desktop path: {desktopPath}");
                Console.WriteLine($"Credentials file: {credentialsFile}");
                Console.WriteLine("Creating credentials file on Desktop...");

                Console.WriteLine("Generating credentials content...");
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
5. Delete this file after successful setup

Security Best Practices:
- Use strong passwords (12+ characters, mixed case, numbers, symbols)
- Enable password rotation reminders
- Limit operator account permissions
- Regularly backup admin settings

⚠️  Please delete this file after completing admin setup.
";

                await File.WriteAllTextAsync(credentialsFile, credentialsContent);

                LoggingService.Application.Information("Setup credentials file created successfully on Desktop",
                    ("CredentialsFile", credentialsFile));

            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to create setup credentials file on Desktop", ex,
                    ("DesktopPath", Environment.GetFolderPath(Environment.SpecialFolder.Desktop)));

                // Don't throw - this is not critical for application startup
                // The user can still access admin panel and change passwords manually
            }
        }

        public static void CleanupSetupCredentials()
        {
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var credentialsFile = Path.Combine(desktopPath, "PhotoBoothX-LOGIN-CREDENTIALS.txt");

            try
            {
                if (File.Exists(credentialsFile))
                {
                    File.Delete(credentialsFile);
                    LoggingService.Application.Information("Setup credentials file deleted successfully", ("CredentialsFile", credentialsFile));
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to delete setup credentials file", ex, ("CredentialsFile", credentialsFile));
                // Don't throw - cleanup failure is not critical
            }
        }

        public static async Task CleanupSetupCredentialsAsync()
        {
            await Task.Run(() =>
            {
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var credentialsFile = Path.Combine(desktopPath, "PhotoBoothX-LOGIN-CREDENTIALS.txt");

                try
                {
                    if (File.Exists(credentialsFile))
                    {
                        File.Delete(credentialsFile);
                        LoggingService.Application.Information("Setup credentials file deleted successfully (async)", ("CredentialsFile", credentialsFile));
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.Application.Error("Failed to delete setup credentials file (async)", ex, ("CredentialsFile", credentialsFile));
                    // Don't throw - cleanup failure is not critical
                }
            });
        }

        private async Task CreateDefaultSettingsDirect(SqliteConnection connection)
        {
            try
            {

                // NOTE: Product pricing and enabled states removed - now managed exclusively in Products table
                var defaultSettings = new[]
                {
                    new { Id = Guid.NewGuid().ToString(), Category = "System", Key = "Volume", Value = "75", DataType = "Integer", Description = "Audio volume level (0-100)" },
                    new { Id = Guid.NewGuid().ToString(), Category = "System", Key = "LightsEnabled", Value = "true", DataType = "Boolean", Description = "Enable camera flash lights" },
                    new { Id = Guid.NewGuid().ToString(), Category = "System", Key = "CurrentCredits", Value = "0", DataType = "Decimal", Description = "Current credit balance in dollars" },
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

                }

            }
            catch
            {

                throw;
            }
        }

        
        #region Camera Settings Methods

        public async Task<DatabaseResult<List<CameraSettings>>> GetAllCameraSettingsAsync()
        {
            try
            {
                Console.WriteLine("DatabaseService: Getting all camera settings");
                var query = "SELECT * FROM CameraSettings ORDER BY SettingName";

                using var connection = await CreateConnectionAsync();
                using var command = new SqliteCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                var settings = new List<CameraSettings>();
                while (await reader.ReadAsync())
                {
                    settings.Add(new CameraSettings
                    {
                        Id = GetIntValue(reader, "Id"),
                        SettingName = GetStringValue(reader, "SettingName"),
                        SettingValue = GetIntValue(reader, "SettingValue"),
                        Description = IsDBNullValue(reader, "Description") ? null : GetStringValue(reader, "Description"),
                        CreatedAt = DateTime.Parse(GetStringValue(reader, "CreatedAt")),
                        UpdatedAt = DateTime.Parse(GetStringValue(reader, "UpdatedAt"))
                    });
                }

                Console.WriteLine($"DatabaseService: Retrieved {settings.Count} camera settings");
                return DatabaseResult<List<CameraSettings>>.SuccessResult(settings);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DatabaseService: Error getting camera settings - {ex.Message}");
                return DatabaseResult<List<CameraSettings>>.ErrorResult($"Failed to get camera settings: {ex.Message}");
            }
        }

        public async Task<DatabaseResult<CameraSettings?>> GetCameraSettingAsync(string settingName)
        {
            try
            {
                Console.WriteLine($"DatabaseService: Getting camera setting '{settingName}'");
                var query = "SELECT * FROM CameraSettings WHERE SettingName = @settingName";

                using var connection = await CreateConnectionAsync();
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@settingName", settingName);
                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    var setting = new CameraSettings
                    {
                        Id = GetIntValue(reader, "Id"),
                        SettingName = GetStringValue(reader, "SettingName"),
                        SettingValue = GetIntValue(reader, "SettingValue"),
                        Description = IsDBNullValue(reader, "Description") ? null : GetStringValue(reader, "Description"),
                        CreatedAt = DateTime.Parse(GetStringValue(reader, "CreatedAt")),
                        UpdatedAt = DateTime.Parse(GetStringValue(reader, "UpdatedAt"))
                    };

                    Console.WriteLine($"DatabaseService: Found camera setting '{settingName}' = {setting.SettingValue}");
                    return DatabaseResult<CameraSettings?>.SuccessResult(setting);
                }

                Console.WriteLine($"DatabaseService: Camera setting '{settingName}' not found");
                return DatabaseResult<CameraSettings?>.SuccessResult(null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DatabaseService: Error getting camera setting '{settingName}' - {ex.Message}");
                return DatabaseResult<CameraSettings?>.ErrorResult($"Failed to get camera setting: {ex.Message}");
            }
        }

        public async Task<DatabaseResult> SaveCameraSettingAsync(string settingName, int settingValue)
        {
            try
            {
                Console.WriteLine($"DatabaseService: Saving camera setting '{settingName}' = {settingValue}");
                var query = @"
                    INSERT INTO CameraSettings (SettingName, SettingValue, CreatedAt, UpdatedAt) 
                    VALUES (@settingName, @settingValue, @createdAt, @updatedAt)
                    ON CONFLICT(SettingName) DO UPDATE SET
                        SettingValue = excluded.SettingValue,
                        UpdatedAt = excluded.UpdatedAt";

                using var connection = await CreateConnectionAsync();
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@settingName", settingName);
                command.Parameters.AddWithValue("@settingValue", settingValue);
                command.Parameters.AddWithValue("@createdAt", DateTime.Now);
                command.Parameters.AddWithValue("@updatedAt", DateTime.Now);

                await command.ExecuteNonQueryAsync();
                Console.WriteLine($"DatabaseService: Successfully saved camera setting '{settingName}' = {settingValue}");
                return DatabaseResult.SuccessResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DatabaseService: Error saving camera setting '{settingName}' - {ex.Message}");
                return DatabaseResult.ErrorResult($"Failed to save camera setting: {ex.Message}");
            }
        }

        public async Task<DatabaseResult> SaveAllCameraSettingsAsync(int brightness, int zoom, int contrast)
        {
            try
            {
                Console.WriteLine($"DatabaseService: Saving all camera settings - Brightness:{brightness}, Zoom:{zoom}, Contrast:{contrast}");
                
                using var connection = await CreateConnectionAsync();
                using var transaction = connection.BeginTransaction();
                
                try
                {
                    var query = @"
                        INSERT INTO CameraSettings (SettingName, SettingValue, CreatedAt, UpdatedAt) 
                        VALUES (@settingName, @settingValue, @createdAt, @updatedAt)
                        ON CONFLICT(SettingName) DO UPDATE SET
                            SettingValue = excluded.SettingValue,
                            UpdatedAt = excluded.UpdatedAt";

                    // Save brightness
                    using var cmd1 = new SqliteCommand(query, connection, transaction);
                    cmd1.Parameters.AddWithValue("@settingName", "Brightness");
                    cmd1.Parameters.AddWithValue("@settingValue", brightness);
                    cmd1.Parameters.AddWithValue("@createdAt", DateTime.Now);
                    cmd1.Parameters.AddWithValue("@updatedAt", DateTime.Now);
                    await cmd1.ExecuteNonQueryAsync();

                    // Save zoom
                    using var cmd2 = new SqliteCommand(query, connection, transaction);
                    cmd2.Parameters.AddWithValue("@settingName", "Zoom");
                    cmd2.Parameters.AddWithValue("@settingValue", zoom);
                    cmd2.Parameters.AddWithValue("@createdAt", DateTime.Now);
                    cmd2.Parameters.AddWithValue("@updatedAt", DateTime.Now);
                    await cmd2.ExecuteNonQueryAsync();

                    // Save contrast
                    using var cmd3 = new SqliteCommand(query, connection, transaction);
                    cmd3.Parameters.AddWithValue("@settingName", "Contrast");
                    cmd3.Parameters.AddWithValue("@settingValue", contrast);
                    cmd3.Parameters.AddWithValue("@createdAt", DateTime.Now);
                    cmd3.Parameters.AddWithValue("@updatedAt", DateTime.Now);
                    await cmd3.ExecuteNonQueryAsync();

                    transaction.Commit();
                    Console.WriteLine("DatabaseService: Successfully saved all camera settings");
                    return DatabaseResult.SuccessResult();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DatabaseService: Error saving camera settings - {ex.Message}");
                return DatabaseResult.ErrorResult($"Failed to save camera settings: {ex.Message}");
            }
        }

        #endregion

    }
} 

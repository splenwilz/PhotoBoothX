using Serilog;
using Serilog.Core;
using Serilog.Events;
using System;
using System.IO;

namespace Photobooth.Services
{
    /// <summary>
    /// Comprehensive logging service for PhotoBoothX kiosk application
    /// Provides structured file-based logging with multiple categories and automatic rotation
    /// </summary>
    public static class LoggingService
    {
        private static Logger? _applicationLogger;
        private static Logger? _hardwareLogger;
        private static Logger? _transactionLogger;
        private static Logger? _errorLogger;
        private static Logger? _performanceLogger;
        
        private static string _logDirectory = "";
        private static bool _isInitialized = false;
        
        // Thread safety lock object for initialization
        private static readonly object _initializationLock = new object();

        /// <summary>
        /// Initialize the logging system with all required loggers
        /// </summary>
        public static void Initialize()
        {
            // Thread-safe initialization using double-checked locking pattern
            if (_isInitialized) return;

            lock (_initializationLock)
            {
                // Double-check after acquiring lock to prevent race conditions
                if (_isInitialized) return;

                try
                {
                    // Create logs directory in AppData
                    var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    _logDirectory = Path.Combine(appDataPath, "PhotoboothX", "Logs");
                    Directory.CreateDirectory(_logDirectory);

                    // Create base configuration template
                    Func<LoggerConfiguration> createBaseConfig = () => new LoggerConfiguration()
                        .Enrich.WithThreadId()
                        .Enrich.WithEnvironmentName()
                        .Enrich.WithMachineName()
                        .MinimumLevel.Debug()
                        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                        .MinimumLevel.Override("System", LogEventLevel.Warning);

                    // Application Logger - General operations, startup, shutdown
                    _applicationLogger = createBaseConfig()
                        .WriteTo.File(
                            path: Path.Combine(_logDirectory, "application-.log"),
                            rollingInterval: RollingInterval.Day,
                            retainedFileCountLimit: 30,
                            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{ThreadId}] {Category}: {Message}{NewLine}{Exception}",
                            restrictedToMinimumLevel: LogEventLevel.Information
                        )
                        .WriteTo.File(
                            path: Path.Combine(_logDirectory, "application-debug-.log"),
                            rollingInterval: RollingInterval.Day,
                            retainedFileCountLimit: 7,
                            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{ThreadId}] {Category}: {Message}{NewLine}{Exception}",
                            restrictedToMinimumLevel: LogEventLevel.Debug
                        )
                        .CreateLogger();

                    // Hardware Logger - Camera, printer, Arduino, RFID
                    _hardwareLogger = createBaseConfig()
                        .WriteTo.File(
                            path: Path.Combine(_logDirectory, "hardware-.log"),
                            rollingInterval: RollingInterval.Day,
                            retainedFileCountLimit: 30,
                            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Component}: {Message} {Properties}{NewLine}{Exception}",
                            restrictedToMinimumLevel: LogEventLevel.Debug
                        )
                        .CreateLogger();

                    // Transaction Logger - Customer interactions, payments, photos
                    _transactionLogger = createBaseConfig()
                        .WriteTo.File(
                            path: Path.Combine(_logDirectory, "transactions-.log"),
                            rollingInterval: RollingInterval.Day,
                            retainedFileCountLimit: 90, // Keep longer for business records
                            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {TransactionId}: {Message} {Properties}{NewLine}{Exception}",
                            restrictedToMinimumLevel: LogEventLevel.Information
                        )
                        .CreateLogger();

                    // Error Logger - All errors and exceptions
                    _errorLogger = createBaseConfig()
                        .WriteTo.File(
                            path: Path.Combine(_logDirectory, "errors-.log"),
                            rollingInterval: RollingInterval.Day,
                            retainedFileCountLimit: 60,
                            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{ThreadId}] {Category}: {Message}{NewLine}{Exception}",
                            restrictedToMinimumLevel: LogEventLevel.Warning
                        )
                        .CreateLogger();

                    // Performance Logger - Timing, memory, resource usage
                    _performanceLogger = createBaseConfig()
                        .WriteTo.File(
                            path: Path.Combine(_logDirectory, "performance-.log"),
                            rollingInterval: RollingInterval.Day,
                            retainedFileCountLimit: 14,
                            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Operation}: {Message} {Properties}{NewLine}",
                            restrictedToMinimumLevel: LogEventLevel.Information
                        )
                        .CreateLogger();

                    _isInitialized = true;
                    
                    // Log successful initialization
                    Application.Information("Logging system initialized successfully", 
                        ("LogDirectory", _logDirectory),
                        ("Loggers", "Application, Hardware, Transaction, Error, Performance"));
                }
                catch (Exception ex)
                {
                    // Fallback to console if logging setup fails
                    Console.WriteLine($"Failed to initialize logging: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Cleanup logging resources
        /// </summary>
        public static void Shutdown()
        {
            if (!_isInitialized) return;

            try
            {
                Application.Information("Logging system shutting down");
                
                _applicationLogger?.Dispose();
                _hardwareLogger?.Dispose();
                _transactionLogger?.Dispose();
                _errorLogger?.Dispose();
                _performanceLogger?.Dispose();
                
                _isInitialized = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during logging shutdown: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the current log directory path
        /// </summary>
        public static string LogDirectory => _logDirectory;

        /// <summary>
        /// Application-level logging (startup, shutdown, general operations)
        /// </summary>
        public static class Application
        {
            public static void Debug(string message, params (string key, object value)[] properties)
                => LogWithProperties(_applicationLogger, LogEventLevel.Debug, "Application", message, properties);

            public static void Information(string message, params (string key, object value)[] properties)
                => LogWithProperties(_applicationLogger, LogEventLevel.Information, "Application", message, properties);

            public static void Warning(string message, params (string key, object value)[] properties)
                => LogWithProperties(_applicationLogger, LogEventLevel.Warning, "Application", message, properties);

            public static void Error(string message, Exception? exception = null, params (string key, object value)[] properties)
                => LogWithPropertiesAndException(_applicationLogger, LogEventLevel.Error, "Application", message, exception, properties);

            public static void Critical(string message, Exception? exception = null, params (string key, object value)[] properties)
                => LogWithPropertiesAndException(_applicationLogger, LogEventLevel.Fatal, "Application", message, exception, properties);
        }

        /// <summary>
        /// Hardware-level logging (camera, printer, Arduino, RFID)
        /// </summary>
        public static class Hardware
        {
            public static void Debug(string component, string message, params (string key, object value)[] properties)
                => LogWithProperties(_hardwareLogger, LogEventLevel.Debug, component, message, properties);

            public static void Information(string component, string message, params (string key, object value)[] properties)
                => LogWithProperties(_hardwareLogger, LogEventLevel.Information, component, message, properties);

            public static void Warning(string component, string message, params (string key, object value)[] properties)
                => LogWithProperties(_hardwareLogger, LogEventLevel.Warning, component, message, properties);

            public static void Error(string component, string message, Exception? exception = null, params (string key, object value)[] properties)
                => LogWithPropertiesAndException(_hardwareLogger, LogEventLevel.Error, component, message, exception, properties);

            public static void Critical(string component, string message, Exception? exception = null, params (string key, object value)[] properties)
                => LogWithPropertiesAndException(_hardwareLogger, LogEventLevel.Fatal, component, message, exception, properties);
        }

        /// <summary>
        /// Transaction-level logging (customer interactions, payments, photos)
        /// </summary>
        public static class Transaction
        {
            public static void Information(string transactionId, string message, params (string key, object value)[] properties)
                => LogWithProperties(_transactionLogger, LogEventLevel.Information, transactionId, message, properties);

            public static void Warning(string transactionId, string message, params (string key, object value)[] properties)
                => LogWithProperties(_transactionLogger, LogEventLevel.Warning, transactionId, message, properties);

            public static void Error(string transactionId, string message, Exception? exception = null, params (string key, object value)[] properties)
                => LogWithPropertiesAndException(_transactionLogger, LogEventLevel.Error, transactionId, message, exception, properties);
        }

        /// <summary>
        /// Error-level logging (all errors and exceptions)
        /// </summary>
        public static class Errors
        {
            public static void Warning(string category, string message, params (string key, object value)[] properties)
                => LogWithProperties(_errorLogger, LogEventLevel.Warning, category, message, properties);

            public static void Error(string category, string message, Exception? exception = null, params (string key, object value)[] properties)
                => LogWithPropertiesAndException(_errorLogger, LogEventLevel.Error, category, message, exception, properties);

            public static void Critical(string category, string message, Exception? exception = null, params (string key, object value)[] properties)
                => LogWithPropertiesAndException(_errorLogger, LogEventLevel.Fatal, category, message, exception, properties);
        }

        /// <summary>
        /// Performance-level logging (timing, memory, resource usage)
        /// </summary>
        public static class Performance
        {
            public static void Information(string operation, string message, params (string key, object value)[] properties)
                => LogWithProperties(_performanceLogger, LogEventLevel.Information, operation, message, properties);

            public static void Warning(string operation, string message, params (string key, object value)[] properties)
                => LogWithProperties(_performanceLogger, LogEventLevel.Warning, operation, message, properties);

            public static void Timing(string operation, long milliseconds, params (string key, object value)[] properties)
            {
                var allProperties = new List<(string key, object value)>(properties)
                {
                    ("Duration", $"{milliseconds}ms")
                };
                LogWithProperties(_performanceLogger, LogEventLevel.Information, operation, "Operation completed", allProperties.ToArray());
            }
        }

        /// <summary>
        /// Helper method to log with structured properties
        /// </summary>
        private static void LogWithProperties(Logger? logger, LogEventLevel level, string category, string message, (string key, object value)[] properties)
        {
            if (logger == null || !_isInitialized) return;

            try
            {
                ILogger contextLogger = logger;

                // Add properties safely using ForContext to prevent template injection
                if (properties.Length > 0)
                {
                    foreach (var (key, value) in properties)
                    {
                        contextLogger = contextLogger.ForContext(key, value);
                    }
                }

                // Use a simple, safe template that doesn't include user-provided keys
                contextLogger.Write(level, "{Category}: {Message}", category, message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Logging error: {ex.Message}");
            }
        }

        /// <summary>
        /// Helper method to log with structured properties and exception
        /// </summary>
        private static void LogWithPropertiesAndException(Logger? logger, LogEventLevel level, string category, string message, Exception? exception, (string key, object value)[] properties)
        {
            if (logger == null || !_isInitialized) return;

            try
            {
                ILogger contextLogger = logger;

                // Add properties safely using ForContext to prevent template injection
                if (properties.Length > 0)
                {
                    foreach (var (key, value) in properties)
                    {
                        contextLogger = contextLogger.ForContext(key, value);
                    }
                }

                // Use a simple, safe template that doesn't include user-provided keys
                if (exception != null)
                {
                    contextLogger.Write(level, exception, "{Category}: {Message}", category, message);
                }
                else
                {
                    contextLogger.Write(level, "{Category}: {Message}", category, message);
                }

                // Also log to error logger if this is an error/critical level
                if ((level == LogEventLevel.Error || level == LogEventLevel.Fatal) && logger != _errorLogger)
                {
                    ILogger? errorContextLogger = _errorLogger;
                    if (properties.Length > 0)
                    {
                        foreach (var (key, value) in properties)
                        {
                            errorContextLogger = errorContextLogger?.ForContext(key, value);
                        }
                    }
                    
                    if (exception != null)
                    {
                        errorContextLogger?.Write(level, exception, "{Category}: {Message}", category, message);
                    }
                    else
                    {
                        errorContextLogger?.Write(level, "{Category}: {Message}", category, message);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Logging error: {ex.Message}");
            }
        }
    }
} 
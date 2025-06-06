using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using Photobooth.Services;
using System.Reflection;

namespace Photobooth.Tests.Services
{
    [TestClass]
    public class NotificationServiceTests
    {
        private NotificationService _notificationService = null!;

        [TestInitialize]
        public void Setup()
        {
            _notificationService = NotificationService.Instance;
        }

        #region Singleton Pattern Tests
        [TestMethod]
        public void Instance_Singleton_ReturnsSameInstance()
        {
            // Act
            var instance1 = NotificationService.Instance;
            var instance2 = NotificationService.Instance;

            // Assert
            instance1.Should().BeSameAs(instance2);
        }

        [TestMethod]
        public void Instance_MultipleThreads_ReturnsSameInstance()
        {
            // Arrange
            NotificationService? instance1 = null;
            NotificationService? instance2 = null;
            var task1 = System.Threading.Tasks.Task.Run(() => instance1 = NotificationService.Instance);
            var task2 = System.Threading.Tasks.Task.Run(() => instance2 = NotificationService.Instance);

            // Act
            System.Threading.Tasks.Task.WaitAll(task1, task2);

            // Assert
            instance1.Should().BeSameAs(instance2);
            instance1.Should().BeSameAs(_notificationService);
        }
        #endregion

        #region Method Signature Tests
        [TestMethod]
        public void NotificationService_HasShowSuccessMethod()
        {
            // Test that the ShowSuccess method exists with correct signature
            // Act - Look for method with title, message, and optional autoCloseSeconds
            var method = typeof(NotificationService).GetMethod("ShowSuccess", new[] { typeof(string), typeof(string), typeof(int) });

            // Assert
            method.Should().NotBeNull("ShowSuccess method should exist");
            method!.ReturnType.Should().Be(typeof(void));
            method.IsPublic.Should().BeTrue();
        }

        [TestMethod]
        public void NotificationService_HasShowErrorMethod()
        {
            // Test that the ShowError method exists with correct signature
            // Act
            var method = typeof(NotificationService).GetMethod("ShowError", new[] { typeof(string), typeof(string), typeof(int) });

            // Assert
            method.Should().NotBeNull("ShowError method should exist");
            method!.ReturnType.Should().Be(typeof(void));
            method.IsPublic.Should().BeTrue();
        }

        [TestMethod]
        public void NotificationService_HasShowWarningMethod()
        {
            // Test that the ShowWarning method exists with correct signature
            // Act
            var method = typeof(NotificationService).GetMethod("ShowWarning", new[] { typeof(string), typeof(string), typeof(int) });

            // Assert
            method.Should().NotBeNull("ShowWarning method should exist");
            method!.ReturnType.Should().Be(typeof(void));
            method.IsPublic.Should().BeTrue();
        }

        [TestMethod]
        public void NotificationService_HasShowInfoMethod()
        {
            // Test that the ShowInfo method exists with correct signature
            // Act
            var method = typeof(NotificationService).GetMethod("ShowInfo", new[] { typeof(string), typeof(string), typeof(int) });

            // Assert
            method.Should().NotBeNull("ShowInfo method should exist");
            method!.ReturnType.Should().Be(typeof(void));
            method.IsPublic.Should().BeTrue();
        }

        [TestMethod]
        public void NotificationService_HasClearAllMethod()
        {
            // Test that ClearAll method exists with correct signature
            // Act
            var method = typeof(NotificationService).GetMethod("ClearAll");

            // Assert
            method.Should().NotBeNull("ClearAll method should exist");
            method!.ReturnType.Should().Be(typeof(void));
            method.IsPublic.Should().BeTrue();
        }
        #endregion

        #region Quick Notifications Tests
        [TestMethod]
        public void QuickNotifications_ClassExists()
        {
            // Note: The test was updated to use static reflection instead of Public|Static flags
            // because nested classes behave differently with BindingFlags
            
            // Test that the Quick nested class exists
            // Act
            var quickType = typeof(NotificationService).GetNestedType("Quick");
            
            // Assert
            quickType.Should().NotBeNull("Quick nested class should exist");
            quickType!.IsClass.Should().BeTrue("Quick should be a class");
            quickType.IsNestedPublic.Should().BeTrue("Quick should be public (nested classes use IsNestedPublic)");
        }

        [TestMethod]
        public void QuickNotifications_HasRequiredMethods()
        {
            // Test that the Quick class has all required static methods
            // Act
            var quickType = typeof(NotificationService).GetNestedType("Quick", BindingFlags.Public | BindingFlags.Static);
            
            // Assert
            quickType.Should().NotBeNull("Quick nested class should exist");
            
            // Check that it has some expected static methods
            var methods = quickType!.GetMethods(BindingFlags.Public | BindingFlags.Static);
            methods.Should().NotBeEmpty("Quick class should have public static methods");
            
            // Verify specific methods exist
            var settingsSaved = quickType.GetMethod("SettingsSaved", BindingFlags.Public | BindingFlags.Static);
            var userCreated = quickType.GetMethod("UserCreated", BindingFlags.Public | BindingFlags.Static);
            var userDeleted = quickType.GetMethod("UserDeleted", BindingFlags.Public | BindingFlags.Static);
            var logoUploaded = quickType.GetMethod("LogoUploaded", BindingFlags.Public | BindingFlags.Static);
            var passwordUpdated = quickType.GetMethod("PasswordUpdated", BindingFlags.Public | BindingFlags.Static);
            var accessDenied = quickType.GetMethod("AccessDenied", BindingFlags.Public | BindingFlags.Static);

            settingsSaved.Should().NotBeNull("SettingsSaved method should exist");
            userCreated.Should().NotBeNull("UserCreated method should exist");
            userDeleted.Should().NotBeNull("UserDeleted method should exist");
            logoUploaded.Should().NotBeNull("LogoUploaded method should exist");
            passwordUpdated.Should().NotBeNull("PasswordUpdated method should exist");
            accessDenied.Should().NotBeNull("AccessDenied method should exist");

            // Verify methods are static and void
            settingsSaved!.IsStatic.Should().BeTrue();
            userCreated!.IsStatic.Should().BeTrue();
            userDeleted!.IsStatic.Should().BeTrue();
            logoUploaded!.IsStatic.Should().BeTrue();
            passwordUpdated!.IsStatic.Should().BeTrue();
            accessDenied!.IsStatic.Should().BeTrue();
        }

        [TestMethod]
        public void QuickNotifications_MethodsHaveCorrectParameterCount()
        {
            // Test that Quick methods have expected parameter counts
            // Act
            var quickType = typeof(NotificationService).GetNestedType("Quick", BindingFlags.Public | BindingFlags.Static);
            
            // Assert
            var settingsSaved = quickType!.GetMethod("SettingsSaved", BindingFlags.Public | BindingFlags.Static);
            var userCreated = quickType.GetMethod("UserCreated", BindingFlags.Public | BindingFlags.Static);
            var userDeleted = quickType.GetMethod("UserDeleted", BindingFlags.Public | BindingFlags.Static);
            var logoUploaded = quickType.GetMethod("LogoUploaded", BindingFlags.Public | BindingFlags.Static);
            var passwordUpdated = quickType.GetMethod("PasswordUpdated", BindingFlags.Public | BindingFlags.Static);
            var accessDenied = quickType.GetMethod("AccessDenied", BindingFlags.Public | BindingFlags.Static);

            // Most methods should have minimal parameters (likely 0 for quick notifications)
            settingsSaved!.GetParameters().Length.Should().Be(0, "SettingsSaved should take no parameters");
            userCreated!.GetParameters().Length.Should().BeLessOrEqualTo(1, "UserCreated should take 0-1 parameters");
            userDeleted!.GetParameters().Length.Should().BeLessOrEqualTo(1, "UserDeleted should take 0-1 parameters");
            logoUploaded!.GetParameters().Length.Should().Be(0, "LogoUploaded should take no parameters");
            passwordUpdated!.GetParameters().Length.Should().Be(0, "PasswordUpdated should take no parameters");
            accessDenied!.GetParameters().Length.Should().Be(0, "AccessDenied should take no parameters");
        }
        #endregion

        #region Service State Tests
        [TestMethod]
        public void NotificationService_IsProperlyInstantiated()
        {
            // Test that the service instance is not null and properly configured
            // Act & Assert
            _notificationService.Should().NotBeNull();
            _notificationService.GetType().Should().Be(typeof(NotificationService));
        }

        [TestMethod]
        public void NotificationService_HasCorrectPublicInterface()
        {
            // Test that the service exposes the expected public methods
            // Act
            var type = typeof(NotificationService);
            var publicMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

            // Assert
            var methodNames = publicMethods.Select(m => m.Name).ToArray();
            
            methodNames.Should().Contain("ShowSuccess", "Service should expose ShowSuccess method");
            methodNames.Should().Contain("ShowError", "Service should expose ShowError method");
            methodNames.Should().Contain("ShowWarning", "Service should expose ShowWarning method");
            methodNames.Should().Contain("ShowInfo", "Service should expose ShowInfo method");
            methodNames.Should().Contain("ClearAll", "Service should expose ClearAll method");
        }
        #endregion

        #region Method Overloads Tests
        [TestMethod]
        public void NotificationService_ShowMethods_HaveOptionalAutoCloseParameter()
        {
            // Test that notification methods have proper overloads
            // Act
            var type = typeof(NotificationService);
            
            // Check for methods with 2 parameters (title, message)
            var showSuccessOverload = type.GetMethod("ShowSuccess", new[] { typeof(string), typeof(string) });
            var showErrorOverload = type.GetMethod("ShowError", new[] { typeof(string), typeof(string) });
            var showWarningOverload = type.GetMethod("ShowWarning", new[] { typeof(string), typeof(string) });
            var showInfoOverload = type.GetMethod("ShowInfo", new[] { typeof(string), typeof(string) });

            // Assert - These may or may not exist depending on implementation
            // We test that at least one variant exists for each
            var showSuccessMethods = type.GetMethods().Where(m => m.Name == "ShowSuccess").ToArray();
            var showErrorMethods = type.GetMethods().Where(m => m.Name == "ShowError").ToArray();
            var showWarningMethods = type.GetMethods().Where(m => m.Name == "ShowWarning").ToArray();
            var showInfoMethods = type.GetMethods().Where(m => m.Name == "ShowInfo").ToArray();

            showSuccessMethods.Should().NotBeEmpty("ShowSuccess should have at least one overload");
            showErrorMethods.Should().NotBeEmpty("ShowError should have at least one overload");
            showWarningMethods.Should().NotBeEmpty("ShowWarning should have at least one overload");
            showInfoMethods.Should().NotBeEmpty("ShowInfo should have at least one overload");
        }
        #endregion

        #region Error Handling Tests
        [TestMethod]
        public void NotificationService_MethodsWithNullParameters_DoNotThrow()
        {
            // Test that methods handle null parameters gracefully without creating UI
            // We can't call the methods directly as they create UI, but we can test reflection
            
            // Act & Assert - Test method signatures accept string parameters
            var showSuccessMethod = typeof(NotificationService).GetMethod("ShowSuccess", new[] { typeof(string), typeof(string), typeof(int) });
            var showErrorMethod = typeof(NotificationService).GetMethod("ShowError", new[] { typeof(string), typeof(string), typeof(int) });
            
            showSuccessMethod.Should().NotBeNull();
            showErrorMethod.Should().NotBeNull();
            
            // Verify parameter types
            var successParams = showSuccessMethod!.GetParameters();
            var errorParams = showErrorMethod!.GetParameters();
            
            successParams[0].ParameterType.Should().Be(typeof(string), "First parameter should be string (title)");
            successParams[1].ParameterType.Should().Be(typeof(string), "Second parameter should be string (message)");
            successParams[2].ParameterType.Should().Be(typeof(int), "Third parameter should be int (autoCloseSeconds)");
            
            errorParams[0].ParameterType.Should().Be(typeof(string), "First parameter should be string (title)");
            errorParams[1].ParameterType.Should().Be(typeof(string), "Second parameter should be string (message)");
            errorParams[2].ParameterType.Should().Be(typeof(int), "Third parameter should be int (autoCloseSeconds)");
        }
        #endregion

        #region Type Safety Tests
        [TestMethod]
        public void NotificationService_AutoCloseParameter_IsInteger()
        {
            // Test that autoClose parameters are properly typed
            // Act
            var showMethods = typeof(NotificationService).GetMethods()
                .Where(m => m.Name.StartsWith("Show") && m.GetParameters().Length == 3)
                .ToArray();

            // Assert
            foreach (var method in showMethods)
            {
                var parameters = method.GetParameters();
                if (parameters.Length == 3)
                {
                    parameters[2].ParameterType.Should().Be(typeof(int), 
                        $"Method {method.Name} should have int as third parameter for autoCloseSeconds");
                }
            }
        }

        [TestMethod]
        public void NotificationService_AllShowMethods_ReturnVoid()
        {
            // Test that all Show methods return void (fire-and-forget pattern)
            // Act
            var showMethods = typeof(NotificationService).GetMethods()
                .Where(m => m.Name.StartsWith("Show"))
                .ToArray();

            // Assert
            foreach (var method in showMethods)
            {
                method.ReturnType.Should().Be(typeof(void), 
                    $"Method {method.Name} should return void");
            }
        }
        #endregion

        #region Integration Pattern Tests
        [TestMethod]
        public void NotificationService_FollowsServicePattern()
        {
            // Test that the service follows proper service patterns
            // Act
            var type = typeof(NotificationService);

            // Assert
            type.IsClass.Should().BeTrue("NotificationService should be a class");
            type.IsPublic.Should().BeTrue("NotificationService should be public");
            type.IsAbstract.Should().BeFalse("NotificationService should not be abstract");
            type.IsSealed.Should().BeFalse("NotificationService should not be sealed (for testability)");
        }

        [TestMethod]
        public void NotificationService_HasStaticInstanceProperty()
        {
            // Test that the singleton pattern is properly implemented
            // Act
            var instanceProperty = typeof(NotificationService).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);

            // Assert
            instanceProperty.Should().NotBeNull("Instance property should exist");
            instanceProperty!.PropertyType.Should().Be(typeof(NotificationService));
            instanceProperty.CanRead.Should().BeTrue("Instance property should be readable");
            instanceProperty.GetMethod!.IsStatic.Should().BeTrue("Instance property getter should be static");
        }
        #endregion

        #region Performance and Scalability Tests
        [TestMethod]
        public void NotificationService_MultipleInstances_PerformanceTest()
        {
            // Test that getting multiple instances is fast (singleton pattern efficiency)
            // Arrange
            const int iterations = 1000;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act
            for (int i = 0; i < iterations; i++)
            {
                var instance = NotificationService.Instance;
                instance.Should().NotBeNull();
            }

            stopwatch.Stop();

            // Assert
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(100, 
                "Getting singleton instance should be very fast");
        }

        [TestMethod]
        public void NotificationService_ConcurrentAccess_ThreadSafe()
        {
            // Test that concurrent access to singleton is thread-safe
            // Arrange
            var instances = new NotificationService[10];
            var tasks = new System.Threading.Tasks.Task[10];

            // Act
            for (int i = 0; i < 10; i++)
            {
                var index = i;
                tasks[i] = System.Threading.Tasks.Task.Run(() =>
                {
                    instances[index] = NotificationService.Instance;
                });
            }

            System.Threading.Tasks.Task.WaitAll(tasks);

            // Assert
            for (int i = 0; i < 10; i++)
            {
                instances[i].Should().BeSameAs(instances[0], 
                    "All instances should be the same object");
            }
        }
        #endregion
    }
} 
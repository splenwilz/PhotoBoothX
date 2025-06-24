using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Photobooth.Services;

namespace Photobooth.Tests.Services
{
    [TestClass]
    public class VirtualKeyboardServiceBasicTests
    {
        #region Service Pattern Tests

        [TestMethod]
        public void Instance_ReturnsNotNull()
        {
            // Act & Assert
            VirtualKeyboardService.Instance.Should().NotBeNull();
        }

        [TestMethod]
        public void Instance_ReturnsSameInstance()
        {
            // Act
            var instance1 = VirtualKeyboardService.Instance;
            var instance2 = VirtualKeyboardService.Instance;

            // Assert
            instance1.Should().BeSameAs(instance2);
        }

        [TestMethod]
        public void Instance_IsThread()
        {
            // Arrange & Act
            var instance1 = VirtualKeyboardService.Instance;
            
            var task = System.Threading.Tasks.Task.Run(() =>
            {
                return VirtualKeyboardService.Instance;
            });
            
            var instance2 = task.Result;

            // Assert - Should return same instance across threads
            instance1.Should().BeSameAs(instance2);
        }

        #endregion

        #region Basic Functionality Tests

        [TestMethod]
        public void HideKeyboard_WithoutShowingKeyboard_DoesNotThrow()
        {
            // Arrange
            var service = VirtualKeyboardService.Instance;

            // Act & Assert
            var act = () => service.HideKeyboard();
            act.Should().NotThrow();
        }

        [TestMethod]
        public void HideKeyboard_MultipleCallsToHide_DoesNotThrow()
        {
            // Arrange
            var service = VirtualKeyboardService.Instance;

            // Act & Assert
            var act = () =>
            {
                service.HideKeyboard();
                service.HideKeyboard();
                service.HideKeyboard();
            };
            act.Should().NotThrow();
        }

        [TestMethod]
        public void ShowKeyboard_WithNullInputControl_DoesNotThrow()
        {
            // Arrange
            var service = VirtualKeyboardService.Instance;

            // Act & Assert
            var act = () => service.ShowKeyboard(null!, null!);
            act.Should().NotThrow();
        }

        #endregion

        #region Enum Tests

        [TestMethod]
        public void VirtualKeyboardMode_HasExpectedValues()
        {
            // Assert - Verify enum has expected values
            var values = Enum.GetValues<VirtualKeyboardMode>();
            values.Should().Contain(VirtualKeyboardMode.Text);
            values.Should().Contain(VirtualKeyboardMode.Numeric);
            values.Should().Contain(VirtualKeyboardMode.Password);
        }

        [TestMethod]
        public void VirtualKeyboardSpecialKey_HasExpectedValues()
        {
            // Assert - Verify enum has expected values
            var values = Enum.GetValues<VirtualKeyboardSpecialKey>();
            values.Should().Contain(VirtualKeyboardSpecialKey.Backspace);
            values.Should().Contain(VirtualKeyboardSpecialKey.Enter);
            values.Should().Contain(VirtualKeyboardSpecialKey.Space);
            values.Should().Contain(VirtualKeyboardSpecialKey.Tab);
            values.Should().Contain(VirtualKeyboardSpecialKey.Shift);
            values.Should().Contain(VirtualKeyboardSpecialKey.CapsLock);
        }

        #endregion

        #region Error Handling Tests

        [TestMethod]
        public void Service_ExceptionHandling_DoesNotCrashApplication()
        {
            // Arrange
            var service = VirtualKeyboardService.Instance;

            // Act & Assert - Various null/invalid calls should not crash
            var act = () =>
            {
                service.ShowKeyboard(null!, null!);
                service.HideKeyboard();
                service.ShowKeyboard(null!, null!);
                service.HideKeyboard();
            };
            act.Should().NotThrow();
        }

        #endregion

        #region Reflection Tests for Private Methods

        [TestMethod]
        public void Service_HasExpectedPrivateMethods()
        {
            // Arrange
            var serviceType = typeof(VirtualKeyboardService);

            // Act & Assert - Verify key private methods exist
            serviceType.GetMethod("HandleKeyPressed", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Should().NotBeNull("HandleKeyPressed method should exist");

            serviceType.GetMethod("HandleBackspace", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Should().NotBeNull("HandleBackspace method should exist");

            serviceType.GetMethod("HandleSpecialKeyPressed", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Should().NotBeNull("HandleSpecialKeyPressed method should exist");

            serviceType.GetMethod("EnsureInputFocus", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Should().NotBeNull("EnsureInputFocus method should exist");
        }

        [TestMethod]
        public void Service_HandleKeyPressed_WithNullActiveInput_DoesNotThrow()
        {
            // Arrange
            var service = VirtualKeyboardService.Instance;
            var method = typeof(VirtualKeyboardService).GetMethod("HandleKeyPressed", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act & Assert
            if (method != null)
            {
                var act = () => method.Invoke(service, new object[] { "A" });
                act.Should().NotThrow();
            }
            else
            {
                Assert.Inconclusive("HandleKeyPressed method not found");
            }
        }

        [TestMethod]
        public void Service_HandleBackspace_WithNullActiveInput_DoesNotThrow()
        {
            // Arrange
            var service = VirtualKeyboardService.Instance;
            var method = typeof(VirtualKeyboardService).GetMethod("HandleBackspace", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act & Assert
            if (method != null)
            {
                var act = () => method.Invoke(service, new object[0]);
                act.Should().NotThrow();
            }
            else
            {
                Assert.Inconclusive("HandleBackspace method not found");
            }
        }

        [TestMethod]
        public void Service_EnsureInputFocus_WithNullActiveInput_DoesNotThrow()
        {
            // Arrange
            var service = VirtualKeyboardService.Instance;
            var method = typeof(VirtualKeyboardService).GetMethod("EnsureInputFocus", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act & Assert
            if (method != null)
            {
                var act = () => method.Invoke(service, new object[0]);
                act.Should().NotThrow();
            }
            else
            {
                Assert.Inconclusive("EnsureInputFocus method not found");
            }
        }

        #endregion

        #region Performance Tests

        [TestMethod]
        public void Service_MultipleRapidCalls_CompletesInReasonableTime()
        {
            // Arrange
            var service = VirtualKeyboardService.Instance;
            var startTime = DateTime.Now;

            // Act - Make rapid calls to service methods
            for (int i = 0; i < 100; i++)
            {
                service.ShowKeyboard(null!, null!);
                service.HideKeyboard();
            }

            var endTime = DateTime.Now;
            var duration = endTime - startTime;

            // Assert - Should complete within reasonable time (less than 2 seconds)
            duration.TotalSeconds.Should().BeLessThan(2);
        }

        #endregion
    }
} 
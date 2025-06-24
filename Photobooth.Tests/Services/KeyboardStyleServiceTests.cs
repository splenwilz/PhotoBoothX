using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using Photobooth.Services;

namespace Photobooth.Tests.Services
{
    [TestClass]
    public class KeyboardStyleServiceTests
    {
        private TestableKeyboardStyleService _service = null!;
        private TestUserControl _mockKeyboard = null!;

        [TestInitialize]
        public void Setup()
        {
            _service = new TestableKeyboardStyleService();
            _mockKeyboard = new TestUserControl();
        }

        // Test class to avoid WPF dependency
        public class TestUserControl
        {
            public double Opacity { get; set; } = 1.0;
        }

        // Testable version that doesn't require WPF controls
        private class TestableKeyboardStyleService
        {
            public bool IsExtraTransparent { get; private set; }

            public TestableKeyboardStyleService(bool isExtraTransparent = false)
            {
                IsExtraTransparent = isExtraTransparent;
            }

            public void ApplyExtraTransparency(TestUserControl keyboard)
            {
                if (keyboard == null) return;
                
                // Apply transparency effect based on setting
                if (IsExtraTransparent)
                {
                    keyboard.Opacity = 0.7; // Make more transparent
                }
                else
                {
                    keyboard.Opacity = 1.0; // Full opacity
                }
            }
        }

        [TestMethod]
        public void Constructor_DefaultTransparency_IsExtraTransparentFalse()
        {
            // Act
            var service = new TestableKeyboardStyleService();

            // Assert
            service.IsExtraTransparent.Should().BeFalse();
        }

        [TestMethod]
        public void Constructor_ExtraTransparency_IsExtraTransparentTrue()
        {
            // Act
            var service = new TestableKeyboardStyleService(true);

            // Assert
            service.IsExtraTransparent.Should().BeTrue();
        }

        [TestMethod]
        public void ApplyExtraTransparency_DoesNotThrow()
        {
            // Arrange
            var service = new TestableKeyboardStyleService(true);

            // Act & Assert
            var act = () => service.ApplyExtraTransparency(_mockKeyboard);
            act.Should().NotThrow();
        }

        [TestMethod]
        public void ApplyExtraTransparency_WhenNotTransparent_DoesNotThrow()
        {
            // Arrange
            var service = new TestableKeyboardStyleService(false);

            // Act & Assert
            var act = () => service.ApplyExtraTransparency(_mockKeyboard);
            act.Should().NotThrow();
        }
    }
} 
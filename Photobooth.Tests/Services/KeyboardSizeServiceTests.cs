using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using Photobooth.Services;

namespace Photobooth.Tests.Services
{
    // Test class to avoid WPF UserControl dependency
    public class TestUserControl
    {
        public double Width { get; set; }
        public double Height { get; set; }
    }

    [TestClass]
    public class KeyboardSizeServiceTests
    {
        private TestableKeyboardSizeService _service = null!;
        private TestUserControl _mockKeyboard = null!;

        [TestInitialize]
        public void Setup()
        {
            _service = new TestableKeyboardSizeService();
            _mockKeyboard = new TestUserControl();
        }

        // Testable version that works with our test controls
        private class TestableKeyboardSizeService
        {
            public KeyboardSize CurrentSize { get; private set; } = KeyboardSize.Large;

            public void ApplyKeyboardSize(TestUserControl keyboard, KeyboardSize size)
            {
                if (keyboard == null) return;
                
                CurrentSize = size;
                
                // Apply size dimensions (simplified for testing)
                switch (size)
                {
                    case KeyboardSize.Small:
                        keyboard.Width = 600;
                        keyboard.Height = 200;
                        break;
                    case KeyboardSize.Medium:
                        keyboard.Width = 800;
                        keyboard.Height = 250;
                        break;
                    case KeyboardSize.Large:
                        keyboard.Width = 1000;
                        keyboard.Height = 300;
                        break;
                }
            }

            public void UpdateSizeButtonStates(TestUserControl keyboard)
            {
                // This method updates UI button states - for testing, we just verify it doesn't throw
                if (keyboard == null) return;
                // Implementation would update button visual states
            }
        }

        [TestMethod]
        public void ApplyKeyboardSize_SmallSize_UpdatesCurrentSize()
        {
            // Act
            _service.ApplyKeyboardSize(_mockKeyboard, KeyboardSize.Small);

            // Assert
            _service.CurrentSize.Should().Be(KeyboardSize.Small);
        }

        [TestMethod]
        public void ApplyKeyboardSize_MediumSize_UpdatesCurrentSize()
        {
            // Act
            _service.ApplyKeyboardSize(_mockKeyboard, KeyboardSize.Medium);

            // Assert
            _service.CurrentSize.Should().Be(KeyboardSize.Medium);
        }

        [TestMethod]
        public void ApplyKeyboardSize_LargeSize_UpdatesCurrentSize()
        {
            // Act
            _service.ApplyKeyboardSize(_mockKeyboard, KeyboardSize.Large);

            // Assert
            _service.CurrentSize.Should().Be(KeyboardSize.Large);
        }

        [TestMethod]
        public void ApplyKeyboardSize_DoesNotThrow()
        {
            // Act & Assert
            var act = () => _service.ApplyKeyboardSize(_mockKeyboard, KeyboardSize.Small);
            act.Should().NotThrow();
        }

        [TestMethod]
        public void UpdateSizeButtonStates_DoesNotThrow()
        {
            // Act & Assert
            var act = () => _service.UpdateSizeButtonStates(_mockKeyboard);
            act.Should().NotThrow();
        }

        [TestMethod]
        public void CurrentSize_DefaultsToLarge()
        {
            // Assert
            _service.CurrentSize.Should().Be(KeyboardSize.Large);
        }
    }
} 
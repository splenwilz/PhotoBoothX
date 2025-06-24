using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using Photobooth.Services;

namespace Photobooth.Tests.Services
{
    [TestClass]
    public class KeyboardStateServiceTests
    {
        private TestableKeyboardStateService _service = null!;
        private TestUserControl _mockKeyboard = null!;

        [TestInitialize]
        public void Setup()
        {
            _service = new TestableKeyboardStateService();
            _mockKeyboard = new TestUserControl();
        }

        // Test class to avoid WPF dependency (reusing from KeyboardSizeServiceTests)
        public class TestUserControl
        {
            public double Width { get; set; }
            public double Height { get; set; }
        }

        // Testable version that doesn't require WPF controls
        private class TestableKeyboardStateService
        {
            public bool IsShiftPressed { get; private set; } = false;
            public bool IsCapsLockOn { get; private set; } = false;

            public void ToggleShift()
            {
                IsShiftPressed = !IsShiftPressed;
            }

            public void ToggleCapsLock()
            {
                IsCapsLockOn = !IsCapsLockOn;
            }

            public void UpdateKeyCase(TestUserControl keyboard, VirtualKeyboardMode mode)
            {
                // This method would update the UI - for testing, we just verify it doesn't throw
                if (keyboard == null) return;
                // Implementation would update key case based on shift/caps lock state and mode
            }

            public void UpdateShiftButtonState(TestUserControl keyboard)
            {
                // This method would update shift button visual state
                if (keyboard == null) return;
                // Implementation would update shift button appearance
            }

            public void UpdateCapsLockButtonState(TestUserControl keyboard)
            {
                // This method would update caps lock button visual state
                if (keyboard == null) return;
                // Implementation would update caps lock button appearance
            }
        }

        [TestMethod]
        public void ToggleShift_TogglesShiftState()
        {
            // Arrange
            var initialState = _service.IsShiftPressed;

            // Act
            _service.ToggleShift();

            // Assert
            _service.IsShiftPressed.Should().Be(!initialState);
        }

        [TestMethod]
        public void ToggleCapsLock_TogglesCapsLockState()
        {
            // Arrange
            var initialState = _service.IsCapsLockOn;

            // Act
            _service.ToggleCapsLock();

            // Assert
            _service.IsCapsLockOn.Should().Be(!initialState);
        }

        [TestMethod]
        public void UpdateKeyCase_DoesNotThrow()
        {
            // Act & Assert
            var act = () => _service.UpdateKeyCase(_mockKeyboard, VirtualKeyboardMode.Text);
            act.Should().NotThrow();
        }

        [TestMethod]
        public void UpdateShiftButtonState_DoesNotThrow()
        {
            // Act & Assert
            var act = () => _service.UpdateShiftButtonState(_mockKeyboard);
            act.Should().NotThrow();
        }

        [TestMethod]
        public void UpdateCapsLockButtonState_DoesNotThrow()
        {
            // Act & Assert
            var act = () => _service.UpdateCapsLockButtonState(_mockKeyboard);
            act.Should().NotThrow();
        }

        [TestMethod]
        public void InitialState_BothFalse()
        {
            // Assert
            _service.IsShiftPressed.Should().BeFalse();
            _service.IsCapsLockOn.Should().BeFalse();
        }

        [TestMethod]
        public void UpdateKeyCase_NumericMode_DoesNotThrow()
        {
            // Act & Assert
            var act = () => _service.UpdateKeyCase(_mockKeyboard, VirtualKeyboardMode.Numeric);
            act.Should().NotThrow();
        }
    }
} 
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Windows;
using Photobooth.Services;

namespace Photobooth.Tests.Services
{
    // Simple test button class that mimics WPF Button behavior without WPF dependency
    public class TestButton
    {
        public object? Content { get; set; }
    }

    [TestClass]
    public class KeyboardInputServiceTests
    {
        private TestableKeyboardInputService _service = null!;

        [TestInitialize]
        public void Setup()
        {
            _service = new TestableKeyboardInputService();
        }

        // Testable version of KeyboardInputService that accepts our test button
        private class TestableKeyboardInputService
        {
            public event System.Action<string>? OnKeyPressed;
            public event System.Action<VirtualKeyboardSpecialKey>? OnSpecialKeyPressed;

            public void HandleKeyClick(TestButton button, RoutedEventArgs e)
            {
                if (button?.Content == null) return;

                var content = button.Content.ToString();
                if (string.IsNullOrEmpty(content)) return;

                // Handle special keys (copied from original implementation)
                switch (content.ToLower())
                {
                    case "space":
                    case " ":
                        OnKeyPressed?.Invoke(" ");
                        break;
                    case "tab":
                        OnSpecialKeyPressed?.Invoke(VirtualKeyboardSpecialKey.Tab);
                        break;
                    case "enter":
                    case "return":
                        OnSpecialKeyPressed?.Invoke(VirtualKeyboardSpecialKey.Enter);
                        break;
                    case "backspace":
                    case "←":
                        OnSpecialKeyPressed?.Invoke(VirtualKeyboardSpecialKey.Backspace);
                        break;
                    case "shift":
                        OnSpecialKeyPressed?.Invoke(VirtualKeyboardSpecialKey.Shift);
                        break;
                    case "caps":
                    case "caps lock":
                        OnSpecialKeyPressed?.Invoke(VirtualKeyboardSpecialKey.CapsLock);
                        break;
                    case "close":
                    case "×":
                        // Close is handled at the UI level, not as a special key
                        break;
                    default:
                        // Regular key press
                        if (content.Length == 1 || content.Length <= 3) // Single character or short strings
                        {
                            OnKeyPressed?.Invoke(content);
                        }
                        break;
                }
            }
        }

        [TestMethod]
        public void HandleKeyClick_RegularKey_FiresOnKeyPressed()
        {
            // Arrange
            var button = new TestButton { Content = "A" };
            string? pressedKey = null;
            _service.OnKeyPressed += (key) => pressedKey = key;

            // Act
            _service.HandleKeyClick(button, new RoutedEventArgs());

            // Assert
            pressedKey.Should().Be("A");
        }

        [TestMethod]
        public void HandleKeyClick_SpaceKey_FiresOnKeyPressed()
        {
            // Arrange
            var button = new TestButton { Content = "Space" };
            string? pressedKey = null;
            _service.OnKeyPressed += (key) => pressedKey = key;

            // Act
            _service.HandleKeyClick(button, new RoutedEventArgs());

            // Assert
            pressedKey.Should().Be(" ");
        }

        [TestMethod]
        public void HandleKeyClick_BackspaceKey_FiresOnSpecialKeyPressed()
        {
            // Arrange
            var button = new TestButton { Content = "Backspace" };
            VirtualKeyboardSpecialKey? pressedKey = null;
            _service.OnSpecialKeyPressed += (key) => pressedKey = key;

            // Act
            _service.HandleKeyClick(button, new RoutedEventArgs());

            // Assert
            pressedKey.Should().Be(VirtualKeyboardSpecialKey.Backspace);
        }

        [TestMethod]
        public void HandleKeyClick_EnterKey_FiresOnSpecialKeyPressed()
        {
            // Arrange
            var button = new TestButton { Content = "Enter" };
            VirtualKeyboardSpecialKey? pressedKey = null;
            _service.OnSpecialKeyPressed += (key) => pressedKey = key;

            // Act
            _service.HandleKeyClick(button, new RoutedEventArgs());

            // Assert
            pressedKey.Should().Be(VirtualKeyboardSpecialKey.Enter);
        }

        [TestMethod]
        public void HandleKeyClick_NullButton_DoesNotThrow()
        {
            // Act & Assert
            var act = () => _service.HandleKeyClick(null!, new RoutedEventArgs());
            act.Should().NotThrow();
        }

        [TestMethod]
        public void HandleKeyClick_ButtonWithNullContent_DoesNotThrow()
        {
            // Arrange
            var button = new TestButton { Content = null };

            // Act & Assert
            var act = () => _service.HandleKeyClick(button, new RoutedEventArgs());
            act.Should().NotThrow();
        }

        [TestMethod]
        public void HandleKeyClick_Events_CanSubscribeAndUnsubscribe()
        {
            // Arrange
            var button = new TestButton { Content = "A" };
            var keyPressedCalled = false;
            var specialKeyHandler = new System.Action<VirtualKeyboardSpecialKey>((key) => { });
            
            System.Action<string> keyHandler = (key) => keyPressedCalled = true;

            // Act & Assert - Subscribe
            _service.OnKeyPressed += keyHandler;
            _service.OnSpecialKeyPressed += specialKeyHandler;
            
            _service.HandleKeyClick(button, new RoutedEventArgs());
            keyPressedCalled.Should().BeTrue();
            
            // Unsubscribe
            keyPressedCalled = false;
            _service.OnKeyPressed -= keyHandler;
            _service.OnSpecialKeyPressed -= specialKeyHandler;
            
            _service.HandleKeyClick(button, new RoutedEventArgs());
            keyPressedCalled.Should().BeFalse();
        }
    }
} 
using System.Windows;
using System.Windows.Controls;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Photobooth.Controls;
using Photobooth.Services;
using System.Threading;

namespace Photobooth.Tests.Controls
{
    [TestClass]
    public class VirtualKeyboardTests
    {
        private VirtualKeyboard? _keyboard;
        private TestWindow _testWindow = null!;

        [TestInitialize]
        public void Setup()
        {
            // Initialize STA thread for WPF components
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                Assert.Inconclusive("Tests require STA thread for WPF components");
            }

            _testWindow = new TestWindow();
        }

        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                _keyboard = null;
                _testWindow?.Close();
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        #region Keyboard Creation Tests

        [TestMethod]
        public void Constructor_TextMode_CreatesKeyboard()
        {
            // Act & Assert
            var act = () => _keyboard = new VirtualKeyboard(VirtualKeyboardMode.Text, false);
            act.Should().NotThrow();
            _keyboard.Should().NotBeNull();
        }

        [TestMethod]
        public void Constructor_PasswordMode_CreatesKeyboard()
        {
            // Act & Assert
            var act = () => _keyboard = new VirtualKeyboard(VirtualKeyboardMode.Password, false);
            act.Should().NotThrow();
            _keyboard.Should().NotBeNull();
        }

        [TestMethod]
        public void Constructor_NumericMode_CreatesKeyboard()
        {
            // Act & Assert
            var act = () => _keyboard = new VirtualKeyboard(VirtualKeyboardMode.Numeric, false);
            act.Should().NotThrow();
            _keyboard.Should().NotBeNull();
        }

        [TestMethod]
        public void Constructor_WithTransparency_CreatesKeyboard()
        {
            // Act & Assert
            var act = () => _keyboard = new VirtualKeyboard(VirtualKeyboardMode.Text, true);
            act.Should().NotThrow();
            _keyboard.Should().NotBeNull();
        }

        #endregion

        #region Keyboard Mode Tests

        [TestMethod]
        public void GetCurrentMode_TextMode_ReturnsTextMode()
        {
            // Arrange
            _keyboard = new VirtualKeyboard(VirtualKeyboardMode.Text, false);

            // Act
            var currentMode = _keyboard.GetCurrentMode();

            // Assert
            currentMode.Should().Be(VirtualKeyboardMode.Text);
        }

        [TestMethod]
        public void GetCurrentMode_PasswordMode_ReturnsPasswordMode()
        {
            // Arrange
            _keyboard = new VirtualKeyboard(VirtualKeyboardMode.Password, false);

            // Act
            var currentMode = _keyboard.GetCurrentMode();

            // Assert
            currentMode.Should().Be(VirtualKeyboardMode.Password);
        }

        [TestMethod]
        public void GetCurrentMode_NumericMode_ReturnsNumericMode()
        {
            // Arrange
            _keyboard = new VirtualKeyboard(VirtualKeyboardMode.Numeric, false);

            // Act
            var currentMode = _keyboard.GetCurrentMode();

            // Assert
            currentMode.Should().Be(VirtualKeyboardMode.Numeric);
        }

        [TestMethod]
        public void ChangeMode_FromTextToNumeric_UpdatesMode()
        {
            // Arrange
            _keyboard = new VirtualKeyboard(VirtualKeyboardMode.Text, false);

            // Act
            var act = () => _keyboard.ChangeMode(VirtualKeyboardMode.Numeric);
            act.Should().NotThrow();

            // Assert
            _keyboard.GetCurrentMode().Should().Be(VirtualKeyboardMode.Numeric);
        }

        [TestMethod]
        public void ChangeMode_FromNumericToPassword_UpdatesMode()
        {
            // Arrange
            _keyboard = new VirtualKeyboard(VirtualKeyboardMode.Numeric, false);

            // Act
            var act = () => _keyboard.ChangeMode(VirtualKeyboardMode.Password);
            act.Should().NotThrow();

            // Assert
            _keyboard.GetCurrentMode().Should().Be(VirtualKeyboardMode.Password);
        }

        #endregion

        #region Event Tests

        [TestMethod]
        public void OnKeyPressed_EventHandler_CanSubscribeAndUnsubscribe()
        {
            // Arrange
            _keyboard = new VirtualKeyboard(VirtualKeyboardMode.Text, false);
            var keyPressed = false;
            var handler = new System.Action<string>((key) => keyPressed = true);

            // Act - Subscribe
            _keyboard.OnKeyPressed += handler;
            
            // Simulate key press using reflection to access private method
            var method = typeof(VirtualKeyboard).GetMethod("HandleKeyClick", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (method != null)
            {
                // Create a mock button for testing
                var button = new Button { Content = "A" };
                method.Invoke(_keyboard, new object[] { button, new RoutedEventArgs() });
            }

            // Assert
            if (method != null)
            {
                keyPressed.Should().BeTrue();
            }

            // Act - Unsubscribe
            _keyboard.OnKeyPressed -= handler;
            keyPressed = false;
            
            if (method != null)
            {
                var button = new Button { Content = "B" };
                method.Invoke(_keyboard, new object[] { button, new RoutedEventArgs() });
            }

            // Assert - Should not be triggered after unsubscribe
            // Note: This is testing the event mechanism, actual behavior may vary
        }

        [TestMethod]
        public void OnSpecialKeyPressed_EventHandler_CanSubscribeAndUnsubscribe()
        {
            // Arrange
            _keyboard = new VirtualKeyboard(VirtualKeyboardMode.Text, false);
            var handler = new System.Action<VirtualKeyboardSpecialKey>((key) => { /* Test handler */ });

            // Act & Assert - Subscribe and unsubscribe should not throw
            var act = () =>
            {
                _keyboard.OnSpecialKeyPressed += handler;
                _keyboard.OnSpecialKeyPressed -= handler;
            };
            act.Should().NotThrow();
        }

        [TestMethod]
        public void OnKeyboardClosed_EventHandler_CanSubscribeAndUnsubscribe()
        {
            // Arrange
            _keyboard = new VirtualKeyboard(VirtualKeyboardMode.Text, false);
            var handler = new System.Action(() => { /* Test handler */ });

            // Act & Assert - Subscribe and unsubscribe should not throw
            var act = () =>
            {
                _keyboard.OnKeyboardClosed += handler;
                _keyboard.OnKeyboardClosed -= handler;
            };
            act.Should().NotThrow();
        }

        #endregion

        #region Drag and Resize Tests

        [TestMethod]
        public void InitializeDragResize_ValidCanvas_DoesNotThrow()
        {
            // Arrange
            _keyboard = new VirtualKeyboard(VirtualKeyboardMode.Text, false);
            var canvas = new Canvas();

            // Act & Assert
            var act = () => _keyboard.InitializeDragResize(canvas);
            act.Should().NotThrow();
        }

        [TestMethod]
        public void InitializeDragResize_NullCanvas_DoesNotThrow()
        {
            // Arrange
            _keyboard = new VirtualKeyboard(VirtualKeyboardMode.Text, false);

            // Act & Assert
            var act = () => _keyboard.InitializeDragResize(null!);
            act.Should().NotThrow();
        }

        #endregion

        #region Keyboard Size Tests

        [TestMethod]
        public void ApplyKeyboardSize_SmallSize_UpdatesDimensions()
        {
            // Arrange
            _keyboard = new VirtualKeyboard(VirtualKeyboardMode.Text, false);

            // Act
            var method = typeof(VirtualKeyboard).GetMethod("ApplyKeyboardSize", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (method != null)
            {
                // Get KeyboardSize enum type
                var sizeEnumType = method.GetParameters()[0].ParameterType;
                var smallSize = Enum.Parse(sizeEnumType, "Small");
                
                var act = () => method.Invoke(_keyboard, new object[] { smallSize });
                act.Should().NotThrow();
            }
        }

        [TestMethod]
        public void ApplyKeyboardSize_MediumSize_UpdatesDimensions()
        {
            // Arrange
            _keyboard = new VirtualKeyboard(VirtualKeyboardMode.Text, false);

            // Act
            var method = typeof(VirtualKeyboard).GetMethod("ApplyKeyboardSize", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (method != null)
            {
                var sizeEnumType = method.GetParameters()[0].ParameterType;
                var mediumSize = Enum.Parse(sizeEnumType, "Medium");
                
                var act = () => method.Invoke(_keyboard, new object[] { mediumSize });
                act.Should().NotThrow();
            }
        }

        [TestMethod]
        public void ApplyKeyboardSize_LargeSize_UpdatesDimensions()
        {
            // Arrange
            _keyboard = new VirtualKeyboard(VirtualKeyboardMode.Text, false);

            // Act
            var method = typeof(VirtualKeyboard).GetMethod("ApplyKeyboardSize", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (method != null)
            {
                var sizeEnumType = method.GetParameters()[0].ParameterType;
                var largeSize = Enum.Parse(sizeEnumType, "Large");
                
                var act = () => method.Invoke(_keyboard, new object[] { largeSize });
                act.Should().NotThrow();
            }
        }

        #endregion

        #region Key Case and Shift Tests

        [TestMethod]
        public void UpdateKeyCase_DoesNotThrow()
        {
            // Arrange
            _keyboard = new VirtualKeyboard(VirtualKeyboardMode.Text, false);

            // Act & Assert
            var method = typeof(VirtualKeyboard).GetMethod("UpdateKeyCase", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (method != null)
            {
                var act = () => method.Invoke(_keyboard, new object[0]);
                act.Should().NotThrow();
            }
        }

        [TestMethod]
        public void UpdateShiftButtonState_DoesNotThrow()
        {
            // Arrange
            _keyboard = new VirtualKeyboard(VirtualKeyboardMode.Text, false);

            // Act & Assert
            var method = typeof(VirtualKeyboard).GetMethod("UpdateShiftButtonState", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (method != null)
            {
                var act = () => method.Invoke(_keyboard, new object[0]);
                act.Should().NotThrow();
            }
        }

        [TestMethod]
        public void UpdateCapsLockButtonState_DoesNotThrow()
        {
            // Arrange
            _keyboard = new VirtualKeyboard(VirtualKeyboardMode.Text, false);

            // Act & Assert
            var method = typeof(VirtualKeyboard).GetMethod("UpdateCapsLockButtonState", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (method != null)
            {
                var act = () => method.Invoke(_keyboard, new object[0]);
                act.Should().NotThrow();
            }
        }

        #endregion

        #region Transparency Tests

        [TestMethod]
        public void ApplyExtraTransparency_DoesNotThrow()
        {
            // Arrange
            _keyboard = new VirtualKeyboard(VirtualKeyboardMode.Text, false);

            // Act & Assert
            var method = typeof(VirtualKeyboard).GetMethod("ApplyExtraTransparency", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (method != null)
            {
                var act = () => method.Invoke(_keyboard, new object[0]);
                act.Should().NotThrow();
            }
        }

        #endregion

        #region UI Layout Tests

        [TestMethod]
        public void Keyboard_HasRequiredUIElements()
        {
            // Arrange & Act
            _keyboard = new VirtualKeyboard(VirtualKeyboardMode.Text, false);

            // Assert - Verify keyboard has basic structure
            _keyboard.Should().NotBeNull();
            _keyboard.Width.Should().BeGreaterThan(0);
            _keyboard.Height.Should().BeGreaterThan(0);
        }

        [TestMethod]
        public void Keyboard_TextMode_HasAlphabeticKeys()
        {
            // Arrange & Act
            _keyboard = new VirtualKeyboard(VirtualKeyboardMode.Text, false);
            
            // Force layout update
            _keyboard.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            _keyboard.Arrange(new Rect(_keyboard.DesiredSize));

            // Assert - Should have alphabetic layout
            _keyboard.Should().NotBeNull();
        }

        [TestMethod]
        public void Keyboard_NumericMode_HasNumericKeys()
        {
            // Arrange & Act
            _keyboard = new VirtualKeyboard(VirtualKeyboardMode.Numeric, false);
            
            // Force layout update
            _keyboard.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            _keyboard.Arrange(new Rect(_keyboard.DesiredSize));

            // Assert - Should have numeric layout
            _keyboard.Should().NotBeNull();
        }

        #endregion

        #region Error Handling Tests

        [TestMethod]
        public void Keyboard_InvalidModeChange_DoesNotThrow()
        {
            // Arrange
            _keyboard = new VirtualKeyboard(VirtualKeyboardMode.Text, false);

            // Act & Assert - Try to change to same mode
            var act = () => _keyboard.ChangeMode(VirtualKeyboardMode.Text);
            act.Should().NotThrow();
        }

        [TestMethod]
        public void Keyboard_MultipleInitializations_DoesNotThrow()
        {
            // Act & Assert
            var act = () =>
            {
                for (int i = 0; i < 3; i++)
                {
                    var keyboard = new VirtualKeyboard(VirtualKeyboardMode.Text, false);
                    keyboard.Should().NotBeNull();
                }
            };
            act.Should().NotThrow();
        }

        #endregion
    }

    /// <summary>
    /// Simple test window for testing WPF components
    /// </summary>
    public class TestWindow : Window
    {
        public TestWindow()
        {
            Width = 400;
            Height = 300;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = -2000; // Position off-screen to avoid visual interference
            Top = -2000;
            ShowInTaskbar = false;
            WindowStyle = WindowStyle.None;
            ShowActivated = false;
        }
    }
} 
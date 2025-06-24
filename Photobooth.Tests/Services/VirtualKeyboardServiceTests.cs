using System.Windows;
using System.Windows.Controls;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Photobooth.Services;
using System.Threading;
using System.Windows.Threading;

namespace Photobooth.Tests.Services
{
    [TestClass]
    public class VirtualKeyboardServiceTests
    {
        private VirtualKeyboardService _service = null!;
        private TestWindow _testWindow = null!;
        private TextBox _testTextBox = null!;
        private PasswordBox _testPasswordBox = null!;

        [TestInitialize]
        public void Setup()
        {
            // Initialize STA thread for WPF components
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                Assert.Inconclusive("Tests require STA thread for WPF components");
            }

            _service = VirtualKeyboardService.Instance;
            
            // Create test window with controls
            _testWindow = new TestWindow();
            _testTextBox = new TextBox
            {
                Name = "TestTextBox",
                Width = 200,
                Height = 30
            };
            _testPasswordBox = new PasswordBox
            {
                Name = "TestPasswordBox", 
                Width = 200,
                Height = 30
            };

            var panel = new StackPanel();
            panel.Children.Add(_testTextBox);
            panel.Children.Add(_testPasswordBox);
            _testWindow.Content = panel;
        }

        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                _service?.HideKeyboard();
                _testWindow?.Close();
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        #region Basic Service Tests

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
        public void HideKeyboard_WithoutShowingKeyboard_DoesNotThrow()
        {
            // Act & Assert
            var act = () => _service.HideKeyboard();
            act.Should().NotThrow();
        }

        #endregion

        #region Keyboard Display Tests

        [TestMethod]
        public void ShowKeyboard_ValidTextBox_DoesNotThrow()
        {
            // Act & Assert
            var act = () => _service.ShowKeyboard(_testTextBox, _testWindow);
            act.Should().NotThrow();
        }

        [TestMethod]
        public void ShowKeyboard_ValidPasswordBox_DoesNotThrow()
        {
            // Act & Assert
            var act = () => _service.ShowKeyboard(_testPasswordBox, _testWindow);
            act.Should().NotThrow();
        }

        [TestMethod]
        public void ShowKeyboard_NullInput_DoesNotThrow()
        {
            // Act & Assert
            var act = () => _service.ShowKeyboard(null!, _testWindow);
            act.Should().NotThrow();
        }

        [TestMethod]
        public void ShowKeyboard_NullWindow_DoesNotThrow()
        {
            // Act & Assert
            var act = () => _service.ShowKeyboard(_testTextBox, null!);
            act.Should().NotThrow();
        }

        [TestMethod]
        public void ShowKeyboard_SwitchBetweenInputs_DoesNotThrow()
        {
            // Arrange
            _service.ShowKeyboard(_testTextBox, _testWindow);

            // Act & Assert
            var act = () => _service.ShowKeyboard(_testPasswordBox, _testWindow);
            act.Should().NotThrow();
        }

        [TestMethod]
        public void ShowKeyboard_SameInputTwice_DoesNotThrow()
        {
            // Act & Assert
            var act = () =>
            {
                _service.ShowKeyboard(_testTextBox, _testWindow);
                _service.ShowKeyboard(_testTextBox, _testWindow);
            };
            act.Should().NotThrow();
        }

        #endregion

        #region Input Type Detection Tests

        [TestMethod]
        public void ShowKeyboard_NumericTextBox_DoesNotThrow()
        {
            // Arrange
            var numericTextBox = new TextBox
            {
                Name = "PriceTextBox" // Name pattern that should trigger numeric mode
            };

            // Act & Assert
            var act = () => _service.ShowKeyboard(numericTextBox, _testWindow);
            act.Should().NotThrow();
        }

        [TestMethod]
        public void ShowKeyboard_SortOrderTextBox_DoesNotThrow()
        {
            // Arrange
            var sortOrderTextBox = new TextBox
            {
                Name = "SortOrderTextBox" // Another numeric pattern
            };

            // Act & Assert
            var act = () => _service.ShowKeyboard(sortOrderTextBox, _testWindow);
            act.Should().NotThrow();
        }

        #endregion

        #region Login Screen Detection Tests

        [TestMethod]
        public void ShowKeyboard_LoginControls_DoesNotThrow()
        {
            // Arrange
            var loginTextBox = new TextBox { Name = "UsernameInput" };
            var loginPasswordBox = new PasswordBox { Name = "PasswordInput" };
            var loginButton = new Button { Name = "LoginButton" };
            
            var loginPanel = new StackPanel();
            loginPanel.Children.Add(loginTextBox);
            loginPanel.Children.Add(loginPasswordBox);
            loginPanel.Children.Add(loginButton);
            
            var loginWindow = new TestWindow { Content = loginPanel };

            // Act & Assert
            var act = () => _service.ShowKeyboard(loginTextBox, loginWindow);
            act.Should().NotThrow();
            
            // Cleanup
            loginWindow?.Close();
        }

        #endregion

        #region Text Input Simulation Tests

        [TestMethod]
        public void HandleKeyPressed_TextBox_UpdatesText()
        {
            // Arrange
            _testTextBox.Text = "Hello";
            _testTextBox.CaretIndex = 5;
            _service.ShowKeyboard(_testTextBox, _testWindow);

            // Act - Use reflection to test private method
            var method = typeof(VirtualKeyboardService).GetMethod("HandleKeyPressed", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (method != null)
            {
                method.Invoke(_service, new object[] { "!" });
                
                // Wait for any dispatcher operations
                Application.Current?.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                
                // Assert
                _testTextBox.Text.Should().Be("Hello!");
            }
            else
            {
                Assert.Inconclusive("HandleKeyPressed method not found via reflection");
            }
        }

        [TestMethod]
        public void HandleKeyPressed_PasswordBox_UpdatesPassword()
        {
            // Arrange
            _testPasswordBox.Password = "pass";
            _service.ShowKeyboard(_testPasswordBox, _testWindow);

            // Act
            var method = typeof(VirtualKeyboardService).GetMethod("HandleKeyPressed", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (method != null)
            {
                method.Invoke(_service, new object[] { "1" });
                
                // Wait for any dispatcher operations
                Application.Current?.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                
                // Assert
                _testPasswordBox.Password.Should().Be("pass1");
            }
            else
            {
                Assert.Inconclusive("HandleKeyPressed method not found via reflection");
            }
        }

        [TestMethod]
        public void HandleBackspace_TextBox_RemovesCharacter()
        {
            // Arrange
            _testTextBox.Text = "Hello";
            _testTextBox.CaretIndex = 5;
            _service.ShowKeyboard(_testTextBox, _testWindow);

            // Act
            var method = typeof(VirtualKeyboardService).GetMethod("HandleBackspace", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (method != null)
            {
                method.Invoke(_service, new object[0]);
                
                // Wait for any dispatcher operations
                Application.Current?.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                
                // Assert
                _testTextBox.Text.Should().Be("Hell");
            }
            else
            {
                Assert.Inconclusive("HandleBackspace method not found via reflection");
            }
        }

        [TestMethod]
        public void HandleBackspace_PasswordBox_RemovesCharacter()
        {
            // Arrange
            _testPasswordBox.Password = "password";
            _service.ShowKeyboard(_testPasswordBox, _testWindow);

            // Act
            var method = typeof(VirtualKeyboardService).GetMethod("HandleBackspace", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (method != null)
            {
                method.Invoke(_service, new object[0]);
                
                // Wait for any dispatcher operations
                Application.Current?.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                
                // Assert
                _testPasswordBox.Password.Should().Be("passwor");
            }
            else
            {
                Assert.Inconclusive("HandleBackspace method not found via reflection");
            }
        }

        [TestMethod]
        public void HandleBackspace_EmptyText_DoesNotThrow()
        {
            // Arrange
            _testTextBox.Text = "";
            _testTextBox.CaretIndex = 0;
            _service.ShowKeyboard(_testTextBox, _testWindow);

            // Act & Assert
            var method = typeof(VirtualKeyboardService).GetMethod("HandleBackspace", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (method != null)
            {
                var act = () => method.Invoke(_service, new object[0]);
                act.Should().NotThrow();
                
                // Text should remain empty
                _testTextBox.Text.Should().Be("");
            }
        }

        #endregion

        #region Special Key Tests

        [TestMethod]
        public void HandleSpecialKeyPressed_Space_DoesNotThrow()
        {
            // Arrange
            _service.ShowKeyboard(_testTextBox, _testWindow);

            // Act & Assert
            var method = typeof(VirtualKeyboardService).GetMethod("HandleSpecialKeyPressed", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (method != null)
            {
                var specialKeyType = typeof(VirtualKeyboardSpecialKey);
                if (specialKeyType != null)
                {
                    var spaceKey = Enum.Parse(specialKeyType, "Space");
                    var act = () => method.Invoke(_service, new object[] { spaceKey });
                    act.Should().NotThrow();
                }
            }
        }

        [TestMethod]
        public void HandleSpecialKeyPressed_Tab_DoesNotThrow()
        {
            // Arrange
            _service.ShowKeyboard(_testTextBox, _testWindow);

            // Act & Assert
            var method = typeof(VirtualKeyboardService).GetMethod("HandleSpecialKeyPressed", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (method != null)
            {
                var specialKeyType = typeof(VirtualKeyboardSpecialKey);
                if (specialKeyType != null)
                {
                    var tabKey = Enum.Parse(specialKeyType, "Tab");
                    var act = () => method.Invoke(_service, new object[] { tabKey });
                    act.Should().NotThrow();
                }
            }
        }

        [TestMethod]
        public void HandleSpecialKeyPressed_Enter_DoesNotThrow()
        {
            // Arrange
            _service.ShowKeyboard(_testTextBox, _testWindow);

            // Act & Assert
            var method = typeof(VirtualKeyboardService).GetMethod("HandleSpecialKeyPressed", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (method != null)
            {
                var specialKeyType = typeof(VirtualKeyboardSpecialKey);
                if (specialKeyType != null)
                {
                    var enterKey = Enum.Parse(specialKeyType, "Enter");
                    var act = () => method.Invoke(_service, new object[] { enterKey });
                    act.Should().NotThrow();
                }
            }
        }

        #endregion

        #region Error Handling Tests

        [TestMethod]
        public void HandleKeyPressed_WithNullActiveInput_DoesNotThrow()
        {
            // Act & Assert
            var method = typeof(VirtualKeyboardService).GetMethod("HandleKeyPressed", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (method != null)
            {
                var act = () => method.Invoke(_service, new object[] { "A" });
                act.Should().NotThrow();
            }
        }

        [TestMethod]
        public void ShowKeyboard_MultipleRapidCalls_DoesNotThrow()
        {
            // Act & Assert
            var act = () =>
            {
                for (int i = 0; i < 5; i++)
                {
                    _service.ShowKeyboard(_testTextBox, _testWindow);
                    _service.ShowKeyboard(_testPasswordBox, _testWindow);
                }
            };
            act.Should().NotThrow();
        }

        [TestMethod]
        public void ShowKeyboard_WindowWithBorderContent_DoesNotThrow()
        {
            // Arrange
            var borderWindow = new TestWindow 
            { 
                Content = new Border 
                { 
                    Child = new StackPanel() 
                } 
            };

            // Act & Assert
            var act = () => _service.ShowKeyboard(_testTextBox, borderWindow);
            act.Should().NotThrow();

            // Cleanup
            borderWindow?.Close();
        }

        #endregion

        #region Focus Management Tests

        [TestMethod]
        public void EnsureInputFocus_DoesNotThrow()
        {
            // Arrange
            _service.ShowKeyboard(_testTextBox, _testWindow);

            // Act & Assert
            var method = typeof(VirtualKeyboardService).GetMethod("EnsureInputFocus", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (method != null)
            {
                var act = () => method.Invoke(_service, new object[0]);
                act.Should().NotThrow();
            }
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
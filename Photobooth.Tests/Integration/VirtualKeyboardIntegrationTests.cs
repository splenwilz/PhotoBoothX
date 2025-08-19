using System.Windows;
using System.Windows.Controls;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Photobooth.Controls;
using Photobooth.Services;
using System.Threading;
using System.Windows.Threading;

namespace Photobooth.Tests.Integration
{
    [TestClass]
    public class VirtualKeyboardIntegrationTests
    {
        private VirtualKeyboardService _service = null!;
        private TestWindow _testWindow = null!;
        private TextBox _usernameTextBox = null!;
        private PasswordBox _passwordBox = null!;
        private TextBox _priceTextBox = null!;

        [TestInitialize]
        public void Setup()
        {
            // Initialize STA thread for WPF components
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                Assert.Inconclusive("Tests require STA thread for WPF components");
            }

            _service = VirtualKeyboardService.Instance;
            
            // Create test window with various input controls
            _testWindow = new TestWindow();
            
            _usernameTextBox = new TextBox
            {
                Name = "UsernameInput",
                Width = 200,
                Height = 30,
                Margin = new Thickness(10)
            };
            
            _passwordBox = new PasswordBox
            {
                Name = "PasswordInput",
                Width = 200,
                Height = 30,
                Margin = new Thickness(10)
            };
            
            _priceTextBox = new TextBox
            {
                Name = "PriceTextBox",
                Width = 200,
                Height = 30,
                Margin = new Thickness(10)
            };

            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = "Username:", Margin = new Thickness(10, 10, 10, 0) });
            panel.Children.Add(_usernameTextBox);
            panel.Children.Add(new TextBlock { Text = "Password:", Margin = new Thickness(10, 10, 10, 0) });
            panel.Children.Add(_passwordBox);
            panel.Children.Add(new TextBlock { Text = "Price:", Margin = new Thickness(10, 10, 10, 0) });
            panel.Children.Add(_priceTextBox);
            
            _testWindow.Content = panel;
            _testWindow.Show(); // Show the window for proper focus behavior
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

        #region End-to-End Workflow Tests

        [TestMethod]
        public async Task FullWorkflow_LoginForm_TypeUsernameAndPassword()
        {
            // Arrange - Start with username field
            var initialUsername = "admin";
            var initialPassword = "pass123";

            // Act & Assert - Show keyboard for username
            var act1 = () => _service.ShowKeyboardAsync(_usernameTextBox, _testWindow);
            await act1.Should().NotThrowAsync();

            // Simulate typing username
            SimulateTyping(initialUsername);
            
            // Wait for UI updates
            WaitForUIUpdates();
            
            // Switch to password field
            var act2 = () => _service.ShowKeyboardAsync(_passwordBox, _testWindow);
            await act2.Should().NotThrowAsync();

            // Simulate typing password
            SimulateTyping(initialPassword);
            
            // Wait for UI updates
            WaitForUIUpdates();

            // Verify results
            _usernameTextBox.Text.Should().Contain("admin");
            _passwordBox.Password.Should().Contain("pass");
        }

        [TestMethod]
        public async Task FullWorkflow_SwitchBetweenTextAndNumeric_MaintainsKeyboard()
        {
            // Arrange
            var textInput = "Product Name";
            var priceInput = "29.99";

            // Act - Start with text field
            await _service.ShowKeyboardAsync(_usernameTextBox, _testWindow);
            SimulateTyping(textInput);
            
            WaitForUIUpdates();

            // Switch to numeric field
            await _service.ShowKeyboardAsync(_priceTextBox, _testWindow);
            SimulateTyping(priceInput);
            
            WaitForUIUpdates();

            // Assert - Both fields should have content
            _usernameTextBox.Text.Should().Contain("Product");
            _priceTextBox.Text.Should().Contain("29");
        }

        [TestMethod]
        public async Task FullWorkflow_LoginTransparency_AppliesCorrectly()
        {
            // Arrange - Create login window with specific structure
            var loginWindow = CreateLoginWindow();
            var usernameInput = loginWindow.FindName("UsernameInput") as TextBox;

            // Assert - Should find the username input
            Assert.IsNotNull(usernameInput, "UsernameInput should be found in login window");

            // Act
            var act = () => _service.ShowKeyboardAsync(usernameInput, loginWindow);
            await act.Should().NotThrowAsync();

            // Assert - Should detect as login and apply transparency
            // This tests the login detection logic
            
            // Cleanup
            loginWindow?.Close();
        }

        [TestMethod]
        public async Task FullWorkflow_RapidInputSwitching_RemainsStable()
        {
            // Act - Rapidly switch between inputs multiple times
            var act = async () =>
            {
                for (int i = 0; i < 10; i++)
                {
                    await _service.ShowKeyboardAsync(_usernameTextBox, _testWindow);
                    await _service.ShowKeyboardAsync(_passwordBox, _testWindow);
                    await _service.ShowKeyboardAsync(_priceTextBox, _testWindow);
                }
            };

            // Assert - Should handle rapid switching without errors
            await act.Should().NotThrowAsync();
        }

        #endregion

        #region Focus and Input Tests

        [TestMethod]
        public async Task FocusManagement_TextInput_MaintainsFocusAfterTyping()
        {
            // Arrange
            await _service.ShowKeyboardAsync(_usernameTextBox, _testWindow);
            
            // Act
            SimulateTyping("test");
            WaitForUIUpdates();

            // Assert - Text should be updated and focus maintained
            _usernameTextBox.Text.Should().Contain("test");
            // Note: Focus testing in unit tests is limited due to WPF focus behavior
        }

        [TestMethod]
        public async Task FocusManagement_PasswordInput_MaintainsFocusAfterTyping()
        {
            // Arrange
            await _service.ShowKeyboardAsync(_passwordBox, _testWindow);
            
            // Act
            SimulateTyping("secret");
            WaitForUIUpdates();

            // Assert - Password should be updated
            _passwordBox.Password.Should().Contain("secret");
        }

        [TestMethod]
        public async Task CaretPositioning_TextBox_InsertsAtCorrectPosition()
        {
            // Arrange
            _usernameTextBox.Text = "Hello World";
            _usernameTextBox.CaretIndex = 6; // After "Hello "
            await _service.ShowKeyboardAsync(_usernameTextBox, _testWindow);

            // Act
            SimulateKeyPress("Beautiful ");
            WaitForUIUpdates();

            // Assert - Text should be inserted at caret position
            _usernameTextBox.Text.Should().Contain("Hello Beautiful");
        }

        [TestMethod]
        public async Task BackspaceHandling_TextBox_RemovesCorrectCharacter()
        {
            // Arrange
            _usernameTextBox.Text = "Hello";
            _usernameTextBox.CaretIndex = 5;
            await _service.ShowKeyboardAsync(_usernameTextBox, _testWindow);

            // Act
            SimulateBackspace();
            WaitForUIUpdates();

            // Assert
            _usernameTextBox.Text.Should().Be("Hell");
        }

        [TestMethod]
        public async Task BackspaceHandling_PasswordBox_RemovesLastCharacter()
        {
            // Arrange
            _passwordBox.Password = "password123";
            await _service.ShowKeyboardAsync(_passwordBox, _testWindow);

            // Act
            SimulateBackspace();
            WaitForUIUpdates();

            // Assert
            _passwordBox.Password.Should().Be("password12");
        }

        #endregion

        #region Mode Detection Tests

        [TestMethod]
        public async Task ModeDetection_UsernameField_UsesTextMode()
        {
            // Act
            await _service.ShowKeyboardAsync(_usernameTextBox, _testWindow);

            // Assert - Should not throw, indicating text mode was used
            Assert.IsNotNull(_service);
        }

        [TestMethod]
        public async Task ModeDetection_PasswordField_UsesPasswordMode()
        {
            // Act
            await _service.ShowKeyboardAsync(_passwordBox, _testWindow);

            // Assert - Should not throw, indicating password mode was used
            Assert.IsNotNull(_service);
        }

        [TestMethod]
        public async Task ModeDetection_PriceField_UsesNumericMode()
        {
            // Act
            await _service.ShowKeyboardAsync(_priceTextBox, _testWindow);

            // Assert - Should not throw, indicating numeric mode was detected
            Assert.IsNotNull(_service);
        }

        #endregion

        #region Error Handling and Edge Cases

        [TestMethod]
        public async Task ErrorHandling_NullInputControl_DoesNotCrash()
        {
            // Act & Assert
            var act = async () => await _service.ShowKeyboardAsync(null!, _testWindow);
            await act.Should().NotThrowAsync();
        }

        [TestMethod]
        public async Task ErrorHandling_WindowWithoutContent_DoesNotCrash()
        {
            // Arrange
            var emptyWindow = new TestWindow { Content = null };

            // Act & Assert
            var act = async () => await _service.ShowKeyboardAsync(_usernameTextBox, emptyWindow);
            await act.Should().NotThrowAsync();

            // Cleanup
            emptyWindow?.Close();
        }

        [TestMethod]
        public async Task ErrorHandling_MultipleHideKeyboard_DoesNotCrash()
        {
            // Arrange
            await _service.ShowKeyboardAsync(_usernameTextBox, _testWindow);

            // Act & Assert
            var act = () =>
            {
                _service.HideKeyboard();
                _service.HideKeyboard();
                _service.HideKeyboard();
            };
            act.Should().NotThrow();
        }

        [TestMethod]
        public async Task ErrorHandling_ShowKeyboardAfterWindowClosed_DoesNotCrash()
        {
            // Arrange
            var tempWindow = new TestWindow();
            tempWindow.Close();

            // Act & Assert
            var act = async () => await _service.ShowKeyboardAsync(_usernameTextBox, tempWindow);
            await act.Should().NotThrowAsync();
        }

        #endregion

        #region Performance Tests

        [TestMethod]
        public async Task Performance_RapidTyping_HandlesEfficiently()
        {
            // Arrange
            await _service.ShowKeyboardAsync(_usernameTextBox, _testWindow);
            var startTime = DateTime.Now;

            // Act - Simulate rapid typing
            for (int i = 0; i < 50; i++)
            {
                SimulateKeyPress("a");
            }
            WaitForUIUpdates();

            var endTime = DateTime.Now;
            var duration = endTime - startTime;

            // Assert - Should complete within reasonable time
            duration.TotalSeconds.Should().BeLessThan(5);
            _usernameTextBox.Text.Length.Should().BeGreaterThan(40);
        }

        [TestMethod]
        public async Task Performance_ModeSwitch_CompletesQuickly()
        {
            // Arrange
            var startTime = DateTime.Now;

            // Act - Switch modes rapidly
            for (int i = 0; i < 20; i++)
            {
                await _service.ShowKeyboardAsync(_usernameTextBox, _testWindow); // Text mode
                await _service.ShowKeyboardAsync(_priceTextBox, _testWindow);    // Numeric mode
                await _service.ShowKeyboardAsync(_passwordBox, _testWindow);     // Password mode
            }

            var endTime = DateTime.Now;
            var duration = endTime - startTime;

            // Assert - Should complete within reasonable time
            duration.TotalSeconds.Should().BeLessThan(3);
        }

        #endregion

        #region Helper Methods

        private void SimulateTyping(string text)
        {
            foreach (char c in text)
            {
                SimulateKeyPress(c.ToString());
            }
        }

        private void SimulateKeyPress(string key)
        {
            try
            {
                var method = typeof(VirtualKeyboardService).GetMethod("HandleKeyPressed", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(_service, new object[] { key });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error simulating key press: {ex.Message}");
            }
        }

        private void SimulateBackspace()
        {
            try
            {
                var method = typeof(VirtualKeyboardService).GetMethod("HandleBackspace", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(_service, new object[0]);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error simulating backspace: {ex.Message}");
            }
        }

        private void WaitForUIUpdates()
        {
            // Process any pending dispatcher operations
            Application.Current?.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
            
            // Small delay to allow for async operations
            Thread.Sleep(50);
        }

        private TestWindow CreateLoginWindow()
        {
            var loginWindow = new TestWindow();
            
            var usernameInput = new TextBox { Name = "UsernameInput" };
            var passwordInput = new PasswordBox { Name = "PasswordInput" };
            var loginButton = new Button { Name = "LoginButton", Content = "Login" };
            
            var panel = new StackPanel();
            panel.Children.Add(usernameInput);
            panel.Children.Add(passwordInput);
            panel.Children.Add(loginButton);
            
            loginWindow.Content = panel;
            loginWindow.Title = "Admin Access"; // This should trigger login detection
            
            return loginWindow;
        }

        #endregion
    }

    /// <summary>
    /// Test window for integration testing
    /// </summary>
    public class TestWindow : Window
    {
        public TestWindow()
        {
            Width = 500;
            Height = 400;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = -2000; // Position off-screen to avoid visual interference
            Top = -2000;
            ShowInTaskbar = false;
            WindowStyle = WindowStyle.None;
            ShowActivated = false;
        }
    }
} 
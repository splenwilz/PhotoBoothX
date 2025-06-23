using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Photobooth.Services;
using System.Collections.Generic;

namespace Photobooth.Controls
{
    public partial class VirtualKeyboard : UserControl
    {
        #region Events

        public event Action<string>? OnKeyPressed;
        public event Action<VirtualKeyboardSpecialKey>? OnSpecialKeyPressed;
        public event Action? OnKeyboardClosed;

        #endregion

        #region Private Fields

        private VirtualKeyboardMode _currentMode;
        private bool _isShiftPressed = false;
        private bool _isCapsLockOn = false;
        private bool _isExtraTransparent = false;
        
        // Drag and resize functionality fields
        private bool _isDragging = false;
        private Point _dragStartPoint;
        private FrameworkElement? _parentContainer;

        // Size presets for touch-friendly resizing
        private KeyboardSize _currentSize = KeyboardSize.Large;

        #endregion

        #region Constructor

        public VirtualKeyboard(VirtualKeyboardMode mode = VirtualKeyboardMode.Text, bool extraTransparent = false)
        {
            InitializeComponent();
            _currentMode = mode;
            _isExtraTransparent = extraTransparent;
            SetKeyboardMode(mode);
            
            if (_isExtraTransparent)
            {
                ApplyExtraTransparency();
            }
        }

        #endregion

        #region Mode Management

        /// <summary>
        /// Set the keyboard to the specified mode
        /// </summary>
        private void SetKeyboardMode(VirtualKeyboardMode mode)
        {
            _currentMode = mode;

            // Hide all layouts first
            TextModeLayout.Visibility = Visibility.Collapsed;
            NumericModeLayout.Visibility = Visibility.Collapsed;

            // Show the appropriate layout
            switch (mode)
            {
                case VirtualKeyboardMode.Text:
                case VirtualKeyboardMode.Password:
                    TextModeLayout.Visibility = Visibility.Visible;
                    UpdateModeButtonStates(TextModeButton);
                    break;
                case VirtualKeyboardMode.Numeric:
                    NumericModeLayout.Visibility = Visibility.Visible;
                    UpdateModeButtonStates(NumericModeButton);
                    break;
            }

            UpdateKeyCase();
        }

        /// <summary>
        /// Update mode button visual states
        /// </summary>
        private void UpdateModeButtonStates(Button activeButton)
        {
            // Reset all buttons to normal state
            TextModeButton.Style = (Style)FindResource("SpecialKeyStyle");
            NumericModeButton.Style = (Style)FindResource("SpecialKeyStyle");

            // Highlight the active button
            activeButton.Style = (Style)FindResource("ActionKeyStyle");
        }

        /// <summary>
        /// Update key case and shift states for letters, numbers, and punctuation
        /// </summary>
        private void UpdateKeyCase()
        {
            if (_currentMode != VirtualKeyboardMode.Text && _currentMode != VirtualKeyboardMode.Password)
                return;

            bool shouldBeUppercase = _isCapsLockOn || _isShiftPressed;

            // Update all letter keys
            var letterKeys = new[]
            {
                KeyQ, KeyW, KeyE, KeyR, KeyT, KeyY, KeyU, KeyI, KeyO, KeyP,
                KeyA, KeyS, KeyD, KeyF, KeyG, KeyH, KeyJ, KeyK, KeyL,
                KeyZ, KeyX, KeyC, KeyV, KeyB, KeyN, KeyM
            };

            foreach (var key in letterKeys)
            {
                if (key != null)
                {
                    var letter = key.Content.ToString();
                    if (!string.IsNullOrEmpty(letter))
                    {
                        key.Content = shouldBeUppercase ? letter.ToUpper() : letter.ToLower();
                    }
                }
            }

            // Update number and punctuation keys with shift mappings
            UpdateNumberAndPunctuationKeys();

            // Reset shift after use (but not caps lock)
            if (_isShiftPressed)
            {
                _isShiftPressed = false;
                UpdateShiftButtonState();
            }
        }

        /// <summary>
        /// Update number and punctuation keys based on shift state
        /// </summary>
        private void UpdateNumberAndPunctuationKeys()
        {
            // Create shift mappings for numbers and common punctuation
            var shiftMappings = new Dictionary<string, string>
            {
                // Numbers to symbols
                {"1", "!"}, {"2", "@"}, {"3", "#"}, {"4", "$"}, {"5", "%"},
                {"6", "^"}, {"7", "&"}, {"8", "*"}, {"9", "("}, {"0", ")"},
                
                // Punctuation marks
                {"-", "_"}, {"=", "+"}, {"[", "{"}, {"]", "}"},
                {";", ":"}, {"'", "\""}, {",", "<"}, {".", ">"}, {"/", "?"},
                
                // Reverse mappings for when shift is released
                {"!", "1"}, {"@", "2"}, {"#", "3"}, {"$", "4"}, {"%", "5"},
                {"^", "6"}, {"&", "7"}, {"*", "8"}, {"(", "9"}, {")", "0"},
                {"_", "-"}, {"+", "="}, {"{", "["}, {"}", "]"},
                {":", ";"}, {"\"", "'"}, {"<", ","}, {">", "."}, {"?", "/"}
            };

            // Find all buttons in the text mode layout and update their content
            if (TextModeLayout != null)
            {
                UpdateButtonsInContainer(TextModeLayout, shiftMappings);
            }
        }

        /// <summary>
        /// Recursively find and update buttons in a container
        /// </summary>
        private void UpdateButtonsInContainer(DependencyObject container, Dictionary<string, string> mappings)
        {
            try
            {
                var childrenCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(container);
                for (int i = 0; i < childrenCount; i++)
                {
                    var child = System.Windows.Media.VisualTreeHelper.GetChild(container, i);
                    
                    if (child is Button button && button.Content != null)
                    {
                        var currentContent = button.Content.ToString();
                        if (!string.IsNullOrEmpty(currentContent))
                        {
                            // Skip special buttons (they have longer text content)
                            if (currentContent.Length == 1 && mappings.ContainsKey(currentContent))
                            {
                                // Only apply shift mapping if shift is pressed
                                if (_isShiftPressed)
                                {
                                    button.Content = mappings[currentContent];
                                }
                                else
                                {
                                    // Check if current content is a shifted character and revert it
                                    if (mappings.ContainsKey(currentContent))
                                    {
                                        var originalChar = mappings[currentContent];
                                        // Only revert if the original is not also a shift character
                                        if (originalChar.Length == 1 && !IsShiftedCharacter(originalChar))
                                        {
                                            button.Content = originalChar;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    
                    // Recursively check child elements
                    UpdateButtonsInContainer(child, mappings);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating buttons in container: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if a character is a shifted character (symbol)
        /// </summary>
        private bool IsShiftedCharacter(string character)
        {
            var shiftedChars = new HashSet<string> { "!", "@", "#", "$", "%", "^", "&", "*", "(", ")", "_", "+", "{", "}", ":", "\"", "<", ">", "?" };
            return shiftedChars.Contains(character);
        }

        /// <summary>
        /// Update shift button visual state
        /// </summary>
        private void UpdateShiftButtonState()
        {
            if (ShiftButton != null)
            {
                if (_isShiftPressed)
                {
                    ShiftButton.Style = (Style)FindResource("ActionKeyStyle");
                    ShiftButton.Content = "⇧ SHIFT";
                }
                else
                {
                    ShiftButton.Style = (Style)FindResource("SpecialKeyStyle");
                    ShiftButton.Content = "⇧ Shift";
                }
            }
        }

        /// <summary>
        /// Update caps lock button visual state
        /// </summary>
        private void UpdateCapsLockButtonState()
        {
            if (CapsLockButton != null)
            {
                if (_isCapsLockOn)
                {
                    CapsLockButton.Style = (Style)FindResource("ActionKeyStyle");
                    CapsLockButton.Content = "⇪ CAPS";
                }
                else
                {
                    CapsLockButton.Style = (Style)FindResource("SpecialKeyStyle");
                    CapsLockButton.Content = "⇪ Caps";
                }
            }
        }

        /// <summary>
        /// Apply extra transparency for login screen
        /// </summary>
        private void ApplyExtraTransparency()
        {
            try
            {
                // Make the main container even more transparent
                this.Opacity = 0.6; // Even more transparent overall
                
                // Find the main border and make it more transparent
                if (this.Content is Border mainBorder)
                {
                    if (mainBorder.Background is SolidColorBrush backgroundBrush)
                    {
                        backgroundBrush.Opacity = 0.15; // Much more transparent
                    }
                    
                    if (mainBorder.BorderBrush is SolidColorBrush borderBrush)
                    {
                        borderBrush.Opacity = 0.1; // Very transparent border
                    }
                }
                
                // Make all buttons more transparent
                ApplyTransparentButtonStyles();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error applying extra transparency: {ex.Message}");
            }
        }

        /// <summary>
        /// Apply transparent styles to all buttons
        /// </summary>
        private void ApplyTransparentButtonStyles()
        {
            try
            {
                // Create ultra-transparent button styles for login screen
                var transparentKeyStyle = new Style(typeof(Button));
                transparentKeyStyle.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromArgb(20, 255, 255, 255)))); // 8% opacity white - ultra transparent
                transparentKeyStyle.Setters.Add(new Setter(Button.BorderBrushProperty, new SolidColorBrush(System.Windows.Media.Color.FromArgb(15, 209, 213, 219)))); // 6% opacity border - ultra transparent
                transparentKeyStyle.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(1)));
                transparentKeyStyle.Setters.Add(new Setter(Button.FontSizeProperty, 16.0));
                transparentKeyStyle.Setters.Add(new Setter(Button.FontWeightProperty, FontWeights.Medium));
                transparentKeyStyle.Setters.Add(new Setter(Button.ForegroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 255, 255, 255)))); // Semi-transparent white text for better contrast
                transparentKeyStyle.Setters.Add(new Setter(Button.HeightProperty, 48.0));
                transparentKeyStyle.Setters.Add(new Setter(Button.MarginProperty, new Thickness(2)));
                transparentKeyStyle.Setters.Add(new Setter(Button.CursorProperty, Cursors.Hand));
                
                // Create template for ultra-transparent buttons with hover effects
                var template = new ControlTemplate(typeof(Button));
                var borderElement = new FrameworkElementFactory(typeof(Border));
                borderElement.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
                borderElement.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
                borderElement.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
                borderElement.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
                borderElement.Name = "BorderElement";
                
                var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
                contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
                borderElement.AppendChild(contentPresenter);
                
                template.VisualTree = borderElement;
                
                // Add transparent hover trigger
                var hoverTrigger = new Trigger();
                hoverTrigger.Property = Button.IsMouseOverProperty;
                hoverTrigger.Value = true;
                hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromArgb(35, 255, 255, 255)), "BorderElement")); // Slightly more visible on hover but still very transparent
                hoverTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush(System.Windows.Media.Color.FromArgb(25, 255, 255, 255)), "BorderElement")); // Slightly more visible border on hover
                template.Triggers.Add(hoverTrigger);
                
                // Add transparent pressed trigger
                var pressedTrigger = new Trigger();
                pressedTrigger.Property = Button.IsPressedProperty;
                pressedTrigger.Value = true;
                pressedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromArgb(50, 255, 255, 255)), "BorderElement")); // More visible when pressed but still transparent
                pressedTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 255, 255, 255)), "BorderElement")); // More visible border when pressed
                template.Triggers.Add(pressedTrigger);
                
                transparentKeyStyle.Setters.Add(new Setter(Button.TemplateProperty, template));
                
                // Apply to all buttons recursively
                ApplyStyleToAllButtons(this, transparentKeyStyle);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error applying transparent button styles: {ex.Message}");
            }
        }

        /// <summary>
        /// Recursively apply style to all buttons in the visual tree
        /// </summary>
        private void ApplyStyleToAllButtons(DependencyObject parent, Style style)
        {
            try
            {
                var childrenCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
                for (int i = 0; i < childrenCount; i++)
                {
                    var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                    
                    if (child is Button button)
                    {
                        button.Style = style;
                    }
                    
                    ApplyStyleToAllButtons(child, style);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error applying style to buttons: {ex.Message}");
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handle mode button clicks
        /// </summary>
        private void TextModeButton_Click(object sender, RoutedEventArgs e)
        {
            SetKeyboardMode(VirtualKeyboardMode.Text);
        }

        private void NumericModeButton_Click(object sender, RoutedEventArgs e)
        {
            SetKeyboardMode(VirtualKeyboardMode.Numeric);
        }

        /// <summary>
        /// Handle regular key button clicks
        /// </summary>
        private void KeyButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("KeyButton_Click triggered");
            
            if (sender is Button button && button.Content != null)
            {
                var key = button.Content.ToString();
                Console.WriteLine($"Key pressed: '{key}'");
                
                if (!string.IsNullOrEmpty(key))
                {
                    Console.WriteLine($"Invoking OnKeyPressed with: '{key}'");
                    OnKeyPressed?.Invoke(key);
                    
                    // Update case after typing a letter (for shift behavior)
                    if (_currentMode == VirtualKeyboardMode.Text || _currentMode == VirtualKeyboardMode.Password)
                    {
                        UpdateKeyCase();
                    }
                }
            }
            else
            {
                Console.WriteLine("Button or content is null");
            }
        }

        /// <summary>
        /// Handle special key button clicks
        /// </summary>
        private void SpaceButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("SpaceButton_Click triggered");
            OnSpecialKeyPressed?.Invoke(VirtualKeyboardSpecialKey.Space);
        }

        private void BackspaceButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("BackspaceButton_Click triggered");
            OnSpecialKeyPressed?.Invoke(VirtualKeyboardSpecialKey.Backspace);
        }

        private void EnterButton_Click(object sender, RoutedEventArgs e)
        {
            OnSpecialKeyPressed?.Invoke(VirtualKeyboardSpecialKey.Enter);
        }

        private void TabButton_Click(object sender, RoutedEventArgs e)
        {
            OnSpecialKeyPressed?.Invoke(VirtualKeyboardSpecialKey.Tab);
        }

        private void ShiftButton_Click(object sender, RoutedEventArgs e)
        {
            _isShiftPressed = !_isShiftPressed;
            UpdateShiftButtonState();
            UpdateKeyCase();
        }

        private void CapsLockButton_Click(object sender, RoutedEventArgs e)
        {
            _isCapsLockOn = !_isCapsLockOn;
            UpdateCapsLockButtonState();
            UpdateKeyCase();
            
            // Update caps lock button appearance (if we had one in our layout)
            // CapsLockButton.Style = _isCapsLockOn ? (Style)FindResource("ActionKeyStyle") : (Style)FindResource("SpecialKeyStyle");
        }

        /// <summary>
        /// Handle close button click
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            OnKeyboardClosed?.Invoke();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Programmatically change the keyboard mode
        /// </summary>
        public void ChangeMode(VirtualKeyboardMode mode)
        {
            SetKeyboardMode(mode);
        }

        /// <summary>
        /// Get the current keyboard mode
        /// </summary>
        public VirtualKeyboardMode GetCurrentMode()
        {
            return _currentMode;
        }

        #endregion

        #region Drag and Resize Functionality

        /// <summary>
        /// Keyboard size presets for touch-friendly resizing
        /// </summary>
        public enum KeyboardSize
        {
            Small,
            Medium,
            Large
        }

        /// <summary>
        /// Initialize drag functionality - should be called when keyboard is added to parent
        /// </summary>
        public void InitializeDragResize(FrameworkElement parentContainer)
        {
            _parentContainer = parentContainer;
            Console.WriteLine("Drag functionality initialized");
            
            // Set initial size immediately
            ApplyKeyboardSize(_currentSize);
            
            // Also apply size after the control is fully loaded (for cases where ActualWidth isn't available yet)
            this.Loaded += (s, e) => {
                Console.WriteLine($"Keyboard loaded, parent ActualWidth: {_parentContainer?.ActualWidth}");
                // Re-apply the current size to ensure full width works properly
                ApplyKeyboardSize(_currentSize);
            };
        }

        /// <summary>
        /// Apply a preset keyboard size
        /// </summary>
        private void ApplyKeyboardSize(KeyboardSize size)
        {
            try
            {
                _currentSize = size;
                
                double width, height, layoutHeight;
                
                switch (size)
                {
                    case KeyboardSize.Small:
                        width = 800;
                        height = 240;
                        layoutHeight = 160;
                        break;
                    case KeyboardSize.Medium:
                        width = 1000;
                        height = 280;
                        layoutHeight = 200;
                        break;
                    case KeyboardSize.Large:
                        // Large size should be truly full width - use stretch instead of fixed width
                        width = double.NaN; // Use NaN to allow stretching
                        height = 320;
                        layoutHeight = 240;
                        break;
                    default:
                        // Default to Large size - full width
                        width = double.NaN; // Use NaN to allow stretching
                        height = 320;
                        layoutHeight = 240;
                        break;
                }

                // Apply new size
                if (double.IsNaN(width))
                {
                    // For full-width (Large), clear width and use stretch alignment
                    this.Width = double.NaN;
                    this.HorizontalAlignment = HorizontalAlignment.Stretch;
                }
                else
                {
                    // For fixed sizes (Small, Medium), set explicit width
                    this.Width = width;
                    this.HorizontalAlignment = HorizontalAlignment.Center;
                }
                this.Height = height;
                
                // Update layout heights
                TextModeLayout.Height = layoutHeight;
                NumericModeLayout.Height = layoutHeight;
                
                // Update size button states
                UpdateSizeButtonStates();
                
                // Position keyboard - center for smaller sizes, full width for Large
                if (_parentContainer != null)
                {
                    if (size == KeyboardSize.Large)
                    {
                        // Large keyboard should be full width
                        Canvas.SetLeft(this, 0);
                        Canvas.SetRight(this, 0); // This forces full width in Canvas
                        
                        // Set width explicitly if parent width is available, otherwise use a large fallback
                        if (_parentContainer.ActualWidth > 0)
                        {
                            this.Width = _parentContainer.ActualWidth;
                            Console.WriteLine($"Large keyboard set to parent width: {_parentContainer.ActualWidth}");
                        }
                        else
                        {
                            // Fallback for when parent width isn't available yet - use a very large width
                            this.Width = 1600; // Large fallback that will be corrected on Loaded event
                            Console.WriteLine("Large keyboard using fallback width (parent not ready)");
                        }
                    }
                    else
                    {
                        // Center smaller keyboards and clear any right positioning
                        Canvas.SetRight(this, double.NaN); // Clear right positioning
                        if (!double.IsNaN(width) && _parentContainer.ActualWidth > 0)
                        {
                            var left = (_parentContainer.ActualWidth - width) / 2;
                            if (left < 0) left = 0;
                            Canvas.SetLeft(this, left);
                        }
                        else
                        {
                            // Fallback centering when parent width not available
                            Canvas.SetLeft(this, 100);
                        }
                    }
                }
                
                Console.WriteLine($"Applied keyboard size: {size} ({width}x{height})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error applying keyboard size: {ex.Message}");
            }
        }

        /// <summary>
        /// Update size button visual states
        /// </summary>
        private void UpdateSizeButtonStates()
        {
            try
            {
                // Reset all buttons to normal state
                SmallSizeButton.Style = (Style)FindResource("SpecialKeyStyle");
                MediumSizeButton.Style = (Style)FindResource("SpecialKeyStyle");
                LargeSizeButton.Style = (Style)FindResource("SpecialKeyStyle");

                // Highlight the active button
                switch (_currentSize)
                {
                    case KeyboardSize.Small:
                        SmallSizeButton.Style = (Style)FindResource("ActionKeyStyle");
                        break;
                    case KeyboardSize.Medium:
                        MediumSizeButton.Style = (Style)FindResource("ActionKeyStyle");
                        break;
                    case KeyboardSize.Large:
                        LargeSizeButton.Style = (Style)FindResource("ActionKeyStyle");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating size button states: {ex.Message}");
            }
        }

        /// <summary>
        /// Small size button click
        /// </summary>
        private void SmallSizeButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyKeyboardSize(KeyboardSize.Small);
        }

        /// <summary>
        /// Medium size button click
        /// </summary>
        private void MediumSizeButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyKeyboardSize(KeyboardSize.Medium);
        }

        /// <summary>
        /// Large size button click
        /// </summary>
        private void LargeSizeButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyKeyboardSize(KeyboardSize.Large);
        }

        /// <summary>
        /// Handle header drag start
        /// </summary>
        private void HeaderGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_parentContainer == null)
            {
                Console.WriteLine("Cannot drag - parent container not set");
                return;
            }

            try
            {
                _isDragging = true;
                _dragStartPoint = e.GetPosition(_parentContainer);
                HeaderGrid.CaptureMouse();
                
                // Convert from bottom positioning to top positioning for dragging
                var currentBottom = Canvas.GetBottom(this);
                if (!double.IsNaN(currentBottom) && _parentContainer.ActualHeight > 0)
                {
                    // Calculate the current top position based on bottom position
                    var currentTop = _parentContainer.ActualHeight - currentBottom - this.ActualHeight;
                    Canvas.SetTop(this, Math.Max(0, currentTop));
                    Canvas.SetBottom(this, double.NaN); // Clear bottom positioning
                }
                else
                {
                    // Fallback if bottom positioning not set
                    var currentTop = Canvas.GetTop(this);
                    if (double.IsNaN(currentTop))
                    {
                        Canvas.SetTop(this, 100); // Default position
                    }
                }
                
                // Add mouse move and up handlers
                HeaderGrid.MouseMove += HeaderGrid_MouseMove;
                HeaderGrid.MouseLeftButtonUp += HeaderGrid_MouseLeftButtonUp;
                
                Console.WriteLine($"Started dragging at {_dragStartPoint}");
                e.Handled = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting drag: {ex.Message}");
                _isDragging = false;
            }
        }

        /// <summary>
        /// Handle header drag movement
        /// </summary>
        private void HeaderGrid_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging || _parentContainer == null) return;

            try
            {
                var currentPosition = e.GetPosition(_parentContainer);
                var deltaX = currentPosition.X - _dragStartPoint.X;
                var deltaY = currentPosition.Y - _dragStartPoint.Y;

                // Get current position of this keyboard (should be using top positioning now)
                var currentLeft = Canvas.GetLeft(this);
                var currentTop = Canvas.GetTop(this);

                // These should not be NaN anymore since we set them in MouseDown, but just in case
                if (double.IsNaN(currentLeft)) currentLeft = 0;
                if (double.IsNaN(currentTop)) currentTop = 0;

                // Calculate new position based on delta from drag start
                var newLeft = currentLeft + deltaX;
                var newTop = currentTop + deltaY;

                // Constrain to parent bounds
                var parentWidth = _parentContainer.ActualWidth;
                var parentHeight = _parentContainer.ActualHeight;
                
                // Ensure parent dimensions are valid
                if (parentWidth <= 0) parentWidth = 1200; // fallback
                if (parentHeight <= 0) parentHeight = 800; // fallback
                
                // Apply boundaries
                if (newLeft < 0) newLeft = 0;
                if (newTop < 0) newTop = 0;
                if (newLeft + this.ActualWidth > parentWidth) newLeft = parentWidth - this.ActualWidth;
                if (newTop + this.ActualHeight > parentHeight) newTop = parentHeight - this.ActualHeight;

                // Ensure we don't get negative values (double check)
                if (newLeft < 0) newLeft = 0;
                if (newTop < 0) newTop = 0;

                // Apply new position
                Canvas.SetLeft(this, newLeft);
                Canvas.SetTop(this, newTop);

                // Update drag start point for next move
                _dragStartPoint = currentPosition;
                
                Console.WriteLine($"Dragging to {newLeft:F1}, {newTop:F1}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during drag: {ex.Message}");
                // Stop dragging on error
                _isDragging = false;
                HeaderGrid.ReleaseMouseCapture();
            }
        }

        /// <summary>
        /// Handle header drag end
        /// </summary>
        private void HeaderGrid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (_isDragging)
                {
                    _isDragging = false;
                    HeaderGrid.ReleaseMouseCapture();
                    
                    // Remove mouse handlers
                    HeaderGrid.MouseMove -= HeaderGrid_MouseMove;
                    HeaderGrid.MouseLeftButtonUp -= HeaderGrid_MouseLeftButtonUp;
                    
                    Console.WriteLine("Drag ended");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error ending drag: {ex.Message}");
                _isDragging = false;
            }
        }

        #endregion
    }
} 
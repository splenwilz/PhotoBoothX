using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Photobooth.Models;
using Photobooth.Services;
using System.Windows.Media;

namespace Photobooth.Controls
{
    public partial class TemplateUploadDialog : Window
    {
        private readonly IDatabaseService _databaseService;
        private readonly TemplateManager _templateManager;
        private List<TemplateLayout> _layouts = new List<TemplateLayout>();
        
        public new bool DialogResult { get; private set; } = false;
        public TemplateUploadResult? UploadResult { get; private set; }

        public TemplateUploadDialog(IDatabaseService databaseService, TemplateManager templateManager)
        {
            InitializeComponent();
            
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _templateManager = templateManager ?? throw new ArgumentNullException(nameof(templateManager));
            
            Loaded += TemplateUploadDialog_Loaded;
        }

        private async void TemplateUploadDialog_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadLayoutsAsync();
        }

        private async Task LoadLayoutsAsync()
        {
            try
            {
                var layoutsResult = await _databaseService.GetTemplateLayoutsAsync();
                if (layoutsResult.Success && layoutsResult.Data != null)
                {
                    _layouts = layoutsResult.Data.Where(l => l.IsActive).OrderBy(l => l.SortOrder).ThenBy(l => l.Name).ToList();
                    LayoutComboBox.ItemsSource = _layouts;
                    
                    // Select first layout by default
                    if (_layouts.Count > 0)
                    {
                        LayoutComboBox.SelectedIndex = 0;
                    }
                }
                else
                {
                    MessageBox.Show("No layouts found. Please ensure template layouts are configured in the database.", 
                                  "No Layouts", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading layouts: {ex.Message}", 
                              "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LayoutComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateUploadButtonState();
            
            if (LayoutComboBox.SelectedItem is TemplateLayout layout)
            {
                LayoutDetailsPanel.Visibility = Visibility.Visible;
                LayoutDimensionsText.Text = $"{layout.Name} - {layout.Width} × {layout.Height} pixels ({layout.PhotoCount} photos)";
                LayoutDescriptionText.Text = $"Description: {layout.Description ?? "No description available"}";
            }
            else
            {
                LayoutDetailsPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateUploadButtonState()
        {
            // Null check to prevent errors during initialization
            if (UploadButton == null || ZipUploadRadio == null || FolderUploadRadio == null || LayoutComboBox == null)
                return;
                
            // For ZIP uploads, layout selection is optional (auto-detection)
            // For folder uploads, layout selection is required
            if (ZipUploadRadio.IsChecked == true)
            {
                UploadButton.IsEnabled = true; // Always enabled for ZIP uploads
            }
            else if (FolderUploadRadio.IsChecked == true)
            {
                UploadButton.IsEnabled = LayoutComboBox.SelectedItem is TemplateLayout; // Required for folder uploads
            }
            else
            {
                UploadButton.IsEnabled = false; // No upload method selected
            }
        }

        private void UploadMethodRadio_Checked(object sender, RoutedEventArgs e)
        {
            // Null checks for initial load - all controls must be initialized
            if (LayoutComboBox != null && LayoutSelectionHint != null && UploadButton != null)
            {
                UpdateUploadButtonState();
                
                // Update UI hints and visual state based on upload method
                if (ZipUploadRadio.IsChecked == true)
                {
                    LayoutSelectionHint.Text = "Layout will be auto-detected from template dimensions";
                    LayoutSelectionHint.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94)); // Green
                    
                    // Visually indicate that layout selection is optional for ZIP uploads
                    LayoutComboBox.Opacity = 0.6;
                    LayoutComboBox.IsEnabled = false;
                }
                else if (FolderUploadRadio.IsChecked == true)
                {
                    LayoutSelectionHint.Text = "Please select the target layout for these templates";
                    LayoutSelectionHint.Foreground = new SolidColorBrush(Color.FromRgb(59, 130, 246)); // Blue
                    
                    // Re-enable layout selection for folder uploads
                    LayoutComboBox.Opacity = 1.0;
                    LayoutComboBox.IsEnabled = true;
                }
            }
        }

        private async void UploadButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UploadButton.IsEnabled = false;
                UploadButton.Content = "Uploading...";

                TemplateUploadResult result;

                if (FolderUploadRadio.IsChecked == true)
                {
                    // Folder uploads require layout selection
                    if (LayoutComboBox.SelectedItem is not TemplateLayout selectedLayout)
                    {
                        MessageBox.Show("Please select a layout for folder uploads.", "No Layout Selected", 
                                      MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    result = await UploadFromFoldersAsync(selectedLayout);
                }
                else if (ZipUploadRadio.IsChecked == true)
                {
                    // ZIP uploads use auto-detection (layout selection is optional)
                    result = await UploadFromZipAsync();
                }
                else
                {
                    MessageBox.Show("Please select an upload method.", "No Method Selected", 
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                UploadResult = result;
                DialogResult = result.SuccessCount > 0;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Upload failed: {ex.Message}", "Upload Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                UploadButton.IsEnabled = true;
                UploadButton.Content = "Start Upload";
            }
        }

        private async Task<TemplateUploadResult> UploadFromFoldersAsync(TemplateLayout layout)
        {
            var dialog = new OpenFolderDialog
            {
                Title = $"Select Template Folders for {layout.LayoutKey}",
                Multiselect = false
            };

            if (dialog.ShowDialog() != true)
            {
                return new TemplateUploadResult { Message = "Upload cancelled" };
            }

            // Pass the selected layout to the upload method
            return await _templateManager.UploadFromFoldersWithLayoutAsync(new[] { dialog.FolderName }, layout.Id);
        }

        private async Task<TemplateUploadResult> UploadFromZipAsync()
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select Template ZIP File (supports multiple layouts)",
                Filter = "ZIP Files|*.zip|All Files|*.*",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() != true)
            {
                return new TemplateUploadResult { Message = "Upload cancelled" };
            }

            // Use auto-detection method instead of forcing a specific layout
            return await _templateManager.UploadFromZipAsync(openFileDialog.FileName);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// Show the upload dialog and return the result
        /// </summary>
        public static Task<(bool success, TemplateUploadResult? result)> ShowUploadDialogAsync(
            IDatabaseService databaseService, 
            TemplateManager templateManager, 
            Window? owner = null)
        {
            var tcs = new TaskCompletionSource<(bool success, TemplateUploadResult? result)>();
            
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    var dialog = new TemplateUploadDialog(databaseService, templateManager);
                    
                    if (owner != null)
                    {
                        dialog.Owner = owner;
                    }
                    else
                    {
                        dialog.Owner = Application.Current.MainWindow;
                    }

                    dialog.ShowDialog();
                    tcs.SetResult((dialog.DialogResult, dialog.UploadResult));
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            
            return tcs.Task;
        }
    }
} 
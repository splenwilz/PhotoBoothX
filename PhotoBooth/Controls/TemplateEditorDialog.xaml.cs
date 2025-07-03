using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Photobooth.Models;
using Photobooth.Services;
using PhotoBooth.Services;

namespace Photobooth.Controls
{
    public partial class TemplateEditorDialog : Window
    {
        #region Private Fields

        private readonly IDatabaseService _databaseService;
        private readonly NotificationService _notificationService;
        private Template _template;
        private List<TemplateCategory> _categories = new List<TemplateCategory>();
        private Action<int>? _refreshTemplateCallback;

        #endregion

        #region Properties

        public bool EditSucceeded { get; private set; } = false;
        public Template? UpdatedTemplate { get; private set; }

        #endregion

        #region Constructor

        public TemplateEditorDialog(Template template, IDatabaseService databaseService, NotificationService notificationService, Action<int>? refreshTemplateCallback = null)
        {
            InitializeComponent();
            
            _template = template ?? throw new ArgumentNullException(nameof(template));
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _refreshTemplateCallback = refreshTemplateCallback;
            
            Loaded += TemplateEditorDialog_Loaded;
        }

        #endregion

        #region Event Handlers

        private async void TemplateEditorDialog_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadCategoriesAsync();
            await LoadLayoutInfoAsync();
            PopulateFields();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            EditSucceeded = false;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            EditSucceeded = false;
            Close();
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            await SaveChangesAsync();
        }

        private void IsActiveCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdateActiveStatusText();
        }

        private void ReplacePreviewButton_Click(object sender, RoutedEventArgs e)
        {
            ReplacePreviewImage();
        }

        private void ReplaceTemplateButton_Click(object sender, RoutedEventArgs e)
        {
            ReplaceTemplateFile();
        }

        // EditConfigButton_Click method removed - no longer using config.json files in layout-based system

        #endregion

        #region Private Methods

        #region Input Validation Methods

        /// <summary>
        /// Validate numeric input for price and sort order fields
        /// </summary>
        private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            try
            {
                // Allow decimal point for price fields, only integers for sort order
                bool allowDecimal = textBox.Name == "PriceTextBox";
                
                // Check if the input is a digit
                if (char.IsDigit(e.Text, 0))
                {
                    return; // Allow digits
                }
                
                // For price fields, allow decimal point if not already present
                if (allowDecimal && e.Text == "." && !textBox.Text.Contains("."))
                {
                    return; // Allow single decimal point
                }
                
                // Block all other characters
                e.Handled = true;
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error validating numeric input", ex);
                e.Handled = true; // Block input on error for safety
            }
        }

        #endregion

        #region File Management Methods

        private void ReplacePreviewImage()
        {
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Title = "Select Preview Image",
                    Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif|PNG Files|*.png|JPEG Files|*.jpg;*.jpeg|All Files|*.*",
                    FilterIndex = 1
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    // Get the actual template folder path using layout-based system
                    var templateFolder = GetActualTemplateFolderPath();


                    // Find existing preview file to preserve extension
                    var previewExtensions = new[] { ".png", ".jpg", ".jpeg" };
                    var existingPreviewFile = previewExtensions
                        .Select(ext => Path.Combine(templateFolder, $"preview{ext}"))
                        .FirstOrDefault(File.Exists);
                    
                    // Use existing extension or default to new file's extension
                    var newFileExtension = Path.GetExtension(openFileDialog.FileName);
                    var previewPath = existingPreviewFile ?? Path.Combine(templateFolder, $"preview{newFileExtension}");


                    // Create backup of original if it exists
                    if (existingPreviewFile != null && File.Exists(existingPreviewFile))
                    {
                        var backupPath = Path.ChangeExtension(existingPreviewFile, $"_backup{Path.GetExtension(existingPreviewFile)}");
                        File.Copy(existingPreviewFile, backupPath, true);

                        // If changing extensions, remove the old file
                        if (Path.GetExtension(existingPreviewFile) != newFileExtension)
                        {
                            File.Delete(existingPreviewFile);

                        }
                    }

                    // Copy new preview image
                    File.Copy(openFileDialog.FileName, previewPath, true);


                    _notificationService.ShowSuccess("Preview Updated", "Preview image has been replaced successfully!");
                    
                    // Update the file info display
                    UpdateFileInfoDisplay();
                    
                    // Refresh template display to show updated preview
                    _refreshTemplateCallback?.Invoke(_template.Id);
                }
            }
            catch (Exception ex)
            {

                _notificationService.ShowError("Replace Preview Error", $"Failed to replace preview image: {ex.Message}");
            }
        }

        private void ReplaceTemplateFile()
        {
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Title = "Select Template File",
                    Filter = "PNG Files|*.png|Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All Files|*.*",
                    FilterIndex = 1
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    // Get the actual template folder path using layout-based system
                    var templateFolder = GetActualTemplateFolderPath();
                    var templatePath = Path.Combine(templateFolder, "template.png");


                    // Create backup of original
                    if (File.Exists(templatePath))
                    {
                        var backupPath = Path.Combine(templateFolder, "template_backup.png");
                        File.Copy(templatePath, backupPath, true);

                    }

                    // Copy new template file
                    File.Copy(openFileDialog.FileName, templatePath, true);


                    _notificationService.ShowSuccess("Template Updated", "Template file has been replaced successfully!");
                    
                    // Update the file info display
                    UpdateFileInfoDisplay();
                }
            }
            catch (Exception ex)
            {

                _notificationService.ShowError("Replace Template Error", $"Failed to replace template file: {ex.Message}");
            }
        }

        private string GetTemplatesFolderPath()
        {
            // Always use the runtime Templates folder for consistency
            // This is where the application reads templates from during execution
            var currentDir = AppDomain.CurrentDomain.BaseDirectory;
            var runtimeTemplatesPath = Path.Combine(currentDir, "Templates");

            return runtimeTemplatesPath;
        }

        private string GetActualTemplateFolderPath()
        {
            // For layout-based system: Templates/layout-folder/template-folder/
            var templatesBaseFolder = GetTemplatesFolderPath();
            
            // If FolderPath is already a full path within Templates, use it
            if (!string.IsNullOrEmpty(_template.FolderPath) && _template.FolderPath.Contains("Templates"))
            {
                return _template.FolderPath;
            }
            
            // Otherwise, try to find the template folder within the layout structure
            var templateFolderName = Path.GetFileName(_template.FolderPath) ?? SanitizeFileName(_template.Name);
            
            // Search through layout folders to find the template
            if (Directory.Exists(templatesBaseFolder))
            {
                foreach (var layoutFolder in Directory.GetDirectories(templatesBaseFolder))
                {
                    var possibleTemplatePath = Path.Combine(layoutFolder, templateFolderName);
                    if (Directory.Exists(possibleTemplatePath))
                    {
                        return possibleTemplatePath;
                    }
                }
            }
            
            // Fallback: assume it's directly in Templates folder (for backwards compatibility)
            return Path.Combine(templatesBaseFolder, templateFolderName);
        }

        // EditConfigFile method removed - using layout-based system instead of config.json files

        private void UpdateFileInfoDisplay()
        {
            try
            {
                // Get the actual template folder path using layout-based system
                var templateFolder = GetActualTemplateFolderPath();


                // Update preview file info - detect actual extension
                var previewExtensions = new[] { ".png", ".jpg", ".jpeg" };
                var actualPreviewFile = previewExtensions
                    .Select(ext => Path.Combine(templateFolder, $"preview{ext}"))
                    .FirstOrDefault(File.Exists);
                
                if (actualPreviewFile != null)
                {
                    var previewInfo = new FileInfo(actualPreviewFile);
                    var fileName = Path.GetFileName(actualPreviewFile);
                    PreviewFileText.Text = $"{fileName} ({FormatFileSize(previewInfo.Length)})";

                }
                else
                {
                    PreviewFileText.Text = "preview file (not found)";

                }

                // Update template file info
                var templatePath = Path.Combine(templateFolder, "template.png");
                if (File.Exists(templatePath))
                {
                    var templateInfo = new FileInfo(templatePath);
                    TemplateFileText.Text = $"template.png ({FormatFileSize(templateInfo.Length)})";

                }
                else
                {
                    TemplateFileText.Text = "template.png (not found)";

                }

            }
            catch
            {
                // Silently handle file info update errors

            }
        }

        // GenerateDefaultPhotoAreas method removed - using layout-based system instead of config.json files

        #endregion

        private async Task LoadCategoriesAsync()
        {
            try
            {
                // Use GetAllTemplateCategoriesAsync to show ALL categories regardless of seasonal status
                // This allows users to assign templates to seasonal categories even when out of season
                var result = await _databaseService.GetAllTemplateCategoriesAsync();
                if (result.Success && result.Data != null)
                {
                    // Only filter by IsActive - show all active categories regardless of season
                    _categories = result.Data.Where(c => c.IsActive).OrderBy(c => c.SortOrder).ToList();
                    CategoryComboBox.ItemsSource = _categories;

                }
                else
                {

                    _notificationService.ShowError("Failed to load categories", result.ErrorMessage ?? "Unknown error");
                }
            }
            catch (Exception ex)
            {

                _notificationService.ShowError("Error loading categories", ex.Message);
            }
        }

        private async Task LoadLayoutInfoAsync()
        {
            try
            {
                if (!string.IsNullOrEmpty(_template.LayoutId))
                {
                    var layoutResult = await _databaseService.GetTemplateLayoutAsync(_template.LayoutId);
                    if (layoutResult.Success && layoutResult.Data != null)
                    {
                        _template.Layout = layoutResult.Data;
                    }
                    else
                    {
                        _notificationService.ShowError("Failed to load layout", layoutResult.ErrorMessage ?? "Unknown error");
                    }
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError("Error loading layout", ex.Message);
            }
        }

        private void PopulateFields()
        {
            // Header
            TemplateNameHeader.Text = _template.Name;
            
            // Basic Information
            NameTextBox.Text = _template.Name;
            DescriptionTextBox.Text = _template.Description ?? "";
            
            // Set selected category
            if (_template.CategoryId > 0)
            {
                CategoryComboBox.SelectedValue = _template.CategoryId;
            }
            
            // Set selected template type
            TemplateTypeComboBox.SelectedIndex = (int)_template.TemplateType;
            
            // Pricing & Settings
            PriceTextBox.Text = _template.Price.ToString("F2", CultureInfo.InvariantCulture);
            SortOrderTextBox.Text = _template.SortOrder.ToString();
            IsActiveCheckBox.IsChecked = _template.IsActive;
            
            // Template Information (Read-only)
            DimensionsText.Text = $"{_template.Width} × {_template.Height} px";
            PhotoCountText.Text = _template.PhotoCount.ToString();
            FileSizeText.Text = FormatFileSize(_template.FileSize);
            UploadDateText.Text = _template.UploadedAt.ToString("MMMM dd, yyyy");
            
            // Layout Information
            if (!string.IsNullOrEmpty(_template.LayoutId))
            {
                var layoutName = _template.Layout?.Name ?? _template.LayoutId;
                var layoutKey = _template.Layout?.LayoutKey ?? "Unknown";
                LayoutText.Text = $"{layoutName} ({layoutKey})";
            }
            else
            {
                LayoutText.Text = "No layout assigned";
            }
            
            // Update status texts
            UpdateActiveStatusText();
            
            // Update file info display
            UpdateFileInfoDisplay();
        }

        private void UpdateActiveStatusText()
        {
            if (IsActiveCheckBox.IsChecked == true)
            {
                ActiveStatusText.Text = "Enabled";
                ActiveStatusText.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Green
            }
            else
            {
                ActiveStatusText.Text = "Disabled";
                ActiveStatusText.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red
            }
        }

        private async Task SaveChangesAsync()
        {
            try
            {
                // Validate inputs
                if (string.IsNullOrWhiteSpace(NameTextBox.Text))
                {
                    _notificationService.ShowWarning("Validation Error", "Template name is required.");
                    NameTextBox.Focus();
                    return;
                }

                if (!decimal.TryParse(PriceTextBox.Text, NumberStyles.Currency | NumberStyles.Number, CultureInfo.InvariantCulture, out decimal price) || price < 0)
                {
                    _notificationService.ShowWarning("Validation Error", "Please enter a valid price (0 or greater).");
                    PriceTextBox.Focus();
                    return;
                }

                if (!int.TryParse(SortOrderTextBox.Text, out int sortOrder))
                {
                    _notificationService.ShowWarning("Validation Error", "Please enter a valid sort order number.");
                    SortOrderTextBox.Focus();
                    return;
                }

                // Photo count is now read-only and derived from PhotoAreas in config
                int photoCount = _template.PhotoCount;

                var selectedCategoryId = CategoryComboBox.SelectedValue as int? ?? 0;
                if (selectedCategoryId == 0)
                {
                    _notificationService.ShowWarning("Validation Error", "Please select a category.");
                    CategoryComboBox.Focus();
                    return;
                }

                // Get selected template type
                var selectedTemplateType = (TemplateType)(TemplateTypeComboBox.SelectedIndex);
                if (TemplateTypeComboBox.SelectedIndex < 0)
                {
                    _notificationService.ShowWarning("Validation Error", "Please select a template type.");
                    TemplateTypeComboBox.Focus();
                    return;
                }

                // Disable save button to prevent double-clicks
                SaveButton.IsEnabled = false;
                SaveButton.Content = "Saving...";

                var newTemplateName = NameTextBox.Text.Trim();
                var nameChanged = newTemplateName != _template.Name;
                string newFolderPath = _template.FolderPath;
                string newTemplatePath = _template.TemplatePath;
                string newPreviewPath = _template.PreviewPath;

                // Handle folder renaming if name changed
                if (nameChanged && !string.IsNullOrEmpty(_template.FolderPath))
                {
                    try
                    {


                        var currentFolderPath = GetActualTemplateFolderPath();
                        var layoutFolder = Path.GetDirectoryName(currentFolderPath);


                        var sanitizedName = SanitizeFileName(newTemplateName);

                        // Maintain layout-based structure: Templates/layout-folder/template-folder
                        if (layoutFolder != null)
                        {
                            newFolderPath = Path.Combine(layoutFolder, sanitizedName);
                        }
                        else
                        {
                            // Fallback: keep in same directory as current template
                            var templatesBaseFolder = GetTemplatesFolderPath();
                            newFolderPath = Path.Combine(templatesBaseFolder, sanitizedName);
                        }

                        // Ensure unique folder name if it already exists
                        int suffix = 1;
                        while (Directory.Exists(newFolderPath) && newFolderPath != currentFolderPath)
                        {
                            var newFolderName = $"{sanitizedName}_{suffix}";
                            if (layoutFolder != null)
                            {
                                newFolderPath = Path.Combine(layoutFolder, newFolderName);
                            }
                            else
                            {
                                var templatesBaseFolder = GetTemplatesFolderPath();
                                newFolderPath = Path.Combine(templatesBaseFolder, newFolderName);
                            }
                            suffix++;

                        }


                        // Only rename if the path actually changed
                        if (newFolderPath != currentFolderPath)
                        {
                            if (Directory.Exists(currentFolderPath))
                            {


                                // Rename in runtime folder (bin/Debug/Templates)
                                Directory.Move(currentFolderPath, newFolderPath);
                                
                                // Verify the move was successful
                                var sourceStillExists = Directory.Exists(currentFolderPath);
                                var destinationExists = Directory.Exists(newFolderPath);


                                if (!sourceStillExists && destinationExists)
                                {

                                    // Also rename in project source folder (PhotoBooth/Templates)
                                    try
                                    {
                                        var currentTemplateFolderName = Path.GetFileName(currentFolderPath);
                                        var layoutFolderName = Path.GetFileName(Path.GetDirectoryName(currentFolderPath));
                                        var newTemplateFolderName = Path.GetFileName(newFolderPath);
                                        
                                        if (!string.IsNullOrEmpty(currentTemplateFolderName) && !string.IsNullOrEmpty(layoutFolderName) && !string.IsNullOrEmpty(newTemplateFolderName))
                                        {
                                            // Get project root directory using robust method
                                            var currentDir = AppDomain.CurrentDomain.BaseDirectory;
                                            var projectRoot = FindProjectRoot(currentDir);
                                            
                                            if (projectRoot != null)
                                            {
                                                var currentSourcePath = Path.Combine(projectRoot, "Templates", layoutFolderName, currentTemplateFolderName);
                                                var newSourcePath = Path.Combine(projectRoot, "Templates", layoutFolderName, newTemplateFolderName);
                                            
                                            if (Directory.Exists(currentSourcePath))
                                            {

                                                Directory.Move(currentSourcePath, newSourcePath);

                                            }
                                            else
                                            {

                                            }
                                            }
                                            else
                                            {
                                                // Project root not found, skip source folder renaming
                                                Console.WriteLine("Warning: Could not find project root directory - skipping source template folder rename");
                                            }
                                        }
                                    }
                                    catch
                                    {

                                        // Continue anyway since runtime folder was successfully renamed
                                    }

                                    // Update all file paths
                                    newTemplatePath = Path.Combine(newFolderPath, "template.png");
                                    
                                    // Detect actual preview file extension
                                    var previewExtensions = new[] { ".png", ".jpg", ".jpeg" };
                                    var actualPreviewFile = previewExtensions
                                        .Select(ext => Path.Combine(newFolderPath, $"preview{ext}"))
                                        .FirstOrDefault(File.Exists);
                                    
                                    if (actualPreviewFile != null)
                                    {
                                        newPreviewPath = actualPreviewFile;

                                    }
                                    else
                                    {
                                        // Fallback to .png if no preview file found
                                        newPreviewPath = Path.Combine(newFolderPath, "preview.png");

                                    }


                                }
                                else
                                {


                                    throw new InvalidOperationException("Folder rename operation did not complete successfully");
                                }
                            }
                            else
                            {

                            }
                        }
                        else
                        {

                        }

                    }
                    catch (Exception ex)
                    {


                        _notificationService.ShowError("Folder Rename Error", $"Failed to rename template folder: {ex.Message}");
                        // Continue with the update but keep old paths
                    }
                }

                // Get the selected category name
                var selectedCategory = _categories.FirstOrDefault(c => c.Id == selectedCategoryId);
                var categoryName = selectedCategory?.Name ?? "Classic";
                
                // Update template properties
                var updatedTemplate = new Template
                {
                    Id = _template.Id,
                    Name = newTemplateName,
                    Description = DescriptionTextBox.Text.Trim(),
                    CategoryId = selectedCategoryId,
                    CategoryName = categoryName,
                    Price = price,
                    SortOrder = sortOrder,
                    IsActive = IsActiveCheckBox.IsChecked == true,
                    TemplateType = selectedTemplateType,
                    LayoutId = _template.LayoutId,
                    FolderPath = newFolderPath,
                    TemplatePath = newTemplatePath,
                    PreviewPath = newPreviewPath,
                    FileSize = _template.FileSize,
                    UploadedAt = _template.UploadedAt,
                    UploadedBy = _template.UploadedBy,
                    ValidationWarnings = _template.ValidationWarnings,
                    Layout = _template.Layout // Preserve layout for computed properties
                };

                // Save to database


                var result = await _databaseService.UpdateTemplateAsync(
                    _template.Id,
                    name: updatedTemplate.Name,
                    isActive: updatedTemplate.IsActive,
                    price: updatedTemplate.Price,
                    categoryId: updatedTemplate.CategoryId,
                    description: updatedTemplate.Description,
                    sortOrder: updatedTemplate.SortOrder,
                    photoCount: updatedTemplate.PhotoCount,
                    templateType: updatedTemplate.TemplateType
                );

                if (!result.Success)
                {

                }

                if (result.Success)
                {
                    // Update database paths if they changed
                    if (nameChanged && newFolderPath != _template.FolderPath)
                    {


                        // Config path removed in layout-based system

                        var pathUpdateResult = await _databaseService.UpdateTemplatePathsAsync(
                            _template.Id,
                            newFolderPath,
                            newTemplatePath,
                            newPreviewPath
                        );

                        if (!pathUpdateResult.Success)
                        {

                        }
                        else
                        {
                            // Update the updatedTemplate object with the new paths

                            updatedTemplate.FolderPath = newFolderPath;
                            updatedTemplate.TemplatePath = newTemplatePath;
                            updatedTemplate.PreviewPath = newPreviewPath;
                            // ConfigPath removed in layout-based system

                        }
                    }
                    else
                    {

                    }

                    // Note: Config files no longer used in layout-based system

                    UpdatedTemplate = updatedTemplate;
                    EditSucceeded = true;


                    _notificationService.ShowSuccess("Template Updated", $"'{updatedTemplate.Name}' has been updated successfully.");
                    Close();
                }
                else
                {

                    _notificationService.ShowError("Update Failed", result.ErrorMessage ?? "Unknown error");
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError("Error saving template", ex.Message);
            }
            finally
            {
                // Re-enable save button
                SaveButton.IsEnabled = true;
                SaveButton.Content = "Save Changes";
            }
        }



        private static string FormatFileSize(long bytes)
        {
            if (bytes == 0) return "0 B";
            
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;
            
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size = size / 1024;
            }
            
            return $"{size:0.##} {sizes[order]}";
        }

        private static string SanitizeFileName(string fileName)
        {
            // Remove invalid characters for folder names
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = fileName;
            
            foreach (var invalidChar in invalidChars)
            {
                sanitized = sanitized.Replace(invalidChar, '_');
            }
            
            // Also replace some additional problematic characters
            sanitized = sanitized.Replace(' ', '-')  // Replace spaces with hyphens
                                .Replace("--", "-")   // Replace double hyphens with single
                                .Trim('-', '_');      // Remove leading/trailing hyphens and underscores
            
            // Ensure it's not empty
            if (string.IsNullOrWhiteSpace(sanitized))
            {
                sanitized = "template";
            }
            
            return sanitized;
        }

        /// <summary>
        /// Find project root directory using robust method instead of fragile ".." navigation
        /// </summary>
        private static string? FindProjectRoot(string startPath)
        {
            var directory = new DirectoryInfo(startPath);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "PhotoBooth.csproj")))
            {
                directory = directory.Parent;
            }
            return directory?.FullName;
        }

        #endregion

        #region Static Methods

        /// <summary>
        /// Show the template editor dialog
        /// </summary>
        public static Task<Template?> ShowEditorAsync(Template template, IDatabaseService databaseService, NotificationService notificationService, Window? owner = null, Action<int>? refreshTemplateCallback = null)
        {
            var dialog = new TemplateEditorDialog(template, databaseService, notificationService, refreshTemplateCallback);
            
            if (owner != null)
            {
                dialog.Owner = owner;
            }
            else
            {
                dialog.Owner = Application.Current.MainWindow;
            }
            
            dialog.ShowDialog();
            
            return Task.FromResult(dialog.EditSucceeded ? dialog.UpdatedTemplate : null);
        }

        #endregion
    }
}
 

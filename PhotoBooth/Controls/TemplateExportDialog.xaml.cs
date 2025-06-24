using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Photobooth.Models;
using Photobooth.Services;

namespace Photobooth.Controls
{
    public partial class TemplateExportDialog : Window
    {
        #region Private Fields

        private readonly IDatabaseService _databaseService;
        private readonly List<Template> _allTemplates;
        private readonly HashSet<int> _selectedTemplateIds;
        private List<TemplateCategory> _categories = new List<TemplateCategory>();

        #endregion

        #region Properties

        public bool ExportCompleted { get; private set; } = false;

        #endregion

        #region Constructor

        public TemplateExportDialog(IDatabaseService databaseService, List<Template> allTemplates, HashSet<int> selectedTemplateIds)
        {

            try
            {

                InitializeComponent();


                _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));

                _allTemplates = allTemplates ?? throw new ArgumentNullException(nameof(allTemplates));

                _selectedTemplateIds = selectedTemplateIds ?? throw new ArgumentNullException(nameof(selectedTemplateIds));


                Loaded += TemplateExportDialog_Loaded;


            }
            catch
            {


                throw;
            }
        }

        #endregion

        #region Event Handlers

        private async void TemplateExportDialog_Loaded(object sender, RoutedEventArgs e)
        {

            try
            {

                await LoadCategoriesAsync();


                UpdateCounts();


                UpdateSummary();


                UpdateExportButtonState();


            }
            catch
            {


                // Don't rethrow here, just log the error to prevent dialog from crashing
                // Set some default values to ensure the dialog can still be used
                try
                {
                    SelectedCountText.Text = "0 templates selected";
                    AllCountText.Text = "0 total templates";
                    SummaryText.Text = "Ready to export templates as ZIP file";
                }
                catch
                {
                    // Ignore errors setting default text
                }
            }
        }

        private void ExportOption_Changed(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CategoryComboBox != null)
                {
                    CategoryComboBox.IsEnabled = ExportCategoryRadio?.IsChecked == true;
                    
                    // Reset category validation when switching away from category export
                    if (ExportCategoryRadio?.IsChecked != true)
                    {
                        HideCategoryValidationError();
                    }
                    else
                    {
                        // Validate category selection when switching to category export
                        ValidateCategorySelection();
                    }
                }
                
                // Only update summary if all required controls are loaded
                if (SummaryText != null && _allTemplates != null && _selectedTemplateIds != null)
                {
                    UpdateSummary();
                }
                
                // Update export button state
                UpdateExportButtonState();
            }
            catch
            {

                // Don't crash the dialog for this
            }
        }

        private void CategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                ValidateCategorySelection();
                UpdateSummary();
                UpdateExportButtonState();
            }
            catch
            {

            }
        }

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate export requirements first
                if (ExportCategoryRadio?.IsChecked == true && CategoryComboBox?.SelectedItem == null)
                {
                    MessageBox.Show("Please select a category to export.", "Export Templates", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ValidateCategorySelection(); // Show visual validation error
                    return;
                }
                
                // Get templates to export
                var templatesToExport = GetTemplatesToExport();
                
                if (!templatesToExport.Any())
                {
                    string message = "No templates to export.";
                    
                    // Provide more specific message based on export type
                    if (ExportSelectedRadio?.IsChecked == true)
                    {
                        message = "No templates are selected for export. Please select templates first.";
                    }
                    else if (ExportCategoryRadio?.IsChecked == true && CategoryComboBox?.SelectedItem is TemplateCategory category)
                    {
                        message = $"No templates found in the '{category.Name}' category.";
                    }
                    
                    MessageBox.Show(message, "Export Templates", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Show save file dialog
                var saveFileDialog = new SaveFileDialog
                {
                    Title = "Export Templates",
                    Filter = "ZIP files (*.zip)|*.zip",
                    FileName = $"PhotoBooth_Templates_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.zip"
                };

                if (saveFileDialog.ShowDialog() != true)
                    return;

                // Disable the export button and show progress
                ExportButton.IsEnabled = false;
                ExportButton.Content = "Exporting...";

                // Perform the export
                var success = await ExportTemplatesAsync(templatesToExport, saveFileDialog.FileName);

                if (success)
                {
                    ExportCompleted = true;
                    NotificationService.Instance.ShowSuccess("Export Complete", 
                        $"Successfully exported {templatesToExport.Count} template(s) to {Path.GetFileName(saveFileDialog.FileName)}");
                    Close();
                }
                else
                {
                    MessageBox.Show("Export failed. Please try again.", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export error: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ExportButton.IsEnabled = true;
                ExportButton.Content = new StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    Children =
                    {
                        new System.Windows.Controls.TextBlock { Text = "📥", FontSize = 14, Margin = new Thickness(0,0,8,0) },
                        new System.Windows.Controls.TextBlock { Text = "Export Templates", FontSize = 14, FontWeight = FontWeights.Medium }
                    }
                };
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion

        #region Validation Methods

        private bool ValidateCategorySelection()
        {
            try
            {
                // Only validate if Export by Category is selected
                if (ExportCategoryRadio?.IsChecked != true)
                {
                    HideCategoryValidationError();
                    return true;
                }

                // Check if a category is selected
                bool isValid = CategoryComboBox?.SelectedItem != null;

                if (isValid)
                {
                    HideCategoryValidationError();
                }
                else
                {
                    ShowCategoryValidationError();
                }

                return isValid;
            }
            catch
            {

                return false;
            }
        }

        private void ShowCategoryValidationError()
        {
            try
            {
                if (CategoryComboBox != null && CategoryErrorText != null)
                {
                    CategoryComboBox.Style = (Style)FindResource("ComboBoxErrorStyle");
                    CategoryErrorText.Visibility = Visibility.Visible;
                }
            }
            catch
            {

            }
        }

        private void HideCategoryValidationError()
        {
            try
            {
                if (CategoryComboBox != null && CategoryErrorText != null)
                {
                    CategoryComboBox.ClearValue(StyleProperty);
                    CategoryErrorText.Visibility = Visibility.Collapsed;
                }
            }
            catch
            {

            }
        }

        private bool IsExportValid()
        {
            try
            {
                // Check if Export by Category is selected and validate it
                if (ExportCategoryRadio?.IsChecked == true)
                {
                    return ValidateCategorySelection();
                }

                // For other export options, check if we have templates to export
                var templates = GetTemplatesToExport();
                return templates.Any();
            }
            catch
            {

                return false;
            }
        }

        private void UpdateExportButtonState()
        {
            try
            {
                if (ExportButton != null)
                {
                    ExportButton.IsEnabled = IsExportValid();
                }
            }
            catch
            {

            }
        }

        #endregion

        #region Private Methods

        private async Task LoadCategoriesAsync()
        {
            try
            {
                var result = await _databaseService.GetAllTemplateCategoriesAsync();
                if (result.Success && result.Data != null)
                {
                    _categories = result.Data.OrderBy(c => c.Name).ToList();
                    CategoryComboBox.ItemsSource = _categories;
                    
                    if (_categories.Any())
                    {
                        CategoryComboBox.SelectedIndex = 0;
                    }
                    
                    // Update export button state after categories are loaded
                    UpdateExportButtonState();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading categories: {ex.Message}");
            }
        }

        private void UpdateCounts()
        {
            var activeTemplates = _allTemplates.Where(t => t.IsActive).Count();
            var totalTemplates = _allTemplates.Count;
            var selectedCount = _selectedTemplateIds.Count;

            SelectedCountText.Text = $"{selectedCount} template{(selectedCount == 1 ? "" : "s")} selected";
            AllCountText.Text = $"{totalTemplates} total template{(totalTemplates == 1 ? "" : "s")} ({activeTemplates} active)";
        }

        private void UpdateSummary()
        {
            try
            {
                var templatesToExport = GetTemplatesToExport();
                var count = templatesToExport?.Count ?? 0;
                
                var organizeText = OrganizeByCategoryCheckBox?.IsChecked == true ? "organized by layout structure" : "in flat structure";
                var includeInactiveText = IncludeInactiveCheckBox?.IsChecked == true ? " (including inactive)" : "";
                var includePreviewsText = IncludePreviewsCheckBox?.IsChecked == true ? " with preview images" : "";
                
                if (SummaryText != null)
                {
                    SummaryText.Text = $"Ready to export {count} template{(count == 1 ? "" : "s")}{includeInactiveText} as ZIP file, {organizeText}{includePreviewsText}.";
                }
                
                // Update export button state when summary changes
                UpdateExportButtonState();
            }
            catch
            {

                // Set a safe default
                if (SummaryText != null)
                {
                    SummaryText.Text = "Ready to export templates as ZIP file";
                }
            }
        }

        private List<Template> GetTemplatesToExport()
        {
            try
            {
                var templates = new List<Template>();
                
                // Return empty list if data isn't ready yet
                if (_allTemplates == null || _selectedTemplateIds == null)
                {
                    return templates;
                }
                
                if (ExportSelectedRadio?.IsChecked == true)
                {
                    templates = _allTemplates.Where(t => _selectedTemplateIds.Contains(t.Id)).ToList();
                }
                else if (ExportAllRadio?.IsChecked == true)
                {
                    templates = _allTemplates.ToList();
                }
                else if (ExportCategoryRadio?.IsChecked == true && CategoryComboBox?.SelectedItem is TemplateCategory category)
                {
                    templates = _allTemplates.Where(t => t.CategoryId == category.Id).ToList();
                }

                // Filter by active status if needed
                if (IncludeInactiveCheckBox?.IsChecked != true)
                {
                    templates = templates.Where(t => t.IsActive).ToList();
                }

                return templates;
            }
            catch
            {

                return new List<Template>(); // Return empty list on error
            }
        }

        private async Task<bool> ExportTemplatesAsync(List<Template> templates, string zipFilePath)
        {
            try
            {
                using (var archive = ZipFile.Open(zipFilePath, ZipArchiveMode.Create))
                {
                    var organizeByCategory = OrganizeByCategoryCheckBox?.IsChecked == true;
                    var includePreviews = IncludePreviewsCheckBox?.IsChecked == true;

                    foreach (var template in templates)
                    {
                        await ExportTemplateToArchiveAsync(archive, template, organizeByCategory, includePreviews);
                    }

                    // Add export manifest
                    await AddExportManifestAsync(archive, templates);
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Export error: {ex.Message}");
                return false;
            }
        }

        private async Task ExportTemplateToArchiveAsync(ZipArchive archive, Template template, bool organizeByCategory, bool includePreviews)
        {
            try
            {
                var templateFolderPath = template.FolderPath;
                if (string.IsNullOrEmpty(templateFolderPath) || !Directory.Exists(templateFolderPath))
                {
                    System.Diagnostics.Debug.WriteLine($"Template folder not found: {templateFolderPath}");
                    return;
                }

                // Determine the base path in the ZIP - match the original structure Layout/Template
                string basePath;
                
                if (organizeByCategory)
                {
                    // Use Layout structure: Layout/Template (matches original folder structure)
                    var layoutKey = template.Layout?.LayoutKey ?? "Unknown-Layout";
                    basePath = $"{SanitizeFileName(layoutKey)}/{SanitizeFileName(template.Name)}";
                }
                else
                {
                    // Flat structure: just template name
                    basePath = SanitizeFileName(template.Name);
                }

                // Copy all files from the template folder
                var templateFiles = Directory.GetFiles(templateFolderPath, "*", SearchOption.AllDirectories);
                
                foreach (var filePath in templateFiles)
                {
                    var relativePath = Path.GetRelativePath(templateFolderPath, filePath);
                    var zipEntryPath = $"{basePath}/{relativePath}".Replace('\\', '/');

                    // Skip preview images if not including them
                    if (!includePreviews && IsPreviewFile(filePath))
                        continue;

                    var entry = archive.CreateEntry(zipEntryPath);
                    entry.LastWriteTime = File.GetLastWriteTime(filePath);

                    using (var entryStream = entry.Open())
                    using (var fileStream = File.OpenRead(filePath))
                    {
                        await fileStream.CopyToAsync(entryStream);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error exporting template {template.Name}: {ex.Message}");
            }
        }

        private async Task AddExportManifestAsync(ZipArchive archive, List<Template> templates)
        {
            try
            {
                var manifest = new
                {
                    ExportDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    TemplateCount = templates.Count,
                    ExportedBy = "PhotoBooth Template Manager",
                    ExportStructure = OrganizeByCategoryCheckBox?.IsChecked == true ? "Layout/Template" : "Flat",
                    Templates = templates.Select(t => new
                    {
                        t.Id,
                        t.Name,
                        Category = t.CategoryName,
                        Layout = t.Layout?.LayoutKey ?? "Unknown-Layout",
                        t.IsActive,
                        t.Price,
                        ExportPath = OrganizeByCategoryCheckBox?.IsChecked == true
                            ? $"{SanitizeFileName(t.Layout?.LayoutKey ?? "Unknown-Layout")}/{SanitizeFileName(t.Name)}"
                            : SanitizeFileName(t.Name)
                    }).ToList()
                };

                var manifestJson = System.Text.Json.JsonSerializer.Serialize(manifest, new System.Text.Json.JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });

                var manifestEntry = archive.CreateEntry("export_manifest.json");
                using (var entryStream = manifestEntry.Open())
                using (var writer = new StreamWriter(entryStream))
                {
                    await writer.WriteAsync(manifestJson);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating export manifest: {ex.Message}");
            }
        }

        private static string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());
        }

        private static bool IsPreviewFile(string filePath)
        {
            var fileName = Path.GetFileName(filePath).ToLowerInvariant();
            return fileName.Contains("preview") || fileName.Contains("thumb");
        }

        #endregion

        #region Static Methods

        public static Task<bool> ShowExportDialogAsync(Window? owner, IDatabaseService? databaseService, 
            List<Template>? allTemplates, HashSet<int>? selectedTemplateIds)
        {

            try
            {


                // Defensive null checks
                if (databaseService == null)
                {

                    throw new ArgumentNullException(nameof(databaseService));
                }
                
                if (allTemplates == null)
                {

                    throw new ArgumentNullException(nameof(allTemplates));
                }
                
                if (selectedTemplateIds == null)
                {

                    throw new ArgumentNullException(nameof(selectedTemplateIds));
                }

                var dialog = new TemplateExportDialog(databaseService, allTemplates, selectedTemplateIds);


                if (owner != null)
                {

                    dialog.Owner = owner;
                }
                else
                {

                    dialog.Owner = Application.Current.MainWindow;
                }


                dialog.ShowDialog();

                var result = dialog.ExportCompleted;


                return Task.FromResult(result);
            }
            catch
            {


                throw;
            }
        }

        #endregion
    }
}

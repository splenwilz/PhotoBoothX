using System;
using System.Windows;
using System.Windows.Controls;
using Photobooth.Models;
using Photobooth.Services;

namespace Photobooth.Controls
{
    public partial class TemplateActionsDialog : UserControl
    {
        private Template? _selectedTemplate;
        
        public Template? SelectedTemplate 
        { 
            get => _selectedTemplate;
            set 
            {
                _selectedTemplate = value;
                UpdateUI();
            }
        }

        public event EventHandler<string>? ActionSelected;

        public TemplateActionsDialog()
        {
            InitializeComponent();
        }

        public TemplateActionsDialog(Template template) : this()
        {
            SelectedTemplate = template;
        }

        private void UpdateUI()
        {
            // Guard against calls before InitializeComponent() or with null template
            if (SelectedTemplate == null || TemplateNameText == null) return;

            TemplateNameText.Text = SelectedTemplate.Name;
            
            // Update toggle button text based on template status
            ToggleButtonTitle.Text = SelectedTemplate.IsActive ? "Disable Template" : "Enable Template";
            ToggleButtonDescription.Text = SelectedTemplate.IsActive 
                ? "Hide this template from users" 
                : "Make this template available to users";
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CloseDialog();
            }
            catch (Exception ex)
            {
                HandleEventError("closing dialog", ex);
            }
        }

        private void EditTemplate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ActionSelected?.Invoke(this, "Edit");
                CloseDialog();
            }
            catch (Exception ex)
            {
                HandleEventError("editing template", ex);
            }
        }

        private void DuplicateTemplate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ActionSelected?.Invoke(this, "Duplicate");
                CloseDialog();
            }
            catch (Exception ex)
            {
                HandleEventError("duplicating template", ex);
            }
        }

        private void RenameTemplate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ActionSelected?.Invoke(this, "Rename");
                CloseDialog();
            }
            catch (Exception ex)
            {
                HandleEventError("renaming template", ex);
            }
        }

        private void ToggleTemplate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ActionSelected?.Invoke(this, "Toggle");
                CloseDialog();
            }
            catch (Exception ex)
            {
                HandleEventError("toggling template status", ex);
            }
        }

        private void DeleteTemplate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ActionSelected?.Invoke(this, "Delete");
                CloseDialog();
            }
            catch (Exception ex)
            {
                HandleEventError("deleting template", ex);
            }
        }

        private void CloseDialog()
        {
            try
            {
                LoggingService.Application.Information("Template actions dialog close initiated");
                
                var parentWindow = Window.GetWindow(this);
                if (parentWindow != null && parentWindow != Application.Current.MainWindow)
                {
                    // This is a standalone modal dialog window - close it properly
                    LoggingService.Application.Information("Found standalone modal window, setting DialogResult to close");
                    parentWindow.DialogResult = false;
                }
                else
                {
                    // This is likely shown via ModalService as an overlay - use ModalService to close
                    LoggingService.Application.Information("Modal shown via ModalService, using HideModal()");
                    ModalService.Instance.HideModal();
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error closing template actions dialog", ex);
                // Fallback: try ModalService first, then window methods
                try
                {
                    if (ModalService.Instance.IsModalShown)
                    {
                        ModalService.Instance.HideModal();
                    }
                    else
                    {
                        var parentWindow = Window.GetWindow(this);
                        parentWindow?.Hide();
                    }
                }
                catch
                {
                    // Ignore secondary errors to prevent infinite error loops
                }
            }
        }

        private void HandleEventError(string operation, Exception ex)
        {
            // Log the error using the application's logging service
            LoggingService.Application.Error($"Template action error while {operation}", ex,
                ("TemplateName", SelectedTemplate?.Name ?? "Unknown"),
                ("Operation", operation));
            
            // Show kiosk-friendly error notification instead of MessageBox
            NotificationService.Instance.ShowError(
                "Template Action Error", 
                $"An error occurred while {operation}. Please try again.");
            
            // Ensure dialog closes even on error to prevent UI lock-up
            try
            {
                CloseDialog();
            }
            catch
            {
                // If even closing fails, try to close parent window directly
                try
                {
                    Window.GetWindow(this)?.Close();
                }
                catch
                {
                    // Last resort - at least we tried
                }
            }
        }
    }
} 
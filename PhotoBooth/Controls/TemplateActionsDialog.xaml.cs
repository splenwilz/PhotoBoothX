using System;
using System.Windows;
using System.Windows.Controls;
using Photobooth.Models;

namespace Photobooth.Controls
{
    public partial class TemplateActionsDialog : UserControl
    {
        public new Template? Template { get; set; }

        public event EventHandler<string>? ActionSelected;

        public TemplateActionsDialog()
        {
            InitializeComponent();
        }

        public TemplateActionsDialog(Template template) : this()
        {
            Template = template;
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (Template == null) return;

            TemplateNameText.Text = Template.Name;
            
            // Update toggle button text based on template status
            ToggleButtonTitle.Text = Template.IsActive ? "Disable Template" : "Enable Template";
            ToggleButtonDescription.Text = Template.IsActive 
                ? "Hide this template from users" 
                : "Make this template available to users";
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseDialog();
        }

        private void EditTemplate_Click(object sender, RoutedEventArgs e)
        {
            ActionSelected?.Invoke(this, "Edit");
            CloseDialog();
        }

        private void DuplicateTemplate_Click(object sender, RoutedEventArgs e)
        {
            ActionSelected?.Invoke(this, "Duplicate");
            CloseDialog();
        }

        private void RenameTemplate_Click(object sender, RoutedEventArgs e)
        {
            ActionSelected?.Invoke(this, "Rename");
            CloseDialog();
        }

        private void ToggleTemplate_Click(object sender, RoutedEventArgs e)
        {
            ActionSelected?.Invoke(this, "Toggle");
            CloseDialog();
        }

        private void DeleteTemplate_Click(object sender, RoutedEventArgs e)
        {
            ActionSelected?.Invoke(this, "Delete");
            CloseDialog();
        }

        private void CloseDialog()
        {
            // Close the parent window
            Window parentWindow = Window.GetWindow(this);
            parentWindow?.Close();
        }
    }
} 
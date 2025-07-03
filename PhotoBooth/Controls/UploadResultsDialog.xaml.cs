using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Photobooth.Models;

namespace Photobooth.Controls
{
    public partial class UploadResultsDialog : Window
    {
        public UploadResultsDialog()
        {
            InitializeComponent();
        }

        public void SetResults(TemplateUploadResult result)
        {
            // Set icon and title based on result
            if (result.SuccessCount > 0 && result.FailureCount == 0)
            {
                IconText.Text = "✅";
                IconText.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94)); // Green
                TitleText.Text = "Upload Completed Successfully";
                TitleText.Foreground = new SolidColorBrush(Color.FromRgb(31, 41, 55)); // Dark gray
            }
            else if (result.SuccessCount > 0 && result.FailureCount > 0)
            {
                IconText.Text = "⚠️";
                IconText.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11)); // Amber
                TitleText.Text = "Upload Completed with Warnings";
                TitleText.Foreground = new SolidColorBrush(Color.FromRgb(31, 41, 55)); // Dark gray
            }
            else
            {
                IconText.Text = "❌";
                IconText.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red
                TitleText.Text = "Upload Failed";
                TitleText.Foreground = new SolidColorBrush(Color.FromRgb(31, 41, 55)); // Dark gray
            }

            // Set summary text
            SummaryText.Text = result.Message;

            // Clear and populate details
            DetailsPanel.Children.Clear();

            if (result.Results.Any())
            {
                foreach (var templateResult in result.Results)
                {
                    var templateBorder = CreateTemplateItem(templateResult);
                    DetailsPanel.Children.Add(templateBorder);
                }
            }
            else
            {
                // No detailed results available
                var noDetailsText = new TextBlock
                {
                    Text = "No detailed results available.",
                    FontStyle = FontStyles.Italic,
                    Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                    Margin = new Thickness(0, 8, 0, 0)
                };
                DetailsPanel.Children.Add(noDetailsText);
            }
        }

        private Border CreateTemplateItem(TemplateValidationResult templateResult)
        {
            var border = new Border
            {
                Margin = new Thickness(0, 4, 0, 0)
            };

            var panel = new StackPanel();
            border.Child = panel;

            // Determine style and status
            string statusText;
            string statusIcon;
            if (templateResult.IsValid)
            {
                border.Style = (Style)FindResource("SuccessItemStyle");
                statusText = "Success";
                statusIcon = "✅";
            }
            else if (templateResult.Warnings.Any() && !templateResult.Errors.Any())
            {
                border.Style = (Style)FindResource("WarningItemStyle");
                statusText = "Warning";
                statusIcon = "⚠️";
            }
            else
            {
                border.Style = (Style)FindResource("ErrorItemStyle");
                statusText = "Failed";
                statusIcon = "❌";
            }

            // Template name header
            var headerPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 4)
            };

            headerPanel.Children.Add(new TextBlock
            {
                Text = statusIcon,
                FontSize = 14,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            });

            headerPanel.Children.Add(new TextBlock
            {
                Text = templateResult.Template?.Name ?? "Unknown Template",
                FontWeight = FontWeights.SemiBold,
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(31, 41, 55)),
                VerticalAlignment = VerticalAlignment.Center
            });

            headerPanel.Children.Add(new TextBlock
            {
                Text = statusText,
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            });

            panel.Children.Add(headerPanel);

            // Errors
            foreach (var error in templateResult.Errors)
            {
                var errorText = new TextBlock
                {
                    Text = $"• {error}",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)), // Red
                    Margin = new Thickness(22, 2, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                };
                panel.Children.Add(errorText);
            }

            // Warnings
            foreach (var warning in templateResult.Warnings)
            {
                var warningText = new TextBlock
                {
                    Text = $"• {warning}",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11)), // Amber
                    Margin = new Thickness(22, 2, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                };
                panel.Children.Add(warningText);
            }

            return border;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Show the upload results dialog
        /// </summary>
        public static void ShowResults(TemplateUploadResult result, Window? owner = null)
        {
            var dialog = new UploadResultsDialog();
            dialog.SetResults(result);
            
            if (owner != null)
            {
                dialog.Owner = owner;
            }
            else
            {
                // Try to find the main window
                dialog.Owner = Application.Current.MainWindow;
            }
            
            dialog.ShowDialog();
        }
    }
} 
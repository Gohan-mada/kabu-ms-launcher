using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;

namespace KabuMSLauncher
{
    public partial class InfoDialog : Window
    {
        public string HeaderText { get; set; } = string.Empty;
        public string BodyText { get; set; } = string.Empty;
        public string LinkText { get; set; } = string.Empty;
        public string LinkUrl { get; set; } = string.Empty;

        public InfoDialog()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                HeaderTextBlock.Text = HeaderText;
                BodyTextBlock.Text = BodyText;
                if (!string.IsNullOrEmpty(LinkText) && !string.IsNullOrEmpty(LinkUrl))
                {
                    LinkRunText.Text = "🔗 " + LinkText;
                    LinkHyperlink.NavigateUri = new Uri(LinkUrl);
                    LinkBlock.Visibility = Visibility.Visible;
                }
                else
                {
                    LinkBlock.Visibility = Visibility.Collapsed;
                }
            };
        }

        private void LinkHyperlink_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var url = LinkHyperlink.NavigateUri?.ToString();
                if (!string.IsNullOrEmpty(url))
                {
                    Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                }
            }
            catch { /* best-effort */ }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}

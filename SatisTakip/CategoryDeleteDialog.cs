using System.Windows;
using System.Windows.Controls;

namespace SatisTakip;

public sealed class CategoryDeleteDialog : Window
{
    public CategoryDeleteDialog(string category, IReadOnlyList<string> productNames)
    {
        Title = "Kategori silme onayı";
        Width = 460;
        Height = productNames.Count == 0 ? 240 : 430;
        MinHeight = 240;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var panel = new DockPanel { Margin = new Thickness(22) };
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0)
        };
        var cancel = new Button { Content = "Vazgeç", Padding = new Thickness(13, 7, 13, 7), Margin = new Thickness(0, 0, 8, 0) };
        cancel.Click += (_, _) => DialogResult = false;
        var confirm = new Button
        {
            Content = "Kategoriyi sil",
            Padding = new Thickness(16, 7, 16, 7),
            Background = System.Windows.Media.Brushes.Firebrick,
            Foreground = System.Windows.Media.Brushes.White,
            IsDefault = true
        };
        confirm.Click += (_, _) => DialogResult = true;
        buttons.Children.Add(cancel);
        buttons.Children.Add(confirm);
        DockPanel.SetDock(buttons, Dock.Bottom);
        panel.Children.Add(buttons);

        var content = new StackPanel();
        content.Children.Add(new TextBlock
        {
            Text = productNames.Count == 0
                ? $"“{category}” kategorisi silinecek. Devam edilsin mi?"
                : $"“{category}” kategorisinde {productNames.Count} ürün kayıtlı. Kategori silinince ürünler korunacak ve “Genel” kategorisine taşınacak. Devam etmek istiyor musunuz?",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        });

        if (productNames.Count > 0)
        {
            content.Children.Add(new TextBlock
            {
                Text = "Bu kategoride kayıtlı ürünler:",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 6)
            });
            content.Children.Add(new ListBox { ItemsSource = productNames });
        }

        panel.Children.Add(content);
        Content = panel;
    }
}

using System.Windows;
using System.Windows.Controls;

namespace SatisTakip;

public sealed class ProductCategoryDialog : Window
{
    private readonly ComboBox categoryBox = new();

    public string? SelectedCategory { get; private set; }

    public ProductCategoryDialog(Product product, IReadOnlyList<string> categories)
    {
        Title = "Ürün kategorisini değiştir";
        Width = 380;
        Height = 210;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var panel = new StackPanel { Margin = new Thickness(22) };
        panel.Children.Add(new TextBlock
        {
            Text = $"{product.Name} için kategori seçin",
            FontSize = 17,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        });
        categoryBox.ItemsSource = categories;
        categoryBox.SelectedItem = product.Category;
        categoryBox.Padding = new Thickness(8, 6, 8, 6);
        panel.Children.Add(categoryBox);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };
        var cancel = new Button { Content = "Vazgeç", Padding = new Thickness(13, 7, 13, 7), Margin = new Thickness(0, 0, 8, 0) };
        cancel.Click += (_, _) => DialogResult = false;
        var save = new Button { Content = "Kaydet", Padding = new Thickness(16, 7, 16, 7), IsDefault = true };
        save.Click += Save_Click;
        buttons.Children.Add(cancel);
        buttons.Children.Add(save);
        panel.Children.Add(buttons);
        Content = panel;
        Loaded += (_, _) => categoryBox.Focus();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (categoryBox.SelectedItem is not string category)
        {
            MessageBox.Show("Önce bir kategori oluşturup seçin.", "Kategori gerekli", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SelectedCategory = category;
        DialogResult = true;
    }
}

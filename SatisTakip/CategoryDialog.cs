using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SatisTakip;

public sealed class CategoryDialog : Window
{
    private readonly TextBox nameBox = new();

    public string CategoryName { get; private set; } = "";

    public CategoryDialog(string? initialCategory = null)
    {
        var isEditing = !string.IsNullOrWhiteSpace(initialCategory);
        Title = isEditing ? "Kategori adını düzenle" : "Yeni kategori";
        Width = 360;
        Height = 190;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var panel = new StackPanel { Margin = new Thickness(22) };
        panel.Children.Add(new TextBlock
        {
            Text = isEditing ? "Kategori adı" : "Yeni kategori adı",
            FontSize = 17,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 10)
        });
        nameBox.Text = initialCategory ?? "";
        nameBox.MaxLength = 80;
        nameBox.Padding = new Thickness(8, 6, 8, 6);
        nameBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                SaveCategory();
                e.Handled = true;
            }
        };
        panel.Children.Add(nameBox);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };
        var cancel = new Button { Content = "Vazgeç", Padding = new Thickness(13, 7, 13, 7), Margin = new Thickness(0, 0, 8, 0) };
        cancel.Click += (_, _) => DialogResult = false;
        var save = new Button { Content = isEditing ? "Kaydet" : "Ekle", Padding = new Thickness(16, 7, 16, 7), IsDefault = true };
        save.Click += (_, _) => SaveCategory();
        buttons.Children.Add(cancel);
        buttons.Children.Add(save);
        panel.Children.Add(buttons);
        Content = panel;
        Loaded += (_, _) =>
        {
            nameBox.Focus();
            nameBox.SelectAll();
        };
    }

    private void SaveCategory()
    {
        var category = nameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(category))
        {
            MessageBox.Show("Kategori adı girin.", "Eksik bilgi", MessageBoxButton.OK, MessageBoxImage.Warning);
            nameBox.Focus();
            return;
        }

        CategoryName = category;
        DialogResult = true;
    }
}

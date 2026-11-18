using System.Windows;
using System.Windows.Controls;

namespace SatisTakip;

public sealed class ProductDialog : Window
{
    private readonly TextBox barcodeBox = new();
    private readonly TextBox nameBox = new();
    private readonly TextBox categoryBox = new();
    private readonly TextBox priceBox = new();
    private readonly TextBox costBox = new();
    private readonly TextBox stockBox = new();
    private readonly TextBox minimumStockBox = new();
    private readonly DatabaseService database = new();
    private readonly Button generateBarcodeButton = new();
    private readonly string originalBarcode;

    public Product? Product { get; private set; }

    public ProductDialog(Product? product = null)
    {
        originalBarcode = product?.Barcode ?? "";
        Title = product is null ? "Yeni ürün" : "Ürünü düzenle";
        Width = 380;
        Height = 610;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = System.Windows.Media.Brushes.White;

        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = "Yeni ürün ekle", FontSize = 22, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 18) });
        AddBarcodeField(panel);
        AddField(panel, "Ürün adı", nameBox);
        AddField(panel, "Kategori", categoryBox);
        AddField(panel, "Alış maliyeti (TL)", costBox);
        AddField(panel, "Satış fiyatı (TL)", priceBox);
        AddField(panel, "Stok miktarı", stockBox);
        AddField(panel, "Kritik stok eşiği", minimumStockBox);

        if (product is not null)
        {
            barcodeBox.Text = product.Barcode;
            nameBox.Text = product.Name;
            categoryBox.Text = product.Category;
            costBox.Text = product.CostPrice.ToString("N2");
            priceBox.Text = product.Price.ToString("N2");
            stockBox.Text = product.Stock.ToString("N2");
            minimumStockBox.Text = product.MinimumStock.ToString("N2");
        }
        else
        {
            stockBox.Text = "0";
            minimumStockBox.Text = "5";
        }

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        var cancel = new Button { Content = "Vazgeç", Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(0, 0, 8, 0) };
        cancel.Click += (_, _) => DialogResult = false;
        var save = new Button { Content = "Ürünü kaydet", Padding = new Thickness(14, 8, 14, 8), Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(23, 107, 135)), Foreground = System.Windows.Media.Brushes.White };
        save.Click += Save_Click;
        buttons.Children.Add(cancel);
        buttons.Children.Add(save);
        panel.Children.Add(buttons);
        Content = panel;
        if (product is null)
        {
            Loaded += (_, _) => barcodeBox.Focus();
        }
    }

    private void AddBarcodeField(StackPanel panel)
    {
        panel.Children.Add(new TextBlock { Text = "Barkod numarası", FontSize = 12, Foreground = System.Windows.Media.Brushes.Gray, Margin = new Thickness(0, 0, 0, 4) });
        var row = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        generateBarcodeButton.Content = "Kod üret";
        generateBarcodeButton.Padding = new Thickness(8, 6, 8, 6);
        generateBarcodeButton.Margin = new Thickness(8, 0, 0, 0);
        generateBarcodeButton.Click += GenerateBarcodeButton_Click;
        generateBarcodeButton.IsEnabled = string.IsNullOrWhiteSpace(barcodeBox.Text);
        barcodeBox.Padding = new Thickness(8, 6, 8, 6);
        barcodeBox.TextChanged += (_, _) => generateBarcodeButton.IsEnabled = string.IsNullOrWhiteSpace(barcodeBox.Text);
        DockPanel.SetDock(generateBarcodeButton, Dock.Right);
        row.Children.Add(generateBarcodeButton);
        row.Children.Add(barcodeBox);
        panel.Children.Add(row);
    }

    private void GenerateBarcodeButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(barcodeBox.Text))
        {
            return;
        }
        barcodeBox.Text = database.GenerateNextInternalBarcode();
        barcodeBox.SelectAll();
    }

    private static void AddField(StackPanel panel, string label, TextBox input)
    {
        panel.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = System.Windows.Media.Brushes.Gray, Margin = new Thickness(0, 0, 0, 4) });
        input.Margin = new Thickness(0, 0, 0, 10);
        input.Padding = new Thickness(8, 6, 8, 6);
        panel.Children.Add(input);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(nameBox.Text) || !decimal.TryParse(priceBox.Text.Replace('.', ','), out var price) || !decimal.TryParse(costBox.Text.Replace('.', ','), out var cost) || !decimal.TryParse(stockBox.Text.Replace('.', ','), out var stock) || stock < 0 || !decimal.TryParse(minimumStockBox.Text.Replace('.', ','), out var minimumStock) || minimumStock < 0)
        {
            MessageBox.Show("Ürün adı, geçerli fiyatlar ve sıfır veya daha büyük stok miktarları girin.", "Eksik bilgi", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var barcode = barcodeBox.Text.Trim();
        if (database.IsBarcodeInUse(barcode, originalBarcode))
        {
            MessageBox.Show("Bu barkod başka bir ürüne kayıtlı. Farklı bir barkod okutun veya üretin.", "Barkod kullanılıyor", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Product = new Product
        {
            Barcode = barcode,
            Name = nameBox.Text.Trim(),
            Category = string.IsNullOrWhiteSpace(categoryBox.Text) ? "Genel" : categoryBox.Text.Trim(),
            CostPrice = cost,
            Price = price,
            Stock = stock,
            MinimumStock = minimumStock
        };
        DialogResult = true;
    }
}

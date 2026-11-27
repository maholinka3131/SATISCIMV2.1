using System.Windows;
using System.Windows.Controls;
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace SatisTakip;

public sealed class ProductDialog : Window
{
    private readonly TextBox barcodeBox = new();
    private readonly TextBox nameBox = new();
    private readonly ComboBox categoryBox = new();
    private readonly TextBox priceBox = new();
    private readonly TextBox costExcludingVatBox = new();
    private readonly TextBox stockBox = new();
    private readonly TextBox minimumStockBox = new();
    private readonly DatePicker expirationDatePicker = new();
    private readonly ComboBox vatRateBox = new();
    private readonly TextBlock costIncludingVatText = new();
    private readonly DatabaseService database = new();
    private readonly Button generateBarcodeButton = new();
    private readonly Product? existingProduct;
    private readonly string originalBarcode;

    public Product? Product { get; private set; }

    public ProductDialog(Product? product = null, string? initialBarcode = null, decimal? initialStock = null)
    {
        existingProduct = product;
        originalBarcode = product?.Barcode ?? "";
        Title = product is null ? "Yeni ürün" : "Ürünü düzenle";
        Width = 380;
        Height = 750;
        MinHeight = 710;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = System.Windows.Media.Brushes.White;

        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = "Yeni ürün ekle", FontSize = 22, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 18) });
        AddBarcodeField(panel);
        AddField(panel, "Ürün adı", nameBox);
        var categoryHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        var addCategoryButton = new Button
        {
            Content = "＋ Yeni kategori",
            Padding = new Thickness(8, 4, 8, 4),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        addCategoryButton.Click += AddCategoryButton_Click;
        DockPanel.SetDock(addCategoryButton, Dock.Right);
        categoryHeader.Children.Add(addCategoryButton);
        categoryHeader.Children.Add(new TextBlock { Text = "Kategori", FontSize = 12, Foreground = System.Windows.Media.Brushes.Gray, VerticalAlignment = VerticalAlignment.Center });
        panel.Children.Add(categoryHeader);
        categoryBox.Margin = new Thickness(0, 0, 0, 10);
        categoryBox.Padding = new Thickness(8, 6, 8, 6);
        categoryBox.ItemsSource = database.LoadCategories();
        panel.Children.Add(categoryBox);
        AddField(panel, "Alış maliyeti (KDV hariç, TL)", costExcludingVatBox);
        panel.Children.Add(new TextBlock { Text = "KDV oranı", FontSize = 12, Foreground = System.Windows.Media.Brushes.Gray, Margin = new Thickness(0, 0, 0, 4) });
        vatRateBox.ItemsSource = new[] { 1m, 10m, 20m };
        vatRateBox.ItemStringFormat = "{0}%";
        vatRateBox.Margin = new Thickness(0, 0, 0, 10);
        vatRateBox.Padding = new Thickness(8, 6, 8, 6);
        panel.Children.Add(vatRateBox);
        panel.Children.Add(new TextBlock { Text = "KDV dâhil alış maliyeti", FontSize = 12, Foreground = System.Windows.Media.Brushes.Gray, Margin = new Thickness(0, 0, 0, 4) });
        costIncludingVatText.Margin = new Thickness(0, 0, 0, 10);
        costIncludingVatText.FontWeight = FontWeights.SemiBold;
        panel.Children.Add(costIncludingVatText);
        AddField(panel, "Satış fiyatı (TL)", priceBox);
        AddField(panel, "Stok miktarı", stockBox);
        panel.Children.Add(new TextBlock
        {
            Text = product is null
                ? "Son kullanma tarihi (stok giriliyorsa zorunlu)"
                : "Artırılan stokun son kullanma tarihi (isteğe bağlı)",
            FontSize = 12,
            Foreground = System.Windows.Media.Brushes.Gray,
            Margin = new Thickness(0, 0, 0, 4)
        });
        expirationDatePicker.SelectedDateFormat = DatePickerFormat.Short;
        expirationDatePicker.Margin = new Thickness(0, 0, 0, 10);
        expirationDatePicker.Padding = new Thickness(8, 6, 8, 6);
        panel.Children.Add(expirationDatePicker);
        AddField(panel, "Kritik stok eşiği", minimumStockBox);

        if (product is not null)
        {
            barcodeBox.Text = product.Barcode;
            nameBox.Text = product.Name;
            categoryBox.SelectedItem = product.Category;
            if (product.VatRate.HasValue)
            {
                vatRateBox.SelectedItem = product.VatRate.Value;
                costExcludingVatBox.Text = (product.CostPrice / (1 + product.VatRate.Value / 100)).ToString("0.####", CultureInfo.CurrentCulture);
            }
            priceBox.Text = product.Price.ToString("N2");
            stockBox.Text = product.Stock.ToString("N2");
            minimumStockBox.Text = product.MinimumStock.ToString("N2");
        }
        else
        {
            barcodeBox.Text = initialBarcode?.Trim() ?? "";
            categoryBox.SelectedItem = categoryBox.Items.Contains("Genel") ? "Genel" : categoryBox.Items.Cast<string>().FirstOrDefault();
            stockBox.Text = (initialStock ?? 0).ToString("N2", CultureInfo.CurrentCulture);
            minimumStockBox.Text = "5";
        }

        costExcludingVatBox.TextChanged += (_, _) => UpdateCostPreview(product);
        vatRateBox.SelectionChanged += (_, _) => UpdateCostPreview(product);
        UpdateCostPreview(product);

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
            Loaded += (_, _) => (string.IsNullOrWhiteSpace(initialBarcode) ? barcodeBox : nameBox).Focus();
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

    private void AddCategoryButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new CategoryDialog { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            database.SaveCategory(dialog.CategoryName);
            var categories = database.LoadCategories();
            categoryBox.ItemsSource = categories;
            categoryBox.SelectedItem = categories.First(category =>
                string.Equals(category, dialog.CategoryName, StringComparison.CurrentCultureIgnoreCase));
        }
        catch (SqliteException exception)
        {
            MessageBox.Show($"Kategori kaydedilemedi.\n{exception.Message}", "Kategori eklenemedi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void AddField(StackPanel panel, string label, TextBox input)
    {
        panel.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = System.Windows.Media.Brushes.Gray, Margin = new Thickness(0, 0, 0, 4) });
        input.Margin = new Thickness(0, 0, 0, 10);
        input.Padding = new Thickness(8, 6, 8, 6);
        panel.Children.Add(input);
    }

    private void UpdateCostPreview(Product? existingProduct)
    {
        if (!decimal.TryParse(costExcludingVatBox.Text.Replace('.', ','), NumberStyles.Number, CultureInfo.CurrentCulture, out var cost) ||
            cost < 0 ||
            vatRateBox.SelectedItem is not decimal vatRate)
        {
            costIncludingVatText.Text = string.IsNullOrWhiteSpace(costExcludingVatBox.Text) && existingProduct is not null
                ? $"Değişiklik yok: {existingProduct.CostPrice:N2} TL"
                : "Tutarı girip KDV oranını seçin.";
            return;
        }

        costIncludingVatText.Text = $"{Product.CalculateCostIncludingVat(cost, vatRate):N2} TL";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(nameBox.Text) ||
            !decimal.TryParse(priceBox.Text.Replace('.', ','), NumberStyles.Number, CultureInfo.CurrentCulture, out var price) ||
            !decimal.TryParse(stockBox.Text.Replace('.', ','), NumberStyles.Number, CultureInfo.CurrentCulture, out var stock) || stock < 0 ||
            !decimal.TryParse(minimumStockBox.Text.Replace('.', ','), NumberStyles.Number, CultureInfo.CurrentCulture, out var minimumStock) || minimumStock < 0)
        {
            MessageBox.Show("Ürün adı, geçerli fiyatlar ve sıfır veya daha büyük stok miktarları girin.", "Eksik bilgi", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        decimal cost;
        decimal? vatRate;
        if (!string.IsNullOrWhiteSpace(costExcludingVatBox.Text))
        {
            if (!decimal.TryParse(costExcludingVatBox.Text.Replace('.', ','), NumberStyles.Number, CultureInfo.CurrentCulture, out var costExcludingVat) ||
                costExcludingVat < 0 ||
                vatRateBox.SelectedItem is not decimal selectedVatRate)
            {
                MessageBox.Show("Geçerli bir KDV hariç alış maliyeti girip KDV oranını seçin.", "Eksik bilgi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            cost = Product.CalculateCostIncludingVat(costExcludingVat, selectedVatRate);
            vatRate = selectedVatRate;
        }
        else if (existingProduct is not null &&
                 ((existingProduct.VatRate is null && vatRateBox.SelectedItem is null) ||
                  (existingProduct.VatRate is decimal originalVatRate &&
                   vatRateBox.SelectedItem is decimal selectedVatRate &&
                   originalVatRate == selectedVatRate)))
        {
            cost = existingProduct.CostPrice;
            vatRate = existingProduct.VatRate;
        }
        else
        {
            MessageBox.Show("KDV hariç alış maliyetini girip KDV oranını seçin.", "Eksik bilgi", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (categoryBox.SelectedItem is not string category)
        {
            MessageBox.Show("Bir kategori seçin veya yeni kategori ekleyin.", "Kategori gerekli", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (stock > 0 &&
            (existingProduct is null || existingProduct.Stock == 0) &&
            !expirationDatePicker.SelectedDate.HasValue)
        {
            MessageBox.Show("İlk stok girişi için son kullanma tarihini girin.", "Son kullanma tarihi gerekli", MessageBoxButton.OK, MessageBoxImage.Warning);
            expirationDatePicker.Focus();
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
            Category = category,
            CostPrice = cost,
            VatRate = vatRate,
            Price = price,
            Stock = stock,
            MinimumStock = minimumStock,
            StockAdditionExpirationDate = expirationDatePicker.SelectedDate?.Date
        };
        DialogResult = true;
    }
}

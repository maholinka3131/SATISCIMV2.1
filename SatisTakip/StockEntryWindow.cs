using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Data.Sqlite;

namespace SatisTakip;

public sealed class StockEntryWindow : Window
{
    private readonly ObservableCollection<Product> products;
    private readonly DatabaseService database;
    private readonly TextBox barcodeBox = new();
    private readonly TextBox quantityBox = new();
    private readonly TextBox costExcludingVatBox = new();
    private readonly TextBlock productDetails = new();
    private readonly TextBlock vatRateLabel = new();
    private readonly TextBlock costIncludingVatText = new();
    private readonly ComboBox vatRateBox = new();
    private readonly Button addStockButton = new();
    private readonly Button createProductButton = new();
    private Product? selectedProduct;

    public Product? AddedProduct { get; private set; }
    public bool AddedToExistingProduct { get; private set; }

    public StockEntryWindow(ObservableCollection<Product> products, DatabaseService database)
    {
        this.products = products;
        this.database = database;
        Title = "Barkodla stok girişi";
        Width = 480;
        Height = 590;
        MinWidth = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;

        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = "Barkodla stok girişi", FontSize = 22, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 8) });
        panel.Children.Add(new TextBlock { Text = "Barkodu okutun veya yazıp Enter'a basın.", Foreground = System.Windows.Media.Brushes.Gray, Margin = new Thickness(0, 0, 0, 16) });

        panel.Children.Add(new TextBlock { Text = "Barkod", Margin = new Thickness(0, 0, 0, 4) });
        barcodeBox.Padding = new Thickness(8, 7, 8, 7);
        barcodeBox.KeyDown += BarcodeBox_KeyDown;
        barcodeBox.TextChanged += BarcodeBox_TextChanged;
        panel.Children.Add(barcodeBox);

        panel.Children.Add(new TextBlock { Text = "Barkod eşleşen ürün", Margin = new Thickness(0, 14, 0, 4) });
        productDetails.Text = "Barkodu okutun; ürün adı ve mevcut stok burada görüntülenir.";
        productDetails.TextWrapping = TextWrapping.Wrap;
        productDetails.Margin = new Thickness(0, 0, 0, 12);
        productDetails.FontWeight = FontWeights.SemiBold;
        panel.Children.Add(productDetails);

        createProductButton.Content = "Bu barkodla yeni ürün oluştur";
        createProductButton.Padding = new Thickness(12, 8, 12, 8);
        createProductButton.Margin = new Thickness(0, 0, 0, 12);
        createProductButton.HorizontalAlignment = HorizontalAlignment.Right;
        createProductButton.Visibility = Visibility.Collapsed;
        createProductButton.Click += (_, _) => CreateProductFromBarcode();
        panel.Children.Add(createProductButton);

        panel.Children.Add(new TextBlock { Text = "Eklenecek adet", Margin = new Thickness(0, 0, 0, 4) });
        quantityBox.Text = "1";
        quantityBox.Padding = new Thickness(8, 7, 8, 7);
        quantityBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                AddStock();
                e.Handled = true;
            }
        };
        panel.Children.Add(quantityBox);

        panel.Children.Add(new TextBlock { Text = "Yeni alış maliyeti (KDV hariç; değiştiyse girin)", Margin = new Thickness(0, 14, 0, 4) });
        costExcludingVatBox.Padding = new Thickness(8, 7, 8, 7);
        costExcludingVatBox.TextChanged += (_, _) => UpdateCostPreview();
        panel.Children.Add(costExcludingVatBox);

        vatRateLabel.Text = "KDV oranı (ürün için ilk kez):";
        vatRateLabel.Margin = new Thickness(0, 10, 0, 4);
        vatRateLabel.Visibility = Visibility.Collapsed;
        panel.Children.Add(vatRateLabel);
        vatRateBox.ItemsSource = new[] { 1m, 10m, 20m };
        vatRateBox.ItemStringFormat = "{0}%";
        vatRateBox.Padding = new Thickness(8, 6, 8, 6);
        vatRateBox.Visibility = Visibility.Collapsed;
        vatRateBox.SelectionChanged += (_, _) => UpdateCostPreview();
        panel.Children.Add(vatRateBox);

        costIncludingVatText.Margin = new Thickness(0, 10, 0, 0);
        costIncludingVatText.FontWeight = FontWeights.SemiBold;
        panel.Children.Add(costIncludingVatText);

        addStockButton.Content = "Stoğa ekle";
        addStockButton.Padding = new Thickness(16, 9, 16, 9);
        addStockButton.Margin = new Thickness(0, 18, 0, 0);
        addStockButton.HorizontalAlignment = HorizontalAlignment.Right;
        addStockButton.IsEnabled = false;
        addStockButton.Click += (_, _) => AddStock();
        panel.Children.Add(addStockButton);
        Content = panel;
        Loaded += (_, _) => barcodeBox.Focus();
    }

    private void BarcodeBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            FindProduct();
            e.Handled = true;
        }
    }

    private void BarcodeBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        selectedProduct = null;
        costExcludingVatBox.Clear();
        vatRateBox.SelectedItem = null;
        vatRateBox.Visibility = Visibility.Collapsed;
        vatRateLabel.Visibility = Visibility.Collapsed;
        productDetails.Text = "Barkodu okutun; ürün adı ve mevcut stok burada görüntülenir.";
        costIncludingVatText.Text = "";
        addStockButton.IsEnabled = false;
        createProductButton.IsEnabled = false;
        createProductButton.Visibility = Visibility.Collapsed;
    }

    private void FindProduct()
    {
        var barcode = barcodeBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(barcode))
        {
            selectedProduct = null;
            productDetails.Text = "Önce ürünün barkodunu okutun veya girin.";
            addStockButton.IsEnabled = false;
            createProductButton.IsEnabled = false;
            createProductButton.Visibility = Visibility.Collapsed;
            barcodeBox.Focus();
            return;
        }

        selectedProduct = products.FirstOrDefault(product =>
            string.Equals(product.Barcode, barcode, StringComparison.OrdinalIgnoreCase));
        if (selectedProduct is null)
        {
            productDetails.Text = "Bu barkod kayıtlı değil. Ürün bilgilerini girerek yeni ürün oluşturabilirsiniz.";
            addStockButton.IsEnabled = false;
            createProductButton.IsEnabled = true;
            createProductButton.Visibility = Visibility.Visible;
            barcodeBox.SelectAll();
            return;
        }

        productDetails.Text = $"{selectedProduct.Name}\nBarkod: {selectedProduct.Barcode}\nMevcut stok: {selectedProduct.Stock:N2}";
        vatRateBox.SelectedItem = selectedProduct.VatRate;
        vatRateBox.Visibility = selectedProduct.VatRate.HasValue ? Visibility.Collapsed : Visibility.Visible;
        vatRateLabel.Visibility = selectedProduct.VatRate.HasValue ? Visibility.Collapsed : Visibility.Visible;
        addStockButton.IsEnabled = true;
        createProductButton.IsEnabled = false;
        createProductButton.Visibility = Visibility.Collapsed;
        quantityBox.Focus();
        quantityBox.SelectAll();
        UpdateCostPreview();
    }

    private void CreateProductFromBarcode()
    {
        var barcode = barcodeBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(barcode))
        {
            FindProduct();
            return;
        }

        if (!decimal.TryParse(quantityBox.Text.Replace('.', ','), NumberStyles.Number, CultureInfo.CurrentCulture, out var initialStock) || initialStock <= 0)
        {
            MessageBox.Show("Yeni ürün için sıfırdan büyük geçerli bir adet girin.", "Geçersiz miktar", MessageBoxButton.OK, MessageBoxImage.Warning);
            quantityBox.Focus();
            quantityBox.SelectAll();
            return;
        }

        var dialog = new ProductDialog(initialBarcode: barcode, initialStock: initialStock) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Product is not { } newProduct)
        {
            barcodeBox.Focus();
            return;
        }

        try
        {
            database.SaveProduct(newProduct);
        }
        catch (SqliteException exception)
        {
            MessageBox.Show($"Yeni ürün kaydedilemedi.\n{exception.Message}", "Ürün kaydı yapılamadı", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        products.Add(newProduct);
        AddedProduct = newProduct;
        Close();
    }

    private void AddStock()
    {
        if (selectedProduct is null)
        {
            FindProduct();
            return;
        }
        if (!decimal.TryParse(quantityBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var quantity) || quantity <= 0)
        {
            MessageBox.Show("Sıfırdan büyük geçerli bir adet girin.", "Geçersiz miktar", MessageBoxButton.OK, MessageBoxImage.Warning);
            quantityBox.Focus();
            quantityBox.SelectAll();
            return;
        }

        decimal? updatedCostPrice = null;
        decimal? updatedVatRate = null;
        var movementNote = "";
        if (!string.IsNullOrWhiteSpace(costExcludingVatBox.Text))
        {
            if (!decimal.TryParse(costExcludingVatBox.Text.Replace('.', ','), NumberStyles.Number, CultureInfo.CurrentCulture, out var costExcludingVat) || costExcludingVat < 0)
            {
                MessageBox.Show("Geçerli ve sıfır veya daha büyük bir KDV hariç alış maliyeti girin.", "Geçersiz maliyet", MessageBoxButton.OK, MessageBoxImage.Warning);
                costExcludingVatBox.Focus();
                costExcludingVatBox.SelectAll();
                return;
            }

            var vatRate = selectedProduct.VatRate;
            if (!vatRate.HasValue && vatRateBox.SelectedItem is decimal selectedVatRate)
            {
                vatRate = selectedVatRate;
            }
            if (!vatRate.HasValue)
            {
                MessageBox.Show("Bu ürün için KDV oranını seçin.", "KDV oranı gerekli", MessageBoxButton.OK, MessageBoxImage.Warning);
                vatRateBox.Focus();
                return;
            }

            updatedCostPrice = Product.CalculateCostIncludingVat(costExcludingVat, vatRate.Value);
            updatedVatRate = vatRate;
            movementNote = $"Alış maliyeti güncellendi: {costExcludingVat:N2} TL + %{vatRate:N0} KDV = {updatedCostPrice:N2} TL";
        }

        try
        {
            selectedProduct.Stock = database.AddStock(selectedProduct.Barcode, selectedProduct.Name, quantity, updatedCostPrice, updatedVatRate, movementNote);
        }
        catch (InvalidOperationException exception)
        {
            MessageBox.Show(exception.Message, "Stok girişi yapılamadı", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        catch (SqliteException exception)
        {
            MessageBox.Show($"Veritabanı stok güncellemesini kaydedemedi.\n{exception.Message}", "Stok girişi yapılamadı", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (updatedCostPrice.HasValue)
        {
            selectedProduct.CostPrice = updatedCostPrice.Value;
            selectedProduct.VatRate = updatedVatRate ?? selectedProduct.VatRate;
        }
        AddedProduct = selectedProduct;
        AddedToExistingProduct = true;
        Close();
    }

    private void UpdateCostPreview()
    {
        if (selectedProduct is null)
        {
            costIncludingVatText.Text = "";
            return;
        }

        if (string.IsNullOrWhiteSpace(costExcludingVatBox.Text))
        {
            costIncludingVatText.Text = $"Mevcut KDV dâhil alış maliyeti: {selectedProduct.CostPrice:N2} TL";
            return;
        }

        if (!decimal.TryParse(costExcludingVatBox.Text.Replace('.', ','), NumberStyles.Number, CultureInfo.CurrentCulture, out var cost) || cost < 0)
        {
            costIncludingVatText.Text = "Geçerli bir maliyet girin.";
            return;
        }

        var vatRate = selectedProduct.VatRate;
        if (!vatRate.HasValue && vatRateBox.SelectedItem is decimal selectedVatRate)
        {
            vatRate = selectedVatRate;
        }
        costIncludingVatText.Text = vatRate.HasValue
            ? $"Yeni KDV dâhil maliyet: {Product.CalculateCostIncludingVat(cost, vatRate.Value):N2} TL"
            : "KDV oranı seçilince KDV dâhil maliyet hesaplanır.";
    }
}

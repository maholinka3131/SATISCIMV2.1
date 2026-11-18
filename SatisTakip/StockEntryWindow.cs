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
    private readonly TextBlock productDetails = new();
    private readonly Button addStockButton = new();
    private Product? selectedProduct;

    public StockEntryWindow(ObservableCollection<Product> products, DatabaseService database)
    {
        this.products = products;
        this.database = database;
        Title = "Barkodla stok girişi";
        Width = 480;
        Height = 350;
        MinWidth = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;

        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = "Barkodla stok girişi", FontSize = 22, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 8) });
        panel.Children.Add(new TextBlock { Text = "Barkodu okutun veya yazıp Enter'a basın.", Foreground = System.Windows.Media.Brushes.Gray, Margin = new Thickness(0, 0, 0, 16) });

        panel.Children.Add(new TextBlock { Text = "Barkod", Margin = new Thickness(0, 0, 0, 4) });
        barcodeBox.Padding = new Thickness(8, 7, 8, 7);
        barcodeBox.KeyDown += BarcodeBox_KeyDown;
        panel.Children.Add(barcodeBox);

        productDetails.Text = "Henüz ürün seçilmedi.";
        productDetails.Margin = new Thickness(0, 16, 0, 12);
        productDetails.FontWeight = FontWeights.SemiBold;
        panel.Children.Add(productDetails);

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

    private void FindProduct()
    {
        var barcode = barcodeBox.Text.Trim();
        selectedProduct = products.FirstOrDefault(product =>
            string.Equals(product.Barcode, barcode, StringComparison.OrdinalIgnoreCase));
        if (selectedProduct is null)
        {
            productDetails.Text = "Bu barkodla kayıtlı ürün bulunamadı.";
            addStockButton.IsEnabled = false;
            barcodeBox.SelectAll();
            return;
        }

        productDetails.Text = $"{selectedProduct.Name}\nMevcut stok: {selectedProduct.Stock:N2}";
        addStockButton.IsEnabled = true;
        quantityBox.Focus();
        quantityBox.SelectAll();
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

        try
        {
            selectedProduct.Stock = database.AddStock(selectedProduct.Barcode, selectedProduct.Name, quantity);
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

        productDetails.Text = $"{selectedProduct.Name}\nYeni stok: {selectedProduct.Stock:N2}";
        barcodeBox.Clear();
        quantityBox.Text = "1";
        selectedProduct = null;
        addStockButton.IsEnabled = false;
        barcodeBox.Focus();
    }
}

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Data.Sqlite;

namespace SatisTakip;

public sealed class ExpirationStockWindow : Window
{
    private readonly DatabaseService database;
    private readonly ComboBox productBox = new();
    private readonly DatePicker expirationDatePicker = new();
    private readonly TextBox quantityBox = new();
    private readonly TextBlock availableQuantityText = new();
    private readonly DataGrid batchesGrid = new();

    public ExpirationStockWindow(IEnumerable<Product> products, DatabaseService database)
    {
        this.database = database;
        Title = "SKT ve stok adedi tanımlama";
        Width = 760;
        Height = 560;
        MinWidth = 640;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brushes.White;

        var root = new DockPanel { Margin = new Thickness(22) };
        var content = new StackPanel();
        content.Children.Add(new TextBlock
        {
            Text = "Mevcut stok için SKT tanımla",
            FontSize = 23,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(29, 41, 57))
        });
        content.Children.Add(new TextBlock
        {
            Text = "Ürünün mevcut, henüz SKT ile eşleştirilmemiş stoğundan tarih ve adet belirleyin.",
            Foreground = Brushes.Gray,
            Margin = new Thickness(0, 5, 0, 16)
        });

        productBox.ItemsSource = products.Where(product => product.Stock > 0).OrderBy(product => product.Name).ToList();
        productBox.DisplayMemberPath = nameof(Product.Name);
        productBox.Padding = new Thickness(8, 6, 8, 6);
        productBox.Margin = new Thickness(0, 0, 0, 8);
        productBox.SelectionChanged += (_, _) => RefreshBatches();
        content.Children.Add(productBox);

        availableQuantityText.Foreground = Brushes.Gray;
        availableQuantityText.Margin = new Thickness(0, 0, 0, 12);
        content.Children.Add(availableQuantityText);

        var entryRow = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        entryRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        entryRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        entryRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var datePanel = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
        datePanel.Children.Add(new TextBlock { Text = "Son kullanma tarihi", Margin = new Thickness(0, 0, 0, 4) });
        expirationDatePicker.SelectedDateFormat = DatePickerFormat.Short;
        expirationDatePicker.Padding = new Thickness(8, 6, 8, 6);
        datePanel.Children.Add(expirationDatePicker);
        entryRow.Children.Add(datePanel);

        var quantityPanel = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
        quantityPanel.Children.Add(new TextBlock { Text = "Adet", Margin = new Thickness(0, 0, 0, 4) });
        quantityBox.Padding = new Thickness(8, 6, 8, 6);
        quantityPanel.Children.Add(quantityBox);
        Grid.SetColumn(quantityPanel, 1);
        entryRow.Children.Add(quantityPanel);

        var addButton = new Button
        {
            Content = "SKT ve adedi ekle",
            Padding = new Thickness(14, 8, 14, 8),
            VerticalAlignment = VerticalAlignment.Bottom,
            Background = new SolidColorBrush(Color.FromRgb(23, 107, 135)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0)
        };
        addButton.Click += AddExpirationBatch_Click;
        Grid.SetColumn(addButton, 2);
        entryRow.Children.Add(addButton);
        content.Children.Add(entryRow);

        batchesGrid.AutoGenerateColumns = false;
        batchesGrid.CanUserAddRows = false;
        batchesGrid.IsReadOnly = true;
        batchesGrid.HeadersVisibility = DataGridHeadersVisibility.Column;
        batchesGrid.GridLinesVisibility = DataGridGridLinesVisibility.None;
        batchesGrid.SelectionMode = DataGridSelectionMode.Single;
        batchesGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Son kullanma tarihi",
            Binding = new System.Windows.Data.Binding(nameof(ProductExpirationBatch.ExpirationDate)) { StringFormat = "d" },
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });
        batchesGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Kalan adet",
            Binding = new System.Windows.Data.Binding(nameof(ProductExpirationBatch.Quantity)) { StringFormat = "N2" },
            Width = 140
        });
        content.Children.Add(batchesGrid);

        var unassignButton = new Button
        {
            Content = "Seçili SKT kaydını kaldır (stok korunur)",
            Padding = new Thickness(12, 7, 12, 7),
            Margin = new Thickness(0, 10, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        unassignButton.Click += UnassignExpirationBatch_Click;
        content.Children.Add(unassignButton);
        root.Children.Add(content);
        Content = root;

        if (productBox.Items.Count > 0)
        {
            productBox.SelectedIndex = 0;
        }
        else
        {
            productBox.IsEnabled = false;
            availableQuantityText.Text = "SKT tanımlanabilecek mevcut stoğu olan ürün bulunamadı.";
        }
    }

    private void AddExpirationBatch_Click(object sender, RoutedEventArgs e)
    {
        if (productBox.SelectedItem is not Product product)
        {
            return;
        }
        if (!expirationDatePicker.SelectedDate.HasValue)
        {
            MessageBox.Show("Son kullanma tarihini seçin.", "Eksik bilgi", MessageBoxButton.OK, MessageBoxImage.Warning);
            expirationDatePicker.Focus();
            return;
        }
        if (!decimal.TryParse(quantityBox.Text.Replace('.', ','), NumberStyles.Number, CultureInfo.CurrentCulture, out var quantity) || quantity <= 0)
        {
            MessageBox.Show("Sıfırdan büyük geçerli bir adet girin.", "Geçersiz adet", MessageBoxButton.OK, MessageBoxImage.Warning);
            quantityBox.Focus();
            quantityBox.SelectAll();
            return;
        }

        try
        {
            database.AddExpirationStock(product.Id, expirationDatePicker.SelectedDate.Value.Date, quantity);
        }
        catch (InvalidOperationException exception)
        {
            MessageBox.Show(exception.Message, "SKT adedi eklenemedi", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        catch (SqliteException exception)
        {
            MessageBox.Show($"SKT adedi veritabanına kaydedilemedi.\n{exception.Message}", "Kayıt yapılamadı", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        expirationDatePicker.SelectedDate = null;
        quantityBox.Clear();
        RefreshBatches();
    }

    private void UnassignExpirationBatch_Click(object sender, RoutedEventArgs e)
    {
        if (productBox.SelectedItem is not Product product ||
            batchesGrid.SelectedItem is not ProductExpirationBatch batch)
        {
            MessageBox.Show("Kaldırmak için bir SKT kaydı seçin.", "Kayıt seçilmedi", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show(
                $"{batch.ExpirationDate:d} tarihli {batch.Quantity:N2} adet SKT eşleştirmesi kaldırılacak. Ürün stoğu değişmeyecek. Devam edilsin mi?",
                "SKT kaydını kaldır",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            database.UnassignExpirationStock(product.Id, batch.Id);
        }
        catch (InvalidOperationException exception)
        {
            MessageBox.Show(exception.Message, "SKT kaydı kaldırılamadı", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        catch (SqliteException exception)
        {
            MessageBox.Show($"SKT kaydı veritabanından kaldırılamadı.\n{exception.Message}", "Kayıt kaldırılamadı", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        RefreshBatches();
    }

    private void RefreshBatches()
    {
        if (productBox.SelectedItem is not Product product)
        {
            batchesGrid.ItemsSource = null;
            return;
        }

        var batches = database.LoadExpirationBatches(product.Id);
        batchesGrid.ItemsSource = batches;
        var assignedQuantity = batches.Sum(batch => batch.Quantity);
        var availableQuantity = Math.Max(product.Stock - assignedQuantity, 0);
        availableQuantityText.Text = $"Mevcut stok: {product.Stock:N2} adet  •  SKT'ye bağlanmamış: {availableQuantity:N2} adet";
    }
}

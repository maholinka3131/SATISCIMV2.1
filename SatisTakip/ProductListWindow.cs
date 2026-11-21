using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Data.Sqlite;

namespace SatisTakip;

public sealed class ProductListWindow : Window
{
    private readonly ObservableCollection<Product> products;
    private readonly DatabaseService database = new();
    private readonly DataGrid productGrid = new();

    public Product? AddedProduct { get; private set; }

    public ProductListWindow(ObservableCollection<Product> products)
    {
        this.products = products;
        Title = "Ürünler";
        Width = 1060;
        Height = 560;
        MinWidth = 900;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(244, 246, 248));

        var root = new DockPanel { Margin = new Thickness(22) };
        var header = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var titlePanel = new StackPanel();
        titlePanel.Children.Add(new TextBlock { Text = "Ürünler", FontSize = 24, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(29, 41, 57)) });
        titlePanel.Children.Add(new TextBlock { Text = "Kayıtlı ürünleri yönetin", FontSize = 13, Foreground = Brushes.Gray, Margin = new Thickness(0, 4, 0, 0) });
        header.Children.Add(titlePanel);

        var addButton = new Button { Content = "＋ Yeni ürün", Padding = new Thickness(16, 10, 16, 10), Background = new SolidColorBrush(Color.FromRgb(23, 107, 135)), Foreground = Brushes.White, BorderThickness = new Thickness(0), VerticalAlignment = VerticalAlignment.Center };
        addButton.Click += AddButton_Click;
        Grid.SetColumn(addButton, 1);
        header.Children.Add(addButton);
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        productGrid.AutoGenerateColumns = false;
        productGrid.CanUserAddRows = false;
        productGrid.IsReadOnly = true;
        productGrid.HeadersVisibility = DataGridHeadersVisibility.Column;
        productGrid.GridLinesVisibility = DataGridGridLinesVisibility.None;
        productGrid.Background = Brushes.White;
        productGrid.BorderThickness = new Thickness(0);
        productGrid.SelectionMode = DataGridSelectionMode.Single;
        productGrid.ItemsSource = products;
        productGrid.LoadingRow += ProductGrid_LoadingRow;
        productGrid.Columns.Add(new DataGridTextColumn { Header = "Barkod", Binding = new System.Windows.Data.Binding(nameof(Product.Barcode)), Width = 120 });
        productGrid.Columns.Add(new DataGridTextColumn { Header = "Ürün adı", Binding = new System.Windows.Data.Binding(nameof(Product.Name)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        productGrid.Columns.Add(new DataGridTextColumn { Header = "Kategori", Binding = new System.Windows.Data.Binding(nameof(Product.Category)), Width = 130 });
        productGrid.Columns.Add(new DataGridTextColumn { Header = "Alış maliyeti", Binding = new System.Windows.Data.Binding(nameof(Product.CostPriceText)), Width = 110 });
        productGrid.Columns.Add(new DataGridTextColumn { Header = "Satış fiyatı", Binding = new System.Windows.Data.Binding(nameof(Product.PriceText)), Width = 105 });
        productGrid.Columns.Add(new DataGridTextColumn { Header = "Stok", Binding = new System.Windows.Data.Binding(nameof(Product.Stock)), Width = 75 });
        productGrid.Columns.Add(new DataGridTextColumn { Header = "Kritik eşik", Binding = new System.Windows.Data.Binding(nameof(Product.MinimumStock)) { StringFormat = "N2" }, Width = 85 });
        productGrid.Columns.Add(new DataGridTextColumn { Header = "Kâr (TL)", Binding = new System.Windows.Data.Binding(nameof(Product.ProfitText)), Width = 95 });
        productGrid.Columns.Add(new DataGridTextColumn { Header = "Kâr (%)", Binding = new System.Windows.Data.Binding(nameof(Product.ProfitMarginText)), Width = 80 });
        root.Children.Add(productGrid);

        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var editButton = new Button { Content = "Düzenle", Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(0, 0, 8, 0) };
        editButton.Click += EditButton_Click;
        var printLabelButton = new Button { Content = "Barkod etiketi yazdır", Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(0, 0, 8, 0) };
        printLabelButton.Click += PrintLabelButton_Click;
        var deleteButton = new Button { Content = "Seçili ürünü sil", Padding = new Thickness(14, 8, 14, 8), Foreground = new SolidColorBrush(Color.FromRgb(194, 65, 58)), Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(230, 170, 165)) };
        deleteButton.Click += DeleteButton_Click;
        footer.Children.Add(editButton);
        footer.Children.Add(printLabelButton);
        footer.Children.Add(deleteButton);
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);
        Content = root;
    }

    private void PrintLabelButton_Click(object sender, RoutedEventArgs e)
    {
        if (productGrid.SelectedItem is not Product product)
        {
            MessageBox.Show("Etiket yazdırmak için bir ürün seçin.", "Ürün seçilmedi", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        BarcodeLabelPrinter.Print(product, this);
    }

    private void ProductGrid_LoadingRow(object? sender, DataGridRowEventArgs e)
    {
        e.Row.Background = e.Row.Item is Product product && product.IsLowStock
            ? new SolidColorBrush(Color.FromRgb(255, 240, 235))
            : Brushes.White;
        var menu = new ContextMenu();
        var categoryItem = new MenuItem { Header = "Kategoriyi değiştir" };
        categoryItem.Click += RowChangeCategoryItem_Click;
        var editItem = new MenuItem { Header = "Ürünü düzenle" };
        editItem.Click += RowEditItem_Click;
        var deleteItem = new MenuItem { Header = "Ürünü sil" };
        deleteItem.Click += RowDeleteItem_Click;
        var printLabelItem = new MenuItem { Header = "Barkod etiketi yazdır" };
        printLabelItem.Click += RowPrintLabelItem_Click;
        menu.Items.Add(categoryItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(editItem);
        menu.Items.Add(deleteItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(printLabelItem);
        e.Row.ContextMenu = menu;
    }

    private void RowEditItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.Parent is ContextMenu menu && menu.PlacementTarget is DataGridRow row && row.Item is Product product)
        {
            productGrid.SelectedItem = product;
            EditButton_Click(sender, e);
        }
    }

    private void RowChangeCategoryItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.Parent is ContextMenu menu && menu.PlacementTarget is DataGridRow row && row.Item is Product product)
        {
            productGrid.SelectedItem = product;
            ChangeProductCategory(product);
        }
    }

    private void RowPrintLabelItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.Parent is ContextMenu menu && menu.PlacementTarget is DataGridRow row && row.Item is Product product)
        {
            productGrid.SelectedItem = product;
            BarcodeLabelPrinter.Print(product, this);
        }
    }

    private void RowDeleteItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.Parent is ContextMenu menu && menu.PlacementTarget is DataGridRow row && row.Item is Product product)
        {
            productGrid.SelectedItem = product;
            DeleteButton_Click(sender, e);
        }
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ProductDialog { Owner = this };
        if (dialog.ShowDialog() == true && dialog.Product is not null)
        {
            products.Add(dialog.Product);
            database.SaveProduct(dialog.Product);
            AddedProduct = dialog.Product;
            DialogResult = true;
        }
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (productGrid.SelectedItem is not Product product)
        {
            MessageBox.Show("Düzenlemek için bir ürün seçin.", "Ürün seçilmedi", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new ProductDialog(product) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.Product is not null)
        {
            product.Barcode = dialog.Product.Barcode;
            product.Name = dialog.Product.Name;
            product.Category = dialog.Product.Category;
            product.CostPrice = dialog.Product.CostPrice;
            product.VatRate = dialog.Product.VatRate;
            product.Price = dialog.Product.Price;
            product.Stock = dialog.Product.Stock;
            product.MinimumStock = dialog.Product.MinimumStock;
            database.SaveProduct(product);
            productGrid.Items.Refresh();
        }
    }

    private void ChangeProductCategory(Product product)
    {
        var dialog = new ProductCategoryDialog(product, database.LoadCategories()) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.SelectedCategory is not { } newCategory)
        {
            return;
        }

        var oldCategory = product.Category;
        product.Category = newCategory;
        try
        {
            database.SaveProduct(product);
        }
        catch (SqliteException exception)
        {
            product.Category = oldCategory;
            MessageBox.Show(this, $"Ürün kategorisi güncellenemedi.\n{exception.Message}", "Kategori değiştirilemedi", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        productGrid.Items.Refresh();
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (productGrid.SelectedItem is not Product product)
        {
            MessageBox.Show("Silmek için bir ürün seçin.", "Ürün seçilmedi", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show($"'{product.Name}' ürününü silmek istiyor musunuz?", "Ürünü sil", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            database.DeleteProduct(product);
            products.Remove(product);
        }
    }
}

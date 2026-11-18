using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SatisTakip;

public sealed class CriticalStockWindow : Window
{
    public CriticalStockWindow(IEnumerable<Product> products)
    {
        Title = "Kritik stok uyarıları";
        Width = 760;
        Height = 520;
        MinWidth = 600;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var lowStockProducts = products.Where(product => product.IsLowStock).OrderBy(product => product.Stock).ThenBy(product => product.Name).ToList();
        var root = new DockPanel { Margin = new Thickness(22) };
        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        header.Children.Add(new TextBlock { Text = "Kritik stok uyarıları", FontSize = 23, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(29, 41, 57)) });
        header.Children.Add(new TextBlock { Text = lowStockProducts.Count == 0 ? "Kritik seviyede ürün yok." : $"{lowStockProducts.Count} ürün kritik stok seviyesinde veya altında.", Foreground = Brushes.Gray, Margin = new Thickness(0, 5, 0, 0) });
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var grid = new DataGrid
        {
            ItemsSource = lowStockProducts,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            IsReadOnly = true,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.None
        };
        grid.Columns.Add(new DataGridTextColumn { Header = "Ürün", Binding = new System.Windows.Data.Binding(nameof(Product.Name)), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Barkod", Binding = new System.Windows.Data.Binding(nameof(Product.Barcode)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Mevcut stok", Binding = new System.Windows.Data.Binding(nameof(Product.Stock)) { StringFormat = "N2" }, Width = 110 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Kritik eşik", Binding = new System.Windows.Data.Binding(nameof(Product.MinimumStock)) { StringFormat = "N2" }, Width = 110 });
        root.Children.Add(grid);
        Content = root;
    }
}

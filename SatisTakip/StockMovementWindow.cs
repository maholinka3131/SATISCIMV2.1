using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SatisTakip;

public sealed class StockMovementWindow : Window
{
    private readonly DatabaseService database;
    private readonly ObservableCollection<StockMovement> movements = new();
    private readonly DatePicker fromDate = new();
    private readonly DatePicker toDate = new();

    public StockMovementWindow(DatabaseService database)
    {
        this.database = database;
        Title = "Stok hareket geçmişi";
        Width = 1050;
        Height = 650;
        MinWidth = 850;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new DockPanel { Margin = new Thickness(22) };
        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        header.Children.Add(new TextBlock { Text = "Stok hareket geçmişi", FontSize = 23, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(29, 41, 57)) });
        var filters = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        fromDate.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        toDate.SelectedDate = DateTime.Today;
        filters.Children.Add(new TextBlock { Text = "Başlangıç", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        filters.Children.Add(fromDate);
        filters.Children.Add(new TextBlock { Text = "Bitiş", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 6, 0) });
        filters.Children.Add(toDate);
        var filterButton = new Button { Content = "Filtrele", Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(12, 0, 0, 0) };
        filterButton.Click += (_, _) => LoadMovements();
        filters.Children.Add(filterButton);
        var allButton = new Button { Content = "Tüm tarihler", Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(6, 0, 0, 0) };
        allButton.Click += (_, _) => { fromDate.SelectedDate = null; toDate.SelectedDate = null; LoadMovements(); };
        filters.Children.Add(allButton);
        header.Children.Add(filters);
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var grid = new DataGrid
        {
            ItemsSource = movements,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            IsReadOnly = true,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.None
        };
        grid.Columns.Add(new DataGridTextColumn { Header = "Tarih", Binding = new System.Windows.Data.Binding(nameof(StockMovement.Date)) { StringFormat = "dd.MM.yyyy HH:mm" }, Width = 145 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Ürün", Binding = new System.Windows.Data.Binding(nameof(StockMovement.ProductName)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "İşlem", Binding = new System.Windows.Data.Binding(nameof(StockMovement.Type)), Width = 110 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Değişim", Binding = new System.Windows.Data.Binding(nameof(StockMovement.Quantity)) { StringFormat = "{0:+0.##;-0.##;0}" }, Width = 90 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Önceki stok", Binding = new System.Windows.Data.Binding(nameof(StockMovement.PreviousStock)) { StringFormat = "N2" }, Width = 100 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Yeni stok", Binding = new System.Windows.Data.Binding(nameof(StockMovement.NewStock)) { StringFormat = "N2" }, Width = 90 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Kullanıcı", Binding = new System.Windows.Data.Binding(nameof(StockMovement.Username)), Width = 100 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Not", Binding = new System.Windows.Data.Binding(nameof(StockMovement.Note)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        root.Children.Add(grid);
        Content = root;
        LoadMovements();
    }

    private void LoadMovements()
    {
        if (fromDate.SelectedDate is DateTime from && toDate.SelectedDate is DateTime to && from.Date > to.Date)
        {
            MessageBox.Show("Başlangıç tarihi bitiş tarihinden sonra olamaz.", "Geçersiz tarih aralığı", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        movements.Clear();
        foreach (var movement in database.LoadStockMovements(fromDate.SelectedDate, toDate.SelectedDate))
        {
            movements.Add(movement);
        }
    }
}

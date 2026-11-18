using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace SatisTakip;

public sealed class SalesReportWindow : Window
{
    private readonly DatabaseService database;
    private readonly DatePicker fromDate = new();
    private readonly DatePicker toDate = new();
    private readonly TextBlock revenueText = new();
    private readonly TextBlock costText = new();
    private readonly TextBlock profitText = new();
    private readonly TextBlock quantityText = new();
    private readonly TextBlock refundedRevenueText = new();
    private readonly TextBlock returnedQuantityText = new();
    private readonly DataGrid productsGrid = new();

    public SalesReportWindow(DatabaseService database)
    {
        this.database = database;
        Title = "Satış raporları";
        Width = 1000;
        Height = 680;
        MinWidth = 800;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new DockPanel { Margin = new Thickness(22) };
        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        header.Children.Add(new TextBlock { Text = "Satış raporları", FontSize = 23, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(29, 41, 57)) });

        var filters = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 14) };
        fromDate.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        toDate.SelectedDate = DateTime.Today;
        filters.Children.Add(new TextBlock { Text = "Başlangıç", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        filters.Children.Add(fromDate);
        filters.Children.Add(new TextBlock { Text = "Bitiş", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 6, 0) });
        filters.Children.Add(toDate);
        var filterButton = new Button { Content = "Filtrele", Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(12, 0, 0, 0) };
        filterButton.Click += (_, _) => LoadReport();
        filters.Children.Add(filterButton);
        var allButton = new Button { Content = "Tüm tarihler", Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(6, 0, 0, 0) };
        allButton.Click += (_, _) => { fromDate.SelectedDate = null; toDate.SelectedDate = null; LoadReport(); };
        filters.Children.Add(allButton);
        header.Children.Add(filters);

        var summary = new UniformGrid { Columns = 6, Margin = new Thickness(0, 0, 0, 14) };
        summary.Children.Add(CreateSummaryItem("Net satış", revenueText));
        summary.Children.Add(CreateSummaryItem("Maliyet", costText));
        summary.Children.Add(CreateSummaryItem("Brüt kâr", profitText));
        summary.Children.Add(CreateSummaryItem("Satılan adet", quantityText));
        summary.Children.Add(CreateSummaryItem("İade edilen tutar", refundedRevenueText));
        summary.Children.Add(CreateSummaryItem("İade edilen adet", returnedQuantityText));
        header.Children.Add(summary);
        header.Children.Add(new TextBlock { Text = "En çok satılan ürünler (adet bazında)", FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 0) });
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        productsGrid.AutoGenerateColumns = false;
        productsGrid.CanUserAddRows = false;
        productsGrid.IsReadOnly = true;
        productsGrid.HeadersVisibility = DataGridHeadersVisibility.Column;
        productsGrid.GridLinesVisibility = DataGridGridLinesVisibility.None;
        productsGrid.Columns.Add(new DataGridTextColumn { Header = "Sıra", Binding = new System.Windows.Data.Binding("Index"), Width = 65 });
        productsGrid.Columns.Add(new DataGridTextColumn { Header = "Ürün", Binding = new System.Windows.Data.Binding(nameof(BestSellingProduct.ProductName)), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
        productsGrid.Columns.Add(new DataGridTextColumn { Header = "Satılan adet", Binding = new System.Windows.Data.Binding(nameof(BestSellingProduct.Quantity)) { StringFormat = "N2" }, Width = 115 });
        productsGrid.Columns.Add(new DataGridTextColumn { Header = "İade edilen adet", Binding = new System.Windows.Data.Binding(nameof(BestSellingProduct.ReturnedQuantity)) { StringFormat = "N2" }, Width = 115 });
        productsGrid.Columns.Add(new DataGridTextColumn { Header = "Net satış", Binding = new System.Windows.Data.Binding(nameof(BestSellingProduct.Revenue)) { StringFormat = "{}{0:N2} TL" }, Width = 120 });
        productsGrid.Columns.Add(new DataGridTextColumn { Header = "İade tutarı", Binding = new System.Windows.Data.Binding(nameof(BestSellingProduct.RefundedRevenue)) { StringFormat = "{}{0:N2} TL" }, Width = 120 });
        productsGrid.Columns.Add(new DataGridTextColumn { Header = "Maliyet", Binding = new System.Windows.Data.Binding(nameof(BestSellingProduct.Cost)) { StringFormat = "{}{0:N2} TL" }, Width = 120 });
        productsGrid.Columns.Add(new DataGridTextColumn { Header = "Brüt kâr", Binding = new System.Windows.Data.Binding(nameof(BestSellingProduct.Profit)) { StringFormat = "{}{0:N2} TL" }, Width = 120 });
        root.Children.Add(productsGrid);
        Content = root;
        LoadReport();
    }

    private static Border CreateSummaryItem(string label, TextBlock value)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = label, Foreground = Brushes.Gray, FontSize = 12 });
        value.FontSize = 18;
        value.FontWeight = FontWeights.SemiBold;
        value.Margin = new Thickness(0, 5, 0, 0);
        panel.Children.Add(value);
        return new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(247, 249, 250)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 8, 0),
            Child = panel
        };
    }

    private void LoadReport()
    {
        if (fromDate.SelectedDate is DateTime from && toDate.SelectedDate is DateTime to && from.Date > to.Date)
        {
            MessageBox.Show("Başlangıç tarihi bitiş tarihinden sonra olamaz.", "Geçersiz tarih aralığı", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var report = database.LoadSalesReport(fromDate.SelectedDate, toDate.SelectedDate);
        revenueText.Text = $"{report.Revenue:N2} TL";
        costText.Text = $"{report.Cost:N2} TL";
        profitText.Text = $"{report.Profit:N2} TL";
        quantityText.Text = $"{report.Quantity:N2}";
        refundedRevenueText.Text = $"{report.RefundedRevenue:N2} TL";
        returnedQuantityText.Text = $"{report.ReturnedQuantity:N2}";
        productsGrid.ItemsSource = report.BestSellers.Select((product, index) => new
        {
            Index = index + 1,
            product.ProductName,
            product.Quantity,
            product.ReturnedQuantity,
            product.Revenue,
            product.RefundedRevenue,
            product.Cost,
            product.Profit
        }).ToList();
    }
}

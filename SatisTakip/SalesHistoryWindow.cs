using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace SatisTakip;

public sealed class SalesHistoryWindow : Window
{
    private readonly List<SaleRecord> sales;
    private readonly ObservableCollection<SaleItemRecord> saleItems = new();
    private readonly ObservableCollection<SaleItemRecord> visibleSaleItems = new();
    private readonly ObservableCollection<SaleItemRecord> returnCart = new();
    private readonly DataGrid salesGrid = new();
    private readonly DataGrid returnCartGrid = new();
    private readonly DatePicker fromDatePicker = new();
    private readonly DatePicker toDatePicker = new();
    private readonly Button addToReturnCartButton = new();
    private readonly Button refundProductButton = new();
    private readonly Button refundSaleButton = new();
    private readonly TextBlock revenueTotalText = new();
    private readonly TextBlock costTotalText = new();
    private readonly TextBlock profitTotalText = new();

    public SalesHistoryWindow()
    {
        Title = "Satış geçmişi ve iadeler";
        Width = 1100;
        Height = 780;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var database = new DatabaseService();
        sales = database.LoadAllSales();
        foreach (var sale in sales)
        {
            foreach (var item in sale.Items)
            {
                saleItems.Add(item);
                visibleSaleItems.Add(item);
            }
        }

        var root = new DockPanel { Margin = new Thickness(22) };
        var summary = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        summary.Children.Add(CreateSummaryItem("Dönem cirosu", revenueTotalText));
        summary.Children.Add(CreateSummaryItem("Dönem maliyeti", costTotalText));
        summary.Children.Add(CreateSummaryItem("Dönem karı", profitTotalText));
        DockPanel.SetDock(summary, Dock.Bottom);
        root.Children.Add(summary);

        var content = new Grid();
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3, GridUnitType.Star) });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2, GridUnitType.Star) });

        var salesSection = new DockPanel();
        var salesHeader = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        var salesHeaderTitle = new DockPanel();
        addToReturnCartButton.Content = "İade sepetine ekle";
        addToReturnCartButton.Padding = new Thickness(12, 6, 12, 6);
        addToReturnCartButton.Visibility = Visibility.Collapsed;
        addToReturnCartButton.Click += (_, _) => AddCheckedItemsToReturnCart();
        DockPanel.SetDock(addToReturnCartButton, Dock.Right);
        salesHeaderTitle.Children.Add(addToReturnCartButton);
        salesHeaderTitle.Children.Add(new TextBlock { Text = "Satış geçmişindeki ürünler", FontSize = 15, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        salesHeader.Children.Add(salesHeaderTitle);

        var dateFilters = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        fromDatePicker.Width = 130;
        fromDatePicker.SelectedDateFormat = DatePickerFormat.Short;
        toDatePicker.Width = 130;
        toDatePicker.SelectedDateFormat = DatePickerFormat.Short;
        dateFilters.Children.Add(new TextBlock { Text = "Başlangıç", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        dateFilters.Children.Add(fromDatePicker);
        dateFilters.Children.Add(new TextBlock { Text = "Bitiş", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 6, 0) });
        dateFilters.Children.Add(toDatePicker);
        var applyFilterButton = new Button { Content = "Filtrele", Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(12, 0, 0, 0) };
        applyFilterButton.Click += (_, _) => ApplyDateFilter();
        dateFilters.Children.Add(applyFilterButton);
        var currentMonthButton = new Button { Content = "Bu ay", Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(6, 0, 0, 0) };
        currentMonthButton.Click += (_, _) => ShowCurrentMonth();
        dateFilters.Children.Add(currentMonthButton);
        var allDatesButton = new Button { Content = "Tüm tarihler", Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(6, 0, 0, 0) };
        allDatesButton.Click += (_, _) => ShowAllDates();
        dateFilters.Children.Add(allDatesButton);
        salesHeader.Children.Add(dateFilters);
        DockPanel.SetDock(salesHeader, Dock.Top);
        salesSection.Children.Add(salesHeader);

        salesGrid.ItemsSource = visibleSaleItems;
        salesGrid.AutoGenerateColumns = false;
        salesGrid.CanUserAddRows = false;
        salesGrid.Columns.Add(new DataGridTextColumn { Header = "Tarih", Binding = new System.Windows.Data.Binding(nameof(SaleItemRecord.SaleDate)) { StringFormat = "dd.MM.yyyy HH:mm" }, Width = 150, IsReadOnly = true });
        salesGrid.Columns.Add(new DataGridTextColumn { Header = "Ürün", Binding = new System.Windows.Data.Binding(nameof(SaleItemRecord.ProductName)), Width = new DataGridLength(2, DataGridLengthUnitType.Star), IsReadOnly = true });
        salesGrid.Columns.Add(new DataGridTextColumn { Header = "Satılan adet", Binding = new System.Windows.Data.Binding(nameof(SaleItemRecord.Quantity)) { StringFormat = "N2" }, Width = 100, IsReadOnly = true });
        salesGrid.Columns.Add(new DataGridTextColumn { Header = "İade edilen", Binding = new System.Windows.Data.Binding(nameof(SaleItemRecord.ReturnedQuantity)) { StringFormat = "N2" }, Width = 95, IsReadOnly = true });
        salesGrid.Columns.Add(new DataGridTextColumn { Header = "Birim fiyat", Binding = new System.Windows.Data.Binding(nameof(SaleItemRecord.UnitPrice)) { StringFormat = "{}{0:N2} TL" }, Width = 110, IsReadOnly = true });
        salesGrid.Columns.Add(new DataGridTextColumn { Header = "Ödeme dağılımı", Binding = new System.Windows.Data.Binding(nameof(SaleItemRecord.PaymentDisplay)), Width = 230, IsReadOnly = true });
        salesGrid.Columns.Add(new DataGridCheckBoxColumn
        {
            Header = "İade seç",
            Binding = new System.Windows.Data.Binding(nameof(SaleItemRecord.IsSelectedForReturn)) { Mode = System.Windows.Data.BindingMode.TwoWay, UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged },
            Width = 75
        });
        salesGrid.CellEditEnding += (_, _) => Dispatcher.BeginInvoke(new Action(UpdateAddToReturnCartButton), DispatcherPriority.Background);
        salesGrid.AddHandler(ToggleButton.CheckedEvent, new RoutedEventHandler(ReturnSelectionChanged), true);
        salesGrid.AddHandler(ToggleButton.UncheckedEvent, new RoutedEventHandler(ReturnSelectionChanged), true);
        salesSection.Children.Add(salesGrid);
        Grid.SetRow(salesSection, 0);
        content.Children.Add(salesSection);

        var returnSection = new DockPanel { Margin = new Thickness(0, 14, 0, 0) };
        var returnHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var returnButtons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        refundProductButton.Content = "Seçili ürünü iade et";
        refundProductButton.Padding = new Thickness(10, 6, 10, 6);
        refundProductButton.IsEnabled = false;
        refundProductButton.Click += (_, _) => RefundSelectedProduct(database);
        refundSaleButton.Content = "Satışın tamamını iade et";
        refundSaleButton.Padding = new Thickness(10, 6, 10, 6);
        refundSaleButton.Margin = new Thickness(8, 0, 0, 0);
        refundSaleButton.IsEnabled = false;
        refundSaleButton.Click += (_, _) => RefundSelectedSale(database);
        returnButtons.Children.Add(refundProductButton);
        returnButtons.Children.Add(refundSaleButton);
        DockPanel.SetDock(returnButtons, Dock.Right);
        returnHeader.Children.Add(returnButtons);
        returnHeader.Children.Add(new TextBlock { Text = "İade sepeti", FontSize = 15, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        DockPanel.SetDock(returnHeader, Dock.Top);
        returnSection.Children.Add(returnHeader);

        returnCartGrid.ItemsSource = returnCart;
        returnCartGrid.IsReadOnly = true;
        returnCartGrid.AutoGenerateColumns = false;
        returnCartGrid.CanUserAddRows = false;
        returnCartGrid.SelectionChanged += (_, _) => UpdateRefundButtons();
        returnCartGrid.PreviewMouseRightButtonDown += ReturnCartGrid_PreviewMouseRightButtonDown;
        returnCartGrid.Columns.Add(new DataGridTextColumn { Header = "Tarih", Binding = new System.Windows.Data.Binding(nameof(SaleItemRecord.SaleDate)) { StringFormat = "dd.MM.yyyy HH:mm" }, Width = 150 });
        returnCartGrid.Columns.Add(new DataGridTextColumn { Header = "Ürün", Binding = new System.Windows.Data.Binding(nameof(SaleItemRecord.ProductName)), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
        returnCartGrid.Columns.Add(new DataGridTextColumn { Header = "İade edilecek adet", Binding = new System.Windows.Data.Binding(nameof(SaleItemRecord.RemainingQuantity)) { StringFormat = "N2" }, Width = 130 });
        returnCartGrid.Columns.Add(new DataGridTextColumn { Header = "Satış no", Binding = new System.Windows.Data.Binding(nameof(SaleItemRecord.SaleId)), Width = 85 });
        var removeFromCart = new MenuItem { Header = "İade sepetinden çıkar" };
        removeFromCart.Click += (_, _) => RemoveSelectedFromReturnCart();
        returnCartGrid.ContextMenu = new ContextMenu();
        returnCartGrid.ContextMenu.Items.Add(removeFromCart);
        returnSection.Children.Add(returnCartGrid);
        Grid.SetRow(returnSection, 1);
        content.Children.Add(returnSection);

        root.Children.Add(content);
        Content = root;
        UpdateTotals();
    }

    private static StackPanel CreateSummaryItem(string label, TextBlock value)
    {
        var panel = new StackPanel { Margin = new Thickness(16, 0, 0, 0) };
        panel.Children.Add(new TextBlock { Text = label, Foreground = System.Windows.Media.Brushes.Gray, HorizontalAlignment = HorizontalAlignment.Right });
        value.FontSize = 16;
        value.FontWeight = FontWeights.SemiBold;
        value.HorizontalAlignment = HorizontalAlignment.Right;
        panel.Children.Add(value);
        return panel;
    }

    private void ReturnSelectionChanged(object sender, RoutedEventArgs e)
    {
        UpdateAddToReturnCartButton();
    }

    private void UpdateAddToReturnCartButton()
    {
        addToReturnCartButton.Visibility = visibleSaleItems.Any(item => item.IsSelectedForReturn)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void AddCheckedItemsToReturnCart()
    {
        var checkedItems = visibleSaleItems.Where(item => item.IsSelectedForReturn).ToList();
        var eligibleItems = checkedItems.Where(item => item.RemainingQuantity > 0).ToList();
        if (eligibleItems.Count == 0)
        {
            MessageBox.Show("İade edilebilecek miktarı olan bir ürün seçin.", "Ürün seçilmedi", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        foreach (var item in eligibleItems)
        {
            if (!returnCart.Any(cartItem => cartItem.Id == item.Id))
            {
                returnCart.Add(item);
            }
        }
        foreach (var item in checkedItems)
        {
            item.IsSelectedForReturn = false;
        }
        salesGrid.Items.Refresh();
        addToReturnCartButton.Visibility = Visibility.Collapsed;
        UpdateRefundButtons();
    }

    private void ApplyDateFilter()
    {
        if (fromDatePicker.SelectedDate is DateTime fromDate &&
            toDatePicker.SelectedDate is DateTime toDate &&
            fromDate.Date > toDate.Date)
        {
            MessageBox.Show("Başlangıç tarihi bitiş tarihinden sonra olamaz.", "Geçersiz tarih aralığı", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        foreach (var item in saleItems)
        {
            item.IsSelectedForReturn = false;
        }
        visibleSaleItems.Clear();
        foreach (var item in saleItems.Where(IsWithinDateFilter))
        {
            visibleSaleItems.Add(item);
        }
        addToReturnCartButton.Visibility = Visibility.Collapsed;
        UpdateTotals();
    }

    private bool IsWithinDateFilter(SaleItemRecord item)
    {
        var date = item.SaleDate.Date;
        return (!fromDatePicker.SelectedDate.HasValue || date >= fromDatePicker.SelectedDate.Value.Date) &&
            (!toDatePicker.SelectedDate.HasValue || date <= toDatePicker.SelectedDate.Value.Date);
    }

    private void ShowCurrentMonth()
    {
        var today = DateTime.Today;
        fromDatePicker.SelectedDate = new DateTime(today.Year, today.Month, 1);
        toDatePicker.SelectedDate = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
        ApplyDateFilter();
    }

    private void ShowAllDates()
    {
        fromDatePicker.SelectedDate = null;
        toDatePicker.SelectedDate = null;
        ApplyDateFilter();
    }

    private void ReturnCartGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source)
        {
            return;
        }
        var row = ItemsControl.ContainerFromElement(returnCartGrid, source) as DataGridRow;
        if (row is not null)
        {
            row.IsSelected = true;
        }
    }

    private void RemoveSelectedFromReturnCart()
    {
        if (returnCartGrid.SelectedItem is SaleItemRecord item)
        {
            returnCart.Remove(item);
        }
        UpdateRefundButtons();
    }

    private void UpdateRefundButtons()
    {
        var item = returnCartGrid.SelectedItem as SaleItemRecord;
        var canRefund = item is not null && item.RemainingQuantity > 0;
        refundProductButton.IsEnabled = canRefund;
        refundSaleButton.IsEnabled = canRefund;
    }

    private void RefundSelectedProduct(DatabaseService database)
    {
        if (returnCartGrid.SelectedItem is not SaleItemRecord item || item.RemainingQuantity <= 0)
        {
            UpdateRefundButtons();
            return;
        }
        if (MessageBox.Show($"{item.ProductName} ({item.RemainingQuantity:N2} adet) iade edilip stoğa eklensin mi?", "Ürün iadesi", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }
        if (!database.RefundSaleItem(item.Id))
        {
            MessageBox.Show("Bu ürün daha önce iade edilmiş veya bulunamadı.", "İade yapılamadı", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        item.ReturnedQuantity = item.Quantity;
        item.IsSelectedForReturn = false;
        returnCart.Remove(item);
        salesGrid.Items.Refresh();
        UpdateTotals();
        UpdateRefundButtons();
    }

    private void RefundSelectedSale(DatabaseService database)
    {
        if (returnCartGrid.SelectedItem is not SaleItemRecord selectedItem)
        {
            UpdateRefundButtons();
            return;
        }
        var sale = sales.FirstOrDefault(candidate => candidate.Id == selectedItem.SaleId);
        if (sale is null)
        {
            MessageBox.Show("Satış kaydı bulunamadı.", "İade yapılamadı", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var itemsToReturn = sale.Items.Where(item => item.RemainingQuantity > 0).ToList();
        if (itemsToReturn.Count == 0)
        {
            UpdateRefundButtons();
            return;
        }
        if (MessageBox.Show($"Satışın kalan {itemsToReturn.Count} ürün satırı iade edilip stoğa eklensin mi?", "Satışın tamamını iade et", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }
        if (!database.RefundSale(sale.Id))
        {
            MessageBox.Show("Bu satış daha önce tamamen iade edilmiş veya bulunamadı.", "İade yapılamadı", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        foreach (var item in itemsToReturn)
        {
            item.ReturnedQuantity = item.Quantity;
            item.IsSelectedForReturn = false;
            returnCart.Remove(item);
        }
        salesGrid.Items.Refresh();
        UpdateTotals();
        UpdateRefundButtons();
    }

    private void UpdateTotals()
    {
        var filteredSales = sales.Where(sale =>
            (!fromDatePicker.SelectedDate.HasValue || sale.Date.Date >= fromDatePicker.SelectedDate.Value.Date) &&
            (!toDatePicker.SelectedDate.HasValue || sale.Date.Date <= toDatePicker.SelectedDate.Value.Date));
        var revenue = filteredSales.Sum(sale => sale.Items.Sum(item => item.RemainingQuantity * item.UnitPrice));
        var cost = filteredSales.Sum(sale => sale.Items.Sum(item => item.RemainingQuantity * item.UnitCost));
        revenueTotalText.Text = $"{revenue:N2} TL";
        costTotalText.Text = $"{cost:N2} TL";
        profitTotalText.Text = $"{revenue - cost:N2} TL";
    }
}

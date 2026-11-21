using ClosedXML.Excel;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Reflection;

namespace SatisTakip;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly ObservableCollection<Product> products = new();
    private readonly ObservableCollection<Product> visibleProducts = new();
    private readonly List<string> categories = new();
    private readonly ObservableCollection<CartItem> cart = new();
    private readonly ObservableCollection<SaleRecord> sales = new();
    private readonly ObservableCollection<Expense> expenses = new();
    private readonly DatabaseService database;
    private readonly System.Windows.Threading.DispatcherTimer barcodeScanTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private CartItem? selectedCartItem;
    private string? selectedCategory;
    private decimal openingCash;

    private const int WmGetMinMaxInfo = 0x0024;
    private const uint MonitorDefaultToNearest = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMinMaxInfo
    {
        public NativePoint Reserved;
        public NativePoint MaxSize;
        public NativePoint MaxPosition;
        public NativePoint MinTrackSize;
        public NativePoint MaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref NativeMonitorInfo monitorInfo);

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(HandleWindowMessage);
        }
    }

    private static IntPtr HandleWindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != WmGetMinMaxInfo)
        {
            return IntPtr.Zero;
        }

        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        var monitorInfo = new NativeMonitorInfo { Size = Marshal.SizeOf<NativeMonitorInfo>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref monitorInfo))
        {
            return IntPtr.Zero;
        }

        var minMaxInfo = Marshal.PtrToStructure<NativeMinMaxInfo>(lParam);
        minMaxInfo.MaxPosition.X = monitorInfo.Work.Left - monitorInfo.Monitor.Left;
        minMaxInfo.MaxPosition.Y = monitorInfo.Work.Top - monitorInfo.Monitor.Top;
        minMaxInfo.MaxSize.X = monitorInfo.Work.Right - monitorInfo.Work.Left;
        minMaxInfo.MaxSize.Y = monitorInfo.Work.Bottom - monitorInfo.Work.Top;
        minMaxInfo.MaxTrackSize = minMaxInfo.MaxSize;
        Marshal.StructureToPtr(minMaxInfo, lParam, false);
        handled = true;
        return IntPtr.Zero;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        SearchBox.Focus();
        Keyboard.Focus(SearchBox);

        var update = await new UpdateService().CheckAsync();
        var currentVersion = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 0, 0);
        if (update is null || !Version.TryParse(update.Version, out var latestVersion) || latestVersion <= currentVersion || string.IsNullOrWhiteSpace(update.DownloadUrl))
        {
            return;
        }

        var result = MessageBox.Show($"Yeni SATIŞÇIM sürümü bulundu: {update.Version}\n\n{update.Notes}\n\nYeni sürüm indirilsin mi?\n\nMevcut kayıtlarınız korunacaktır.", "Güncelleme bulundu", MessageBoxButton.YesNo, MessageBoxImage.Information);
        if (result == MessageBoxResult.Yes)
        {
            UpdateService.OpenInstaller(update.DownloadUrl);
        }
    }

    public MainWindow()
    {
        InitializeComponent();
        VersionText.Text = $"Sürüm {Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "Bilinmiyor"}";
        barcodeScanTimer.Tick += BarcodeScanTimer_Tick;
        database = new DatabaseService();
        ConfigurePermissions();
        database.RemoveTemporaryProductsOnce();
        LoadProductsFromDatabase();
        foreach (var sale in database.LoadSalesForDate(DateTime.Now))
        {
            sales.Add(sale);
        }
        foreach (var expense in database.LoadExpensesForDate(DateTime.Now))
        {
            expenses.Add(expense);
        }
        if (decimal.TryParse(database.GetSetting("opening_cash"), out var savedOpeningCash))
        {
            openingCash = savedOpeningCash;
            OpeningCashBox.Text = openingCash.ToString("N2");
        }
        ProductButtons.ItemsSource = visibleProducts;
        ReloadCategories();
        CartGrid.ItemsSource = cart;
        cart.CollectionChanged += Cart_CollectionChanged;
        RefreshProductList();
        UpdateTotals();
        UpdateFinancialSummary();
    }

    private void MinimizeWindowButton_Click(object sender, RoutedEventArgs e)
        => WindowState = System.Windows.WindowState.Minimized;

    private void ToggleWindowStateButton_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == System.Windows.WindowState.Maximized
            ? System.Windows.WindowState.Normal
            : System.Windows.WindowState.Maximized;

    private void CloseWindowButton_Click(object sender, RoutedEventArgs e)
        => Close();

    private void LoadProductsFromDatabase()
    {
        var storedProducts = database.LoadProducts();
        if (storedProducts.Count > 0)
        {
            foreach (var product in storedProducts)
            {
                products.Add(product);
            }
            return;
        }

        // Ürün listesi boş başlar; ürünler manuel veya Excel ile eklenir.
    }

    private void SeedProducts()
    {
        products.Add(new Product { Barcode = "1001", Name = "Tam Yağlı Süt", Category = "Süt Ürünleri", CostPrice = 24.00m, Price = 32.50m });
        products.Add(new Product { Barcode = "1002", Name = "Beyaz Ekmek", Category = "Fırın", CostPrice = 8.00m, Price = 12.00m });
        products.Add(new Product { Barcode = "1003", Name = "Maden Suyu", Category = "İçecek", CostPrice = 6.00m, Price = 9.50m });
        products.Add(new Product { Barcode = "1004", Name = "Türk Kahvesi", Category = "Kahvaltılık", CostPrice = 56.00m, Price = 74.90m });
        products.Add(new Product { Barcode = "1005", Name = "Çamaşır Deterjanı", Category = "Temizlik", CostPrice = 148.00m, Price = 189.90m });
        products.Add(new Product { Barcode = "1006", Name = "Bitter Çikolata", Category = "Atıştırmalık", CostPrice = 21.00m, Price = 29.90m });
        products.Add(new Product { Barcode = "1007", Name = "Kağıt Havlu", Category = "Temizlik", CostPrice = 64.00m, Price = 84.50m });
        products.Add(new Product { Barcode = "1008", Name = "Gazlı İçecek", Category = "İçecek", CostPrice = 25.00m, Price = 36.00m });
    }

    private void RefreshProductList()
    {
        var query = SearchBox?.Text?.Trim() ?? "";
        visibleProducts.Clear();
        foreach (var product in products.Where(product =>
                     (selectedCategory is null || string.Equals(product.Category, selectedCategory, StringComparison.CurrentCultureIgnoreCase)) &&
                     (string.IsNullOrWhiteSpace(query) || product.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) || product.Barcode.Contains(query, StringComparison.CurrentCultureIgnoreCase))))
        {
            visibleProducts.Add(product);
        }

        if (ProductCountText is not null)
        {
            ProductCountText.Text = $"{visibleProducts.Count} ürün";
        }
        if (StockAlertsNavButton is not null)
        {
            var criticalStockCount = products.Count(product => product.IsLowStock);
            var alertLabel = new TextBlock();
            alertLabel.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding(nameof(Button.Foreground)) { Source = StockAlertsNavButton });
            alertLabel.Text = $"Kritik stok ({criticalStockCount})";
            StockAlertsNavButton.Tag = criticalStockCount > 0 ? "Alert" : null;
            StockAlertsNavButton.Content = alertLabel;
        }
    }

    private void ReloadCategories()
    {
        categories.Clear();
        categories.AddRange(database.LoadCategories());
        if (selectedCategory is not null &&
            !categories.Any(category => string.Equals(category, selectedCategory, StringComparison.CurrentCultureIgnoreCase)))
        {
            selectedCategory = null;
        }

        CategorySelectionText.Text = selectedCategory ?? "Tüm kategoriler";
        RefreshCategoryButtons();
        RefreshProductList();
    }

    private void RefreshCategoryButtons()
    {
        CategoryButtonsPanel.Children.Clear();
        var query = CategorySearchBox?.Text?.Trim() ?? "";
        AddCategoryButton("Tüm ürünler", null, selectedCategory is null);

        foreach (var category in categories.Where(category =>
                     string.IsNullOrWhiteSpace(query) ||
                     category.Contains(query, StringComparison.CurrentCultureIgnoreCase)))
        {
            AddCategoryButton(category, category, string.Equals(category, selectedCategory, StringComparison.CurrentCultureIgnoreCase));
        }
    }

    private void AddCategoryButton(string title, string? category, bool isSelected)
    {
        var button = new Button
        {
            Content = title,
            Tag = category,
            Style = (Style)FindResource("RoundedButton"),
            Background = isSelected
                ? (System.Windows.Media.Brush)FindResource("Accent")
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(242, 248, 250)),
            Foreground = isSelected
                ? System.Windows.Media.Brushes.White
                : (System.Windows.Media.Brush)FindResource("Accent"),
            BorderBrush = isSelected
                ? (System.Windows.Media.Brush)FindResource("Accent")
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(213, 233, 238)),
            Padding = new Thickness(10, 5, 10, 5),
            Margin = new Thickness(0, 0, 6, 6),
            FontSize = 11
        };
        button.Click += CategoryFilterButton_Click;
        if (category is not null)
        {
            var menu = new ContextMenu();
            var editCategory = new MenuItem { Header = "Kategori adını düzenle" };
            editCategory.Click += EditCategoryMenuItem_Click;
            var deleteCategory = new MenuItem { Header = "Kategoriyi sil" };
            deleteCategory.Click += DeleteCategoryMenuItem_Click;
            menu.Items.Add(editCategory);
            menu.Items.Add(deleteCategory);
            button.ContextMenu = menu;
        }
        CategoryButtonsPanel.Children.Add(button);
    }

    private void CategoryFilterButton_Click(object sender, RoutedEventArgs e)
    {
        selectedCategory = (sender as Button)?.Tag as string;
        CategorySelectionText.Text = selectedCategory ?? "Tüm kategoriler";
        RefreshCategoryButtons();
        RefreshProductList();
    }

    private void CategorySearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshCategoryButtons();
    }

    private void EditCategoryMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetCategoryFromMenu(sender, out var category))
        {
            return;
        }

        var dialog = new CategoryDialog(category) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            database.RenameCategory(category, dialog.CategoryName);
        }
        catch (InvalidOperationException exception)
        {
            MessageBox.Show(this, exception.Message, "Kategori düzenlenemedi", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        catch (Microsoft.Data.Sqlite.SqliteException exception)
        {
            MessageBox.Show(this, $"Kategori düzenlenemedi.\n{exception.Message}", "Kategori düzenlenemedi", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        foreach (var product in products.Where(product => string.Equals(product.Category, category, StringComparison.CurrentCultureIgnoreCase)))
        {
            product.Category = dialog.CategoryName.Trim();
        }
        if (string.Equals(selectedCategory, category, StringComparison.CurrentCultureIgnoreCase))
        {
            selectedCategory = dialog.CategoryName.Trim();
        }
        ReloadCategories();
    }

    private void DeleteCategoryMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetCategoryFromMenu(sender, out var category))
        {
            return;
        }
        if (string.Equals(category, "Genel", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, "Genel kategorisi varsayılan kategori olduğu için silinemez.", "Kategori silinemedi", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var productNames = database.LoadProductNamesInCategory(category);
        var dialog = new CategoryDeleteDialog(category, productNames) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            database.DeleteCategory(category);
        }
        catch (InvalidOperationException exception)
        {
            MessageBox.Show(this, exception.Message, "Kategori silinemedi", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        catch (Microsoft.Data.Sqlite.SqliteException exception)
        {
            MessageBox.Show(this, $"Kategori silinemedi.\n{exception.Message}", "Kategori silinemedi", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        foreach (var product in products.Where(product => string.Equals(product.Category, category, StringComparison.CurrentCultureIgnoreCase)))
        {
            product.Category = "Genel";
        }
        if (string.Equals(selectedCategory, category, StringComparison.CurrentCultureIgnoreCase))
        {
            selectedCategory = null;
        }
        ReloadCategories();
    }

    private static bool TryGetCategoryFromMenu(object sender, out string category)
    {
        if (sender is MenuItem menuItem &&
            menuItem.Parent is ContextMenu contextMenu &&
            contextMenu.PlacementTarget is Button button &&
            button.Tag is string categoryName)
        {
            category = categoryName;
            return true;
        }

        category = "";
        return false;
    }

    private void ChangeProductCategoryMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem ||
            menuItem.Parent is not ContextMenu contextMenu ||
            contextMenu.PlacementTarget is not Button button ||
            button.Tag is not Product product)
        {
            return;
        }

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
        catch (Microsoft.Data.Sqlite.SqliteException exception)
        {
            product.Category = oldCategory;
            MessageBox.Show(this, $"Ürün kategorisi güncellenemedi.\n{exception.Message}", "Kategori değiştirilemedi", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        ReloadCategories();
    }

    private void ConfigurePermissions()
    {
        var isAdmin = UserSession.CurrentUser?.IsAdmin == true;
        FinancialSummaryPanel.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
        foreach (var control in new Control[]
        {
            ProductsNavButton, StockEntryNavButton, StockMovementsNavButton, ImportExcelNavButton,
            ExpensesNavButton, SalesHistoryNavButton, ReportsNavButton, DayEndNavButton,
            BackupNavButton, RestoreNavButton, UserManagementNavButton, AdminSettingsNavButton,
            OpeningCashBox, StartDayButton, AddExpenseTopButton, DayEndTopButton, AddProductTopButton
        })
        {
            control.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private bool RequireAdmin()
    {
        if (RefreshCurrentUser() && UserSession.CurrentUser?.IsAdmin == true)
        {
            return true;
        }
        MessageBox.Show("Bu işlem için yönetici yetkisi gerekir.", "Yetki gerekli", MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
    }

    private bool RequireActiveUser()
    {
        if (RefreshCurrentUser())
        {
            return true;
        }
        MessageBox.Show("Kullanıcı hesabı etkin değil. Lütfen yeniden giriş yapın.", "Giriş gerekli", MessageBoxButton.OK, MessageBoxImage.Warning);
        Close();
        return false;
    }

    private bool RefreshCurrentUser()
    {
        if (UserSession.CurrentUser is not AppUser user)
        {
            return false;
        }
        var wasAdmin = user.IsAdmin;
        UserSession.CurrentUser = database.LoadUser(user.Id);
        if (UserSession.CurrentUser?.IsActive != true)
        {
            Close();
            return false;
        }
        if (UserSession.CurrentUser.IsAdmin != wasAdmin)
        {
            ConfigurePermissions();
        }
        return true;
    }

    private void ProductButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not Product product)
        {
            return;
        }

        AddProductToCart(product);
    }

    private void AddProductToCart(Product product)
    {
        var existing = cart.FirstOrDefault(item => item.Product == product);
        if (existing is null)
        {
            existing = new CartItem(product);
            cart.Add(existing);
        }
        else
        {
            existing.Quantity++;
        }

        selectedCartItem = existing;
        CartGrid.SelectedItem = existing;
        UpdateSelectedProduct();
        UpdateTotals();
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        barcodeScanTimer.Stop();
        var barcode = SearchBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return;
        }

        var product = products.FirstOrDefault(item =>
            string.Equals(item.Barcode, barcode, StringComparison.OrdinalIgnoreCase));
        if (product is null)
        {
            MessageBox.Show("Bu barkodla kayıtlı bir ürün bulunamadı.", "Ürün bulunamadı", MessageBoxButton.OK, MessageBoxImage.Information);
            SearchBox.SelectAll();
            return;
        }

        AddProductToCart(product);
        SearchBox.Clear();
        SearchBox.Focus();
    }

    private void BarcodeScanTimer_Tick(object? sender, EventArgs e)
    {
        barcodeScanTimer.Stop();
        if (!SearchBox.IsKeyboardFocusWithin)
        {
            return;
        }

        var barcode = SearchBox.Text.Trim();
        var product = products.FirstOrDefault(item =>
            string.Equals(item.Barcode, barcode, StringComparison.OrdinalIgnoreCase));
        if (product is null)
        {
            return;
        }

        AddProductToCart(product);
        SearchBox.Clear();
        SearchBox.Focus();
    }

    private void CartGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        selectedCartItem = CartGrid.SelectedItem as CartItem;
        UpdateSelectedProduct();
    }

    private void RemoveCartItemButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not CartItem item)
        {
            return;
        }

        cart.Remove(item);
        if (selectedCartItem == item)
        {
            selectedCartItem = null;
        }
        UpdateSelectedProduct();
        UpdateTotals();
    }

    private void IncreaseQuantityButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is CartItem clickedItem)
        {
            CartGrid.SelectedItem = clickedItem;
        }
        if (selectedCartItem is null) return;
        selectedCartItem.Quantity++;
        UpdateTotals();
    }

    private void DecreaseQuantityButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is CartItem clickedItem)
        {
            CartGrid.SelectedItem = clickedItem;
        }
        if (selectedCartItem is null) return;
        if (selectedCartItem.Quantity == 1)
        {
            cart.Remove(selectedCartItem);
            selectedCartItem = null;
        }
        else
        {
            selectedCartItem.Quantity--;
        }
        UpdateSelectedProduct();
        UpdateTotals();
    }

    private void ClearCartButton_Click(object sender, RoutedEventArgs e)
    {
        if (cart.Count > 0 && MessageBox.Show("Sepetteki tüm ürünler silinsin mi?", "Sepeti sıfırla", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        ResetCart();
    }

    private void ResetCart()
    {
        cart.Clear();
        selectedCartItem = null;
        UpdateSelectedProduct();
        UpdateTotals();
    }

    private void PaymentButton_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireActiveUser()) return;
        if (cart.Count == 0)
        {
            MessageBox.Show("Satışı tamamlamak için sepete ürün ekleyin.", "Sepet boş", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (cart.Any(item => item.Product.Stock < item.Quantity))
        {
            MessageBox.Show("Sepetteki ürünlerden birinin stoğu yetersiz.", "Yetersiz stok", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var paymentMethod = (sender as Button)?.Tag as string ?? "Nakit";
        var total = cart.Sum(item => item.LineTotal);
        var cashAmount = paymentMethod == "Nakit" ? total : 0;
        var cardAmount = paymentMethod == "Kart" ? total : 0;
        decimal cashChange = 0;
        if (paymentMethod == "Nakit")
        {
            var cashDialog = new CashPaymentDialog(total) { Owner = this };
            if (cashDialog.ShowDialog() != true)
            {
                return;
            }
            cashChange = cashDialog.Change;
        }
        if (paymentMethod == "Karma")
        {
            var mixedDialog = new MixedPaymentDialog(total) { Owner = this };
            if (mixedDialog.ShowDialog() != true)
            {
                return;
            }
            cashAmount = mixedDialog.CashAmount;
            cardAmount = mixedDialog.CardAmount;
        }
        Customer? creditCustomer = null;
        if (paymentMethod == "Veresiye")
        {
            var customerWindow = new CustomerSelectionWindow(total) { Owner = this };
            if (customerWindow.ShowDialog() != true || customerWindow.SelectedCustomer is null)
            {
                return;
            }
            creditCustomer = customerWindow.SelectedCustomer;
        }

        database.SaveSale(cart, total, cart.Sum(item => item.CostTotal), paymentMethod, creditCustomer?.Id, cashAmount, cardAmount);
        if (creditCustomer is not null)
        {
            creditCustomer.Balance = database.AddCustomerDebt(creditCustomer, total);
        }
        foreach (var item in cart)
        {
            item.Product.Stock -= item.Quantity;
            database.SaveProduct(item.Product);
        }
        RefreshProductList();
        sales.Add(new SaleRecord { Revenue = total, Cost = cart.Sum(item => item.CostTotal), PaymentMethod = paymentMethod, CashAmount = cashAmount, CardAmount = cardAmount });
        UpdateFinancialSummary();
        var paymentMessage = creditCustomer is not null
            ? $"Müşteri: {creditCustomer.Name}\nBu satış borcu: {total:N2} TL\nToplam borç: {creditCustomer.Balance:N2} TL"
            : paymentMethod == "Karma"
                ? $"Ödeme: Nakit {cashAmount:N2} TL + Kart {cardAmount:N2} TL"
                : paymentMethod == "Nakit"
                    ? $"Ödeme: Nakit {cashAmount:N2} TL\nPara üstü: {cashChange:N2} TL"
                    : $"Ödeme: {paymentMethod}";
        MessageBox.Show($"Satış tamamlandı.\n{paymentMessage}", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
        ResetCart();
    }

    private void AddProductButton_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdmin()) return;
        var dialog = new ProductDialog { Owner = this };
        var result = dialog.ShowDialog();
        ReloadCategories();
        if (result == true && dialog.Product is not null)
        {
            products.Add(dialog.Product);
            database.SaveProduct(dialog.Product);
            RefreshProductList();
            SearchBox.Clear();
            MessageBox.Show(this,
                $"{dialog.Product.Name} stoğa eklendi.\nBaşlangıç stoğu: {dialog.Product.Stock:N2} adet",
                "Ürün eklendi",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private void EditProductMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdmin()) return;
        if (sender is not MenuItem menuItem || menuItem.Parent is not ContextMenu contextMenu || contextMenu.PlacementTarget is not Button button || button.Tag is not Product product)
        {
            return;
        }

        var dialog = new ProductDialog(product) { Owner = this };
        var result = dialog.ShowDialog();
        ReloadCategories();
        if (result == true && dialog.Product is not null)
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
            RefreshProductList();
            UpdateTotals();
        }
    }

    private void DeleteProductMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdmin()) return;
        if (sender is not MenuItem menuItem || menuItem.Parent is not ContextMenu contextMenu || contextMenu.PlacementTarget is not Button button || button.Tag is not Product product)
        {
            return;
        }

        if (MessageBox.Show($"'{product.Name}' ürününü katalogdan silmek istiyor musunuz?", "Ürünü sil", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        products.Remove(product);
        database.DeleteProduct(product);
        visibleProducts.Remove(product);
        var cartItem = cart.FirstOrDefault(item => item.Product == product);
        if (cartItem is not null)
        {
            cart.Remove(cartItem);
            if (selectedCartItem == cartItem)
            {
                selectedCartItem = null;
            }
        }
        RefreshProductList();
        UpdateSelectedProduct();
        UpdateTotals();
    }

    private void ProductsButton_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdmin()) return;
        var window = new ProductListWindow(products) { Owner = this };
        window.ShowDialog();
        ReloadCategories();
        if (window.AddedProduct is { } addedProduct)
        {
            MessageBox.Show(this,
                $"{addedProduct.Name} stoğa eklendi.\nBaşlangıç stoğu: {addedProduct.Stock:N2} adet",
                "Ürün eklendi",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private void StockEntryButton_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdmin()) return;
        var window = new StockEntryWindow(products, database) { Owner = this };
        window.ShowDialog();
        ReloadCategories();
        if (window.AddedProduct is { } addedProduct)
        {
            var stockMessage = window.AddedToExistingProduct
                ? $"Güncel stok: {addedProduct.Stock:N2} adet"
                : $"Başlangıç stoğu: {addedProduct.Stock:N2} adet";
            MessageBox.Show(this,
                $"{addedProduct.Name} stoğa eklendi.\n{stockMessage}",
                window.AddedToExistingProduct ? "Stok güncellendi" : "Ürün eklendi",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private void AddExpenseButton_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdmin()) return;
        var dialog = new ExpenseDialog { Owner = this };
        if (dialog.ShowDialog() == true && dialog.Expense is not null)
        {
            expenses.Add(dialog.Expense);
            database.SaveExpense(dialog.Expense);
            UpdateFinancialSummary();
        }
    }

    private void StartDayButton_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdmin()) return;
        if (!decimal.TryParse(OpeningCashBox.Text.Trim().Replace('.', ','), out var amount) || amount < 0)
        {
            MessageBox.Show("Geçerli bir başlangıç kasa tutarı girin.", "Hatalı tutar", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        openingCash = amount;
        database.SetSetting("opening_cash", openingCash.ToString(System.Globalization.CultureInfo.InvariantCulture));
        OpeningCashBox.Text = amount.ToString("N2");
        OpeningCashSummaryText.Text = $"{openingCash:N2} TL";
        MessageBox.Show($"Gün başlangıç kasası {openingCash:N2} TL olarak kaydedildi.", "Gün başladı", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void DayEndButton_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdmin()) return;
        var report = new DayEndReportWindow(DateTime.Now, openingCash, sales, expenses) { Owner = this };
        report.ShowDialog();
    }

    private void SalesHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdmin()) return;
        var window = new SalesHistoryWindow { Owner = this };
        window.ShowDialog();
        RefreshProductStocksFromDatabase();
    }

    private void RefreshProductStocksFromDatabase()
    {
        var storedProducts = database.LoadProducts();
        foreach (var product in products)
        {
            var storedProduct = storedProducts.FirstOrDefault(candidate => candidate.Id == product.Id);
            if (storedProduct is not null)
            {
                product.Stock = storedProduct.Stock;
                product.MinimumStock = storedProduct.MinimumStock;
            }
        }
        RefreshProductList();
    }

    private void CustomersButton_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireActiveUser()) return;
        var window = new CustomerWindow { Owner = this };
        window.ShowDialog();
    }

    private void BackupButton_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdmin()) return;
        var dialog = new SaveFileDialog
        {
            Filter = "SQLite yedek dosyası (*.db)|*.db",
            FileName = $"satis_takip_{DateTime.Now:yyyyMMdd_HHmm}.db",
            Title = "Veritabanı yedeğini kaydet"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        database.Backup(dialog.FileName);
        MessageBox.Show("Veritabanı yedeği oluşturuldu.", "Yedekleme", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdmin()) return;
        var dialog = new OpenFileDialog
        {
            Filter = "SQLite yedek dosyası (*.db)|*.db",
            Title = "Geri yüklenecek yedeği seçin"
        };
        if (dialog.ShowDialog() != true) return;
        if (MessageBox.Show("Mevcut verilerin üzerine yedek geri yüklensin mi?", "Geri yükleme", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        database.Restore(dialog.FileName);
        MessageBox.Show("Yedek geri yüklendi. Değişikliklerin görünmesi için uygulama yeniden açılacak.", "Geri yükleme", MessageBoxButton.OK, MessageBoxImage.Information);
        Application.Current.Shutdown();
    }

    private void AdminSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdmin()) return;
        var window = new AdminSettingsWindow { Owner = this };
        window.ShowDialog();
    }

    private void StockAlertsButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshProductStocksFromDatabase();
        var window = new CriticalStockWindow(products) { Owner = this };
        window.ShowDialog();
    }

    private void StockMovementsButton_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdmin()) return;
        var window = new StockMovementWindow(database) { Owner = this };
        window.ShowDialog();
    }

    private void ReportsButton_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdmin()) return;
        var window = new SalesReportWindow(database) { Owner = this };
        window.ShowDialog();
    }

    private void UserManagementButton_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdmin()) return;
        var window = new UserManagementWindow(database) { Owner = this };
        window.ShowDialog();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshProductList();
        barcodeScanTimer.Stop();

        var barcode = SearchBox.Text.Trim();
        if (products.Any(product => string.Equals(product.Barcode, barcode, StringComparison.OrdinalIgnoreCase)))
        {
            barcodeScanTimer.Start();
        }

    }

    private void ImportExcelButton_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdmin()) return;
        var dialog = new OpenFileDialog
        {
            Filter = "Excel dosyaları (*.xlsx)|*.xlsx|Tüm dosyalar (*.*)|*.*",
            Title = "Ürün Excel dosyasını seçin"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var imported = 0;
            using var workbook = new XLWorkbook(dialog.FileName);
            var sheet = workbook.Worksheets.First();
            foreach (var row in sheet.RowsUsed().Skip(1))
            {
                var name = row.Cell(2).GetString().Trim();
                if (string.IsNullOrWhiteSpace(name)) continue;
                var priceText = row.Cell(4).GetString().Trim().Replace('.', ',');
                if (!decimal.TryParse(priceText, out var price)) continue;
                var costText = row.Cell(5).GetString().Trim().Replace('.', ',');
                decimal.TryParse(costText, out var cost);

                var barcode = row.Cell(1).GetString().Trim();
                var existing = products.FirstOrDefault(product => product.Barcode == barcode && barcode.Length > 0);
                var minimumStockText = row.Cell(7).GetString().Trim().Replace('.', ',');
                if (existing is not null)
                {
                    existing.Name = name;
                    existing.Category = row.Cell(3).GetString().Trim();
                    existing.CostPrice = cost;
                    existing.Price = price;
                    existing.Stock = (decimal)row.Cell(6).GetDouble();
                    if (decimal.TryParse(minimumStockText, out var minimumStock) && minimumStock >= 0)
                    {
                        existing.MinimumStock = minimumStock;
                    }
                    database.SaveProduct(existing);
                }
                else
                {
                    var product = new Product { Barcode = barcode, Name = name, Category = row.Cell(3).GetString().Trim(), CostPrice = cost, Price = price, Stock = (decimal)row.Cell(6).GetDouble() };
                    if (decimal.TryParse(minimumStockText, out var minimumStock) && minimumStock >= 0)
                    {
                        product.MinimumStock = minimumStock;
                    }
                    products.Add(product);
                    database.SaveProduct(product);
                }
                imported++;
            }

            ReloadCategories();
            MessageBox.Show($"{imported} ürün içe aktarıldı.", "Excel aktarımı", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show($"Excel dosyası okunamadı.\n{exception.Message}", "Aktarım hatası", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Cart_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (CartItem item in e.NewItems)
            {
                item.PropertyChanged += CartItem_PropertyChanged;
            }
        }
        UpdateTotals();
    }

    private void CartItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateTotals();
        UpdateSelectedProduct();
    }

    private void UpdateSelectedProduct()
    {
        if (selectedCartItem is null)
        {
            SelectedProductText.Text = "Bir ürün seçin";
        }
        else
        {
            SelectedProductText.Text = selectedCartItem.Name;
        }
        QuantityText.Text = selectedCartItem?.Quantity.ToString() ?? "1";
    }

    private void UpdateTotals()
    {
        var total = cart.Sum(item => item.LineTotal);
        TotalText.Text = $"{total:N2} TL";
    }

    private void UpdateFinancialSummary()
    {
        var revenue = sales.Sum(item => item.Revenue);
        var cost = sales.Sum(item => item.Cost);
        var expenseTotal = expenses.Sum(item => item.Amount);
        TodaySalesText.Text = $"{revenue:N2} TL";
        TodayExpenseText.Text = $"{expenseTotal:N2} TL";
        TodayProfitText.Text = $"{revenue - cost - expenseTotal:N2} TL";
        OpeningCashSummaryText.Text = $"{openingCash:N2} TL";
    }
}
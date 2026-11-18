using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SatisTakip;

public sealed class CustomerSelectionWindow : Window
{
    private readonly DatabaseService database = new();
    private readonly ObservableCollection<Customer> customers = new();
    private readonly DataGrid grid = new();
    public Customer? SelectedCustomer { get; private set; }

    public CustomerSelectionWindow(decimal amount)
    {
        Title = "Veresiye müşterisi seçin";
        Width = 720;
        Height = 540;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        foreach (var customer in database.LoadCustomers()) customers.Add(customer);

        var root = new DockPanel { Margin = new Thickness(22) };
        var title = new TextBlock { Text = $"Bu satışın veresiye tutarı: {amount:N2} TL", FontSize = 20, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 14) };
        DockPanel.SetDock(title, Dock.Top);
        root.Children.Add(title);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 0, 14) };
        var add = new Button { Content = "＋ Yeni müşteri", Padding = new Thickness(14, 8, 14, 8), Background = new SolidColorBrush(Color.FromRgb(23, 107, 135)), Foreground = Brushes.White, BorderThickness = new Thickness(0) };
        add.Click += Add_Click;
        var select = new Button { Content = "Müşteriyi seç ve borca ekle", Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(8, 0, 0, 0), Background = new SolidColorBrush(Color.FromRgb(23, 107, 135)), Foreground = Brushes.White, BorderThickness = new Thickness(0) };
        select.Click += Select_Click;
        actions.Children.Add(add);
        actions.Children.Add(select);
        DockPanel.SetDock(actions, Dock.Top);
        root.Children.Add(actions);

        grid.ItemsSource = customers;
        grid.AutoGenerateColumns = false;
        grid.IsReadOnly = true;
        grid.CanUserAddRows = false;
        grid.SelectionMode = DataGridSelectionMode.Single;
        grid.Columns.Add(new DataGridTextColumn { Header = "Ad soyad", Binding = new System.Windows.Data.Binding(nameof(Customer.Name)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Telefon", Binding = new System.Windows.Data.Binding(nameof(Customer.Phone)), Width = 150 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Mevcut borç", Binding = new System.Windows.Data.Binding(nameof(Customer.BalanceText)), Width = 125 });
        root.Children.Add(grid);
        Content = root;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new CustomerEntryDialog { Owner = this };
        if (dialog.ShowDialog() == true && dialog.Customer is not null)
        {
            var id = database.SaveCustomer(dialog.Customer);
            customers.Clear();
            foreach (var customer in database.LoadCustomers()) customers.Add(customer);
            grid.SelectedItem = customers.FirstOrDefault(customer => customer.Id == id);
        }
    }

    private void Select_Click(object sender, RoutedEventArgs e)
    {
        if (grid.SelectedItem is not Customer customer)
        {
            MessageBox.Show("Veresiye için bir müşteri seçin veya yeni müşteri oluşturun.", "Müşteri seçilmedi", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        SelectedCustomer = customer;
        DialogResult = true;
    }
}

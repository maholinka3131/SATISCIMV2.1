using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SatisTakip;

public sealed class CustomerWindow : Window
{
    private readonly DatabaseService database = new();
    private readonly ObservableCollection<Customer> customers = new();
    private readonly DataGrid grid = new();

    public CustomerWindow()
    {
        Title = "Müşteriler ve Veresiye";
        Width = 720;
        Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        foreach (var customer in database.LoadCustomers()) customers.Add(customer);

        var root = new DockPanel { Margin = new Thickness(22) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 0, 14) };
        var add = new Button { Content = "＋ Müşteri ekle", Padding = new Thickness(14, 8, 14, 8), Background = new SolidColorBrush(Color.FromRgb(23, 107, 135)), Foreground = Brushes.White };
        add.Click += Add_Click;
        var payment = new Button { Content = "Tahsilat al", Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(8, 0, 0, 0) };
        payment.Click += Payment_Click;
        var refresh = new Button { Content = "Yenile", Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(8, 0, 0, 0) };
        refresh.Click += (_, _) => ReloadCustomers();
        buttons.Children.Add(add);
        buttons.Children.Add(payment);
        buttons.Children.Add(refresh);
        DockPanel.SetDock(buttons, Dock.Top);
        root.Children.Add(buttons);

        grid.ItemsSource = customers;
        grid.AutoGenerateColumns = false;
        grid.IsReadOnly = true;
        grid.CanUserAddRows = false;
        grid.Columns.Add(new DataGridTextColumn { Header = "Müşteri", Binding = new System.Windows.Data.Binding(nameof(Customer.Name)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Telefon", Binding = new System.Windows.Data.Binding(nameof(Customer.Phone)), Width = 150 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Borç", Binding = new System.Windows.Data.Binding(nameof(Customer.BalanceText)), Width = 120 });
        root.Children.Add(grid);
        Content = root;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var name = Prompt("Müşteri adı:", "Yeni müşteri");
        if (string.IsNullOrWhiteSpace(name)) return;
        var phone = Prompt("Telefon (isteğe bağlı):", "Telefon");
        var customer = new Customer { Name = name.Trim(), Phone = phone.Trim() };
        database.SaveCustomer(customer);
        ReloadCustomers();
    }

    private void Payment_Click(object sender, RoutedEventArgs e)
    {
        if (grid.SelectedItem is not Customer customer)
        {
            MessageBox.Show("Tahsilat için müşteri seçin.");
            return;
        }
        if (!decimal.TryParse(Prompt("Tahsilat tutarı:", "Tahsilat"), out var amount) || amount <= 0) return;
        database.SaveCustomerPayment(customer, amount, "Müşteri tahsilatı");
        customer.Balance = Math.Max(0, customer.Balance - amount);
        grid.Items.Refresh();
    }

    private void ReloadCustomers()
    {
        customers.Clear();
        foreach (var item in database.LoadCustomers()) customers.Add(item);
        grid.Items.Refresh();
    }

    private static string Prompt(string message, string title)
    {
        var window = new Window { Title = title, Width = 320, Height = 170, WindowStartupLocation = WindowStartupLocation.CenterScreen };
        var panel = new StackPanel { Margin = new Thickness(18) };
        panel.Children.Add(new TextBlock { Text = message, Margin = new Thickness(0, 0, 0, 8) });
        var input = new TextBox { Padding = new Thickness(7) };
        panel.Children.Add(input);
        var button = new Button { Content = "Tamam", Padding = new Thickness(12, 6, 12, 6), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        button.Click += (_, _) => window.DialogResult = true;
        panel.Children.Add(button);
        window.Content = panel;
        return window.ShowDialog() == true ? input.Text : "";
    }
}

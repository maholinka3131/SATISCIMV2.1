using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Data.Sqlite;

namespace SatisTakip;

public sealed class UserManagementWindow : Window
{
    private readonly DatabaseService database;
    private readonly ObservableCollection<AppUser> users = new();
    private readonly DataGrid usersGrid = new();
    private readonly TextBox usernameBox = new();
    private readonly PasswordBox passwordBox = new();
    private readonly ComboBox newRoleBox = new();
    private readonly ComboBox selectedRoleBox = new();
    private readonly PasswordBox selectedPasswordBox = new();
    private readonly Button updateUserButton = new();
    private readonly Button toggleUserButton = new();
    private AppUser? selectedUser;

    public UserManagementWindow(DatabaseService database)
    {
        this.database = database;
        Title = "Kullanıcı yönetimi";
        Width = 780;
        Height = 760;
        MinWidth = 680;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new Grid { Margin = new Thickness(22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        header.Children.Add(new TextBlock { Text = "Kullanıcı yönetimi", FontSize = 23, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(29, 41, 57)) });
        header.Children.Add(new TextBlock { Text = "Yöneticiler tüm işlemleri; kasiyerler satış ve müşteri/veresiye işlemlerini yapabilir.", Foreground = Brushes.Gray, Margin = new Thickness(0, 4, 0, 0) });
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        usersGrid.ItemsSource = users;
        usersGrid.AutoGenerateColumns = false;
        usersGrid.CanUserAddRows = false;
        usersGrid.IsReadOnly = true;
        usersGrid.SelectionMode = DataGridSelectionMode.Single;
        usersGrid.SelectionChanged += UsersGrid_SelectionChanged;
        usersGrid.Columns.Add(new DataGridTextColumn { Header = "Kullanıcı adı", Binding = new System.Windows.Data.Binding(nameof(AppUser.Username)), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
        usersGrid.Columns.Add(new DataGridTextColumn { Header = "Rol", Binding = new System.Windows.Data.Binding(nameof(AppUser.RoleText)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        usersGrid.Columns.Add(new DataGridCheckBoxColumn { Header = "Etkin", Binding = new System.Windows.Data.Binding(nameof(AppUser.IsActive)), Width = 75 });
        Grid.SetRow(usersGrid, 1);
        root.Children.Add(usersGrid);

        var forms = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        var addGroup = new GroupBox { Header = "Yeni kullanıcı", Padding = new Thickness(10) };
        var addPanel = new Grid();
        addPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        addPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        addPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
        addPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
        addPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        addPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        usernameBox.Margin = new Thickness(0, 0, 8, 0);
        usernameBox.Padding = new Thickness(7);
        usernameBox.ToolTip = "Kullanıcı adı";
        passwordBox.Margin = new Thickness(0, 0, 8, 0);
        passwordBox.Padding = new Thickness(7);
        passwordBox.ToolTip = "En az 8 karakter";
        SetRoles(newRoleBox);
        newRoleBox.Margin = new Thickness(0, 0, 8, 0);
        var addButton = new Button { Content = "Kullanıcı ekle", Padding = new Thickness(10, 6, 10, 6) };
        addButton.Click += AddUser_Click;
        AddFieldLabel(addPanel, "Kullanıcı adı", 0);
        AddFieldLabel(addPanel, "Parola (en az 8 karakter)", 1);
        AddFieldLabel(addPanel, "Rol", 2);
        Grid.SetRow(usernameBox, 1);
        addPanel.Children.Add(usernameBox);
        Grid.SetRow(passwordBox, 1);
        Grid.SetColumn(passwordBox, 1);
        addPanel.Children.Add(passwordBox);
        Grid.SetRow(newRoleBox, 1);
        Grid.SetColumn(newRoleBox, 2);
        addPanel.Children.Add(newRoleBox);
        Grid.SetRow(addButton, 1);
        Grid.SetColumn(addButton, 3);
        addPanel.Children.Add(addButton);
        addGroup.Content = addPanel;
        forms.Children.Add(addGroup);

        var editGroup = new GroupBox { Header = "Seçili kullanıcı", Padding = new Thickness(10), Margin = new Thickness(0, 8, 0, 0) };
        var editPanel = new Grid();
        editPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        editPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        editPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        editPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        editPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        editPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        SetRoles(selectedRoleBox);
        selectedRoleBox.Margin = new Thickness(0, 0, 8, 0);
        selectedRoleBox.IsEnabled = false;
        selectedPasswordBox.Margin = new Thickness(0, 0, 8, 0);
        selectedPasswordBox.Padding = new Thickness(7);
        selectedPasswordBox.ToolTip = "Parolayı değiştirmek istemiyorsanız boş bırakın";
        selectedPasswordBox.IsEnabled = false;
        updateUserButton.Content = "Rol / parola uygula";
        updateUserButton.Padding = new Thickness(10, 6, 10, 6);
        updateUserButton.IsEnabled = false;
        updateUserButton.Click += UpdateUser_Click;
        toggleUserButton.Content = "Etkinliği değiştir";
        toggleUserButton.Padding = new Thickness(10, 6, 10, 6);
        toggleUserButton.IsEnabled = false;
        toggleUserButton.Margin = new Thickness(8, 0, 0, 0);
        toggleUserButton.Click += ToggleUser_Click;
        AddFieldLabel(editPanel, "Rol", 0);
        AddFieldLabel(editPanel, "Yeni parola (isteğe bağlı)", 1);
        Grid.SetRow(selectedRoleBox, 1);
        editPanel.Children.Add(selectedRoleBox);
        Grid.SetRow(selectedPasswordBox, 1);
        Grid.SetColumn(selectedPasswordBox, 1);
        editPanel.Children.Add(selectedPasswordBox);
        Grid.SetRow(updateUserButton, 1);
        Grid.SetColumn(updateUserButton, 2);
        editPanel.Children.Add(updateUserButton);
        Grid.SetRow(toggleUserButton, 1);
        Grid.SetColumn(toggleUserButton, 3);
        editPanel.Children.Add(toggleUserButton);
        editGroup.Content = editPanel;
        forms.Children.Add(editGroup);
        Grid.SetRow(forms, 2);
        root.Children.Add(forms);

        Content = root;
        ReloadUsers();
    }

    private static void SetRoles(ComboBox comboBox)
    {
        comboBox.Items.Add(new ComboBoxItem { Content = "Yönetici", Tag = "Admin" });
        comboBox.Items.Add(new ComboBoxItem { Content = "Kasiyer", Tag = "Cashier" });
        comboBox.SelectedIndex = 1;
    }

    private static void AddFieldLabel(Grid grid, string text, int column)
    {
        var label = new TextBlock { Text = text, Foreground = Brushes.Gray, Margin = new Thickness(0, 0, 8, 4) };
        Grid.SetColumn(label, column);
        grid.Children.Add(label);
    }

    private void ReloadUsers()
    {
        users.Clear();
        foreach (var user in database.LoadUsers())
        {
            users.Add(user);
        }
    }

    private void UsersGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        selectedUser = usersGrid.SelectedItem as AppUser;
        var enabled = selectedUser is not null;
        selectedRoleBox.IsEnabled = enabled;
        selectedPasswordBox.IsEnabled = enabled;
        updateUserButton.IsEnabled = enabled;
        toggleUserButton.IsEnabled = enabled;
        if (selectedUser is null)
        {
            toggleUserButton.Content = "Etkinliği değiştir";
            return;
        }

        selectedRoleBox.SelectedIndex = selectedUser.IsAdmin ? 0 : 1;
        toggleUserButton.Content = selectedUser.IsActive ? "Devre dışı bırak" : "Etkinleştir";
        selectedPasswordBox.Clear();
    }

    private void AddUser_Click(object sender, RoutedEventArgs e)
    {
        var role = (newRoleBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
        try
        {
            database.SaveUser(usernameBox.Text, passwordBox.Password, role);
            usernameBox.Clear();
            passwordBox.Clear();
            ReloadUsers();
        }
        catch (ArgumentException exception)
        {
            MessageBox.Show(exception.Message, "Kullanıcı eklenemedi", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (SqliteException exception)
        {
            MessageBox.Show($"Kullanıcı eklenemedi. Kullanıcı adı daha önce kullanılıyor olabilir.\n{exception.Message}", "Kullanıcı eklenemedi", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void UpdateUser_Click(object sender, RoutedEventArgs e)
    {
        if (selectedUser is null) return;
        var role = (selectedRoleBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
        try
        {
            database.UpdateUser(selectedUser.Id, role, selectedPasswordBox.Password, UserSession.CurrentUser?.Username ?? "");
            selectedPasswordBox.Clear();
            ReloadUsers();
        }
        catch (ArgumentException exception)
        {
            MessageBox.Show(exception.Message, "Kullanıcı güncellenemedi", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (InvalidOperationException exception)
        {
            MessageBox.Show(exception.Message, "Kullanıcı güncellenemedi", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ToggleUser_Click(object sender, RoutedEventArgs e)
    {
        if (selectedUser is null) return;
        try
        {
            database.SetUserActive(selectedUser.Id, !selectedUser.IsActive, UserSession.CurrentUser?.Username ?? "");
            ReloadUsers();
        }
        catch (InvalidOperationException exception)
        {
            MessageBox.Show(exception.Message, "Kullanıcı güncellenemedi", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}

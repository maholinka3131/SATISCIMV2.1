using System.Windows;
using System.Windows.Input;

namespace SatisTakip;

public sealed partial class LoginWindow : Window
{
    private readonly DatabaseService database = new();
    public AppUser? User { get; private set; }

    public LoginWindow()
    {
        InitializeComponent();
    }

    private void LoginWindow_Loaded(object sender, RoutedEventArgs e)
        => usernameBox.Focus();

    private void MinimizeWindowButton_Click(object sender, RoutedEventArgs e)
        => WindowState = System.Windows.WindowState.Minimized;

    private void CloseWindowButton_Click(object sender, RoutedEventArgs e)
        => Close();

    private void UsernameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            passwordBox.Focus();
            e.Handled = true;
        }
    }

    private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Login_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private void Login_Click(object sender, RoutedEventArgs e)
    {
        User = database.AuthenticateUser(usernameBox.Text, passwordBox.Password);
        if (User is null)
        {
            errorText.Text = "Kullanıcı adı veya parola hatalı. Bilgilerinizi kontrol edip tekrar deneyin.";
            passwordBox.Clear();
            passwordBox.Focus();
            return;
        }
        errorText.Text = "";
        DialogResult = true;
    }
}

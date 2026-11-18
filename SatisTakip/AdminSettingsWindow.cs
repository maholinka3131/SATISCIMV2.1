using Microsoft.Data.Sqlite;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SatisTakip;

public sealed class AdminSettingsWindow : Window
{
    private readonly DatabaseService database = new();
    private readonly PasswordBox currentPasswordBox = new();
    private readonly PasswordBox newPasswordBox = new();
    private readonly PasswordBox confirmPasswordBox = new();
    private readonly PasswordBox resetPasswordBox = new();
    private readonly Button resetButton = new();
    private readonly TextBlock passwordStatusText = new();
    private readonly TextBlock resetStatusText = new();

    public AdminSettingsWindow()
    {
        Title = "Yönetici ayarları";
        Width = 540;
        Height = 720;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;
        MinHeight = 560;
        Background = Brushes.White;

        var panel = new StackPanel { Margin = new Thickness(30) };
        panel.Children.Add(new TextBlock { Text = "Yönetici ayarları", FontSize = 25, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(29, 41, 57)) });
        panel.Children.Add(new TextBlock { Text = "Yönetici hesabınızın giriş parolasını buradan değiştirebilirsiniz.", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Gray, Margin = new Thickness(0, 8, 0, 20) });

        panel.Children.Add(new TextBlock { Text = "Yönetici giriş parolasını değiştir", FontSize = 18, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(29, 41, 57)), Margin = new Thickness(0, 0, 0, 12) });
        AddPasswordField(panel, "Mevcut parola", currentPasswordBox);
        AddPasswordField(panel, "Yeni parola (en az 8 karakter)", newPasswordBox);
        AddPasswordField(panel, "Yeni parola (tekrar)", confirmPasswordBox);
        var changePassword = new Button { Content = "Yönetici parolasını değiştir", Height = 42, Background = new SolidColorBrush(Color.FromRgb(23, 107, 135)), Foreground = Brushes.White, BorderThickness = new Thickness(0), FontSize = 14 };
        changePassword.Click += ChangePassword_Click;
        panel.Children.Add(changePassword);
        passwordStatusText.TextWrapping = TextWrapping.Wrap;
        passwordStatusText.Margin = new Thickness(0, 10, 0, 18);
        panel.Children.Add(passwordStatusText);

        panel.Children.Add(new Separator { Margin = new Thickness(0, 0, 0, 18) });
        panel.Children.Add(new TextBlock { Text = "Fabrika ayarlarına dön", FontSize = 18, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(29, 41, 57)) });
        panel.Children.Add(new TextBlock { Text = "Bu işlemden önce yönetici parolasını doğrulayın.", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Gray, Margin = new Thickness(0, 6, 0, 12) });
        AddPasswordField(panel, "Yönetici parolası", resetPasswordBox);
        var verify = new Button { Content = "Parolayı doğrula", Height = 42, Background = new SolidColorBrush(Color.FromRgb(23, 107, 135)), Foreground = Brushes.White, BorderThickness = new Thickness(0), FontSize = 14 };
        verify.Click += Verify_Click;
        panel.Children.Add(verify);
        resetStatusText.Foreground = Brushes.Gray;
        resetStatusText.Margin = new Thickness(0, 10, 0, 12);
        panel.Children.Add(resetStatusText);
        resetButton.Content = "Tüm verileri sil ve fabrika ayarlarına dön";
        resetButton.Height = 50;
        resetButton.IsEnabled = false;
        resetButton.Background = new SolidColorBrush(Color.FromRgb(194, 65, 58));
        resetButton.Foreground = Brushes.White;
        resetButton.BorderThickness = new Thickness(0);
        resetButton.FontWeight = FontWeights.Bold;
        resetButton.Click += ResetButton_Click;
        panel.Children.Add(resetButton);
        panel.Children.Add(new TextBlock { Text = "Bu işlem ürünleri, stokları, satışları, müşterileri, giderleri ve ayarları kalıcı olarak siler. Yönetici hesabı korunur.", TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(194, 65, 58)), Margin = new Thickness(0, 14, 0, 0) });
        Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = panel };
    }

    private static void AddPasswordField(Panel panel, string label, PasswordBox passwordBox)
    {
        panel.Children.Add(new TextBlock { Text = label, Foreground = Brushes.Gray });
        passwordBox.Margin = new Thickness(0, 6, 0, 12);
        passwordBox.Padding = new Thickness(10);
        panel.Children.Add(passwordBox);
    }

    private void ChangePassword_Click(object sender, RoutedEventArgs e)
    {
        if (newPasswordBox.Password != confirmPasswordBox.Password)
        {
            passwordStatusText.Text = "Yeni parola ve tekrarı eşleşmiyor.";
            passwordStatusText.Foreground = Brushes.Firebrick;
            return;
        }

        try
        {
            if (!database.ChangeAdminPassword(currentPasswordBox.Password, newPasswordBox.Password))
            {
                passwordStatusText.Text = "Mevcut yönetici parolası hatalı.";
                passwordStatusText.Foreground = Brushes.Firebrick;
                return;
            }
        }
        catch (ArgumentException exception)
        {
            passwordStatusText.Text = exception.Message;
            passwordStatusText.Foreground = Brushes.Firebrick;
            return;
        }
        catch (SqliteException exception)
        {
            MessageBox.Show($"Yönetici parolası güncellenemedi.\n{exception.Message}", "Parola değiştirilemedi", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        currentPasswordBox.Clear();
        newPasswordBox.Clear();
        confirmPasswordBox.Clear();
        resetPasswordBox.Clear();
        resetButton.IsEnabled = false;
        resetStatusText.Text = "";
        passwordStatusText.Text = "Yönetici parolası değiştirildi. Uygulamaya girişte yeni parolayı kullanın.";
        passwordStatusText.Foreground = Brushes.DarkGreen;
    }

    private void Verify_Click(object sender, RoutedEventArgs e)
    {
        if (!database.ValidateAdmin(resetPasswordBox.Password))
        {
            resetButton.IsEnabled = false;
            resetStatusText.Text = "Yönetici parolası hatalı.";
            resetStatusText.Foreground = Brushes.Firebrick;
            return;
        }
        resetButton.IsEnabled = true;
        resetStatusText.Text = "Parola doğrulandı. Sıfırlama düğmesi aktif.";
        resetStatusText.Foreground = Brushes.DarkGreen;
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Tüm ürünler, satışlar, müşteriler, giderler ve ayarlar silinecek. Devam edilsin mi?", "Fabrika ayarları", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }
        database.ResetAllBusinessData();
        MessageBox.Show("Veriler temizlendi. Uygulama temiz veritabanıyla yeniden açılacak.", "Tamamlandı", MessageBoxButton.OK, MessageBoxImage.Information);
        Application.Current.Shutdown();
    }
}

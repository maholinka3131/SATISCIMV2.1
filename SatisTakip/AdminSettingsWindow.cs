using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SatisTakip;

public sealed class AdminSettingsWindow : Window
{
    private readonly DatabaseService database = new();
    private readonly PasswordBox passwordBox = new();
    private readonly Button resetButton = new();
    private readonly TextBlock statusText = new();

    public AdminSettingsWindow()
    {
        Title = "Yönetici ayarları";
        Width = 500;
        Height = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = Brushes.White;

        var panel = new StackPanel { Margin = new Thickness(30) };
        panel.Children.Add(new TextBlock { Text = "Yönetici ayarları", FontSize = 25, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(29, 41, 57)) });
        panel.Children.Add(new TextBlock { Text = "Fabrika ayarlarına dönmeden önce yönetici parolasını doğrulayın.", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Gray, Margin = new Thickness(0, 8, 0, 22) });
        panel.Children.Add(new TextBlock { Text = "Yönetici parolası", Foreground = Brushes.Gray });
        passwordBox.Margin = new Thickness(0, 6, 0, 14);
        passwordBox.Padding = new Thickness(10);
        panel.Children.Add(passwordBox);
        var verify = new Button { Content = "Parolayı doğrula", Height = 42, Background = new SolidColorBrush(Color.FromRgb(23, 107, 135)), Foreground = Brushes.White, BorderThickness = new Thickness(0), FontSize = 14 };
        verify.Click += Verify_Click;
        panel.Children.Add(verify);
        statusText.Foreground = Brushes.Gray;
        statusText.Margin = new Thickness(0, 12, 0, 16);
        panel.Children.Add(statusText);
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
        Content = panel;
    }

    private void Verify_Click(object sender, RoutedEventArgs e)
    {
        if (!database.ValidateAdmin(passwordBox.Password))
        {
            resetButton.IsEnabled = false;
            statusText.Text = "Yönetici parolası hatalı.";
            statusText.Foreground = Brushes.Firebrick;
            return;
        }
        resetButton.IsEnabled = true;
        statusText.Text = "Parola doğrulandı. Sıfırlama düğmesi aktif.";
        statusText.Foreground = Brushes.DarkGreen;
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

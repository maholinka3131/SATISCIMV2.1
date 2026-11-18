using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SatisTakip;

public sealed class CashPaymentDialog : Window
{
    private readonly decimal total;
    private readonly TextBox receivedBox = new();
    private readonly TextBlock changeText = new();

    public decimal AmountReceived { get; private set; }
    public decimal Change => AmountReceived - total;

    public CashPaymentDialog(decimal total)
    {
        this.total = total;
        Title = "Nakit ödeme";
        Width = 430;
        Height = 390;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;

        var panel = new StackPanel { Margin = new Thickness(28) };
        panel.Children.Add(new TextBlock
        {
            Text = "Nakit ödeme",
            FontSize = 23,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 8)
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"Sepet tutarı: {total:N2} TL",
            FontSize = 15,
            Foreground = Brushes.Gray,
            Margin = new Thickness(0, 0, 0, 22)
        });

        panel.Children.Add(new TextBlock
        {
            Text = "Müşteriden alınan para (TL)",
            Foreground = Brushes.Gray,
            Margin = new Thickness(0, 0, 0, 5)
        });
        receivedBox.Text = "0,00";
        receivedBox.Padding = new Thickness(10);
        receivedBox.Margin = new Thickness(0, 0, 0, 14);
        receivedBox.FontSize = 16;
        receivedBox.GotFocus += (_, _) =>
        {
            if (receivedBox.Text is "0,00" or "0.00")
            {
                receivedBox.Clear();
            }
            receivedBox.SelectAll();
        };
        receivedBox.TextChanged += ReceivedBox_TextChanged;
        panel.Children.Add(receivedBox);

        var changeBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(232, 245, 233)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(165, 214, 167)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14),
            Margin = new Thickness(0, 0, 0, 18)
        };
        var changePanel = new DockPanel();
        changePanel.Children.Add(new TextBlock
        {
            Text = "Para üstü",
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });
        changeText.Text = "0,00 TL";
        changeText.FontSize = 20;
        changeText.FontWeight = FontWeights.Bold;
        changeText.Foreground = new SolidColorBrush(Color.FromRgb(46, 125, 50));
        changeText.HorizontalAlignment = HorizontalAlignment.Right;
        DockPanel.SetDock(changeText, Dock.Right);
        changePanel.Children.Add(changeText);
        changeBorder.Child = changePanel;
        panel.Children.Add(changeBorder);

        var confirm = new Button
        {
            Content = "Ödemeyi onayla",
            Height = 46,
            Background = new SolidColorBrush(Color.FromRgb(23, 107, 135)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            FontSize = 15
        };
        confirm.Click += Confirm_Click;
        panel.Children.Add(confirm);

        Content = panel;
    }

    private void ReceivedBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!TryParseAmount(receivedBox.Text, out var received))
        {
            changeText.Text = "Geçersiz tutar";
            changeText.Foreground = Brushes.Red;
            return;
        }

        var change = received - total;
        changeText.Text = $"{Math.Max(0, change):N2} TL";
        changeText.Foreground = change >= 0
            ? new SolidColorBrush(Color.FromRgb(46, 125, 50))
            : Brushes.Red;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParseAmount(receivedBox.Text, out var received) || received < 0)
        {
            MessageBox.Show("Geçerli bir alınan para tutarı girin.", "Geçersiz tutar", MessageBoxButton.OK, MessageBoxImage.Warning);
            receivedBox.Focus();
            return;
        }

        if (received < total)
        {
            MessageBox.Show($"Alınan tutar sepet toplamından düşük. Eksik: {(total - received):N2} TL", "Yetersiz ödeme", MessageBoxButton.OK, MessageBoxImage.Warning);
            receivedBox.Focus();
            receivedBox.SelectAll();
            return;
        }

        AmountReceived = received;
        DialogResult = true;
    }

    private static bool TryParseAmount(string text, out decimal amount)
    {
        text = text.Trim();
        return decimal.TryParse(text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands, CultureInfo.GetCultureInfo("tr-TR"), out amount)
            || decimal.TryParse(text.Replace('.', ','), NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands, CultureInfo.GetCultureInfo("tr-TR"), out amount)
            || decimal.TryParse(text.Replace(',', '.'), NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out amount);
    }
}

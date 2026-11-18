using System.Windows;
using System.Windows.Controls;

namespace SatisTakip;

public sealed class MixedPaymentDialog : Window
{
    private readonly TextBox cashBox = new();
    private readonly TextBox cardBox = new();
    private readonly decimal total;
    private bool updatingRemaining;
    public decimal CashAmount { get; private set; }
    public decimal CardAmount { get; private set; }

    public MixedPaymentDialog(decimal total)
    {
        this.total = total;
        Title = "Karma ödeme";
        Width = 430;
        Height = 390;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        var panel = new StackPanel { Margin = new Thickness(28) };
        panel.Children.Add(new TextBlock { Text = "Karma ödeme dağılımı", FontSize = 23, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 8) });
        panel.Children.Add(new TextBlock { Text = $"Ödenecek toplam: {total:N2} TL", FontSize = 15, Foreground = System.Windows.Media.Brushes.Gray, Margin = new Thickness(0, 0, 0, 22) });
        AddField(panel, "Nakit tutarı (TL)", cashBox);
        AddField(panel, "Kart tutarı (TL)", cardBox);
        cashBox.TextChanged += CashBox_TextChanged;
        cardBox.TextChanged += CardBox_TextChanged;
        var hint = new TextBlock { Text = "Nakit + kart toplamı satış toplamına eşit olmalıdır.", Foreground = System.Windows.Media.Brushes.Gray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 18) };
        panel.Children.Add(hint);
        var save = new Button { Content = "Ödemeyi onayla", Height = 46, Background = System.Windows.Media.Brushes.SteelBlue, Foreground = System.Windows.Media.Brushes.White, BorderThickness = new Thickness(0), FontSize = 15 };
        save.Click += Save_Click;
        panel.Children.Add(save);
        Content = panel;
    }

    private static void AddField(StackPanel panel, string label, TextBox box)
    {
        panel.Children.Add(new TextBlock { Text = label, Foreground = System.Windows.Media.Brushes.Gray, Margin = new Thickness(0, 0, 0, 5) });
        box.Text = "0,00";
        box.Padding = new Thickness(10);
        box.Margin = new Thickness(0, 0, 0, 14);
        box.GotFocus += (_, _) =>
        {
            if (box.Text is "0,00" or "0.00")
            {
                box.Clear();
            }
            box.SelectAll();
        };
        panel.Children.Add(box);
    }

    private void CashBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (updatingRemaining || !cashBox.IsKeyboardFocusWithin) return;
        if (!decimal.TryParse(cashBox.Text.Trim().Replace('.', ','), out var cash)) return;
        var remaining = Math.Max(0, total - cash);
        updatingRemaining = true;
        cardBox.Text = remaining.ToString("N2");
        updatingRemaining = false;
    }

    private void CardBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (updatingRemaining || !cardBox.IsKeyboardFocusWithin) return;
        if (!decimal.TryParse(cardBox.Text.Trim().Replace('.', ','), out var card)) return;
        var remaining = Math.Max(0, total - card);
        updatingRemaining = true;
        cashBox.Text = remaining.ToString("N2");
        updatingRemaining = false;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(cashBox.Text.Trim().Replace('.', ','), out var cash) || !decimal.TryParse(cardBox.Text.Trim().Replace('.', ','), out var card) || cash < 0 || card < 0 || Math.Abs(cash + card - total) > 0.01m)
        {
            MessageBox.Show($"Nakit ve kart toplamı {total:N2} TL olmalıdır.", "Geçersiz ödeme dağılımı", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        CashAmount = cash;
        CardAmount = card;
        DialogResult = true;
    }
}

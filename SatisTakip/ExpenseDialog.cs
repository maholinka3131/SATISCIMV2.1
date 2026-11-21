using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SatisTakip;

public sealed class ExpenseDialog : Window
{
    private readonly TextBox descriptionBox = new();
    private readonly TextBox amountBox = new();
    public Expense? Expense { get; private set; }

    public ExpenseDialog()
    {
        Title = "Gider ekle";
        Width = 380;
        Height = 400;
        MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = Brushes.White;

        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = "Gider ekle", FontSize = 22, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 18) });
        AddField(panel, "Gider açıklaması", descriptionBox, "Örn. elektrik, personel, nakliye");
        AddField(panel, "Tutar (TL)", amountBox, "0,00");

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        var cancel = new Button { Content = "Vazgeç", Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(0, 0, 8, 0) };
        cancel.Click += (_, _) => DialogResult = false;
        var save = new Button { Content = "Gideri kaydet", Padding = new Thickness(14, 8, 14, 8), Background = new SolidColorBrush(Color.FromRgb(23, 107, 135)), Foreground = Brushes.White };
        save.Click += Save_Click;
        buttons.Children.Add(cancel);
        buttons.Children.Add(save);
        panel.Children.Add(buttons);
        Content = panel;
    }

    private static void AddField(StackPanel panel, string label, TextBox input, string placeholder)
    {
        panel.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = Brushes.Gray, Margin = new Thickness(0, 0, 0, 4) });
        input.Text = placeholder;
        input.Foreground = Brushes.Gray;
        input.Margin = new Thickness(0, 0, 0, 10);
        input.Padding = new Thickness(8, 6, 8, 6);
        input.GotFocus += (_, _) =>
        {
            if (input.Text == placeholder)
            {
                input.Clear();
                input.Foreground = Brushes.Black;
            }
        };
        panel.Children.Add(input);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(descriptionBox.Text) ||
            descriptionBox.Text == "Örn. elektrik, personel, nakliye" ||
            !decimal.TryParse(amountBox.Text.Replace('.', ','), out var amount) || amount <= 0)
        {
            MessageBox.Show("Gider açıklaması ve geçerli bir tutar girin.", "Eksik bilgi", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Expense = new Expense { Description = descriptionBox.Text.Trim(), Amount = amount };
        DialogResult = true;
    }
}

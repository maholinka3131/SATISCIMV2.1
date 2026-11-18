using System.Windows;
using System.Windows.Controls;

namespace SatisTakip;

public sealed class CustomerEntryDialog : Window
{
    private readonly TextBox nameBox = new();
    private readonly TextBox phoneBox = new();
    public Customer? Customer { get; private set; }

    public CustomerEntryDialog()
    {
        Title = "Yeni müşteri";
        Width = 390;
        Height = 330;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        var panel = new StackPanel { Margin = new Thickness(26) };
        panel.Children.Add(new TextBlock { Text = "Yeni müşteri kaydı", FontSize = 22, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 20) });
        AddField(panel, "Ad soyad", nameBox);
        AddField(panel, "Telefon", phoneBox);
        var save = new Button { Content = "Müşteriyi kaydet", Height = 44, Margin = new Thickness(0, 14, 0, 0), Background = System.Windows.Media.Brushes.SteelBlue, Foreground = System.Windows.Media.Brushes.White, BorderThickness = new Thickness(0) };
        save.Click += Save_Click;
        panel.Children.Add(save);
        Content = panel;
    }

    private static void AddField(StackPanel panel, string label, TextBox box)
    {
        panel.Children.Add(new TextBlock { Text = label, Foreground = System.Windows.Media.Brushes.Gray, Margin = new Thickness(0, 0, 0, 5) });
        box.Padding = new Thickness(9);
        box.Margin = new Thickness(0, 0, 0, 14);
        panel.Children.Add(box);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(nameBox.Text))
        {
            MessageBox.Show("Ad soyad alanını doldurun.", "Eksik bilgi", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Customer = new Customer { Name = nameBox.Text.Trim(), Phone = phoneBox.Text.Trim() };
        DialogResult = true;
    }
}

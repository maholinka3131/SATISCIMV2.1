using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace SatisTakip;

public sealed class LoginWindow : Window
{
    private readonly TextBox usernameBox = new() { Text = "admin" };
    private readonly PasswordBox passwordBox = new();
    private readonly DatabaseService database = new();
    private readonly TextBlock errorText = new();
    public AppUser? User { get; private set; }

    public LoginWindow()
    {
        Title = "SATIŞÇIM | Giriş";
        Width = 940;
        Height = 600;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(244, 246, 248));
        FontFamily = new FontFamily("Segoe UI");
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;

        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.92, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.08, GridUnitType.Star) });
        root.Children.Add(CreateBrandPanel());

        var signInArea = new Grid { Background = new SolidColorBrush(Color.FromRgb(246, 248, 250)) };
        var card = CreateSignInCard();
        card.HorizontalAlignment = HorizontalAlignment.Center;
        card.VerticalAlignment = VerticalAlignment.Center;
        signInArea.Children.Add(card);
        Grid.SetColumn(signInArea, 1);
        root.Children.Add(signInArea);
        Content = root;

        usernameBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                passwordBox.Focus();
                e.Handled = true;
            }
        };
        passwordBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Login_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
        };
        Loaded += (_, _) => usernameBox.Focus();
    }

    private static Border CreateBrandPanel()
    {
        var background = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
            GradientStops =
            {
                new GradientStop(Color.FromRgb(13, 43, 55), 0),
                new GradientStop(Color.FromRgb(18, 60, 74), 0.55),
                new GradientStop(Color.FromRgb(23, 107, 135), 1)
            }
        };
        var panel = new Grid { Margin = new Thickness(54, 68, 42, 58) };
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        brand.Children.Add(new Border
        {
            Width = 54,
            Height = 54,
            CornerRadius = new CornerRadius(16),
            Background = new SolidColorBrush(Color.FromRgb(244, 184, 96)),
            Child = new TextBlock
            {
                Text = "S",
                FontSize = 34,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(18, 60, 74)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        });
        var brandText = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
        brandText.Children.Add(new TextBlock { Text = "SATIŞÇIM", FontSize = 26, FontWeight = FontWeights.Bold, Foreground = Brushes.White });
        brandText.Children.Add(new TextBlock { Text = "SATIŞ  •  STOK  •  KASA", FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(174, 211, 219)), Margin = new Thickness(1, 3, 0, 0) });
        brand.Children.Add(brandText);
        panel.Children.Add(brand);

        var message = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
        message.Children.Add(new Border { Width = 48, Height = 3, Background = new SolidColorBrush(Color.FromRgb(244, 184, 96)), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 22) });
        message.Children.Add(new TextBlock { Text = "İşletmenizin\nkontrolü sizde.", FontSize = 34, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, LineHeight = 43 });
        message.Children.Add(new TextBlock
        {
            Text = "Satışlarınızı takip edin, stoklarınızı yönetin ve gününüzü güvenle tamamlayın.",
            FontSize = 15,
            Foreground = new SolidColorBrush(Color.FromRgb(195, 220, 225)),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 24,
            Margin = new Thickness(0, 16, 0, 0)
        });
        Grid.SetRow(message, 1);
        panel.Children.Add(message);

        var footer = new TextBlock
        {
            Text = "Günlük işlerinizi tek ekrandan yönetin.",
            Foreground = new SolidColorBrush(Color.FromRgb(174, 211, 219)),
            FontSize = 12
        };
        Grid.SetRow(footer, 2);
        panel.Children.Add(footer);
        return new Border { Background = background, Child = panel };
    }

    private Border CreateSignInCard()
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = "Hoş geldiniz",
            FontSize = 29,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(29, 41, 57))
        });
        panel.Children.Add(new TextBlock
        {
            Text = "Hesabınıza giriş yaparak devam edin.",
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.FromRgb(102, 112, 133)),
            Margin = new Thickness(0, 7, 0, 30)
        });

        panel.Children.Add(CreateFieldLabel("Kullanıcı adı"));
        AddInputContainer(panel, usernameBox, "♙");
        panel.Children.Add(CreateFieldLabel("Parola", new Thickness(0, 18, 0, 7)));
        AddInputContainer(panel, passwordBox, "●");

        errorText.Foreground = new SolidColorBrush(Color.FromRgb(180, 35, 24));
        errorText.FontSize = 12;
        errorText.TextWrapping = TextWrapping.Wrap;
        errorText.MinHeight = 20;
        errorText.Margin = new Thickness(0, 8, 0, 8);
        panel.Children.Add(errorText);

        var login = new Button
        {
            Content = "Giriş yap",
            Height = 48,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Background = new SolidColorBrush(Color.FromRgb(23, 107, 135)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        login.Template = CreateButtonTemplate();
        login.Click += Login_Click;
        panel.Children.Add(login);

        panel.Children.Add(new TextBlock
        {
            Text = "Güvenli oturum açma",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(152, 162, 179)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 22, 0, 0)
        });

        return new Border
        {
            Width = 400,
            Padding = new Thickness(34),
            Background = Brushes.White,
            CornerRadius = new CornerRadius(18),
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(16, 24, 40),
                Opacity = 0.1,
                BlurRadius = 28,
                ShadowDepth = 5
            },
            Child = panel
        };
    }

    private static TextBlock CreateFieldLabel(string text, Thickness? margin = null)
        => new()
        {
            Text = text,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(52, 64, 84)),
            Margin = margin ?? new Thickness(0, 0, 0, 7)
        };

    private static void AddInputContainer(Panel panel, Control input, string icon)
    {
        input.Margin = new Thickness(0);
        if (input is TextBox textBox)
        {
            textBox.Padding = new Thickness(0);
            textBox.BorderThickness = new Thickness(0);
            textBox.FontSize = 14;
            textBox.VerticalContentAlignment = VerticalAlignment.Center;
        }
        else if (input is PasswordBox passwordBox)
        {
            passwordBox.Padding = new Thickness(0);
            passwordBox.BorderThickness = new Thickness(0);
            passwordBox.FontSize = 14;
            passwordBox.VerticalContentAlignment = VerticalAlignment.Center;
        }

        var row = new DockPanel();
        var iconText = new TextBlock
        {
            Text = icon,
            FontSize = 15,
            Foreground = new SolidColorBrush(Color.FromRgb(102, 112, 133)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
            Width = 18,
            TextAlignment = TextAlignment.Center
        };
        DockPanel.SetDock(iconText, Dock.Left);
        row.Children.Add(iconText);
        row.Children.Add(input);
        panel.Children.Add(new Border
        {
            Height = 48,
            Padding = new Thickness(13, 0, 13, 0),
            CornerRadius = new CornerRadius(9),
            BorderBrush = new SolidColorBrush(Color.FromRgb(208, 213, 221)),
            BorderThickness = new Thickness(1),
            Background = Brushes.White,
            Child = row
        });
    }

    private static ControlTemplate CreateButtonTemplate()
    {
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);
        template.VisualTree = border;
        return template;
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

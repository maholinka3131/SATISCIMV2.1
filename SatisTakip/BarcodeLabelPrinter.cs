using System.Globalization;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using ZXing;

namespace SatisTakip;

public static class BarcodeLabelPrinter
{
    private const double DipsPerMillimeter = 96d / 25.4d;

    public static void Print(Product product, Window owner)
    {
        if (string.IsNullOrWhiteSpace(product.Barcode))
        {
            MessageBox.Show("Etiket yazdırmadan önce ürüne barkod girin veya barkod üretin.", "Barkod bulunamadı", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var copies = AskForCopies(owner);
        if (copies is null)
        {
            return;
        }

        var printDialog = new PrintDialog();
        try
        {
            var pageWidth = 50 * DipsPerMillimeter;
            var pageHeight = 30 * DipsPerMillimeter;
            printDialog.PrintTicket.PageMediaSize = new PageMediaSize(PageMediaSizeName.Unknown, pageWidth, pageHeight);

            var document = new FixedDocument();
            document.DocumentPaginator.PageSize = new Size(pageWidth, pageHeight);
            for (var index = 0; index < copies.Value; index++)
            {
                var page = new FixedPage
                {
                    Width = pageWidth,
                    Height = pageHeight,
                    Background = Brushes.White
                };
                page.Children.Add(CreateLabel(product, pageWidth, pageHeight));
                var pageContent = new PageContent();
                ((IAddChild)pageContent).AddChild(page);
                document.Pages.Add(pageContent);
            }

            if (!ShowPreview(copies.Value, product, owner))
            {
                return;
            }

            if (printDialog.ShowDialog() != true)
            {
                return;
            }

            printDialog.PrintDocument(document.DocumentPaginator, $"Barkod etiketi - {product.Name}");
        }
        catch (PrintSystemException exception)
        {
            MessageBox.Show($"Etiket yazdırılamadı. Yazıcıda 50 × 30 mm etiket ölçüsünün tanımlı olduğunu kontrol edin.\n{exception.Message}", "Yazdırma hatası", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (InvalidOperationException exception)
        {
            MessageBox.Show($"Etiket yazdırılamadı.\n{exception.Message}", "Yazdırma hatası", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static bool ShowPreview(int copies, Product product, Window owner)
    {
        var window = new Window
        {
            Title = $"Barkod etiketi ön izleme - {product.Name}",
            Owner = owner,
            Width = 900,
            Height = 720,
            MinWidth = 640,
            MinHeight = 480,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var layout = new DockPanel { Margin = new Thickness(14) };
        var footer = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition());
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var summary = new TextBlock
        {
            Text = $"{product.Name}  •  {copies} etiket  •  50 × 30 mm",
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        footer.Children.Add(summary);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancelButton = new Button { Content = "Vazgeç", Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(0, 0, 8, 0), IsCancel = true };
        cancelButton.Click += (_, _) => window.DialogResult = false;
        var printButton = new Button { Content = "Yazdır", Padding = new Thickness(14, 8, 14, 8), IsDefault = true };
        printButton.Click += (_, _) => window.DialogResult = true;
        buttons.Children.Add(cancelButton);
        buttons.Children.Add(printButton);
        Grid.SetColumn(buttons, 1);
        footer.Children.Add(buttons);

        var previewPage = new Grid
        {
            Width = 320,
            Height = 192,
            Background = Brushes.White,
            ClipToBounds = true
        };
        previewPage.Children.Add(CreateLabel(product, 320, 192));
        var previewBorder = new Border
        {
            Child = previewPage,
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(20),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 12,
                ShadowDepth = 2,
                Opacity = 0.25
            }
        };
        var previewScroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = previewBorder
        };
        DockPanel.SetDock(footer, Dock.Bottom);
        layout.Children.Add(footer);
        layout.Children.Add(previewScroll);
        window.Content = layout;
        return window.ShowDialog() == true;
    }

    private static int? AskForCopies(Window owner)
    {
        var window = new Window
        {
            Title = "Etiket adedi",
            Owner = owner,
            Width = 300,
            Height = 170,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var panel = new StackPanel { Margin = new Thickness(18) };
        panel.Children.Add(new TextBlock { Text = "Kaç adet etiket yazdırılsın? (1–500)" });
        var input = new TextBox { Text = "1", Margin = new Thickness(0, 10, 0, 12), Padding = new Thickness(6) };
        panel.Children.Add(input);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Vazgeç", Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 8, 0) };
        cancel.Click += (_, _) => window.DialogResult = false;
        var print = new Button { Content = "Yazdır", Padding = new Thickness(10, 5, 10, 5) };
        print.Click += (_, _) =>
        {
            if (!int.TryParse(input.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var count) || count < 1 || count > 500)
            {
                MessageBox.Show("1 ile 500 arasında bir etiket adedi girin.", "Geçersiz adet", MessageBoxButton.OK, MessageBoxImage.Warning);
                input.Focus();
                input.SelectAll();
                return;
            }
            window.Tag = count;
            window.DialogResult = true;
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(print);
        panel.Children.Add(buttons);
        window.Content = panel;
        window.Loaded += (_, _) => input.Focus();
        return window.ShowDialog() == true ? (int)window.Tag : null;
    }

    private static UIElement CreateLabel(Product product, double pageWidth, double pageHeight)
    {
        const double margin = 4;
        var contentWidth = pageWidth - margin * 2;
        var stack = new StackPanel
        {
            Width = contentWidth,
            Height = pageHeight - margin * 2,
            Margin = new Thickness(margin),
            ClipToBounds = true
        };
        stack.Children.Add(new TextBlock
        {
            Text = product.Name,
            Height = 19,
            FontSize = 8,
            FontWeight = FontWeights.SemiBold,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        });

        var barcode = new MultiFormatWriter().encode(product.Barcode, BarcodeFormat.CODE_128, 600, 1);
        var barcodeCanvas = new Canvas { Width = contentWidth, Height = 43, ClipToBounds = true };
        var moduleWidth = contentWidth / barcode.Width;
        for (var x = 0; x < barcode.Width;)
        {
            if (!barcode[x, 0])
            {
                x++;
                continue;
            }
            var start = x;
            while (x < barcode.Width && barcode[x, 0])
            {
                x++;
            }
            var bar = new Rectangle
            {
                Width = (x - start) * moduleWidth,
                Height = barcodeCanvas.Height,
                Fill = Brushes.Black
            };
            Canvas.SetLeft(bar, start * moduleWidth);
            barcodeCanvas.Children.Add(bar);
        }
        stack.Children.Add(barcodeCanvas);
        stack.Children.Add(new TextBlock
        {
            Text = product.Barcode,
            Height = 15,
            FontSize = 7,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });
        stack.Children.Add(new TextBlock
        {
            Text = $"{product.Price:N2} TL",
            Height = 18,
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });
        return stack;
    }
}

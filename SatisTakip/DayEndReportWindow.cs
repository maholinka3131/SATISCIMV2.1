using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace SatisTakip;

public sealed class DayEndReportWindow : Window
{
    private readonly FlowDocument reportDocument;

    public DayEndReportWindow(DateTime date, decimal openingCash, IReadOnlyList<SaleRecord> sales, IReadOnlyList<Expense> expenses)
    {
        Title = "Gün Sonu Raporu";
        Width = 760;
        Height = 680;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(244, 246, 248));

        var revenue = sales.Sum(item => item.Revenue);
        var cost = sales.Sum(item => item.Cost);
        var expensesTotal = expenses.Sum(item => item.Amount);
        var grossProfit = revenue - cost;
        var netProfit = grossProfit - expensesTotal;
        var cashSales = sales.Sum(GetCashAmount);
        var cardSales = sales.Sum(GetCardAmount);
        var expectedCash = openingCash + cashSales - expensesTotal;

        var root = new DockPanel { Margin = new Thickness(22) };
        var printButton = new Button { Content = "Yazdır", Padding = new Thickness(18, 10, 18, 10), HorizontalAlignment = HorizontalAlignment.Right, Background = new SolidColorBrush(Color.FromRgb(23, 107, 135)), Foreground = Brushes.White, BorderThickness = new Thickness(0) };
        printButton.Click += PrintButton_Click;
        DockPanel.SetDock(printButton, Dock.Bottom);
        root.Children.Add(printButton);

        reportDocument = new FlowDocument { PagePadding = new Thickness(36), FontFamily = new FontFamily("Segoe UI"), FontSize = 13, Background = Brushes.White };
        reportDocument.Blocks.Add(new Paragraph(new Run("SATIŞÇIM | GÜN SONU RAPORU")) { FontSize = 22, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(18, 60, 74)) });
        reportDocument.Blocks.Add(new Paragraph(new Run($"Tarih: {date:dd.MM.yyyy HH:mm}")) { Foreground = Brushes.Gray });
        reportDocument.Blocks.Add(new Paragraph(new Run("ÖZET")) { FontSize = 15, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 18, 0, 6) });
        reportDocument.Blocks.Add(CreateSummaryTable(openingCash, revenue, cashSales, cardSales, cost, grossProfit, expensesTotal, netProfit, expectedCash));
        reportDocument.Blocks.Add(new Paragraph(new Run("ÖDEME YÖNTEMLERİ")) { FontSize = 15, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 18, 0, 6) });
        reportDocument.Blocks.Add(CreatePaymentTable(sales));
        reportDocument.Blocks.Add(new Paragraph(new Run("Karma satışlarda işlem, kullanılan her ödeme yönteminin altında ayrı sayılır.")) { Foreground = Brushes.Gray, FontSize = 10, Margin = new Thickness(0, 4, 0, 0) });
        reportDocument.Blocks.Add(new Paragraph(new Run("GİDERLER")) { FontSize = 15, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 18, 0, 6) });
        reportDocument.Blocks.Add(CreateExpenseTable(expenses));
        reportDocument.Blocks.Add(new Paragraph(new Run($"Toplam satış işlemi: {sales.Count}")) { Foreground = Brushes.Gray, Margin = new Thickness(0, 18, 0, 0) });

        var viewer = new FlowDocumentScrollViewer { Document = reportDocument, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Brushes.White };
        root.Children.Add(viewer);
        Content = root;
    }

    private static Table CreateSummaryTable(decimal openingCash, decimal revenue, decimal cashSales, decimal cardSales, decimal cost, decimal grossProfit, decimal expenses, decimal netProfit, decimal expectedCash)
    {
        var table = new Table { CellSpacing = 0 };
        foreach (var value in new[] { ("Başlangıç kasası", openingCash), ("Gerçek satış cirosu", revenue), ("Nakit satış", cashSales), ("Kart satış", cardSales), ("Ürün maliyeti", cost), ("Brüt kâr", grossProfit), ("Toplam gider", expenses), ("Net kâr", netProfit), ("Beklenen gün sonu kasası", expectedCash) })
        {
            var row = new TableRow();
            row.Cells.Add(new TableCell(new Paragraph(new Run(value.Item1))) { Padding = new Thickness(8), BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0, 0, 0, 1) });
            row.Cells.Add(new TableCell(new Paragraph(new Run($"{value.Item2:N2} TL"))) { Padding = new Thickness(8), TextAlignment = TextAlignment.Right, BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0, 0, 0, 1) });
            table.RowGroups.Add(new TableRowGroup { Rows = { row } });
        }
        return table;
    }

    private static Table CreateExpenseTable(IReadOnlyList<Expense> expenses)
    {
        var table = new Table { CellSpacing = 0 };
        if (expenses.Count == 0)
        {
            table.RowGroups.Add(new TableRowGroup { Rows = { new TableRow { Cells = { new TableCell(new Paragraph(new Run("Bu gün gider kaydı yok."))) } } } });
            return table;
        }
        foreach (var expense in expenses)
        {
            var row = new TableRow();
            row.Cells.Add(new TableCell(new Paragraph(new Run(expense.Description))) { Padding = new Thickness(8), BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0, 0, 0, 1) });
            row.Cells.Add(new TableCell(new Paragraph(new Run($"{expense.Amount:N2} TL"))) { Padding = new Thickness(8), TextAlignment = TextAlignment.Right, BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0, 0, 0, 1) });
            table.RowGroups.Add(new TableRowGroup { Rows = { row } });
        }
        return table;
    }

    private static Table CreatePaymentTable(IReadOnlyList<SaleRecord> sales)
    {
        var table = new Table { CellSpacing = 0 };
        var paymentRows = new[]
        {
            ("Nakit", sales.Count(item => GetCashAmount(item) > 0), sales.Sum(GetCashAmount)),
            ("Kart", sales.Count(item => GetCardAmount(item) > 0), sales.Sum(GetCardAmount)),
            ("Veresiye", sales.Count(item => item.PaymentMethod == "Veresiye"), sales.Where(item => item.PaymentMethod == "Veresiye").Sum(item => item.Revenue))
        };
        foreach (var (method, count, amount) in paymentRows)
        {
            var row = new TableRow();
            row.Cells.Add(new TableCell(new Paragraph(new Run(method))) { Padding = new Thickness(8), BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0, 0, 0, 1) });
            row.Cells.Add(new TableCell(new Paragraph(new Run($"{count} işlem"))) { Padding = new Thickness(8), BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0, 0, 0, 1) });
            row.Cells.Add(new TableCell(new Paragraph(new Run($"{amount:N2} TL"))) { Padding = new Thickness(8), TextAlignment = TextAlignment.Right, BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0, 0, 0, 1) });
            table.RowGroups.Add(new TableRowGroup { Rows = { row } });
        }
        return table;
    }

    private static decimal GetCashAmount(SaleRecord sale)
    {
        if (sale.CashAmount > 0 || sale.CardAmount > 0) return sale.CashAmount;
        return sale.PaymentMethod == "Nakit" ? sale.Revenue : 0;
    }

    private static decimal GetCardAmount(SaleRecord sale)
    {
        if (sale.CashAmount > 0 || sale.CardAmount > 0) return sale.CardAmount;
        return sale.PaymentMethod == "Kart" ? sale.Revenue : 0;
    }

    private void PrintButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() == true)
        {
            reportDocument.PageWidth = dialog.PrintableAreaWidth;
            reportDocument.PageHeight = dialog.PrintableAreaHeight;
            dialog.PrintDocument(((IDocumentPaginatorSource)reportDocument).DocumentPaginator, "Gün Sonu Raporu");
        }
    }
}

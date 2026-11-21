namespace SatisTakip;

public sealed class SaleRecord
{
    public long Id { get; init; }
    public DateTime Date { get; init; } = DateTime.Now;
    public decimal Revenue { get; init; }
    public decimal Cost { get; init; }
    public string PaymentMethod { get; init; } = "Nakit";
    public decimal CashAmount { get; init; }
    public decimal CardAmount { get; init; }
    public bool IsRefunded { get; init; }
    public List<SaleItemRecord> Items { get; init; } = [];
    public string ProductsDisplay => string.Join(", ", Items.Select(item => $"{item.ProductName} ({item.Quantity:N2})"));
    public decimal Profit => Revenue - Cost;
}

public sealed class SaleItemRecord
{
    public long Id { get; init; }
    public long SaleId { get; init; }
    public DateTime SaleDate { get; init; }
    public string PaymentMethod { get; init; } = "";
    public decimal CashAmount { get; init; }
    public decimal CardAmount { get; init; }
    public string PaymentDisplay
    {
        get
        {
            var payments = new List<string>();
            if (CashAmount > 0) payments.Add($"Nakit {CashAmount:N2} TL");
            if (CardAmount > 0) payments.Add($"Kart {CardAmount:N2} TL");
            return payments.Count > 0
                ? string.Join(" + ", payments)
                : PaymentMethod == "Karma"
                    ? "Dağılım kaydedilmemiş"
                    : PaymentMethod;
        }
    }
    public string ProductName { get; init; } = "";
    public decimal Quantity { get; set; }
    public decimal ReturnedQuantity { get; set; }
    public decimal RemainingQuantity => Math.Max(0, Quantity - ReturnedQuantity);
    public bool IsSelectedForReturn { get; set; }
    public decimal UnitPrice { get; init; }
    public decimal UnitCost { get; init; }
    public decimal Revenue => Quantity * UnitPrice;
    public decimal Cost => Quantity * UnitCost;
    public decimal Profit => Revenue - Cost;
}

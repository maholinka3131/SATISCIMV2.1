namespace SatisTakip;

public sealed class Product
{
    public long Id { get; set; }
    public string Barcode { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "Genel";
    public decimal CostPrice { get; set; }
    public decimal? VatRate { get; set; }
    public decimal Price { get; set; }
    public decimal Stock { get; set; }
    public decimal MinimumStock { get; set; } = 5;
    public bool IsLowStock => Stock <= MinimumStock;
    public string CostPriceText => $"{CostPrice:N2} TL";
    public string PriceText => $"{Price:N2} TL";
    public decimal Profit => Price - CostPrice;
    public decimal ProfitMargin => Price == 0 ? 0 : Profit / Price * 100;
    public string ProfitText => $"{Profit:N2} TL";
    public string ProfitMarginText => $"{ProfitMargin:N2}%";

    public static decimal CalculateCostIncludingVat(decimal costExcludingVat, decimal vatRate) =>
        Math.Round(costExcludingVat * (1 + vatRate / 100), 2, MidpointRounding.AwayFromZero);
}

namespace SatisTakip;

public sealed class AppUser
{
    public long Id { get; init; }
    public string Username { get; init; } = "";
    public string Role { get; init; } = "Cashier";
    public bool IsActive { get; init; } = true;
    public bool IsAdmin => Role == "Admin";
    public string RoleText => IsAdmin ? "Yönetici" : "Kasiyer";
}

public static class UserSession
{
    public static AppUser? CurrentUser { get; set; }
}

public sealed class StockMovement
{
    public string ProductName { get; init; } = "";
    public DateTime Date { get; init; }
    public string Type { get; init; } = "";
    public decimal Quantity { get; init; }
    public decimal PreviousStock { get; init; }
    public decimal NewStock { get; init; }
    public string Note { get; init; } = "";
    public string Username { get; init; } = "";
}

public sealed class BestSellingProduct
{
    public string ProductName { get; init; } = "";
    public decimal Quantity { get; init; }
    public decimal ReturnedQuantity { get; init; }
    public decimal Revenue { get; init; }
    public decimal RefundedRevenue { get; init; }
    public decimal Cost { get; init; }
    public decimal Profit => Revenue - Cost;
}

public sealed class SalesReportData
{
    public decimal Revenue { get; init; }
    public decimal RefundedRevenue { get; init; }
    public decimal Cost { get; init; }
    public decimal Quantity { get; init; }
    public decimal ReturnedQuantity { get; init; }
    public decimal Profit => Revenue - Cost;
    public List<BestSellingProduct> BestSellers { get; } = [];
}

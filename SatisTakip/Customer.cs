namespace SatisTakip;

public sealed class Customer
{
    public long Id { get; init; }
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public decimal Balance { get; set; }
    public string BalanceText => $"{Balance:N2} TL";
}

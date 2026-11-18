namespace SatisTakip;

public sealed class Expense
{
    public long Id { get; init; }
    public DateTime Date { get; init; } = DateTime.Now;
    public string Description { get; init; } = "";
    public decimal Amount { get; init; }
}

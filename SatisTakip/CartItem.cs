using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SatisTakip;

public sealed class CartItem : INotifyPropertyChanged
{
    public Product Product { get; }
    public string Name => Product.Name;
    public decimal UnitPrice => Product.Price;
    public decimal UnitCost => Product.CostPrice;

    private int quantity = 1;
    public int Quantity
    {
        get => quantity;
        set
        {
            if (quantity == value)
            {
                return;
            }

            quantity = Math.Max(1, value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(LineTotal));
        }
    }

    public decimal LineTotal => UnitPrice * Quantity;
    public decimal CostTotal => UnitCost * Quantity;

    public CartItem(Product product)
    {
        Product = product;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

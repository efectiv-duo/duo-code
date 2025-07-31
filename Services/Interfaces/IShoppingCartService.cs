using duo_code.Models;

namespace duo_code.Services.Interfaces
{
    public interface IShoppingCartService
    {
        void AddToCart(int productId);
        void RemoveFromCart(int productId);
        ShoppingCart GetCart();
        decimal GetCartTotal();
    }
}


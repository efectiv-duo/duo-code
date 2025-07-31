using System;
using duo_code.Models;
using duo_code.Services.Interfaces;

namespace duo_code.Services
{
    public class ShoppingCartService : IShoppingCartService
    {
        private ShoppingCart _cart = new ShoppingCart();

        public void AddToCart(int productId)
        {
            // In a real application, we would fetch the product from the database
            // For simplicity, we're using a hardcoded list
            var product = new ProductService().GetProductById(productId);
            if (product != null)
            {
                _cart.AddItem(product);
                Console.WriteLine($"Added {product.Name} to cart.");
            }
            else
            {
                Console.WriteLine("Product not found.");
            }
        }

        public void RemoveFromCart(int productId)
        {
            _cart.RemoveItem(productId);
            Console.WriteLine("Product removed from cart.");
        }

        public ShoppingCart GetCart()
        {
            return _cart;
        }

        public decimal GetCartTotal()
        {
            return _cart.GetTotalPrice();
        }
    }
}


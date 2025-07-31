using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using duo_code.Models;
using duo_code.Services.Interfaces;
using duo_code.Commands.Core;

namespace duo_code.Commands.Actions
{
    public class OnlineStoreCommand : ICommand
    {
        private readonly IProductService _productService;
        private readonly IShoppingCartService _shoppingCartService;

        public OnlineStoreCommand(IProductService productService, IShoppingCartService shoppingCartService)
        {
            _productService = productService;
            _shoppingCartService = shoppingCartService;
        }

        public string Name => "store";
        public string Description => "Manage online store operations";
        public CommandType Type => CommandType.Action; // Fixed: No longer throws NotImplementedException

        public Task<CommandResult> ExecuteAsync(string[] args)
        {
            if (args.Length == 0)
            {
                return Task.FromResult(CommandResult.Ok(GetHelp()));
            }

            try
            {
                var result = args[0].ToLower() switch
                {
                    "list" => ListProducts(),
                    "view" when args.Length > 1 && int.TryParse(args[1], out int productId) => ViewProduct(productId),
                    "view" => "Please specify a product ID.",
                    "add" when args.Length > 1 && int.TryParse(args[1], out int addId) => AddToCart(addId),
                    "add" => "Please specify a product ID to add to cart.",
                    "remove" when args.Length > 1 && int.TryParse(args[1], out int removeId) => RemoveFromCart(removeId),
                    "remove" => "Please specify a product ID to remove from cart.",
                    "cart" => ShowCart(),
                    "help" => GetHelp(),
                    _ => $"Unknown command: {args[0]}\n\n{GetHelp()}"
                };

                return Task.FromResult(CommandResult.Ok(result));
            }
            catch (Exception ex)
            {
                return Task.FromResult(CommandResult.Error($"An error occurred: {ex.Message}"));
            }
        }

        private string ListProducts()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Available Products:");
            sb.AppendLine("------------------");
            
            foreach (var product in _productService.GetAllProducts())
            {
                sb.AppendLine($"{product.Id}. {product.Name} - ${product.Price:F2}");
                sb.AppendLine($"   {product.Description}");
            }

            return sb.ToString();
        }

        private string ViewProduct(int productId)
        {
            var product = _productService.GetProductById(productId);
            if (product != null)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Product Details: {product.Name}");
                sb.AppendLine("-----------------------------");
                sb.AppendLine($"ID: {product.Id}");
                sb.AppendLine($"Price: ${product.Price:F2}");
                sb.AppendLine($"Description: {product.Description}");
                sb.AppendLine($"Image: {product.ImageUrl}");
                return sb.ToString();
            }
            else
            {
                return "Product not found.";
            }
        }

        private string AddToCart(int productId)
        {
            try
            {
                _shoppingCartService.AddToCart(productId);
                return $"Product {productId} added to cart successfully.";
            }
            catch (Exception ex)
            {
                return $"Failed to add product to cart: {ex.Message}";
            }
        }

        private string RemoveFromCart(int productId)
        {
            try
            {
                _shoppingCartService.RemoveFromCart(productId);
                return $"Product {productId} removed from cart successfully.";
            }
            catch (Exception ex)
            {
                return $"Failed to remove product from cart: {ex.Message}";
            }
        }

        private string ShowCart()
        {
            var cart = _shoppingCartService.GetCart();
            if (cart.Items.Count == 0)
            {
                return "Your cart is empty.";
            }

            var sb = new StringBuilder();
            sb.AppendLine("Your Shopping Cart:");
            sb.AppendLine("------------------");
            
            foreach (var item in cart.Items)
            {
                sb.AppendLine($"{item.Id}. {item.Name} - ${item.Price:F2}");
            }
            
            sb.AppendLine($"Total: ${_shoppingCartService.GetCartTotal():F2}");
            return sb.ToString();
        }

        private string GetHelp()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Online Store Commands:");
            sb.AppendLine("  store list - List all available products");
            sb.AppendLine("  store view [id] - View details of a specific product");
            sb.AppendLine("  store add [id] - Add a product to your cart");
            sb.AppendLine("  store remove [id] - Remove a product from your cart");
            sb.AppendLine("  store cart - View your shopping cart");
            sb.AppendLine("  store help - Show this help message");
            return sb.ToString();
        }
    }
}
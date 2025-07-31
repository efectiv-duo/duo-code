using System.Collections.Generic;
using duo_code.Models;
using duo_code.Services.Interfaces;

namespace duo_code.Services
{
    public class ProductService : IProductService
    {
        private List<Product> _products = new List<Product>
        {
            new Product { Id = 1, Name = "Laptop", Price = 999.99m, Description = "High performance laptop", ImageUrl = "https://example.com/laptop.jpg" },
            new Product { Id = 2, Name = "Smartphone", Price = 699.99m, Description = "Latest smartphone model", ImageUrl = "https://example.com/smartphone.jpg" },
            new Product { Id = 3, Name = "Headphones", Price = 199.99m, Description = "Noise-cancelling headphones", ImageUrl = "https://example.com/headphones.jpg" }
        };

        public List<Product> GetAllProducts()
        {
            return _products;
        }

        public Product? GetProductById(int id)
        {
            return _products.FirstOrDefault(p => p.Id == id);
        }

        public void AddProduct(Product product)
        {
            _products.Add(product);
        }

        public void UpdateProduct(Product product)
        {
            var existingProduct = _products.FirstOrDefault(p => p.Id == product.Id);
            if (existingProduct != null)
            {
                existingProduct.Name = product.Name;
                existingProduct.Price = product.Price;
                existingProduct.Description = product.Description;
                existingProduct.ImageUrl = product.ImageUrl;
            }
        }

        public void DeleteProduct(int id)
        {
            var product = _products.FirstOrDefault(p => p.Id == id);
            if (product != null)
            {
                _products.Remove(product);
            }
        }
    }
}

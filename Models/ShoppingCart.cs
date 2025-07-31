using System.Collections.Generic;
using System.Linq;
using System.Linq;

namespace duo_code.Models
{
    public class ShoppingCart
    {
        public List<Product> Items { get; set; } = new List<Product>();
        
        public void AddItem(Product product)
        {
            Items.Add(product);
        }
        
        public void RemoveItem(int productId)
        {
            var item = Items.FirstOrDefault(p => p.Id == productId);
            if (item != null)
            {
                Items.Remove(item);
            }
        }
        
        public decimal GetTotalPrice()
        {
            return Items.Sum(item => item.Price);
        }
    }
}

//Next, let's create the service interfaces:

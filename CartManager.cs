using System.Collections.Generic;
using System.Linq;

namespace ChudoShoes
{
    public class CartItem
    {
        public Product Product { get; set; }

        public int ID_StockItem { get; set; }

        public string SizeValue { get; set; }

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public int MaxAvailable { get; set; }

        public decimal TotalPrice
        {
            get
            {
                return Quantity * UnitPrice;
            }
        }
    }

    public static class CartManager
    {
        public static List<CartItem> Items = new List<CartItem>();

        // Клиент, для которого формируется заказ (если заказ создаёт Менеджер или Администратор)
        public static User SelectedClientForOrder { get; set; }

        // Флаг режима выбора товаров из каталога при добавлении нового заказа Менеджером/Администратором
        public static bool IsCreatingOrderByStaff { get; set; }

        public static void AddProduct(Product product, int stockId, string sizeVal, int qty, decimal price, int maxAvailable)
        {
            var existing = Items.FirstOrDefault(i => i.ID_StockItem == stockId);
            if (existing != null)
            {
                int newQty = existing.Quantity + qty;
                if (newQty > maxAvailable)
                {
                    existing.Quantity = maxAvailable;
                }
                else
                {
                    existing.Quantity = newQty;
                }
            }
            else
            {
                CartItem newItem = new CartItem();
                newItem.Product = product;
                newItem.ID_StockItem = stockId;
                newItem.SizeValue = sizeVal;
                newItem.Quantity = qty;
                newItem.UnitPrice = price;
                newItem.MaxAvailable = maxAvailable;
                Items.Add(newItem);
            }
        }

        public static void Clear()
        {
            Items.Clear();
            SelectedClientForOrder = null;
            IsCreatingOrderByStaff = false;
        }

        public static decimal GetTotalSum()
        {
            return Items.Sum(i => i.TotalPrice);
        }

        public static int GetTotalCount()
        {
            return Items.Sum(i => i.Quantity);
        }
    }
}
using System;
using System.Data.Entity;
using System.Linq;

namespace ChudoShoes
{
    public partial class Product
    {
        public int TotalQuantity
        {
            get
            {
                try
                {
                    var context = ChudoShoesFatkhlislamovDBEntities.GetContext();
                    var list = context.StockItem.Where(s => s.ID_Product == this.ID_Product).ToList();
                    return list.Sum(s => s.QuantityAvailable);
                }
                catch
                {
                    return 0;
                }
            }
        }

        public string QuantityText
        {
            get
            {
                return TotalQuantity > 5 ? "много" : "мало";
            }
        }

        public string ProductColor
        {
            get
            {
                return TotalQuantity <= 3 ? "#FF8080" : "#FFFFFF";
            }
        }

        public decimal DiscountPrice
        {
            get
            {
                try
                {
                    var context = ChudoShoesFatkhlislamovDBEntities.GetContext();

                    // Определяем предыдущий календарный месяц относительно даты расчета (максимальной даты заказа в БД)
                    DateTime baseDate = context.Order.Any() ? context.Order.Max(o => o.OrderDate) : DateTime.Now;
                    DateTime firstDayPrevMonth = new DateTime(baseDate.Year, baseDate.Month, 1).AddMonths(-1);
                    DateTime lastDayPrevMonth = new DateTime(baseDate.Year, baseDate.Month, 1).AddDays(-1);

                    var stockIds = context.StockItem
                        .Where(s => s.ID_Product == this.ID_Product)
                        .Select(s => s.ID_StockItem)
                        .ToList();

                    if (stockIds.Any())
                    {
                        bool hasOrdersInPrevMonth = context.OrderItem.Any(oi =>
                            stockIds.Contains(oi.ID_StockItem) &&
                            oi.Order != null &&
                            oi.Order.OrderDate >= firstDayPrevMonth &&
                            oi.Order.OrderDate <= lastDayPrevMonth);

                        if (!hasOrdersInPrevMonth)
                        {
                            return Math.Round(this.Price * 0.75m, 2);
                        }
                    }
                }
                catch
                {
                }
                return this.Price;
            }
        }

        public string OldPriceVisibility
        {
            get
            {
                return DiscountPrice < Price ? "Visible" : "Collapsed";
            }
        }

        public string ProductPhoto
        {
            get
            {
                if (string.IsNullOrWhiteSpace(this.Image))
                {
                    return "/res/picture.png";
                }

                string cleanImg = this.Image.Trim().Replace('\\', '/');

                if (cleanImg.StartsWith("Images/", StringComparison.OrdinalIgnoreCase))
                {
                    return "/res/" + cleanImg;
                }

                if (cleanImg.StartsWith("/Images/", StringComparison.OrdinalIgnoreCase))
                {
                    return "/res" + cleanImg;
                }

                if (cleanImg.StartsWith("/res/", StringComparison.OrdinalIgnoreCase))
                {
                    return cleanImg;
                }

                return "/res/Images/" + cleanImg;
            }
        }

        // Размерный ряд модели (все размеры модели в базе данных)
        public string AllSizesRangeString
        {
            get
            {
                try
                {
                    var context = ChudoShoesFatkhlislamovDBEntities.GetContext();
                    var allSizes = context.StockItem
                        .Include(s => s.Size)
                        .Where(s => s.ID_Product == this.ID_Product && s.Size != null)
                        .OrderBy(s => s.Size.SizeValue).Select(s => s.Size.SizeValue)
                        .ToList();

                    if (allSizes.Any())
                    {
                        return string.Join(", ", allSizes.Select(v => v.ToString("0.#")));
                    }
                }
                catch
                {
                }
                return "не указан";
            }
        }

        // Доступные для заказа размеры (с остатком больше 0)
        public string SizesString
        {
            get
            {
                try
                {
                    var context = ChudoShoesFatkhlislamovDBEntities.GetContext();
                    var sizes = context.StockItem
                        .Include(s => s.Size)
                        .Where(s => s.ID_Product == this.ID_Product && s.QuantityAvailable > 0 && s.Size != null)
                        .OrderBy(s => s.Size.SizeValue)
                        .Select(s => s.Size.SizeValue)
                        .ToList();

                    if (sizes.Any())
                    {
                        return string.Join(", ", sizes.Select(v => v.ToString("0.#")));
                    }
                }
                catch
                {
                }
                return "нет в наличии";
            }
        }

        public string CleanProductName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(this.ProductName))
                {
                    return "Без названия";
                }

                return this.ProductName
                    .Replace("\r", " ")
                    .Replace("\n", " ")
                    .Trim();
            }
        }

        public string CleanDescription
        {
            get
            {
                if (string.IsNullOrWhiteSpace(this.Description))
                {
                    return "";
                }

                return this.Description
                    .Replace("\r", " ")
                    .Replace("\n", " ")
                    .Trim();
            }
        }

        public string ManufacturerTitle
        {
            get
            {
                if (this.Manufacturer == null || string.IsNullOrWhiteSpace(this.Manufacturer.ManufacturerName))
                {
                    return "Не указан";
                }

                return this.Manufacturer.ManufacturerName
                    .Replace("\r", " ")
                    .Replace("\n", " ")
                    .Trim();
            }
        }

        public string CategoryTitle
        {
            get
            {
                return this.Category != null ? this.Category.CategoryName : "";
            }
        }
    }
}
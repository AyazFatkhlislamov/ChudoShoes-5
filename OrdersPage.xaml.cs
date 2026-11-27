using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ChudoShoes
{
    public partial class OrdersPage : Page
    {
        private User _currentUser;

        public class OrderViewItem
        {
            public int ID_Order { get; set; }
            public int OrderNumber { get; set; }
            public DateTime OrderDate { get; set; }
            public string ClientFullName { get; set; }
            public decimal TotalOrderSum { get; set; }
        }

        public class OrderDetailViewItem
        {
            public int ID_OrderItem { get; set; }
            public string ModelAndManufacturer { get; set; }
            public string SizeValue { get; set; }
            public int Quantity { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal ItemTotal { get; set; }
        }

        public class StockSizeOption
        {
            public int ID_StockItem { get; set; }
            public string DisplaySizeAndStock { get; set; }
            public int AvailableQty { get; set; }
        }

        public OrdersPage(User user)
        {
            InitializeComponent();
            _currentUser = user;

            if (_currentUser != null)
            {
                UserName.Text = (_currentUser.Surname + " " + _currentUser.Name + " " + _currentUser.Patronymic).Trim();
                UserRole.Text = _currentUser.Role != null ? _currentUser.Role.RoleName : "Сотрудник";
            }

            bool isAdmin = _currentUser != null && _currentUser.ID_Role == 1;
            bool isManagerOrAdmin = _currentUser != null && (_currentUser.ID_Role == 1 || _currentUser.ID_Role == 2);

            // Менеджер и Администратор могут добавлять новые заказы и удалять существующие заказы
            BtnDeleteOrder.Visibility = isManagerOrAdmin ? Visibility.Visible : Visibility.Collapsed;
            BtnCreateOrderViaCatalog.Visibility = isManagerOrAdmin ? Visibility.Visible : Visibility.Collapsed;

            // Редактировать существующий заказ (менять дату, добавлять/удалять позиции в заказе) может ТОЛЬКО Администратор
            BorderAdminDateEdit.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
            BorderAddPositionBox.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
            BtnDeleteItem.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;

            LoadCatalogProductsForSearch("");
            LoadOrders(null);
        }

        private StockItem ResolveStockItem(int rawStockId, List<StockItem> allStockItems)
        {
            if (allStockItems == null || !allStockItems.Any())
            {
                return null;
            }
            var direct = allStockItems.FirstOrDefault(s => s.ID_StockItem == rawStockId);
            if (direct != null)
            {
                return direct;
            }
            int mappedIndex = Math.Abs(rawStockId - 1) % allStockItems.Count;
            return allStockItems[mappedIndex];
        }

        private void LoadCatalogProductsForSearch(string searchText)
        {
            try
            {
                var context = ChudoShoesFatkhlislamovDBEntities.GetContext();
                var products = context.Product.Include(p => p.Manufacturer).ToList();
                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    string lower = searchText.Trim().ToLower();
                    products = products.Where(p => p.CleanProductName.ToLower().Contains(lower)).ToList();
                }
                ComboCatalogProducts.ItemsSource = products;
                ComboCatalogProducts.DisplayMemberPath = "CleanProductName";
                if (products.Any())
                {
                    ComboCatalogProducts.SelectedIndex = 0;
                }
                else
                {
                    ComboCatalogSizes.ItemsSource = null;
                }
            }
            catch
            {
            }
        }

        private void TBoxCatalogSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            LoadCatalogProductsForSearch(TBoxCatalogSearch.Text);
        }

        private void ComboCatalogProducts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedProduct = ComboCatalogProducts.SelectedItem as Product;
            if (selectedProduct == null)
            {
                ComboCatalogSizes.ItemsSource = null;
                return;
            }
            try
            {
                var context = ChudoShoesFatkhlislamovDBEntities.GetContext();
                var stocks = context.StockItem
                    .Include(s => s.Size)
                    .Where(s => s.ID_Product == selectedProduct.ID_Product && s.QuantityAvailable > 0 && s.Size != null)
                    .OrderBy(s => s.Size.SizeValue)
                    .ToList();

                var options = stocks.Select(s => new StockSizeOption
                {
                    ID_StockItem = s.ID_StockItem,
                    DisplaySizeAndStock = s.Size.SizeValue.ToString("0.#") + " (" + s.QuantityAvailable + " шт.)",
                    AvailableQty = s.QuantityAvailable
                }).ToList();

                ComboCatalogSizes.ItemsSource = options;
                ComboCatalogSizes.DisplayMemberPath = "DisplaySizeAndStock";
                if (options.Any())
                {
                    ComboCatalogSizes.SelectedIndex = 0;
                }
            }
            catch
            {
            }
        }

        private void TBoxAddQty_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!e.Text.All(char.IsDigit))
            {
                e.Handled = true;
            }
        }

        private void LoadOrders(int? selectOrderId)
        {
            try
            {
                var context = ChudoShoesFatkhlislamovDBEntities.GetContext();
                var allOrders = context.Order.ToList();
                var allUsers = context.User.ToList();
                var allOrderItems = context.OrderItem.ToList();
                var allStockItems = context.StockItem.OrderBy(s => s.ID_StockItem).ToList();
                var allProducts = context.Product.Include(p => p.Manufacturer).ToList();
                var list = new List<OrderViewItem>();

                foreach (var ord in allOrders)
                {
                    var user = allUsers.FirstOrDefault(u => u.ID_User == ord.ID_User);
                    string fio = user != null ? (user.Surname + " " + user.Name + " " + user.Patronymic).Trim() : "Не указан";
                    var currentItems = allOrderItems.Where(oi => oi.ID_Order == ord.ID_Order).ToList();
                    decimal totalSum = 0m;

                    foreach (var item in currentItems)
                    {
                        var stock = ResolveStockItem(item.ID_StockItem, allStockItems);
                        if (stock != null)
                        {
                            var prod = allProducts.FirstOrDefault(p => p.ID_Product == stock.ID_Product);
                            if (prod != null)
                            {
                                totalSum += item.Quantity * prod.DiscountPrice;
                            }
                        }
                    }

                    list.Add(new OrderViewItem
                    {
                        ID_Order = ord.ID_Order,
                        OrderNumber = ord.OrderNumber,
                        OrderDate = ord.OrderDate,
                        ClientFullName = fio,
                        TotalOrderSum = totalSum
                    });
                }

                var orderedList = list.OrderByDescending(o => o.OrderDate).ThenByDescending(o => o.OrderNumber).ToList();
                DataGridOrders.ItemsSource = orderedList;
                TextBlockRecordsCount.Text = "Всего заказов в системе: " + orderedList.Count;

                if (selectOrderId.HasValue)
                {
                    var target = orderedList.FirstOrDefault(o => o.ID_Order == selectOrderId.Value);
                    if (target != null)
                    {
                        DataGridOrders.SelectedItem = target;
                        return;
                    }
                }
                else if (orderedList.Any())
                {
                    DataGridOrders.SelectedIndex = 0;
                    return;
                }

                DataGridOrderItems.ItemsSource = null;
                TextBlockOrderTitle.Text = "Выберите заказ слева для просмотра его состава";
                TextBlockTotalSum.Text = "0.00 ₽";
                DatePickerOrder.SelectedDate = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки заказов: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DataGridOrders_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedOrder = DataGridOrders.SelectedItem as OrderViewItem;
            if (selectedOrder == null)
            {
                return;
            }
            DatePickerOrder.SelectedDate = selectedOrder.OrderDate;
            try
            {
                var context = ChudoShoesFatkhlislamovDBEntities.GetContext();
                var orderItems = context.OrderItem.Where(oi => oi.ID_Order == selectedOrder.ID_Order).ToList();
                var stockItems = context.StockItem.OrderBy(s => s.ID_StockItem).ToList();
                var products = context.Product.Include(p => p.Manufacturer).ToList();
                var sizes = context.Size.ToList();
                var details = new List<OrderDetailViewItem>();
                decimal calcTotal = 0m;

                foreach (var oi in orderItems)
                {
                    var stock = ResolveStockItem(oi.ID_StockItem, stockItems);
                    string modelAndManuf = "Товарная позиция (ID: " + oi.ID_StockItem + ")";
                    string sVal = "-";
                    decimal price = 0m;

                    if (stock != null)
                    {
                        var prod = products.FirstOrDefault(p => p.ID_Product == stock.ID_Product);
                        if (prod != null)
                        {
                            modelAndManuf = prod.CleanProductName + " (Производство: " + prod.ManufacturerTitle + ")";
                            price = prod.DiscountPrice;
                        }
                        var sz = sizes.FirstOrDefault(s => s.ID_Size == stock.ID_Size);
                        if (sz != null)
                        {
                            sVal = sz.SizeValue.ToString("0.#");
                        }
                    }

                    decimal lineTotal = oi.Quantity * price;
                    calcTotal += lineTotal;
                    details.Add(new OrderDetailViewItem
                    {
                        ID_OrderItem = oi.ID_OrderItem,
                        ModelAndManufacturer = modelAndManuf,
                        SizeValue = sVal,
                        Quantity = oi.Quantity,
                        UnitPrice = price,
                        ItemTotal = lineTotal
                    });
                }

                DataGridOrderItems.ItemsSource = details;
                TextBlockOrderTitle.Text = "Состав заказа № " + selectedOrder.OrderNumber + " от " + selectedOrder.OrderDate.ToString("dd.MM.yyyy") + " (" + selectedOrder.ClientFullName + ")";
                TextBlockTotalSum.Text = calcTotal.ToString("N2") + " ₽";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки состава заказа: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCreateOrderViaCatalog_Click(object sender, RoutedEventArgs e)
        {
            CartManager.Clear();
            CartManager.IsCreatingOrderByStaff = true;
            try
            {
                var context = ChudoShoesFatkhlislamovDBEntities.GetContext();
                var firstClient = context.User.Where(u => u.ID_Role == 3).OrderBy(u => u.Surname).FirstOrDefault();
                CartManager.SelectedClientForOrder = firstClient ?? _currentUser;
            }
            catch
            {
                CartManager.SelectedClientForOrder = _currentUser;
            }
            Manager.MainFrame.Navigate(new ProductPage(_currentUser));
        }

        private void BtnOpenProductCard_Click(object sender, RoutedEventArgs e)
        {
            var selectedProduct = ComboCatalogProducts.SelectedItem as Product;
            if (selectedProduct == null)
            {
                MessageBox.Show("Выберите модель обуви из списка!", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            CartManager.IsCreatingOrderByStaff = true;
            Manager.MainFrame.Navigate(new ProductDetailPage(selectedProduct, _currentUser));
        }

        private void BtnAddPositionToOrder_Click(object sender, RoutedEventArgs e)
        {
            var selectedOrder = DataGridOrders.SelectedItem as OrderViewItem;
            var selectedProduct = ComboCatalogProducts.SelectedItem as Product;
            var selectedSizeOpt = ComboCatalogSizes.SelectedItem as StockSizeOption;

            if (selectedOrder == null || selectedProduct == null || selectedSizeOpt == null)
            {
                MessageBox.Show("Выберите заказ слева, а также модель и доступный размер обуви!", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int qty;
            if (!int.TryParse(TBoxAddQty.Text.Trim(), out qty) || qty <= 0 || qty > selectedSizeOpt.AvailableQty)
            {
                MessageBox.Show("Укажите корректное количество в пределах доступного остатка (" + selectedSizeOpt.AvailableQty + " шт.)!", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var context = ChudoShoesFatkhlislamovDBEntities.GetContext();
                var stock = context.StockItem.FirstOrDefault(s => s.ID_StockItem == selectedSizeOpt.ID_StockItem);
                if (stock == null || stock.QuantityAvailable < qty)
                {
                    return;
                }

                var existingItem = context.OrderItem.FirstOrDefault(oi => oi.ID_Order == selectedOrder.ID_Order && oi.ID_StockItem == stock.ID_StockItem);
                if (existingItem != null)
                {
                    existingItem.Quantity += qty;
                }
                else
                {
                    context.OrderItem.Add(new OrderItem { ID_Order = selectedOrder.ID_Order, ID_StockItem = stock.ID_StockItem, Quantity = qty });
                }

                stock.QuantityAvailable -= qty;
                context.SaveChanges();
                LoadCatalogProductsForSearch(TBoxCatalogSearch.Text);
                LoadOrders(selectedOrder.ID_Order);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка добавления позиции: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSaveDate_Click(object sender, RoutedEventArgs e)
        {
            var selectedOrder = DataGridOrders.SelectedItem as OrderViewItem;
            if (selectedOrder == null || !DatePickerOrder.SelectedDate.HasValue)
            {
                MessageBox.Show("Выберите заказ и укажите дату!", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                var context = ChudoShoesFatkhlislamovDBEntities.GetContext();
                var ord = context.Order.FirstOrDefault(o => o.ID_Order == selectedOrder.ID_Order);
                if (ord != null)
                {
                    ord.OrderDate = DatePickerOrder.SelectedDate.Value.Date;
                    context.SaveChanges();
                    MessageBox.Show("Дата заказа успешно обновлена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadOrders(selectedOrder.ID_Order);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка обновления даты: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnDeleteItem_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = DataGridOrderItems.SelectedItem as OrderDetailViewItem;
            var selectedOrder = DataGridOrders.SelectedItem as OrderViewItem;
            if (selectedItem == null || selectedOrder == null)
            {
                MessageBox.Show("Выберите товарную позицию в составе заказа для удаления!", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show("Удалить выбранную позицию из заказа?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    var context = ChudoShoesFatkhlislamovDBEntities.GetContext();
                    var itemToDelete = context.OrderItem.FirstOrDefault(oi => oi.ID_OrderItem == selectedItem.ID_OrderItem);
                    if (itemToDelete != null)
                    {
                        var allStocks = context.StockItem.OrderBy(s => s.ID_StockItem).ToList();
                        var stock = ResolveStockItem(itemToDelete.ID_StockItem, allStocks);
                        if (stock != null)
                        {
                            stock.QuantityAvailable += itemToDelete.Quantity;
                        }
                        context.OrderItem.Remove(itemToDelete);
                        context.SaveChanges();

                        if (!context.OrderItem.Any(oi => oi.ID_Order == selectedOrder.ID_Order))
                        {
                            var emptyOrder = context.Order.FirstOrDefault(o => o.ID_Order == selectedOrder.ID_Order);
                            if (emptyOrder != null)
                            {
                                context.Order.Remove(emptyOrder);
                                context.SaveChanges();
                            }
                            LoadOrders(null);
                        }
                        else
                        {
                            LoadOrders(selectedOrder.ID_Order);
                        }
                        LoadCatalogProductsForSearch(TBoxCatalogSearch.Text);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка удаления позиции: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnDeleteOrder_Click(object sender, RoutedEventArgs e)
        {
            var selectedOrder = DataGridOrders.SelectedItem as OrderViewItem;
            if (selectedOrder == null)
            {
                MessageBox.Show("Выберите заказ для удаления!", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show("Вы действительно хотите удалить заказ № " + selectedOrder.OrderNumber + "?", "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    var context = ChudoShoesFatkhlislamovDBEntities.GetContext();
                    var itemsToDelete = context.OrderItem.Where(oi => oi.ID_Order == selectedOrder.ID_Order).ToList();
                    var allStocks = context.StockItem.OrderBy(s => s.ID_StockItem).ToList();
                    foreach (var itm in itemsToDelete)
                    {
                        var stock = ResolveStockItem(itm.ID_StockItem, allStocks);
                        if (stock != null)
                        {
                            stock.QuantityAvailable += itm.Quantity;
                        }
                    }
                    context.OrderItem.RemoveRange(itemsToDelete);
                    var orderToDelete = context.Order.FirstOrDefault(o => o.ID_Order == selectedOrder.ID_Order);
                    if (orderToDelete != null)
                    {
                        context.Order.Remove(orderToDelete);
                    }
                    context.SaveChanges();
                    LoadCatalogProductsForSearch(TBoxCatalogSearch.Text);
                    LoadOrders(null);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка удаления заказа: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnBackToCatalog_Click(object sender, RoutedEventArgs e)
        {
            Manager.MainFrame.Navigate(new ProductPage(_currentUser));
        }
    }
}

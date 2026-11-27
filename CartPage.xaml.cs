using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ChudoShoes
{
    public partial class CartPage : Page
    {
        private User _currentUser;

        public class ClientComboItem
        {
            public User UserEntity { get; set; }
            public string DisplayFio { get; set; }
        }

        public CartPage(User user)
        {
            InitializeComponent();
            _currentUser = user;
            TextBlockOrderDateInfo.Text = "Дата фиксации заказа: " + DateTime.Now.ToString("dd.MM.yyyy");

            if (_currentUser != null)
            {
                UserName.Text = (_currentUser.Surname + " " + _currentUser.Name + " " + _currentUser.Patronymic).Trim();
                UserRole.Text = _currentUser.Role != null ? _currentUser.Role.RoleName : "Авторизованный пользователь";
            }

            ConfigureClientSelector();
            RefreshCart();
        }

        private void ConfigureClientSelector()
        {
            if (_currentUser != null && (_currentUser.ID_Role == 1 || _currentUser.ID_Role == 2))
            {
                BorderClientSelector.Visibility = Visibility.Visible;
                try
                {
                    var context = ChudoShoesFatkhlislamovDBEntities.GetContext();
                    var clients = context.User
                        .Where(u => u.ID_Role == 3)
                        .OrderBy(u => u.Surname)
                        .ThenBy(u => u.Name)
                        .ToList();

                    var comboItems = clients.Select(u => new ClientComboItem
                    {
                        UserEntity = u,
                        DisplayFio = (u.Surname + " " + u.Name + " " + u.Patronymic).Trim()
                    }).ToList();

                    ComboOrderClient.ItemsSource = comboItems;
                    ComboOrderClient.DisplayMemberPath = "DisplayFio";

                    User targetUser = CartManager.SelectedClientForOrder;
                    var match = targetUser != null ? comboItems.FirstOrDefault(c => c.UserEntity.ID_User == targetUser.ID_User) : null;
                    if (match != null)
                    {
                        ComboOrderClient.SelectedItem = match;
                    }
                    else if (comboItems.Any())
                    {
                        ComboOrderClient.SelectedIndex = 0;
                    }
                }
                catch
                {
                }
            }
            else
            {
                BorderClientSelector.Visibility = Visibility.Collapsed;
            }
        }

        private void ComboOrderClient_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = ComboOrderClient.SelectedItem as ClientComboItem;
            if (selected != null)
            {
                CartManager.SelectedClientForOrder = selected.UserEntity;
            }
        }

        private void RefreshCart()
        {
            DataGridCart.ItemsSource = null;
            DataGridCart.ItemsSource = CartManager.Items.ToList();
            TextBlockCartSum.Text = "Итоговая сумма по заказу: " + CartManager.GetTotalSum().ToString("N2") + " ₽";
            BtnSubmitOrder.IsEnabled = CartManager.Items.Any();
        }

        private void BtnDecreaseQty_Click(object sender, RoutedEventArgs e)
        {
            var selected = DataGridCart.SelectedItem as CartItem;
            if (selected == null)
            {
                MessageBox.Show("Выделите строку с товаром, чтобы уменьшить количество пар.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (selected.Quantity > 1)
            {
                selected.Quantity -= 1;
                RefreshCart();
            }
            else
            {
                MessageBox.Show("Количество пар не может быть меньше 1. Для удаления нажмите «Удалить позицию».", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnIncreaseQty_Click(object sender, RoutedEventArgs e)
        {
            var selected = DataGridCart.SelectedItem as CartItem;
            if (selected == null)
            {
                MessageBox.Show("Выделите строку с товаром, чтобы увеличить количество пар.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (selected.Quantity < selected.MaxAvailable)
            {
                selected.Quantity += 1;
                RefreshCart();
            }
            else
            {
                MessageBox.Show("На складе доступно максимум " + selected.MaxAvailable + " шт. данного размера.", "Ограничение остатка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnRemoveItem_Click(object sender, RoutedEventArgs e)
        {
            var selected = DataGridCart.SelectedItem as CartItem;
            if (selected == null)
            {
                MessageBox.Show("Выберите товарную позицию для удаления из заказа.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (MessageBox.Show("Удалить выбранную позицию из заказа?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                CartManager.Items.Remove(selected);
                RefreshCart();
            }
        }

        private void BtnCancelOrder_Click(object sender, RoutedEventArgs e)
        {
            if (CartManager.Items.Any())
            {
                if (MessageBox.Show("Отказаться от оформления заказа и очистить выбранные позиции?", "Отказ от заказа", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                {
                    return;
                }
            }
            CartManager.Clear();
            RefreshCart();
            Manager.MainFrame.GoBack();
        }

        private void BtnSubmitOrder_Click(object sender, RoutedEventArgs e)
        {
            if (!CartManager.Items.Any() || _currentUser == null)
            {
                return;
            }

            User orderOwner = _currentUser;
            if ((_currentUser.ID_Role == 1 || _currentUser.ID_Role == 2) && ComboOrderClient.SelectedItem is ClientComboItem selectedClient)
            {
                orderOwner = selectedClient.UserEntity;
            }

            string clientFio = (orderOwner.Surname + " " + orderOwner.Name + " " + orderOwner.Patronymic).Trim();
            string confirmText = "Подтвердить оформление заказа?\nКлиент: " + clientFio + "\nКоличество пар: " + CartManager.GetTotalCount() + " шт.\nИтоговая сумма: " + CartManager.GetTotalSum().ToString("N2") + " ₽";

            if (MessageBox.Show(confirmText, "Подтверждение заказа", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                var context = ChudoShoesFatkhlislamovDBEntities.GetContext();
                int nextOrderNumber = context.Order.Any() ? context.Order.Max(o => o.OrderNumber) + 1 : 1;
                var newOrder = new Order
                {
                    OrderNumber = nextOrderNumber,
                    OrderDate = DateTime.Now.Date,
                    ID_User = orderOwner.ID_User
                };

                context.Order.Add(newOrder);
                context.SaveChanges();

                foreach (var item in CartManager.Items)
                {
                    context.OrderItem.Add(new OrderItem
                    {
                        ID_Order = newOrder.ID_Order,
                        ID_StockItem = item.ID_StockItem,
                        Quantity = item.Quantity
                    });

                    var stock = context.StockItem.FirstOrDefault(s => s.ID_StockItem == item.ID_StockItem);
                    if (stock != null)
                    {
                        stock.QuantityAvailable -= item.Quantity;
                    }
                }

                context.SaveChanges();
                bool returnToOrders = (_currentUser.ID_Role == 1 || _currentUser.ID_Role == 2);
                CartManager.Clear();

                MessageBox.Show("Заказ № " + nextOrderNumber + " успешно оформлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                if (returnToOrders)
                {
                    Manager.MainFrame.Navigate(new OrdersPage(_currentUser));
                }
                else
                {
                    Manager.MainFrame.Navigate(new ProductPage(_currentUser));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка сохранения заказа: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            Manager.MainFrame.Navigate(new ProductPage(_currentUser));
        }
    }
}

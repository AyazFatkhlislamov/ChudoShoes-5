using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ChudoShoes
{
    public partial class ProductPage : Page
    {
        private User _currentUser;

        public ProductPage(User user)
        {
            InitializeComponent();
            _currentUser = user;
            Manager.CurrentUser = user;

            if (_currentUser != null)
            {
                UserName.Text = (_currentUser.Surname + " " + _currentUser.Name + " " + _currentUser.Patronymic).Trim();
                UserRole.Text = _currentUser.Role != null ? _currentUser.Role.RoleName : "Авторизованный пользователь";
                FiltersWrapPanel.IsEnabled = true;
                BtnCart.Visibility = Visibility.Visible;

                if (_currentUser.ID_Role == 1 || _currentUser.ID_Role == 2)
                {
                    BtnOrders.Visibility = Visibility.Visible;
                }
                else
                {
                    BtnOrders.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                UserName.Text = "Гость";
                UserRole.Text = "Неавторизованный пользователь";
                FiltersWrapPanel.IsEnabled = false;
                BtnOrders.Visibility = Visibility.Collapsed;
                BtnCart.Visibility = Visibility.Collapsed;
            }

            LoadCategories();
            UpdateServices();
            UpdateCartButtonState();

            this.IsVisibleChanged += ProductPage_IsVisibleChanged;
        }

        private void ProductPage_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                UpdateServices();
                UpdateCartButtonState();
            }
        }

        private void UpdateCartButtonState()
        {
            int count = CartManager.GetTotalCount();
            BtnCart.Content = "Текущий заказ (" + count + ")";

            if (CartManager.IsCreatingOrderByStaff && CartManager.SelectedClientForOrder != null)
            {
                string clientFio = (CartManager.SelectedClientForOrder.Surname + " " + CartManager.SelectedClientForOrder.Name + " " + CartManager.SelectedClientForOrder.Patronymic).Trim();
                TextBlockOrderModeBanner.Text = "Режим добавления заказа для клиента: " + clientFio + " (выберите модель обуви из каталога)";
                TextBlockOrderModeBanner.Visibility = Visibility.Visible;
            }
            else
            {
                TextBlockOrderModeBanner.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnCart_Click(object sender, RoutedEventArgs e)
        {
            Manager.MainFrame.Navigate(new CartPage(_currentUser));
        }

        private void BtnOrders_Click(object sender, RoutedEventArgs e)
        {
            Manager.MainFrame.Navigate(new OrdersPage(_currentUser));
        }

        private void ProductListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedProduct = ProductListView.SelectedItem as Product;
            if (selectedProduct != null)
            {
                Manager.MainFrame.Navigate(new ProductDetailPage(selectedProduct, _currentUser));
                ProductListView.SelectedItem = null;
            }
        }

        private void LoadCategories()
        {
            try
            {
                var categories = ChudoShoesFatkhlislamovDBEntities.GetContext().Category.ToList();
                var categoryNames = new List<string> { "Все категории" };
                categoryNames.AddRange(categories.Select(c => c.CategoryName));

                CBoxFilter.ItemsSource = categoryNames;
                CBoxFilter.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Не удалось загрузить список категорий из базы данных.\nТехническая информация: " + ex.Message,
                    "Ошибка загрузки данных",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void UpdateServices()
        {
            if (!IsInitialized || ProductListView == null || CBoxFilter == null || TBoxSearch == null || ComboSort == null)
            {
                return;
            }

            try
            {
                var context = ChudoShoesFatkhlislamovDBEntities.GetContext();

                var currentProducts = context.Product
                    .Include(p => p.Manufacturer)
                    .Include(p => p.Category)
                    .Include(p => p.Subcategory)
                    .ToList();

                int totalCount = currentProducts.Count;

                // 1. Фильтрация по категории товара
                if (CBoxFilter.SelectedIndex > 0 && CBoxFilter.SelectedItem != null)
                {
                    string selectedCat = CBoxFilter.SelectedItem.ToString();
                    currentProducts = currentProducts
                        .Where(p => p.Category != null && p.Category.CategoryName == selectedCat)
                        .ToList();
                }

                // 2. Поиск в реальном времени по наименованию и описанию товара
                if (!string.IsNullOrWhiteSpace(TBoxSearch.Text))
                {
                    string search = TBoxSearch.Text.Trim().ToLower();
                    currentProducts = currentProducts.Where(p =>
                        (p.CleanProductName != null && p.CleanProductName.ToLower().Contains(search)) ||
                        (p.CleanDescription != null && p.CleanDescription.ToLower().Contains(search))
                    ).ToList();
                }

                // 3. Сортировка по цене со скидкой (выбор сохраняется при поиске и фильтрации)
                if (ComboSort.SelectedIndex == 1)
                {
                    currentProducts = currentProducts.OrderBy(p => p.DiscountPrice).ToList();
                }
                else if (ComboSort.SelectedIndex == 2)
                {
                    currentProducts = currentProducts.OrderByDescending(p => p.DiscountPrice).ToList();
                }

                ProductListView.ItemsSource = null;
                ProductListView.ItemsSource = currentProducts;
                TBNumRecords.Text = "Показано: " + currentProducts.Count + " из " + totalCount;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Произошла ошибка при обновлении каталога товаров.\nТехническая информация: " + ex.Message,
                    "Ошибка фильтрации",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void TBoxSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateServices();
        }

        private void CBoxFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateServices();
        }

        private void ComboSort_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateServices();
        }
    }
}

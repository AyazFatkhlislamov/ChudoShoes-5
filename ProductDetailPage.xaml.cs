using System;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace ChudoShoes
{
    public partial class ProductDetailPage : Page
    {
        private Product _currentProduct;

        private User _currentUser;

        public class SizeItemView
        {
            public int ID_StockItem { get; set; }

            public string DisplaySize { get; set; }

            public int AvailableQty { get; set; }
        }

        public ProductDetailPage(Product product, User user)
        {
            InitializeComponent();
            _currentProduct = product;
            _currentUser = user;

            FillUserInfo();
            LoadProductDetailsFromDatabase();
        }

        private void FillUserInfo()
        {
            if (_currentUser == null)
            {
                UserName.Text = "Гость";
                UserRole.Text = "Неавторизованный пользователь";
                return;
            }

            UserName.Text = (_currentUser.Surname + " " + _currentUser.Name + " " + _currentUser.Patronymic).Trim();
            UserRole.Text = _currentUser.Role != null ? _currentUser.Role.RoleName : "Авторизованный пользователь";
        }

        private void LoadProductDetailsFromDatabase()
        {
            if (_currentProduct == null)
            {
                return;
            }

            try
            {
                var context = ChudoShoesFatkhlislamovDBEntities.GetContext();

                var dbProduct = context.Product
                    .Include(p => p.Category)
                    .Include(p => p.Subcategory)
                    .Include(p => p.Manufacturer)
                    .FirstOrDefault(p => p.ID_Product == _currentProduct.ID_Product);

                if (dbProduct != null)
                {
                    _currentProduct = dbProduct;
                }

                TextBlockName.Text = _currentProduct.CleanProductName;
                string subCat = _currentProduct.Subcategory != null ? " (" + _currentProduct.Subcategory.SubcategoryName + ")" : "";
                TextBlockCategory.Text = "Категория: " + _currentProduct.CategoryTitle + subCat;
                TextBlockManufacturer.Text = "Производство: " + _currentProduct.ManufacturerTitle;
                TextBlockComposition.Text = "Состав: " + (string.IsNullOrWhiteSpace(_currentProduct.Composition) ? "Не указан" : _currentProduct.Composition.Trim());
                TextBlockAllSizesRange.Text = "Размерный ряд модели: " + _currentProduct.AllSizesRangeString;
                TextBlockAvailableSizes.Text = "Доступные для заказа размеры: " + _currentProduct.SizesString;
                TextBlockDescription.Text = string.IsNullOrWhiteSpace(_currentProduct.CleanDescription) ? "Описание отсутствует." : _currentProduct.CleanDescription;

                if (_currentProduct.DiscountPrice < _currentProduct.Price)
                {
                    TextBlockOldPrice.Text = _currentProduct.Price.ToString("N2") + " ₽";
                    TextBlockOldPrice.Visibility = Visibility.Visible;
                }
                else
                {
                    TextBlockOldPrice.Visibility = Visibility.Collapsed;
                }

                TextBlockDiscountPrice.Text = _currentProduct.DiscountPrice.ToString("N2") + " ₽";

                try
                {
                    ProductImage.Source = new BitmapImage(new Uri(_currentProduct.ProductPhoto, UriKind.RelativeOrAbsolute));
                }
                catch
                {
                    ProductImage.Source = new BitmapImage(new Uri("/res/picture.png", UriKind.RelativeOrAbsolute));
                }

                if (_currentUser == null)
                {
                    OrderActionPanel.Visibility = Visibility.Collapsed;
                    BtnGoToCart.Visibility = Visibility.Collapsed;
                    TextBlockGuestNotice.Visibility = Visibility.Visible;
                    return;
                }

                BtnGoToCart.Visibility = Visibility.Visible;

                var stockList = context.StockItem
                    .Include(s => s.Size)
                    .Where(s => s.ID_Product == _currentProduct.ID_Product && s.QuantityAvailable > 0 && s.Size != null)
                    .OrderBy(s => s.Size.SizeValue)
                    .ToList();

                var sizeItems = stockList.Select(s => new SizeItemView
                {
                    ID_StockItem = s.ID_StockItem,
                    DisplaySize = s.Size.SizeValue.ToString("0.#"),
                    AvailableQty = s.QuantityAvailable
                }).ToList();

                ComboSizes.ItemsSource = sizeItems;
                ComboSizes.DisplayMemberPath = "DisplaySize";
                ComboSizes.SelectedValuePath = "ID_StockItem";

                if (sizeItems.Any())
                {
                    ComboSizes.SelectedIndex = 0;
                    BtnAddToCart.IsEnabled = true;
                }
                else
                {
                    TextBlockStockAvailable.Text = "Нет доступных для заказа размеров на складе";
                    BtnAddToCart.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Ошибка при загрузке данных карточки товара из базы данных.\nТехническая информация: " + ex.Message,
                    "Ошибка загрузки",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ComboSizes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = ComboSizes.SelectedItem as SizeItemView;
            if (selected != null)
            {
                var inCart = CartManager.Items.FirstOrDefault(i => i.ID_StockItem == selected.ID_StockItem);
                int alreadyInCart = inCart != null ? inCart.Quantity : 0;
                TextBlockStockAvailable.Text = "Доступно на складе: " + selected.AvailableQty + " шт. (в текущем заказе: " + alreadyInCart + " шт.)";
            }
        }

        private void TextBoxQuantity_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            bool isDigit = e.Text.All(char.IsDigit);
            if (!isDigit)
            {
                e.Handled = true;
                MessageBox.Show(
                    "В поле «Количество пар» разрешен ввод только арабских цифр (целых положительных чисел).\nПожалуйста, укажите корректное числовое значение (например: 1, 2).",
                    "Некорректный ввод данных",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void BtnAddToCart_Click(object sender, RoutedEventArgs e)
        {
            var selectedSize = ComboSizes.SelectedItem as SizeItemView;
            if (selectedSize == null)
            {
                MessageBox.Show(
                    "Не выбран размер обуви.\nПожалуйста, выберите требуемый размер из выпадающего списка «Выберите доступный размер» и повторите попытку.",
                    "Предупреждение",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            int qty;
            bool isParsed = int.TryParse(TextBoxQuantity.Text.Trim(), out qty);
            if (!isParsed || qty <= 0)
            {
                MessageBox.Show(
                    "Указано некорректное количество пар обуви.\nПожалуйста, введите целое положительное число больше нуля (например, 1).",
                    "Ошибка ввода количества",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                TextBoxQuantity.Focus();
                return;
            }

            var existingInCart = CartManager.Items.FirstOrDefault(i => i.ID_StockItem == selectedSize.ID_StockItem);
            int alreadyInCart = existingInCart != null ? existingInCart.Quantity : 0;

            if (qty + alreadyInCart > selectedSize.AvailableQty)
            {
                MessageBox.Show(
                    "Запрошенное количество превышает доступный остаток на складе!\n" +
                    "Доступно всего: " + selectedSize.AvailableQty + " шт. (уже добавлено в текущий заказ: " + alreadyInCart + " шт.).\n" +
                    "Пожалуйста, уменьшите количество пар до " + (selectedSize.AvailableQty - alreadyInCart) + " или выберите другой размер.",
                    "Превышение доступного остатка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            CartManager.AddProduct(
                _currentProduct,
                selectedSize.ID_StockItem,
                selectedSize.DisplaySize,
                qty,
                _currentProduct.DiscountPrice,
                selectedSize.AvailableQty);

            var result = MessageBox.Show(
                "Товарная позиция «" + _currentProduct.CleanProductName + "» (размер " + selectedSize.DisplaySize + ", кол-во: " + qty + " шт.) добавлена в заказ.\n\n" +
                "Нажмите «Да», чтобы перейти к просмотру и подтверждению заказа, или «Нет», чтобы продолжить выбор обуви в каталоге.",
                "Позиция добавлена в заказ",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                Manager.MainFrame.Navigate(new CartPage(_currentUser));
            }
            else
            {
                Manager.MainFrame.GoBack();
            }
        }

        private void BtnAbortOrder_Click(object sender, RoutedEventArgs e)
        {
            if (CartManager.Items.Any())
            {
                var confirm = MessageBox.Show(
                    "Вы действительно хотите полностью отказаться от текущего формируемого заказа и очистить выбранные позиции?",
                    "Отказ от оформления заказа",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirm != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            CartManager.Clear();
            MessageBox.Show(
                "Формирование заказа отменено. Все несохраненные позиции очищены.",
                "Информация",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Manager.MainFrame.GoBack();
        }

        private void BtnGoToCart_Click(object sender, RoutedEventArgs e)
        {
            Manager.MainFrame.Navigate(new CartPage(_currentUser));
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Manager.MainFrame.GoBack();
        }
    }
}

using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ChudoShoes
{
    public partial class AuthPage : Page
    {
        public AuthPage()
        {
            InitializeComponent();
            this.Title = "Чудо Обувь — Авторизация";
        }

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string login = TBoxLogin.Text.Trim();

            if (string.IsNullOrEmpty(login))
            {
                MessageBox.Show(
                    "Поле логина не может быть пустым.\nПожалуйста, введите ваш логин из списка пользователей системы и нажмите кнопку «Войти».",
                    "Предупреждение",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                TBoxLogin.Focus();
                return;
            }

            try
            {
                var context = ChudoShoesFatkhlislamovDBEntities.GetContext();
                var user = context.User.FirstOrDefault(u => u.Login == login);

                if (user != null)
                {
                    CartManager.Clear();
                    Manager.CurrentUser = user;

                    string fullName = (user.Surname + " " + user.Name + " " + user.Patronymic).Trim();
                    string roleName = user.Role != null ? user.Role.RoleName : "Авторизованный пользователь";

                    MessageBox.Show(
                        "Добро пожаловать, " + fullName + "!\nВаша роль в системе: " + roleName + ".",
                        "Успешный вход",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    Manager.MainFrame.Navigate(new ProductPage(user));
                }
                else
                {
                    MessageBox.Show(
                        "Пользователь с логином «" + login + "» не найден в базе данных.\nПроверьте правильность написания логина (регистр и раскладку клавиатуры) и повторите попытку.",
                        "Ошибка авторизации",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(
                    "Не удалось выполнить подключение к базе данных при авторизации.\nТехническая информация: " + ex.Message,
                    "Ошибка базы данных",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void BtnGuest_Click(object sender, RoutedEventArgs e)
        {
            CartManager.Clear();
            Manager.CurrentUser = null;
            Manager.MainFrame.Navigate(new ProductPage(null));
        }
    }
}
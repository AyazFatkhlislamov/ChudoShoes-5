using System;
using System.Windows;
using System.Windows.Controls;

namespace ChudoShoes
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Manager.MainFrame = MainFrame;
            MainFrame.Navigate(new AuthPage());
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (MainFrame.CanGoBack)
            {
                MainFrame.GoBack();
            }
        }

        private void MainFrame_ContentRendered(object sender, EventArgs e)
        {
            Page currentPage = MainFrame.Content as Page;
            if (currentPage != null && !string.IsNullOrWhiteSpace(currentPage.Title))
            {
                this.Title = currentPage.Title;
            }

            if (MainFrame.Content is AuthPage)
            {
                BtnBack.Visibility = Visibility.Collapsed;
            }
            else
            {
                BtnBack.Visibility = Visibility.Visible;
            }
        }
    }
}
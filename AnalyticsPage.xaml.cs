using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using PdfCore = iTextSharp.text.pdf;
using PdfText = iTextSharp.text;

namespace ChudoShoes
{
    public partial class AnalyticsPage : Page
    {
        private readonly User _currentUser;
        private bool _isReady;

        public class TopProductRow
        {
            public int Rank { get; set; }
            public string ProductName { get; set; }
            public string ManufacturerName { get; set; }
            public int Quantity { get; set; }
        }

        public class AnalyticsReport
        {
            public int OrdersCount { get; set; }
            public decimal TotalSum { get; set; }
            public List<TopProductRow> TopProducts { get; set; }
        }

        public AnalyticsPage(User user)
        {
            InitializeComponent();
            _currentUser = user;

            if (_currentUser != null)
            {
                UserName.Text = (_currentUser.Surname + " " + _currentUser.Name + " " + _currentUser.Patronymic).Trim();
                UserRole.Text = _currentUser.Role != null ? _currentUser.Role.RoleName : "Администратор";
            }

            // По умолчанию выбран текущий календарный месяц
            DateTime today = DateTime.Today;
            StartDatePicker.SelectedDate = new DateTime(today.Year, today.Month, 1);
            EndDatePicker.SelectedDate = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));

            _isReady = true;
            RefreshData();
        }

        // Пересчёт при любом изменении даты, без кнопки «Обновить»
        private void DatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isReady)
            {
                return;
            }
            RefreshData();
        }

        // Пользователь ввёл в поле даты текст, который не является датой
        private void DatePicker_DateValidationError(object sender, DatePickerDateValidationErrorEventArgs e)
        {
            e.ThrowException = false;
            DatePicker picker = sender as DatePicker;
            string wrongText = e.Text;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                MessageBox.Show(
                    "Введена некорректная дата: «" + wrongText + "».\nВведите дату в формате ДД.ММ.ГГГГ или выберите её в календаре.",
                    "Ошибка ввода даты",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                if (picker != null)
                {
                    picker.Text = picker.SelectedDate.HasValue ? picker.SelectedDate.Value.ToShortDateString() : string.Empty;
                }
            }));
        }

        private bool TryGetPeriod(out DateTime start, out DateTime end, out string error)
        {
            start = DateTime.MinValue;
            end = DateTime.MinValue;
            error = null;

            if (!StartDatePicker.SelectedDate.HasValue || !EndDatePicker.SelectedDate.HasValue)
            {
                error = "Укажите дату начала и дату окончания периода.";
                return false;
            }

            start = StartDatePicker.SelectedDate.Value.Date;
            end = EndDatePicker.SelectedDate.Value.Date;

            if (start > end)
            {
                error = "Дата начала не может быть позже даты окончания.";
                return false;
            }

            return true;
        }

        private void ShowPeriodError(string message)
        {
            TextBlockError.Text = message;
            TextBlockError.Visibility = Visibility.Visible;
            ClearResults();
            MessageBox.Show(message, "Ошибка периода", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void ClearResults()
        {
            TextBlockOrdersCount.Text = "0";
            TextBlockTotalSum.Text = "0,00 ₽";
            ListViewTop.ItemsSource = null;
            TextBlockEmpty.Visibility = Visibility.Collapsed;
        }

        private void RefreshData()
        {
            DateTime start;
            DateTime end;
            string error;

            if (!TryGetPeriod(out start, out end, out error))
            {
                ShowPeriodError(error);
                return;
            }

            TextBlockError.Visibility = Visibility.Collapsed;

            try
            {
                AnalyticsReport report = BuildReport(start, end);
                TextBlockOrdersCount.Text = report.OrdersCount.ToString();
                TextBlockTotalSum.Text = report.TotalSum.ToString("N2") + " ₽";
                ListViewTop.ItemsSource = report.TopProducts;
                TextBlockEmpty.Visibility = report.TopProducts.Any() ? Visibility.Collapsed : Visibility.Visible;
            }
            catch (Exception ex)
            {
                ClearResults();
                MessageBox.Show(
                    "Не удалось получить данные аналитики из базы данных.\nТехническая информация: " + ex.Message,
                    "Ошибка загрузки данных",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // Цена одной пары в расчёте суммы продаж.
        // Берётся так же, как в «Управлении заказами» (с учётом скидки 25%).
        // Чтобы считать по полной цене, замените строку на: return product.Price;
        private static decimal GetUnitPrice(Product product)
        {
            return product.DiscountPrice;
        }

        private AnalyticsReport BuildReport(DateTime startDate, DateTime endDate)
        {
            var context = ChudoShoesFatkhlislamovDBEntities.GetContext();
            DateTime periodStart = startDate.Date;
            DateTime periodEndExclusive = endDate.Date.AddDays(1);

            List<int> orderIds = context.Order
                .Where(o => o.OrderDate >= periodStart && o.OrderDate < periodEndExclusive)
                .Select(o => o.ID_Order)
                .ToList();

            List<OrderItem> items = context.OrderItem
                .Where(oi => orderIds.Contains(oi.ID_Order))
                .ToList();

            Dictionary<int, int> stockToProduct = context.StockItem
                .ToDictionary(s => s.ID_StockItem, s => s.ID_Product);

            Dictionary<int, Product> products = context.Product
                .Include(p => p.Manufacturer)
                .ToList()
                .ToDictionary(p => p.ID_Product);

            var priceByProduct = new Dictionary<int, decimal>();
            var quantityByProduct = new Dictionary<int, int>();
            decimal totalSum = 0m;

            foreach (OrderItem item in items)
            {
                int productId;
                if (!stockToProduct.TryGetValue(item.ID_StockItem, out productId))
                {
                    continue;
                }

                Product product;
                if (!products.TryGetValue(productId, out product))
                {
                    continue;
                }

                decimal price;
                if (!priceByProduct.TryGetValue(productId, out price))
                {
                    price = GetUnitPrice(product);
                    priceByProduct[productId] = price;
                }

                totalSum += price * item.Quantity;

                int currentQuantity;
                quantityByProduct.TryGetValue(productId, out currentQuantity);
                quantityByProduct[productId] = currentQuantity + item.Quantity;
            }

            List<TopProductRow> top = quantityByProduct
                .Select(pair => new { Product = products[pair.Key], Quantity = pair.Value })
                .OrderByDescending(x => x.Quantity)
                .ThenBy(x => x.Product.CleanProductName)
                .Take(5)
                .Select((x, index) => new TopProductRow
                {
                    Rank = index + 1,
                    ProductName = x.Product.CleanProductName,
                    ManufacturerName = x.Product.ManufacturerTitle,
                    Quantity = x.Quantity
                })
                .ToList();

            return new AnalyticsReport
            {
                OrdersCount = orderIds.Count,
                TotalSum = totalSum,
                TopProducts = top
            };
        }

        private void BtnExportPdf_Click(object sender, RoutedEventArgs e)
        {
            DateTime start;
            DateTime end;
            string error;

            if (!TryGetPeriod(out start, out end, out error))
            {
                MessageBox.Show("Экспорт невозможен. " + error, "Ошибка экспорта", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                AnalyticsReport report = BuildReport(start, end);

                SaveFileDialog dialog = new SaveFileDialog();
                dialog.Title = "Сохранение отчёта по продажам";
                dialog.Filter = "PDF-файлы (*.pdf)|*.pdf";
                dialog.DefaultExt = ".pdf";
                dialog.FileName = "Отчёт_по_продажам_" + start.ToString("yyyy-MM-dd") + "_" + end.ToString("yyyy-MM-dd") + ".pdf";

                if (dialog.ShowDialog() != true)
                {
                    return;
                }

                WritePdf(dialog.FileName, start, end, report);

                MessageBox.Show(
                    "Отчёт успешно сохранён:\n" + dialog.FileName,
                    "Экспорт в PDF",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Не удалось сформировать PDF-файл. Возможно, файл открыт в другой программе.\nТехническая информация: " + ex.Message,
                    "Ошибка экспорта",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // Шрифт с кириллицей из папки шрифтов Windows
        private static PdfCore.BaseFont CreatePdfBaseFont()
        {
            string fontsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            string[] fontFiles = { "calibri.ttf", "arial.ttf", "tahoma.ttf" };

            foreach (string fontFile in fontFiles)
            {
                string fontPath = Path.Combine(fontsFolder, fontFile);
                if (!File.Exists(fontPath))
                {
                    continue;
                }

                try
                {
                    return PdfCore.BaseFont.CreateFont(fontPath, PdfCore.BaseFont.IDENTITY_H, PdfCore.BaseFont.EMBEDDED);
                }
                catch (Exception)
                {
                }
            }

            throw new FileNotFoundException("В папке шрифтов Windows не найден подходящий шрифт (calibri.ttf или arial.ttf).");
        }

        private static PdfCore.PdfPCell CreateCell(string text, PdfText.Font font, int alignment, PdfText.BaseColor background)
        {
            PdfCore.PdfPCell cell = new PdfCore.PdfPCell(new PdfText.Phrase(text, font));
            cell.HorizontalAlignment = alignment;
            cell.Padding = 5f;
            if (background != null)
            {
                cell.BackgroundColor = background;
            }
            return cell;
        }

        private static void WritePdf(string filePath, DateTime start, DateTime end, AnalyticsReport report)
        {
            PdfCore.BaseFont baseFont = CreatePdfBaseFont();
            PdfText.Font titleFont = new PdfText.Font(baseFont, 18f, PdfText.Font.BOLD);
            PdfText.Font headerFont = new PdfText.Font(baseFont, 12f, PdfText.Font.BOLD);
            PdfText.Font textFont = new PdfText.Font(baseFont, 12f, PdfText.Font.NORMAL);
            PdfText.BaseColor headerColor = new PdfText.BaseColor(210, 246, 231);

            using (FileStream stream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                PdfText.Document document = new PdfText.Document(PdfText.PageSize.A4, 40f, 40f, 40f, 40f);
                try
                {
                    PdfCore.PdfWriter.GetInstance(document, stream);
                    document.Open();
                    document.AddTitle("Чудо Обувь — отчёт по продажам");

                    PdfText.Paragraph title = new PdfText.Paragraph("Чудо Обувь — отчёт по продажам", titleFont);
                    title.Alignment = PdfText.Element.ALIGN_CENTER;
                    title.SpacingAfter = 14f;
                    document.Add(title);

                    document.Add(new PdfText.Paragraph("Период: " + start.ToString("dd.MM.yyyy") + " — " + end.ToString("dd.MM.yyyy"), textFont));
                    document.Add(new PdfText.Paragraph("Дата формирования: " + DateTime.Now.ToString("dd.MM.yyyy HH:mm"), textFont));
                    document.Add(new PdfText.Paragraph("Количество заказов: " + report.OrdersCount, textFont));

                    PdfText.Paragraph sumLine = new PdfText.Paragraph("Общая сумма продаж: " + report.TotalSum.ToString("N2") + " руб.", textFont);
                    sumLine.SpacingAfter = 14f;
                    document.Add(sumLine);

                    PdfText.Paragraph topTitle = new PdfText.Paragraph("Топ-5 моделей по количеству проданных пар", headerFont);
                    topTitle.SpacingAfter = 8f;
                    document.Add(topTitle);

                    if (report.TopProducts.Count == 0)
                    {
                        document.Add(new PdfText.Paragraph("За выбранный период заказов нет.", textFont));
                    }
                    else
                    {
                        PdfCore.PdfPTable table = new PdfCore.PdfPTable(4);
                        table.WidthPercentage = 100f;
                        table.SetWidths(new float[] { 8f, 50f, 27f, 15f });
                        table.HeaderRows = 1;

                        table.AddCell(CreateCell("№", headerFont, PdfText.Element.ALIGN_CENTER, headerColor));
                        table.AddCell(CreateCell("Наименование модели", headerFont, PdfText.Element.ALIGN_LEFT, headerColor));
                        table.AddCell(CreateCell("Производитель", headerFont, PdfText.Element.ALIGN_LEFT, headerColor));
                        table.AddCell(CreateCell("Кол-во пар", headerFont, PdfText.Element.ALIGN_CENTER, headerColor));

                        foreach (TopProductRow row in report.TopProducts)
                        {
                            table.AddCell(CreateCell(row.Rank.ToString(), textFont, PdfText.Element.ALIGN_CENTER, null));
                            table.AddCell(CreateCell(row.ProductName, textFont, PdfText.Element.ALIGN_LEFT, null));
                            table.AddCell(CreateCell(row.ManufacturerName, textFont, PdfText.Element.ALIGN_LEFT, null));
                            table.AddCell(CreateCell(row.Quantity.ToString(), textFont, PdfText.Element.ALIGN_CENTER, null));
                        }

                        document.Add(table);
                    }
                }
                finally
                {
                    if (document.IsOpen())
                    {
                        document.Close();
                    }
                }
            }
        }
    }
}

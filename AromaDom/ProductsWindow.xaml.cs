using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Windows;

namespace aromadom
{
    public partial class ProductsWindow : Window
    {
        private readonly string materialName;

        public ProductsWindow(string name, string unit)
        {
            InitializeComponent();

            materialName = name;

            Title = "Продукция — " + name;
            HeaderText.Text = "Материал: " + name;
            QuantityColumn.Header = "Количество, " + unit;
        }

        private async void Window_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            await LoadProducts();
        }

        private async Task LoadProducts()
        {
            IsEnabled = false;
            StatusText.Text = "Загрузка...";

            try
            {
                DataTable table = await Task.Run(
                    () => Db.Query(@"
                        SELECT
                            NameProduction,
                            SUM(QuantityMaterialUnit)
                                AS QuantityMaterialUnit
                        FROM dbo.Material_products_import
                        WHERE NameMaterial = @Name
                        GROUP BY NameProduction
                        ORDER BY NameProduction;",
                        new SqlParameter("@Name", materialName)));

                ProductsGrid.ItemsSource = table.DefaultView;

                StatusText.Text = table.Rows.Count == 0
                    ? "Материал пока не используется в продукции."
                    : "Количество наименований: " + table.Rows.Count;
            }
            catch (Exception ex)
            {
                StatusText.Text = "Ошибка загрузки.";

                MessageBox.Show(
                    "Не удалось загрузить продукцию.\n" +
                    "Проверьте подключение и нажмите «Обновить».\n\n" +
                    ex.Message,
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsEnabled = true;
            }
        }

        private async void Refresh_Click(
            object sender,
            RoutedEventArgs e)
        {
            await LoadProducts();
        }

        private void Back_Click(
            object sender,
            RoutedEventArgs e)
        {
            Close();
        }
    }
}
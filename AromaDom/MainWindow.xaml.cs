using System;
using System.Data;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace aromadom
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            await LoadMaterials();
        }

        private async Task LoadMaterials()
        {
            IsEnabled = false;
            StatusText.Text = "Загрузка материалов...";

            try
            {
                string sql = @"
                    SELECT
                        m.*,
                        ISNULL(
                            (
                                SELECT SUM(mp.QuantityMaterialUnit)
                                FROM dbo.Material_products_import AS mp
                                WHERE mp.NameMaterial = m.NameMaterial
                            ),
                            0
                        ) AS RequiredQuantity
                    FROM dbo.Materials_import AS m
                    ORDER BY m.NameMaterial;";

                DataTable table = await Task.Run(
                    () => Db.Query(sql));

                MaterialsList.ItemsSource = table.DefaultView;

                StatusText.Text =
                    "Количество материалов: " + table.Rows.Count;
            }
            catch (Exception ex)
            {
                StatusText.Text = "Ошибка загрузки.";

                MessageBox.Show(
                    "Не удалось загрузить материалы.\n\n" +
                    "Проверьте подключение к базе GIA " +
                    "и нажмите «Обновить».\n\n" +
                    "Подробности: " + ex.Message,
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsEnabled = true;
            }
        }

        private async void Add_Click(
            object sender,
            RoutedEventArgs e)
        {
            MaterialWindow window = new MaterialWindow();
            window.Owner = this;

            if (window.ShowDialog() == true)
            {
                await LoadMaterials();
            }
        }

        private async void Edit_Click(
            object sender,
            RoutedEventArgs e)
        {
            DataRowView material =
                ((Button)sender).DataContext as DataRowView;

            if (material == null)
            {
                return;
            }

            int id = Convert.ToInt32(material["MaterialId"]);

            MaterialWindow window = new MaterialWindow(id);
            window.Owner = this;

            if (window.ShowDialog() == true)
            {
                await LoadMaterials();
            }
        }

        private void Products_Click(
            object sender,
            RoutedEventArgs e)
        {
            DataRowView material =
                ((Button)sender).DataContext as DataRowView;

            if (material == null)
            {
                return;
            }

            ProductsWindow window = new ProductsWindow(
                Convert.ToString(material["NameMaterial"]),
                Convert.ToString(material["Unit"]));

            window.Owner = this;
            window.ShowDialog();
        }

        private async void Refresh_Click(
            object sender,
            RoutedEventArgs e)
        {
            await LoadMaterials();
        }
    }
}
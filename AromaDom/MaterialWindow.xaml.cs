using System;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace aromadom
{
    public partial class MaterialWindow : Window
    {
        private readonly int? materialId;

        private bool isReady;
        private bool isSaving;
        private bool isSaved;

        private string initialState;

        public MaterialWindow(int? id = null)
        {
            InitializeComponent();

            materialId = id;

            Title = id.HasValue
                ? "Редактирование материала"
                : "Добавление материала";

            HeaderText.Text = Title;
        }

        private async void Window_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            IsEnabled = false;

            try
            {
                DataTable types = await Task.Run(
                    () => Db.Query(@"
                        SELECT TypeMaterial
                        FROM dbo.Material_type_import
                        ORDER BY TypeMaterial;"));

                TypeBox.ItemsSource = types.DefaultView;

                if (materialId.HasValue)
                {
                    DataTable table = await Task.Run(
                        () => Db.Query(@"
                            SELECT *
                            FROM dbo.Materials_import
                            WHERE MaterialId = @Id;",
                            new SqlParameter("@Id", materialId.Value)));

                    if (table.Rows.Count == 0)
                    {
                        throw new Exception(
                            "Материал не найден. Обновите список.");
                    }

                    DataRow row = table.Rows[0];

                    TypeBox.SelectedValue = row["TypeMaterial"];
                    NameBox.Text = Convert.ToString(row["NameMaterial"]);
                    UnitBox.Text = Convert.ToString(row["Unit"]);

                    PriceBox.Text = NumberText(row["PriceEd"]);
                    PackageBox.Text = NumberText(row["PackageQuantity"]);
                    StockBox.Text = NumberText(row["StockQuantity"]);
                    MinBox.Text = NumberText(row["MinQuantity"]);
                }
                else
                {
                    PriceBox.Text = "0";
                    PackageBox.Text = "0";
                    StockBox.Text = "0";
                    MinBox.Text = "0";
                }

                initialState = GetState();
                isReady = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Не удалось открыть форму.\n" +
                    "Проверьте подключение и обновите список.\n\n" +
                    ex.Message,
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Close();
            }
            finally
            {
                IsEnabled = true;
            }
        }

        private async void Save_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!isReady || isSaving)
            {
                return;
            }

            if (TypeBox.SelectedValue == null ||
                string.IsNullOrWhiteSpace(NameBox.Text) ||
                string.IsNullOrWhiteSpace(UnitBox.Text))
            {
                MessageBox.Show(
                    "Выберите тип, введите название " +
                    "и единицу измерения.",
                    "Ошибка ввода",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            decimal price;
            decimal package;
            decimal stock;
            decimal minimum;

            if (!ReadNumber(PriceBox, "Цена", out price) ||
                !ReadNumber(PackageBox, "Количество в упаковке", out package) ||
                !ReadNumber(StockBox, "Количество на складе", out stock) ||
                !ReadNumber(MinBox, "Минимальное количество", out minimum))
            {
                return;
            }

            if (decimal.Round(price, 2) != price)
            {
                MessageBox.Show(
                    "В цене допускается не более двух " +
                    "знаков после запятой.",
                    "Ошибка ввода",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                PriceBox.Focus();
                return;
            }

            string name = NameBox.Text.Trim();
            string type = Convert.ToString(TypeBox.SelectedValue);
            string unit = UnitBox.Text.Trim();

            isSaving = true;
            IsEnabled = false;

            try
            {
                await Task.Run(() =>
                {
                    object duplicate = Db.Scalar(@"
                        SELECT COUNT(*)
                        FROM dbo.Materials_import
                        WHERE NameMaterial = @Name
                          AND (@Id IS NULL OR MaterialId <> @Id);",
                        new SqlParameter("@Name", name),
                        new SqlParameter("@Id", SqlDbType.Int)
                        {
                            Value = (object)materialId ?? DBNull.Value
                        });

                    if (Convert.ToInt32(duplicate) > 0)
                    {
                        throw new Exception(
                            "Материал с таким названием уже существует. " +
                            "Введите другое название.");
                    }

                    string sql = materialId.HasValue
                        ? @"
                            UPDATE dbo.Materials_import
                            SET
                                NameMaterial = @Name,
                                TypeMaterial = @Type,
                                PriceEd = @Price,
                                Unit = @Unit,
                                PackageQuantity = @Package,
                                StockQuantity = @Stock,
                                MinQuantity = @Minimum
                            WHERE MaterialId = @Id;"
                        : @"
                            INSERT INTO dbo.Materials_import
                            (
                                NameMaterial,
                                TypeMaterial,
                                PriceEd,
                                Unit,
                                PackageQuantity,
                                StockQuantity,
                                MinQuantity
                            )
                            VALUES
                            (
                                @Name,
                                @Type,
                                @Price,
                                @Unit,
                                @Package,
                                @Stock,
                                @Minimum
                            );";

                    int count = Db.Execute(
                        sql,
                        new SqlParameter("@Name", name),
                        new SqlParameter("@Type", type),
                        new SqlParameter("@Price", price),
                        new SqlParameter("@Unit", unit),
                        new SqlParameter("@Package", package),
                        new SqlParameter("@Stock", stock),
                        new SqlParameter("@Minimum", minimum),
                        new SqlParameter("@Id", SqlDbType.Int)
                        {
                            Value = (object)materialId ?? DBNull.Value
                        });

                    if (count == 0)
                    {
                        throw new Exception(
                            "Материал не найден. " +
                            "Закройте форму и обновите список.");
                    }
                });

                isSaved = true;

                MessageBox.Show(
                    "Материал сохранён.",
                    "Сохранение",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                DialogResult = true;
            }
            catch (SqlException ex)
            {
                string message = ex.Number == 547
                    ? "Нарушена связь между таблицами.\n" +
                      "Если вы изменили название используемого материала, " +
                      "верните прежнее название и сохраните остальные поля."
                    : ex.Number == 2601 || ex.Number == 2627
                        ? "Материал с таким названием уже существует. " +
                          "Введите другое название."
                        : "Проверьте подключение и соответствие " +
                          "введённых значений типам столбцов БД.";

                ShowSaveError(message + "\n\n" + ex.Message);
            }
            catch (Exception ex)
            {
                ShowSaveError(ex.Message);
            }
            finally
            {
                isSaving = false;
                IsEnabled = true;
            }
        }

        private bool ReadNumber(
            TextBox box,
            string field,
            out decimal value)
        {
            string text = box.Text.Trim().Replace(',', '.');

            bool valid = decimal.TryParse(
                text,
                NumberStyles.AllowDecimalPoint |
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out value);

            if (valid && value >= 0)
            {
                return true;
            }

            MessageBox.Show(
                "Поле «" + field + "» должно содержать " +
                "неотрицательное число.\nНапример: 12,50.",
                "Ошибка ввода",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            box.Focus();
            box.SelectAll();

            return false;
        }

        private string NumberText(object value)
        {
            return Convert.ToDecimal(value).ToString(
                "0.############################",
                CultureInfo.GetCultureInfo("ru-RU"));
        }

        private void ShowSaveError(string message)
        {
            MessageBox.Show(
                "Не удалось сохранить материал.\n\n" + message,
                "Ошибка сохранения",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        private string GetState()
        {
            return string.Join("\u001F", new string[]
            {
                Convert.ToString(TypeBox.SelectedValue),
                NameBox.Text,
                PriceBox.Text,
                UnitBox.Text,
                PackageBox.Text,
                StockBox.Text,
                MinBox.Text
            });
        }

        private void Back_Click(
            object sender,
            RoutedEventArgs e)
        {
            Close();
        }

        private void Window_Closing(
            object sender,
            CancelEventArgs e)
        {
            if (isSaving && !isSaved)
            {
                e.Cancel = true;
                return;
            }

            if (!isReady || isSaved || GetState() == initialState)
            {
                return;
            }

            e.Cancel = MessageBox.Show(
                "Выйти без сохранения изменений?",
                "Предупреждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes;
        }
    }
}
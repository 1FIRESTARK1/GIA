using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace test
{
    public class Material
    {
        public string NameMaterial { get; set; }
        public string TypeMaterial { get; set; }
        public decimal PriceEd { get; set; }
        public string Unit { get; set; }
        public decimal PackageQuantity { get; set; }
        public decimal StockQuantity { get; set; }
        public decimal MinQuantity { get; set; }
        public decimal RequiredQuantity { get; set; }
    }

    public static class MaterialRepository
    {
        private static readonly string connectionString =
            @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=GIA;Integrated Security=True;";

        public static List<Material> GetMaterials()
        {
            List<Material> materials = new List<Material>();

            string query = @"
                SELECT
                    m.NameMaterial,
                    m.TypeMaterial,
                    m.PriceEd,
                    m.Unit,
                    m.PackageQuantity,
                    m.StockQuantity,
                    m.MinQuantity,
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

            using (SqlConnection connection =
                new SqlConnection(connectionString))
            using (SqlCommand command =
                new SqlCommand(query, connection))
            {
                connection.Open();

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Material material = ReadMaterial(reader);

                        material.RequiredQuantity = Convert.ToDecimal(
                            reader["RequiredQuantity"]);

                        materials.Add(material);
                    }
                }
            }

            return materials;
        }

        public static Material GetMaterial(string name)
        {
            string query = @"
                SELECT
                    NameMaterial,
                    TypeMaterial,
                    PriceEd,
                    Unit,
                    PackageQuantity,
                    StockQuantity,
                    MinQuantity
                FROM dbo.Materials_import
                WHERE NameMaterial = @Name;";

            using (SqlConnection connection =
                new SqlConnection(connectionString))
            using (SqlCommand command =
                new SqlCommand(query, connection))
            {
                command.Parameters.Add(
                    "@Name", SqlDbType.NVarChar, -1).Value = name;

                connection.Open();

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return ReadMaterial(reader);
                    }
                }
            }

            return null;
        }

        public static List<string> GetMaterialTypes()
        {
            List<string> types = new List<string>();

            string query = @"
                SELECT TypeMaterial
                FROM dbo.Material_type_import
                ORDER BY TypeMaterial;";

            using (SqlConnection connection =
                new SqlConnection(connectionString))
            using (SqlCommand command =
                new SqlCommand(query, connection))
            {
                connection.Open();

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        types.Add(
                            Convert.ToString(reader["TypeMaterial"]));
                    }
                }
            }

            return types;
        }

        public static void SaveMaterial(
            Material material,
            string originalName)
        {
            bool isEditing = originalName != null;

            using (SqlConnection connection =
                new SqlConnection(connectionString))
            {
                connection.Open();

                using (SqlTransaction transaction =
                    connection.BeginTransaction())
                {
                    try
                    {
                        if (isEditing)
                        {
                            string existsQuery = @"
                                SELECT COUNT(*)
                                FROM dbo.Materials_import
                                    WITH (UPDLOCK, HOLDLOCK)
                                WHERE NameMaterial = @OriginalName;";

                            using (SqlCommand command = new SqlCommand(
                                existsQuery, connection, transaction))
                            {
                                command.Parameters.Add(
                                    "@OriginalName",
                                    SqlDbType.NVarChar,
                                    -1).Value = originalName;

                                int count = Convert.ToInt32(
                                    command.ExecuteScalar());

                                if (count != 1)
                                {
                                    throw new InvalidOperationException(
                                        "Материал не найден или его название " +
                                        "не уникально. Закройте форму, " +
                                        "обновите список и повторите действие.");
                                }
                            }
                            if (!string.Equals(
                                originalName,
                                material.NameMaterial,
                                StringComparison.Ordinal))
                            {
                                string usageQuery = @"
                                    SELECT COUNT(*)
                                    FROM dbo.Material_products_import
                                        WITH (UPDLOCK, HOLDLOCK)
                                    WHERE NameMaterial = @OriginalName;";

                                using (SqlCommand command = new SqlCommand(
                                    usageQuery, connection, transaction))
                                {
                                    command.Parameters.Add(
                                        "@OriginalName",
                                        SqlDbType.NVarChar,
                                        -1).Value = originalName;

                                    int count = Convert.ToInt32(
                                        command.ExecuteScalar());

                                    if (count > 0)
                                    {
                                        throw new InvalidOperationException(
                                            "Этот материал используется " +
                                            "в составе продукции. " +
                                            "Сейчас его название менять нельзя, " +
                                            "так как связи настроены по названиям. " +
                                            "Верните прежнее название. " +
                                            "Остальные поля можно изменить.");
                                    }
                                }
                            }
                        }

                        string duplicateQuery = @"
                            SELECT COUNT(*)
                            FROM dbo.Materials_import
                                WITH (UPDLOCK, HOLDLOCK)
                            WHERE NameMaterial = @NameMaterial";

                        if (isEditing)
                        {
                            duplicateQuery +=
                                " AND NameMaterial <> @OriginalName";
                        }

                        using (SqlCommand command = new SqlCommand(
                            duplicateQuery, connection, transaction))
                        {
                            command.Parameters.Add(
                                "@NameMaterial",
                                SqlDbType.NVarChar,
                                -1).Value = material.NameMaterial;

                            if (isEditing)
                            {
                                command.Parameters.Add(
                                    "@OriginalName",
                                    SqlDbType.NVarChar,
                                    -1).Value = originalName;
                            }

                            int count = Convert.ToInt32(
                                command.ExecuteScalar());

                            if (count > 0)
                            {
                                throw new InvalidOperationException(
                                    "Материал с таким названием уже существует. " +
                                    "Введите другое название.");
                            }
                        }

                        string query;

                        if (isEditing)
                        {
                            query = @"
                                UPDATE dbo.Materials_import
                                SET
                                    NameMaterial = @NameMaterial,
                                    TypeMaterial = @TypeMaterial,
                                    PriceEd = @PriceEd,
                                    Unit = @Unit,
                                    PackageQuantity = @PackageQuantity,
                                    StockQuantity = @StockQuantity,
                                    MinQuantity = @MinQuantity
                                WHERE NameMaterial = @OriginalName;";
                        }
                        else
                        {
                            query = @"
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
                                    @NameMaterial,
                                    @TypeMaterial,
                                    @PriceEd,
                                    @Unit,
                                    @PackageQuantity,
                                    @StockQuantity,
                                    @MinQuantity
                                );";
                        }

                        using (SqlCommand command = new SqlCommand(
                            query, connection, transaction))
                        {
                            AddMaterialParameters(command, material);

                            if (isEditing)
                            {
                                command.Parameters.Add(
                                    "@OriginalName",
                                    SqlDbType.NVarChar,
                                    -1).Value = originalName;
                            }

                            command.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private static Material ReadMaterial(SqlDataReader reader)
        {
            return new Material
            {
                NameMaterial =
                    Convert.ToString(reader["NameMaterial"]),
                TypeMaterial =
                    Convert.ToString(reader["TypeMaterial"]),
                PriceEd =
                    Convert.ToDecimal(reader["PriceEd"]),
                Unit =
                    Convert.ToString(reader["Unit"]),
                PackageQuantity =
                    Convert.ToDecimal(reader["PackageQuantity"]),
                StockQuantity =
                    Convert.ToDecimal(reader["StockQuantity"]),
                MinQuantity =
                    Convert.ToDecimal(reader["MinQuantity"])
            };
        }

        private static void AddMaterialParameters(
            SqlCommand command,
            Material material)
        {
            command.Parameters.Add(
                "@NameMaterial",
                SqlDbType.NVarChar,
                -1).Value = material.NameMaterial;

            command.Parameters.Add(
                "@TypeMaterial",
                SqlDbType.NVarChar,
                -1).Value = material.TypeMaterial;

            command.Parameters.Add(
                "@Unit",
                SqlDbType.NVarChar,
                -1).Value = material.Unit;

            AddDecimalParameter(
                command, "@PriceEd", material.PriceEd);

            AddDecimalParameter(
                command, "@PackageQuantity", material.PackageQuantity);

            AddDecimalParameter(
                command, "@StockQuantity", material.StockQuantity);

            AddDecimalParameter(
                command, "@MinQuantity", material.MinQuantity);
        }

        private static void AddDecimalParameter(
            SqlCommand command,
            string name,
            decimal value)
        {
            SqlParameter parameter = command.Parameters.Add(
                name, SqlDbType.Decimal);

            parameter.Precision = 28;
            parameter.Scale = 10;
            parameter.Value = value;
        }
    }
}
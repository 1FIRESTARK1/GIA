using System;
using System.Data.SqlClient;
using System.Globalization;

namespace aromadom
{
    public static class ProductionCalculator
    {
        public static int CalculateProductCount(
            int productTypeId,
            int materialTypeId,
            int rawMaterialQuantity,
            double parameter1,
            double parameter2)
        {
            if (productTypeId <= 0 ||
                materialTypeId <= 0 ||
                rawMaterialQuantity < 0 ||
                !IsPositive(parameter1) ||
                !IsPositive(parameter2))
            {
                return -1;
            }

            object coefficientValue = Db.Scalar(@"
                SELECT koefTypeProduct
                FROM dbo.Product_type_import
                WHERE ProductTypeId = @Id;",
                new SqlParameter("@Id", productTypeId));

            object lossValue = Db.Scalar(@"
                SELECT PercentageMaterial
                FROM dbo.Material_type_import
                WHERE MaterialTypeId = @Id;",
                new SqlParameter("@Id", materialTypeId));

            decimal coefficient;
            decimal loss;

            if (!TryReadDecimal(coefficientValue, out coefficient) ||
                !TryReadDecimal(lossValue, out loss) ||
                coefficient <= 0 ||
                loss < 0 ||
                loss > 100)
            {
                return -1;
            }

            try
            {
                decimal first = (decimal)parameter1;
                decimal second = (decimal)parameter2;

                if (first <= 0 || second <= 0)
                {
                    return -1;
                }

                decimal consumption = first * second * coefficient;

                if (consumption <= 0)
                {
                    return -1;
                }

                // Например, 0,12% переводим в долю как 0,12 / 100.
                decimal usableMaterial =
                    rawMaterialQuantity * (1m - loss / 100m);

                decimal count = decimal.Floor(
                    usableMaterial / consumption);

                return count > int.MaxValue ? -1 : (int)count;
            }
            catch (OverflowException)
            {
                return -1;
            }
        }

        private static bool IsPositive(double value)
        {
            return value > 0 &&
                   !double.IsNaN(value) &&
                   !double.IsInfinity(value);
        }

        private static bool TryReadDecimal(
            object value,
            out decimal result)
        {
            string text = Convert.ToString(
                value, CultureInfo.InvariantCulture);

            text = text
                .Replace("%", "")
                .Replace(" ", "")
                .Replace("\u00A0", "")
                .Replace(',', '.')
                .Trim();

            return decimal.TryParse(
                text,
                NumberStyles.AllowDecimalPoint |
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out result);
        }
    }
}
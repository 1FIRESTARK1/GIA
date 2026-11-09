using System.Data;
using System.Data.SqlClient;

namespace aromadom
{
    public static class Db
    {
        private static readonly string connectionString =
            @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=GIA;Integrated Security=True;";

        public static DataTable Query(
            string sql,
            params SqlParameter[] parameters)
        {
            using (SqlConnection connection =
                new SqlConnection(connectionString))
            using (SqlCommand command =
                new SqlCommand(sql, connection))
            using (SqlDataAdapter adapter =
                new SqlDataAdapter(command))
            {
                command.Parameters.AddRange(parameters);

                DataTable table = new DataTable();
                adapter.Fill(table);

                return table;
            }
        }

        public static int Execute(
            string sql,
            params SqlParameter[] parameters)
        {
            using (SqlConnection connection =
                new SqlConnection(connectionString))
            using (SqlCommand command =
                new SqlCommand(sql, connection))
            {
                command.Parameters.AddRange(parameters);
                connection.Open();

                return command.ExecuteNonQuery();
            }
        }

        public static object Scalar(
            string sql,
            params SqlParameter[] parameters)
        {
            using (SqlConnection connection =
                new SqlConnection(connectionString))
            using (SqlCommand command =
                new SqlCommand(sql, connection))
            {
                command.Parameters.AddRange(parameters);
                connection.Open();

                return command.ExecuteScalar();
            }
        }
    }
}
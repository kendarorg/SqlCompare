using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Data.SqlClient
{
    public static class QueryDict
    {
        public static IEnumerable<Dictionary<string, string>> QueryDictionary(this SqlConnection connection, string query)
        {
            var cmd = new SqlCommand(query, connection);
            var reader = cmd.ExecuteReader();

            var columns = new List<string>();

            for (int i = 0; i < reader.FieldCount; i++)
            {
                columns.Add(reader.GetName(i));
            }
            while (reader.Read())
            {
                var item = new Dictionary<string, string>();
                for (int i = 0; i < columns.Count; i++)
                {
                    var col = columns[i];
                    item.Add(col, Convert.ToString(reader[col]));
                }
                yield return item;
            }
            reader.Close();
        }
    }
}

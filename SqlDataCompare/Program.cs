using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqlDataCompare
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Count() < 4)
            {
                Console.WriteLine("Usage:");
                Console.WriteLine("SqlDataCompare [db1] [db2] [table] (dstFile)");
            }
            var res = Compare(args[0], args[1], args[2]);
            if (args.Count() == 5)
            {
                var path = args[5];
                if (!Path.IsPathRooted(path))
                {
                    path = Path.Combine(Environment.CurrentDirectory, path);
                }
                File.WriteAllText(path, res);
            }
            else
            {
                Console.WriteLine(res);
            }
            Console.ReadKey();
        }

        public static string Compare(string srcConn, string dstConn, string table)
        {
            var src = new SqlConnection(ConfigurationManager.ConnectionStrings[srcConn].ConnectionString);
            var dst = new SqlConnection(ConfigurationManager.ConnectionStrings[dstConn].ConnectionString);



            src.Open();
            dst.Open();

            var res = string.Format("Compare table '{0}' from '{1}' with '{2}'\r\n", table, srcConn, dstConn);

            res += "\r\n";

            var keysArray = GetKeys(src, table).ToArray();

            var packagesSrc = src.QueryDictionary(string.Format("SELECT * FROM {0}", table)).ToList();
            var packagesDst = dst.QueryDictionary(string.Format("SELECT * FROM {0}", table)).ToList();
            res += DoCompare(table, packagesSrc, packagesDst, keysArray);


            src.Close();
            dst.Close();

            return res;
        }

        private static IEnumerable<string> GetKeys(SqlConnection conn, string table)
        {

            var tb = table.Split('.');

            string schemaName = tb[0].Trim(new[] { '[', ']' });
            string tableName = tb[1].Trim(new[] { '[', ']' });




            SqlCommand command = new SqlCommand(@"
        SELECT column_name
        FROM information_schema.key_column_usage
        WHERE TABLE_SCHEMA = @schemaName AND TABLE_NAME = @tableName
          AND OBJECTPROPERTY(object_id(constraint_name), 'IsPrimaryKey') = 1
        ORDER BY table_schema, table_name", conn);
            command.Parameters.Add("@schemaName", SqlDbType.VarChar, 100).Value = schemaName;
            command.Parameters.Add("@tableName", SqlDbType.VarChar, 100).Value = tableName;
            SqlDataReader reader = command.ExecuteReader();
            try
            {
                while (reader.Read())
                {
                    yield return reader[0].ToString();
                }
            }
            finally
            {
                reader.Close();
            }
        }

        private static string DoCompare(string tableName, List<Dictionary<string, string>> src, List<Dictionary<string, string>> dst, string[] keys)
        {

            var result = string.Empty;
            var propsSrc = GetStructure(src);
            var propsDst = GetStructure(dst);


            foreach (var key in propsSrc.Keys)
            {
                if (!propsDst.ContainsKey(key))
                {
                    result += string.Format("Missing field '{0}' on destination.\r\n", key);
                }
            }

            foreach (var key in propsDst.Keys)
            {
                if (!propsSrc.ContainsKey(key))
                {
                    result += string.Format("Missing field '{0}' on source.\r\n", key);
                }
            }

            var different = new List<Tuple<Dictionary<string, string>, Dictionary<string, string>>>();
            var missingOnDst = new List<Dictionary<string, string>>();
            var missingOnSrc = new List<Dictionary<string, string>>();
            var matching = new List<Tuple<Dictionary<string, string>, Dictionary<string, string>>>();
            foreach (var srcItem in src)
            {
                foreach (var dstItem in dst)
                {
                    if (IsKeyMatching(propsSrc, srcItem, dstItem, keys))
                    {
                        if (!IsAllMatching(propsSrc, srcItem, dstItem))
                        {
                            different.Add(new Tuple<Dictionary<string, string>, Dictionary<string, string>>(srcItem, dstItem));
                        }
                        else
                        {
                            matching.Add(new Tuple<Dictionary<string, string>, Dictionary<string, string>>(srcItem, dstItem));
                        }
                    }
                }
            }

            foreach (var srcItem in src)
            {
                if (!matching.Any(m => object.ReferenceEquals(m.Item1, srcItem)) && !different.Any(m => object.ReferenceEquals(m.Item1, srcItem)))
                {
                    missingOnDst.Add(srcItem);
                }
            }

            foreach (var dstItem in dst)
            {
                if (!matching.Any(m => object.ReferenceEquals(m.Item2, dstItem)) && !different.Any(m => object.ReferenceEquals(m.Item2, dstItem)))
                {
                    missingOnSrc.Add(dstItem);
                }
            }

            result += "\r\nDifferent\r\n";
            foreach (var item in different)
            {
                result += "~ (" + PrintItem(item.Item1, propsSrc) + ") FROM\r\n  (" + PrintItem(item.Item2, propsSrc) + ")\r\n";
            }
            result += "\r\nMissing on source\r\n";
            foreach (var item in missingOnSrc)
            {
                result += "- (-) FROM (" + PrintItem(item, propsSrc) + ")\r\n";
            }
            result += "\r\nMissing on destionation\r\n";
            foreach (var item in missingOnDst)
            {
                result += "+ (" + PrintItem(item, propsSrc) + ") FROM (-)\r\n";
            }

            return result;

        }

        private static Dictionary<string, Func<object, object>> GetStructure(List<Dictionary<string, string>> src)
        {
            var dict = new Dictionary<string, Func<object, object>>();
            var firstItem = src.First();
            foreach (var prp in firstItem.Keys)
            {
                dict.Add(prp, new Func<object, object>((s) =>
                {
                    var d = (Dictionary<string, string>)s;
                    if (d.ContainsKey(prp))
                    {
                        return (object)d[prp];
                    }
                    return "#MISSING#";

                }));
            }
            return dict;
        }

        private static string PrintItem(object target, Dictionary<string, Func<object, object>> props)
        {
            var result = new List<string>();
            foreach (var kvp in props.OrderBy(i => i.Key))
            {
                var val = kvp.Value(target);
                if (!(val is string))
                {
                    if (val != null && val.GetType().IsArray || (val as IEnumerable) != null) continue;
                }
                result.Add(string.Format("{0}={1}", kvp.Key, val));
            }
            return string.Join(",", result);
        }

        private static bool IsAllMatching(Dictionary<string, Func<object, object>> props, object src, object dst)
        {
            foreach (var key in props.Keys)
            {
                var srcKey = props[key](src);
                var dstKey = props[key](dst);
                if (srcKey == null && dstKey != null) return false;
                if (srcKey != null && dstKey == null) return false;
                if (srcKey == null && dstKey == null) continue;
                if (srcKey.GetType() != dstKey.GetType()) return false;
                if (srcKey is string)
                {
                    if (srcKey == "#MISSING#" || dstKey == "#MISSING#") continue;
                    if (srcKey.ToString() != dstKey.ToString()) return false;
                    if (!object.Equals(srcKey, dstKey)) return false;
                }
                if (srcKey.GetType().IsArray || (srcKey as IEnumerable) != null) continue;
                if (dstKey.GetType().IsArray || (dstKey as IEnumerable) != null) continue;

               
                if (!object.Equals(srcKey, dstKey)) return false;

            }
            return true;
        }

        private static bool IsKeyMatching(Dictionary<string, Func<object, object>> props, object src, object dst, string[] keys)
        {
            foreach (var key in keys)
            {
                var srcKey = props[key](src);
                var dstKey = props[key](dst);
                if (srcKey == null && dstKey != null) return false;
                if (srcKey != null && dstKey == null) return false;
                if (srcKey == null && dstKey == null) continue;
                if (srcKey.GetType() != dstKey.GetType()) return false;
                if (srcKey is string)
                {
                    if (srcKey == "#MISSING#" || dstKey == "#MISSING#") continue;
                    if (srcKey.ToString() != dstKey.ToString()) return false;
                    if (!object.Equals(srcKey, dstKey)) return false;
                }
                if (srcKey.GetType().IsArray || (srcKey as IEnumerable) != null) continue;
                if (dstKey.GetType().IsArray || (dstKey as IEnumerable) != null) continue;
                if (!object.Equals(srcKey, dstKey)) return false;

            }
            return true;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using System.Threading.Tasks;
using sql = System.Data.SqlClient;

namespace Kama.DatabaseModel
{
    static class Extenstion
    {
        public static void TryClose(this sql.SqlConnection connection)
        {
            if (connection?.State != System.Data.ConnectionState.Closed)
                connection?.Close();
        }

        public static void TryOpen(this sql.SqlConnection connection)
        {
            if (connection?.State == System.Data.ConnectionState.Closed)
                connection?.Open();
        }

        public static Task TryOpenAsync(this sql.SqlConnection connection)
        {
            if (connection?.State == System.Data.ConnectionState.Closed)
                return connection?.OpenAsync();
            else
                return Task.CompletedTask;
        }

        public static string ToCamelCase(this string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return s;
            else
                return s[0].ToString().ToLower() + s.Substring(1);
        }

        public static string ToPascalCase(this string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return s;
            else
                return s[0].ToString().ToUpper() + s.Substring(1);
        }

        public static string GetCSharpDataType(this ParameterInfo p)
        {
            var dic = new Dictionary<string, string>
            {
                {"bigint", "long?"},
                {"binary", "byte[]"},
                {"bit", "bool?"},
                {"char", "char?"},
                {"date", "DateTime?"},
                {"datetime", "DateTime?"},
                {"datetime2", "DateTime?"},
                {"datetimeoffset", "TimeSpan?"},
                {"decimal", "decimal?"},
                {"float", "float?"},
                {"geography", "string"},
                {"geometry", "string"},
                {"hierarchyid", "string"},
                {"image", "byte[]"},
                {"int", "int?"},
                {"money", "decimal?"},
                {"nchar", "char?"},
                {"ntext", "string"},
                {"numeric", "decimal?"},
                {"nvarchar", "string"},
                {"real", "decimal?"},
                {"smalldatetime", "DateTime?"},
                {"smallint", "short?"},
                {"smallmoney", "decimal?"},
                {"sql_variant", "object"},
                {"sysname", "object"},
                {"text", "string"},
                {"time", "DateTime?"},
                {"timestamp", "DateTime?"},
                {"tinyint", "byte?"},
                {"uniqueidentifier", "Guid?"},
                {"varbinary", "byte[]"},
                {"varchar", "string"},
                {"xml", "string"}
            };

            string s = "object";
            dic.TryGetValue(p.TypeName, out s);
            if (s.TrimEnd('?') == "char" && p.Size != 1)
                s = "string";
            return s;
        }

        public static string GetCSharpArgumentName(this ParameterInfo p)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(p.ParameterName) || p.ParameterName.Length < 2)
                    return $"_{p.ParameterName}";

                var name = p.ParameterName.Substring(2);
                if (name.Length < 3)
                    return $"_{name.ToLower()}";

                name = name[0].ToString().ToLower() + name.Substring(1);
                return $"_{name}";
            }
            catch
            {
                return $"_{p.ParameterName}";
            }
        }
    }
}

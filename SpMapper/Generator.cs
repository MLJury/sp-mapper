using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using q = System.Data.SqlClient;

namespace Kama.DatabaseModel
{
    enum MethodType : byte
    {
        CommandMethod = 1,
        AsyncMethod = 2,
        SyncMethod = 3,
      //  AsyncListMethod = 4,
       // SyncListMethod = 5
    }

    
    public class Generator
    {
        public Generator(string connectionString, string ignoredSchema = "")
        {
            _connectionString = connectionString;
            _ignoredSchema = getIgnoredSchema(ignoredSchema);
        }

        public Generator(Func<string> getConnectionString, string ignoredSchema = "")
            : this(getConnectionString?.Invoke(), ignoredSchema)
        {
        }

        readonly string _ignoredSchema;
        readonly string _connectionString;

        private string getIgnoredSchema(string ignoredSchema)
        {
            ignoredSchema = string.IsNullOrWhiteSpace(ignoredSchema) ? "sys" : ("sys," + ignoredSchema);
            return ignoredSchema.Split(',').Aggregate((a, b) => $"'{a.Trim()}', '{b.Trim()}'");
        }

        private DataTable loadData(string query)
        {
            var table = new DataTable();

            using (var connection = new q.SqlConnection(_connectionString))
            {
                connection.TryOpen();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = query;
                    using (var reader = command.ExecuteReader())
                    {
                        table.Load(reader);
                        reader.Close();
                    }
                }

                connection.TryClose();
            }

            return table;
        }

        private IEnumerable<T> MapToModel<T>(DataTable table)
            where T : class, new()
        {
            var models = new List<T>();

            var properties = typeof(T).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var fields = typeof(T).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            string procedureName = "";
            foreach (DataRow row in table.Rows)
            {
                try
                {
                    var model = new T();
                    procedureName = row["ProcedureName"].ToString();

                    foreach (DataColumn col in table.Columns)
                    {
                        var val = row[col];
                        if (val != null && val.GetType() == typeof(DBNull))
                            val = null;

                        var field = fields.FirstOrDefault(f => f.Name.Equals(col.ColumnName));
                        if (field != null)
                            field.SetValue(model, val);
                        else
                        {
                            var prop = properties.FirstOrDefault(p => p.Name.Equals(col.ColumnName));
                            if (prop != null)
                                prop.SetValue(model, val);
                        }
                    }

                    models.Add(model);
                }
                catch (Exception e)
                {
                }
            }

            return models;
        }

        private IEnumerable<ParameterInfo> getProcedureWithParameters()
        {
            var query = @"SELECT p.schema_id SchemaID
                                , p.[object_id] ProcedureID
	                            , COALESCE(a.parameter_id, -1) ParameterID
                                , COALESCE(a.system_type_id, -1) TypeID
                                , h.[name] SchemaName
                                , p.[name] ProcedureName
	                            , t.[name] TypeName
                                , COALESCE(a.max_length, 0) Size
                                , a.[name] ParameterName
                                , CAST(COALESCE(a.is_output, 0) AS BIT) IsOutput
                            FROM sys.procedures p
                            INNER JOIN sys.schemas h ON p.[schema_id] = h.[schema_id]
                            LEFT JOIN sys.parameters a ON p.[object_id] = a.[object_id]
                            LEFT JOIN sys.types t ON a.user_type_id = t.user_type_id
                          WHERE RIGHT(p.[name], 1) <> '_' AND h.[name] NOT IN (" + _ignoredSchema + ")";

            var table = loadData(query);

            return MapToModel<ParameterInfo>(table);
        }

        private string generateCommandMethod(MethodInfo info)
        {
            var method = new StringBuilder();
            method.AppendLine(info.GetSignatureLine(MethodType.CommandMethod));
            method.AppendLine("{");
            var indent = new string('\t', 5);

            method.AppendLine($"return base.CreateCommand(\"{info.StroredProcedureName}\", \n\tSystem.Data.CommandType.StoredProcedure, \n\tnew Parameter[]{{");

            foreach (var parameter in info.Parameters.Where(p => p.ParameterID > 0))
            {
                string argType = parameter.GetCSharpDataType(),
                       argName = parameter.GetCSharpArgumentName();

                if (parameter.IsOutput)
                    method.AppendLine($"{indent}new Parameter {{ Name = \"{parameter.ParameterName}\", IsOutput = true }}, ");
                else
                {
                    string paramValue = "";
                    if (argType.Equals("string", StringComparison.OrdinalIgnoreCase))
                        paramValue = $"string.IsNullOrWhiteSpace({argName}) ? DBNull.Value : (object)ReplaceArabicWithPersianChars({argName})";
                    else
                        paramValue = $"{argName} == null ? DBNull.Value : (object){argName}";

                    method.AppendLine($"{indent}new Parameter {{ Name = \"{parameter.ParameterName}\", IsOutput = false, Value = {paramValue} }}, ");
                }
            }

            method.AppendLine("\t});\n");

            method.AppendLine("}");

            return method.ToString();
           
        }

        private string generateAsyncMethod(MethodInfo info)
        {
            var method = new StringBuilder();
            method.AppendLine(info.GetSignatureLine(MethodType.AsyncMethod));
            method.AppendLine("{");
            method.AppendLine($"\tusing(var cmd = {info.CommandMethodName}({info.CallArguments}))\n{{");
            method.AppendLine("\treturn new ResultSet(cmd, await ExecuteAsync(cmd), _modelValueBinder);");  //, contextInfo
            method.AppendLine("}\n}");
            return method.ToString();
        }

        private string generateSyncMethod(MethodInfo info)
        {
            var method = new StringBuilder();
            method.AppendLine(info.GetSignatureLine(MethodType.SyncMethod));
            method.AppendLine("{");
            method.AppendLine($"\tusing(var cmd = {info.CommandMethodName}({info.CallArguments}))\n{{");
            method.AppendLine("\treturn new ResultSet(cmd, Execute(cmd), _modelValueBinder);");  //, contextInfo
            method.AppendLine("}\n}");
            return method.ToString();
        }

        private string generateClass(string schemaName, IEnumerable<ParameterInfo> parameters)
        {
            var classLines = new StringBuilder();

            classLines.AppendLine($"class {schemaName.ToUpper()}: Database");
            classLines.AppendLine("{");

            #region Generate Constructors
            classLines.AppendLine($"#region Constructors");

            classLines.AppendLine($"public {schemaName.ToUpper()}(string connectionString)");
            classLines.AppendLine("\t:base(connectionString){}\n");

            classLines.AppendLine($"public {schemaName.ToUpper()}(string connectionString, IModelValueBinder modelValueBinder)");
            classLines.AppendLine("\t:base(connectionString, modelValueBinder){}");

            classLines.AppendLine("#endregion\n"); 
            #endregion

            var procedures = parameters.GroupBy(p => p.ProcedureID);

            foreach (var procedure in procedures)
            {
                var info = new MethodInfo(procedure);

                classLines.AppendLine($"#region {info.SynMethodName}\n");
                classLines.AppendLine(generateCommandMethod(info));
                classLines.AppendLine(generateAsyncMethod(info));
                classLines.AppendLine(generateSyncMethod(info));
                classLines.AppendLine("#endregion\n");
            }

            classLines.AppendLine("}"); //-- end of schema class

            return classLines.ToString();
        }

        public string Generate(string @namespace, params string[] usings)
        {
            Func<string, bool> hasItem = item => usings.Any(e => e.Equals(item));

            var lines = new StringBuilder();

            if (!hasItem("System"))
                lines.AppendLine("using System;");

            if (!hasItem("Kama.DatabaseModel"))
                lines.AppendLine("using Kama.DatabaseModel;");

            if (!hasItem("System.Threading.Tasks"))
                lines.AppendLine("using System.Threading.Tasks;");

            foreach (var item in usings)
                lines.AppendLine($"using {item.TrimEnd(' ', ';').Trim()};");

            lines.AppendLine($"\nnamespace {@namespace}");
            lines.AppendLine("{");

            var allProcedures = getProcedureWithParameters();

            foreach (var schemaItems in allProcedures.GroupBy(p => p.SchemaName))
            {
                var schemaName = schemaItems.Key.ToUpper();

                var schemaClass = generateClass(schemaName, schemaItems);

                lines.AppendLine(schemaClass);
            }

            lines.AppendLine("}");

            return lines.ToString();
        }
    }
}
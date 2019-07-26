using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kama.DatabaseModel
{
    public enum SpType
    {
        Modify = 1,
        Delete = 2,
        List = 3,
        Get = 4
    }

    public class TableColumn
    {
        public string Name { get; set; }

        public string Type { get; set; }

        public string Length { get; set; }

        public string FullTypeName
            => IsString ? $"{Type.ToUpper()}({Length})" : $"{Type.ToUpper()}";

        public string Description { get; set; }

        public bool IsPrimaryKey { get; set; }

        public bool IsNullable { get; set; }

        public bool IsUniqueidentifier
        {
            get
            {
                return Type.ToLower() == "uniqueidentifier";
            }
        }

        public bool IsNumeric
        {
            get
            {
                return Type.ToLower() == "int"
                      || Type.ToLower() == "tinyint"
                      || Type.ToLower() == "bigint"
                      || Type.ToLower() == "smallint";
            }
        }

        public bool IsString
        {
            get
            {
                return Type.ToLower() == "char"
                      || Type.ToLower() == "nchar"
                      || Type.ToLower() == "varchar"
                      || Type.ToLower() == "nvarchar";
            }
        }

        public bool IsTinyint
        {
            get
            {
                return Type.ToLower() == "tinyint";
            }
        }

        public bool IsBit
        {
            get
            {
                return Type.ToLower() == "bit";
            }
        }

        public bool IsUnicode
        {
            get
            {
                return Type.ToLower() == "nchar"
                      || Type.ToLower() == "nvarchar";
            }
        }

    }

    public class SPGenerator
    {
        public string ConnectionString { get; set; }
        public string DatabaseName
        {
            get
            {
                var index1 = ConnectionString.IndexOf("Initial Catalog");
                index1 = ConnectionString.IndexOf("=", index1) + 1;
                var index2 = ConnectionString.IndexOf(";", index1);
                return ConnectionString.Substring(index1, index2 - index1).Trim();
            }
        }
        public string Catalog { get; set; }
        public string TableName { get; set; }
        string TableSingularName
        {
            get
            {
                if (TableName.ToLower().EndsWith("ies"))
                    return TableName.Substring(0, TableName.Length - 3) + "y";
                else if (TableName.ToLower().EndsWith("s"))
                    return TableName.Substring(0, TableName.Length - 1);
                else
                    return TableName;
            }
        }
        public string SpName
        {
            get
            {
                if (SpType == SpType.List)
                    return $"spGet{TableName}s";
                else
                    return $"sp{SpType.ToString()}{TableName}";
            }
        }
        public SpType SpType { get; set; }

        public List<TableColumn> Columns { get; set; }

        string GenerateModifySP()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"USE [{DatabaseName}]");
            sb.AppendLine("GO");
            sb.AppendLine();

            sb.AppendLine($"IF EXISTS(SELECT 1 FROM sys.procedures WHERE [object_id] = OBJECT_ID('{Catalog}.{SpName}'))");
            sb.Append("\t").AppendLine($"DROP PROCEDURE {Catalog}.{SpName}");
            sb.AppendLine("GO");
            sb.AppendLine();

            sb.AppendLine($"CREATE PROCEDURE {Catalog}.{SpName}");

            // -------------------------------------------------------- Input Parameters
            sb.Append("\t").AppendLine($"@AIsNewRecord BIT,");
            foreach (var col in Columns)
            {
                if (col.Type.ToLower() == "hierarchyid")
                    sb.Append("\t").AppendLine("@AParentID UNIQUEIDENTIFIER,");
                else
                    sb.Append("\t").AppendLine($"@A{col.Name} {col.FullTypeName},");
            }
            sb.Append("\t").AppendLine("@ALog NVARCHAR(MAX)");

            // ------------------------------------------------------------- some Header
            sb.AppendLine("WITH ENCRYPTION");
            sb.AppendLine("AS");
            sb.AppendLine("BEGIN");
            sb.Append("\t").AppendLine("SET NOCOUNT ON;");
            sb.Append("\t").AppendLine("SET XACT_ABORT ON;");
            sb.AppendLine();

            // ------------------------------------------------------------- Assign Input Parameters to main Parameters
            sb.Append("\t").AppendLine("DECLARE");
            sb.Append("\t\t").AppendLine("@IsNewRecord BIT = COALESCE(@AIsNewRecord, 0),");
            foreach (var col in Columns)
            {
                if (col.Type.ToLower() == "hierarchyid")
                    sb.Append("\t\t").AppendLine("@ParentID UNIQUEIDENTIFIER = @AParentID,");
                else if (col.IsString)
                    sb.Append("\t\t").AppendLine($"@{col.Name} {col.FullTypeName} = LTRIM(RTRIM(@A{col.Name})),");
                else if (col.IsTinyint || col.IsBit)
                    sb.Append("\t\t").AppendLine($"@{col.Name} {col.FullTypeName} = COALESCE(@A{col.Name}, 0),");
                else
                    sb.Append("\t\t").AppendLine($"@{col.Name} {col.FullTypeName} = @A{col.Name},");
            }
            sb.Append("\t\t").Append("@Log NVARCHAR(MAX) = LTRIM(RTRIM(@ALog))");
            if (Columns.Where(col => col.Type.ToLower() == "hierarchyid").Count() == 0)
            {
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine(",");
                sb.Append("\t\t").AppendLine("@ParentNode HIERARCHYID,");
                sb.Append("\t\t").AppendLine("@Node HIERARCHYID");
                sb.AppendLine();
                sb.Append("\t").AppendLine($"SET @parentNode = (SELECT Node FROM {Catalog}.{TableName} WHERE ID = @ParentID)");
                sb.Append("\t").AppendLine($"IF @ParentNode IS NULL");
                sb.Append("\t\t").AppendLine($"SET @ParentNode = HIERARCHYID::GetRoot()");
                sb.Append("\t").AppendLine($"DECLARE @LastChildNode HIERARCHYID = (SELECT MAX([Node]) FROM {Catalog}.{TableName} WHERE [Node].GetAncestor(1) = @ParentNode)");
                sb.Append("\t").AppendLine($"SET @Node = @ParentNode.GetDescendant(@LastChildNode, NULL)");
            }
            sb.AppendLine();

            // ---------------------------------------------------------- Insert
            sb.Append("\t").AppendLine("BEGIN TRY");
            sb.Append("\t\t").AppendLine("BEGIN TRAN");
            sb.Append("\t\t\t").AppendLine("IF @IsNewRecord = 1 -- insert");
            sb.Append("\t\t\t").AppendLine("BEGIN");
            sb.Append("\t\t\t\t").AppendLine($"INSERT INTO {Catalog}.{TableName}");
            sb.Append("\t\t\t\t").Append("(");
            Columns.ForEach(x => sb.Append($"{x.Name}").Append(x == Columns[Columns.Count - 1] ? "" : ", "));
            sb.AppendLine(")");
            sb.Append("\t\t\t\t").AppendLine("VALUES");
            sb.Append("\t\t\t\t").Append("(");
            Columns.ForEach(x => sb.Append($"@{x.Name}").Append(x == Columns[Columns.Count - 1] ? "" : ", "));
            sb.Append(")");
            sb.AppendLine();
            sb.Append("\t\t\t").AppendLine("END");

            // ------------------------------------------------------------ Update 
            sb.Append("\t\t\t").AppendLine("ELSE    -- update");
            sb.Append("\t\t\t").AppendLine("BEGIN");
            sb.Append("\t\t\t\t").AppendLine($"UPDATE {Catalog}.{TableName}");
            sb.Append("\t\t\t\t").Append("SET ");
            Columns.Where(x => x.Name != "ID").ToList().ForEach(x => sb.Append($"{x.Name} = @{x.Name}").Append(x == Columns[Columns.Count-1] ? "" : ", "));
            sb.AppendLine();
            sb.Append("\t\t\t\t").Append("WHERE ID = @ID");
            sb.AppendLine();
            sb.Append("\t\t\t").AppendLine("END");

            // ---------------------------------------------------------- end of SP
            sb.Append("\t\t\t").AppendLine("EXEC pbl.spAddLog @Log");
            sb.Append("\t\t").AppendLine("COMMIT");
            sb.Append("\t").AppendLine("END TRY");
            sb.Append("\t").AppendLine("BEGIN CATCH");
            sb.Append("\t\t").AppendLine(";THROW");
            sb.Append("\t").AppendLine("END CATCH");
            sb.AppendLine();
            sb.Append("\t").AppendLine("RETURN @@ROWCOUNT");
            sb.AppendLine("END");
            return sb.ToString();
        }

        string GenerateDeleteSP()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"USE [{DatabaseName}]");
            sb.AppendLine("GO");
            sb.AppendLine();

            sb.AppendLine($"IF EXISTS(SELECT 1 FROM sys.procedures WHERE [object_id] = OBJECT_ID('{Catalog}.{SpName}'))");
            sb.Append("\t").AppendLine($"DROP PROCEDURE {Catalog}.{SpName}");
            sb.AppendLine("GO");
            sb.AppendLine("");

            sb.AppendLine($"CREATE PROCEDURE {Catalog}.{SpName}");

            // -------------------------------------------------------- Input Parameters
            sb.Append("\t").AppendLine("@AID UNIQUEIDENTIFIER,");
            sb.Append("\t").AppendLine("@ALog NVARCHAR(MAX)");

            // ------------------------------------------------------------- some Header
            sb.AppendLine("WITH ENCRYPTION");
            sb.AppendLine("AS");
            sb.AppendLine("BEGIN");
            sb.Append("\t").AppendLine("SET NOCOUNT ON;");
            sb.Append("\t").AppendLine("SET XACT_ABORT ON;");
            sb.AppendLine();

            // ------------------------------------------------------------- Assign Input Parameters to main Parameters
            sb.Append("\t").AppendLine("DECLARE");
            sb.Append("\t\t").AppendLine("@ID UNIQUEIDENTIFIER = @AID,");
            sb.Append("\t\t").AppendLine("@Log NVARCHAR(MAX) = LTRIM(RTRIM(@ALog))");
            sb.AppendLine();

            sb.Append("\t").AppendLine("BEGIN TRY");
            sb.Append("\t\t").AppendLine("BEGIN TRAN");
            sb.Append("\t\t\t").AppendLine($"DELETE {Catalog}.{TableName}");
            sb.Append("\t\t\t").AppendLine("WHERE ID = @ID");
            sb.AppendLine();

            // ---------------------------------------------------------- end of SP
            sb.Append("\t\t\t").AppendLine("EXEC pbl.spAddLog @Log");
            sb.Append("\t\t").AppendLine("COMMIT");
            sb.Append("\t").AppendLine("END TRY");
            sb.Append("\t").AppendLine("BEGIN CATCH");
            sb.Append("\t\t").AppendLine(";THROW");
            sb.Append("\t").AppendLine("END CATCH");
            sb.AppendLine();
            sb.Append("\t").AppendLine("RETURN @@ROWCOUNT");
            sb.AppendLine("END");
            return sb.ToString();
        }

        string GenerateListSP()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"USE [{this.DatabaseName}]");
            sb.AppendLine("GO");
            sb.AppendLine();

            sb.AppendLine($"IF EXISTS(SELECT 1 FROM sys.procedures WHERE [object_id] = OBJECT_ID('{Catalog}.{SpName}'))");
            sb.AppendLine($"DROP PROCEDURE {Catalog}.{SpName}");
            sb.AppendLine("GO");
            sb.AppendLine();

            sb.AppendLine($"CREATE PROCEDURE {Catalog}.{SpName}");

            // -------------------------------------------------------- Input Parameters
            // input params
            foreach (var col in Columns.Where(c => c.Name.ToUpper() != "ID"))
            {
                if (col.Type.ToLower() == "hierarchyid")
                    sb.Append("\t").AppendLine("@AParentID UNIQUEIDENTIFIER,");
                else
                    sb.Append("\t").AppendLine($"@A{col.Name} {col.FullTypeName},");
            }
            sb.Append("\t").AppendLine("@ASortExp NVARCHAR(MAX),");
            sb.Append("\t").AppendLine("@APageSize INT,");
            sb.Append("\t").AppendLine("@APageIndex INT");

            // ------------------------------------------------------------- some Header
            sb.AppendLine("WITH ENCRYPTION");
            sb.AppendLine("AS");
            sb.AppendLine("BEGIN");
            sb.Append("\t").AppendLine("SET NOCOUNT ON;");
            sb.AppendLine();

            // ------------------------------------------------------------- Assign Input Parameters to main Parameters
            sb.Append("\t").AppendLine("DECLARE");
            foreach (var col in Columns.Where(c => c.Name.ToUpper() != "ID"))
            {
                if (col.Type.ToLower() == "hierarchyid")
                    sb.Append("\t\t").AppendLine("@ParentID UNIQUEIDENTIFIER = @AParentID,");
                else if (col.IsString)
                    sb.Append("\t\t").AppendLine($"@{col.Name} {col.FullTypeName} = LTRIM(RTRIM(@A{col.Name})),");
                else if (col.IsTinyint || col.IsBit)
                    sb.Append("\t\t").AppendLine($"@{col.Name} {col.FullTypeName} = COALESCE(@A{col.Name}, 0),");
                else
                    sb.Append("\t\t").AppendLine($"@{col.Name} {col.FullTypeName} = @A{col.Name},");
            }
            sb.Append("\t\t").AppendLine("@SortExp NVARCHAR(MAX) = LTRIM(RTRIM(@ASortExp)),");
            sb.Append("\t\t").AppendLine("@PageSize INT = COALESCE(@APageSize, 0),");
            sb.Append("\t\t").AppendLine("@PageIndex INT = COALESCE(@APageIndex, 0)");

            // constant input params
            sb.AppendLine();

            // ---------------------------------------------------------- Paging settings
            sb.Append("\t").AppendLine("IF @PageIndex = 0 ");
            sb.Append("\t").AppendLine("BEGIN");
            sb.Append("\t\t").AppendLine("SET @pagesize = 10000000");
            sb.Append("\t\t").AppendLine("SET @PageIndex = 1");
            sb.Append("\t").AppendLine("END");
            sb.AppendLine();

            sb.Append("\t").AppendLine(@";WITH MainSelect AS");
            sb.Append("\t").AppendLine(@"(");
            sb.Append("\t\t").AppendLine(@"SELECT");
            foreach (var col in Columns)
                if (col.Type.ToLower() != "hierarchyid")
                {
                    if (col != Columns[Columns.Count - 1])
                        sb.Append("\t\t\t").Append($"{TableSingularName}.{col.Name}");
                    else
                        sb.Append("\t\t\t").Append($"{TableSingularName}.{col.Name}");

                    if (col != Columns[Columns.Count - 1])
                        sb.AppendLine(",");
                    else
                        sb.AppendLine();
                }
            sb.Append("\t\t").AppendLine($"FROM {Catalog}.{TableName}");

            // WHERE
            sb.Append("\t\t").AppendLine("WHERE");
            foreach (var col in Columns.Where(c => c.Name.ToUpper() != "ID"))
            {
                if (col != Columns[0])
                    sb.Append("\t\t\t").Append("AND ");
                else
                    sb.Append("\t\t\t");

                if (col.Type.ToLower() == "hierarchyid")
                    sb.AppendLine($"(@ParentID IS NULL OR {TableSingularName}.ParentID = @ParentID)");
                else if (col.IsNumeric || col.IsBit)
                    sb.AppendLine($"(@{col.Name} < 1 OR {TableSingularName}.{col.Name} = @{col.Name})");
                else
                    sb.AppendLine($"(@{col.Name} IS NULL OR {TableSingularName}.{col.Name} = @{col.Name})");
            }

            sb.Append("\t").AppendLine(")");
            sb.Append("\t").AppendLine(",Total AS");
            sb.Append("\t").AppendLine("(");
            sb.Append("\t\t").AppendLine("SELECT COUNT(*) AS Total FROM MainSelect");
            sb.Append("\t").AppendLine(")");
            sb.Append("\t").AppendLine("SELECT * FROM MainSelect,Total");
            sb.Append("\t").AppendLine("ORDER BY ID");
            sb.Append("\t").AppendLine("OFFSET ((@PageIndex - 1) * @PageSize) ROWS FETCH NEXT @PageSize ROWS ONLY;");
            sb.AppendLine("END");

            return sb.ToString();
        }

        string GenerateGetSP()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"USE [{this.DatabaseName}]");
            sb.AppendLine("GO");
            sb.AppendLine("");

            sb.AppendLine($"IF EXISTS(SELECT 1 FROM sys.procedures WHERE [object_id] = OBJECT_ID('{Catalog}.{SpName}'))");
            sb.Append("\t").AppendLine($"DROP PROCEDURE {Catalog}.{SpName}");
            sb.AppendLine("GO");
            sb.AppendLine();
            sb.AppendLine($"CREATE PROCEDURE {Catalog}.{SpName}");
            sb.Append("\t").AppendLine("@AID UNIQUEIDENTIFIER");
            sb.AppendLine("WITH ENCRYPTION");
            sb.AppendLine("AS");
            sb.AppendLine("BEGIN");
            sb.Append("\t").AppendLine("SET NOCOUNT ON;");
            sb.AppendLine();

            sb.Append("\t").AppendLine("DECLARE @ID UNIQUEIDENTIFIER = @AID");
            sb.AppendLine();

            sb.Append("\t").AppendLine(@"SELECT");

            foreach (var col in Columns)
                if (col.Type.ToLower() != "hierarchyid")
                {
                    sb.Append("\t\t").Append($"{TableSingularName}.{col.Name}");

                    if (col != Columns[Columns.Count-1])
                        sb.AppendLine(",");
                    else
                        sb.AppendLine();
                }
            sb.Append("\t").AppendLine($"FROM {Catalog}.{TableName}");
            sb.Append("\t").AppendLine($"WHERE ID = @ID");
            sb.AppendLine("END");

            return sb.ToString();
        }

        void FindColumns()
        {
            Columns = new List<TableColumn>();

            var dbConnection = new SqlConnection(ConnectionString);
            using (SqlCommand command = dbConnection.CreateCommand())
            {

                command.CommandText = string.Format(@"select sc.name Name, tp.name [Type], sc.max_length [Length], COALESCE(sep.value, '') [Description], sc.*
                                                    from sys.columns sc 
                                                    inner join sys.tables st on st.object_id = sc.object_id and st.type = 'U'
                                                    inner join sys.types tp on tp.system_type_id = sc.system_type_id and tp.user_type_id = sc.user_type_id 
                                                    left join sys.extended_properties sep on st.object_id = sep.major_id
                                                                                             and sc.column_id = sep.minor_id
                                                                                             and sep.name = 'MS_Description'
                                                    where st.name = '{0}' 
                                                    order by sc.column_id", this.TableName);
                dbConnection.Open();
                using (var reader = command.ExecuteReader())
                    while (reader.Read())
                    {

                        var col = new TableColumn
                        {
                            Name = reader.GetString(0)
                                  ,
                            Type = reader.GetString(1)
                                  ,
                            Length = reader.GetInt16(2).ToString()
                                  ,
                            Description = reader.GetString(3)
                        };

                        if (col.Length == "-1")
                            col.Length = "Max";
                        else if (col.IsString && col.IsUnicode)
                        {
                            int len = int.Parse(col.Length) / 2;

                            col.Length = len.ToString();
                        }
                        Columns.Add(col);
                    }
                dbConnection.Close();
            }
        }

        public string Generate()
        {
            FindColumns();
            switch (SpType)
            {
                case SpType.Modify:
                    return GenerateModifySP();
                case SpType.Delete:
                    return GenerateDeleteSP();
                case SpType.List:
                    return GenerateListSP();
                case SpType.Get:
                    return GenerateGetSP();
                default:
                    return "";
            }
        }
    }
}

namespace Kama.DatabaseModel
{
    public class ParameterInfo
    {
        public override string ToString() => $"{SchemaName}.{ProcedureName}.{ParameterName.Substring(1)}";

        public int SchemaID { get; set; }

        public int ProcedureID { get; set; }

        public int ParameterID { get; set; }

        public int TypeID { get; set; }

        public string SchemaName { get; set; }

        public string ProcedureName { get; set; }

        public string TypeName { get; set; }

        public string ParameterName { get; set; }

        public bool IsOutput { get; set; }

        public int Size { get; set; }

        public object ParameterValue { get; set; }
    }
}

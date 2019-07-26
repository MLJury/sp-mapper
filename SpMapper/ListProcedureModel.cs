namespace Kama.DatabaseModel
{
    public class ListProcedureModel
    {
        public override string ToString() => ProcedureName;

        public string ProcedureName { get; set; }

        public string ModelName { get; set; }
    }
}

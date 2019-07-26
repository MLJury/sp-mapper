using System;
using System.Collections.Generic;
using System.Linq;

namespace Kama.DatabaseModel
{
    class MethodInfo
    {
        public MethodInfo(IGrouping<int, ParameterInfo> parameters)
        {
            var procedure = parameters.FirstOrDefault();

            Parameters = parameters;
            StroredProcedureName = procedure == null ? string.Empty : $"{procedure.SchemaName}.{procedure.ProcedureName}";
            generateMthodName(procedure);
            generateArguments(parameters);
        }

        private void generateArguments(IEnumerable<ParameterInfo> parameters)
        {
            foreach (var parameter in parameters.Where(p => p.ParameterID > 0 && !p.IsOutput))
            {
                MethodArguments += $"{parameter.GetCSharpDataType()} {parameter.GetCSharpArgumentName()}, ";
                CallArguments += $"{parameter.GetCSharpArgumentName()}, ";
            }

            
            MethodArguments = MethodArguments?.TrimEnd(' ', ',');
            CallArguments = CallArguments?.TrimEnd(' ', ',');
        }

        private void generateMthodName(ParameterInfo procedure)
        {
            if(procedure == null)
            {
                SynMethodName = AsynMethodName = CommandMethodName = string.Empty;
                return;
            }

            SynMethodName = procedure.ProcedureName.Substring(2).ToPascalCase();
            CommandMethodName = $"GetCommand_{SynMethodName}";
            AsynMethodName = $"{SynMethodName}Async";
        }

        public IEnumerable<ParameterInfo> Parameters { get; private set; }

        public string StroredProcedureName { get; private set; }

        public string CallArguments { get; private set; } = "";

        public string MethodArguments { get; private set; } = "";

        public string AsynMethodName { get; private set; }

        public string CommandMethodName { get; private set; }

        public string SynMethodName { get; private set; }

        public string GetSignatureLine(MethodType type)
        {
            
            if(type == MethodType.AsyncMethod)
                return $"public async Task<ResultSet> {AsynMethodName}({( $"{MethodArguments}".TrimEnd(' ', ',') )})";  //IContextInfo contextInfo, 
            else if(type == MethodType.SyncMethod)
                return $"public ResultSet {SynMethodName}({($"{MethodArguments}".TrimEnd(' ', ','))})";  //IContextInfo contextInfo, 
            else
                return $"public System.Data.SqlClient.SqlCommand {CommandMethodName}({MethodArguments})";

          /*  switch (type)
            {
                
                case MethodType.AsyncMethod:
                    return $"public async Task<ResultSet> {AsynMethodName}({MethodArguments})";
                case MethodType.SyncMethod:
                    return $"public ResultSet {SynMethodName}({MethodArguments})";
                case MethodType.AsyncListMethod:
                    return $"public async Task<ResultSet<T>> {AsynMethodName}<T>({MethodArguments}) where T : class, new()";
                case MethodType.SyncListMethod:
                    return $"public ResultSet<T> {SynMethodName}<T>({MethodArguments}) where T : class, new()";
                case MethodType.CommandMethod:
                default:
                    return $"public System.Data.SqlClient.SqlCommand {CommandMethodName}({MethodArguments})";
            }*/
        }
    }
}

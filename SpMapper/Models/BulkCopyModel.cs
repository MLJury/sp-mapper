using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kama.DatabaseModel
{
    public class BulkCopyModel
    {
        public int BatchSize { get; set; }

        public string TableName { get; set; }

        public List<SqlBulkCopyColumnMapping> ColumnMappings { get; set; }
        
        public DataTable DataTable { get; set; }

        public int Timeout { get; set; }
    }
}

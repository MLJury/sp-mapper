using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;

namespace Kama.DatabaseModel
{
    public class ResultSet
    {
        public ResultSet(SqlCommand command)
        {
            this.CommandText = command.CommandText;
            for (int i = 0; i < command.Parameters.Count; i++)
            {
                var prm = command.Parameters[i];
                _parameters.Add(prm.ParameterName, prm.Value);
            }

            ReturnValue = Convert.ToInt32(GetParameterValue(ReturnValueParameterName) ?? 0);
        }

        public ResultSet(SqlCommand command, System.Data.DataSet dataSet, IModelValueBinder modelValueBinder)
        {
            this.CommandText = command.CommandText;
            _dataSet = dataSet;
            _modelValueBinder = modelValueBinder;
            for (int i = 0; i < command.Parameters.Count; i++)
            {
                var prm = command.Parameters[i];
                _parameters.Add(prm.ParameterName, prm.Value);
            }

            ReturnValue = Convert.ToInt32(GetParameterValue(ReturnValueParameterName) ?? 0);
        }

        
        internal const string ReturnValueParameterName = "AReturnValue";
        readonly IModelValueBinder _modelValueBinder;
        readonly System.Data.DataSet _dataSet;
        readonly IDictionary<string, object> _parameters = new Dictionary<string, object>();

        private TModel[] ToModel<TModel>(System.Data.DataTable table)
            where TModel : class, new()
        {
            var models = new List<TModel>();

            IModelValueBinder binder = _modelValueBinder ?? new DefaultModelValueBinder(typeof(TModel));

            if (typeof(TModel) == typeof(System.Dynamic.ExpandoObject))
            {
                foreach (System.Data.DataRow row in table.Rows)
                {
                    var model = new System.Dynamic.ExpandoObject();

                    foreach (System.Data.DataColumn col in table.Columns)
                    {
                        var val = row[col];
                        if (val != null && val.GetType() == typeof(DBNull))
                            val = null;

                        var dictionary = (IDictionary<string, object>)model;
                        dictionary.Add(col.ColumnName, val);
                    }

                    models.Add(model as TModel);
                }
            }
            else
            {
                foreach (System.Data.DataRow row in table.Rows)
                {
                    var model = new TModel();

                    foreach (System.Data.DataColumn col in table.Columns)
                    {
                        var val = row[col];
                        if (val != null && val.GetType() == typeof(DBNull))
                            val = null;

                        binder.SetValue(model, col.ColumnName, val);
                    }

                    models.Add(model);
                }
            }

            return models.ToArray();
        }

        public string CommandText { get; private set; }

        public int ReturnValue { get; private set; }

        public object GetParameterValue(string parameterName)
        {
            object objValue = null;
            _parameters.TryGetValue(parameterName, out objValue);
            return objValue;
        }

        public object GetParameterValue(string parameterName, object defaultValue)
        {
            object objValue = null;

            if (!_parameters.TryGetValue(parameterName, out objValue))
                return null;

            return objValue ?? defaultValue;
        }

        public AppCore.Result ToActionResult(string message = "")
            => AppCore.Result.Set(success: this.ReturnValue >= 0, code: Math.Abs(this.ReturnValue), message: message);

        public AppCore.Result<T> ToActionResult<T>(string message = "")
            where T : class, new()
            => AppCore.Result<T>.Set(success: this.ReturnValue >= 0, code: Math.Abs(this.ReturnValue), data: this.GetModels<T>().FirstOrDefault(), message: message);

        public AppCore.Result<IEnumerable<T>> ToListActionResult<T>(string message = "")
            where T : class, new()
        {
            int totalCount = 0;
            if (this._dataSet.Tables != null
                && this._dataSet.Tables.Count != 0)
            {
                if (this._dataSet.Tables[0].Rows.Count > 0)
                {
                    var row = this._dataSet.Tables[0].Rows[0];
                    if (row.Table.Columns.Contains("Total"))
                        totalCount = (int)row["Total"];
                }
            }

            return AppCore.Result<IEnumerable<T>>.Set(success: this.ReturnValue >= 0, code: Math.Abs(this.ReturnValue), message: message, data: this.GetModels<T>(), totalCount: totalCount);
        }

        public bool DbSucceed()
            => this.ReturnValue >= 0;

        public IEnumerable<TModel> GetModels<TModel>(int setIndex = 0)
            where TModel : class, new()
            => (_dataSet == null || setIndex >= _dataSet.Tables.Count) ? new TModel[] { } : ToModel<TModel>(_dataSet.Tables[setIndex]);
    }
}

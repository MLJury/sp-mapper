using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using sql = System.Data.SqlClient;
using System.Data.SqlClient;

namespace Kama.DatabaseModel
{
    public abstract class Database
    {
        #region Constructors
        public Database(string connectionString)
            : this(connectionString, null)
        {
        }

        public Database(string connectionString, IModelValueBinder modelValueBinder)
        {
            _connectionString = connectionString;
            _modelValueBinder = modelValueBinder;
        }
        #endregion

        protected readonly IModelValueBinder _modelValueBinder;
        readonly string _connectionString;

        private void setSessionContext(sql.SqlConnection connection, string key, object value)
        {
            if (connection.State == System.Data.ConnectionState.Closed || string.IsNullOrWhiteSpace(key))
                return;

            using (var cmd = new sql.SqlCommand("sp_set_session_context", connection))
            {
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("key", key);
                cmd.Parameters.AddWithValue("value", value);
                cmd.Parameters.AddWithValue("read_only", true);
                cmd.ExecuteNonQuery();
            }
        }

        private void setContextInfo(sql.SqlConnection connection, IContextInfo contextInfo)
        {
            if (contextInfo == null || string.IsNullOrWhiteSpace(contextInfo.Key) || string.IsNullOrWhiteSpace(contextInfo.Value()))
                return;

            setSessionContext(connection, contextInfo.Key, contextInfo.Value());
        }

        private void clearContextInfo(sql.SqlConnection connection, IContextInfo contextInfo)
        {
            if (contextInfo == null || string.IsNullOrWhiteSpace(contextInfo.Key))
                return;

            setSessionContext(connection, contextInfo.Key, null);
        }

        protected class Parameter
        {
            public string Name { get; set; }

            public bool IsOutput { get; set; }

            public object Value { get; set; }
        };

        protected sql.SqlCommand CreateCommand(string cmdText, System.Data.CommandType cmdType = System.Data.CommandType.StoredProcedure, IEnumerable<Parameter> parameters = null)
        {
            var command = new sql.SqlCommand(cmdText);
            command.CommandType = cmdType;

            foreach (var parameter in parameters)
            {
                if (parameter.IsOutput)
                    command.Parameters.Add(new sql.SqlParameter { ParameterName = parameter.Name.TrimStart('@'), Direction = System.Data.ParameterDirection.Output, Size = int.MaxValue });
                else
                    command.Parameters.AddWithValue(parameter.Name.TrimStart('@'), parameter.Value);
            }

            command.Parameters.Add(new sql.SqlParameter { ParameterName = ResultSet.ReturnValueParameterName, Direction = System.Data.ParameterDirection.ReturnValue });

            return command;
        }

        #region Execute
        protected async Task<System.Data.DataSet> ExecuteAsync(sql.SqlCommand command)   //, IContextInfo contextInfo
        {
            var dataSet = new System.Data.DataSet();

            using (var connection = new sql.SqlConnection(_connectionString))
            {
                //connection.StateChange += new System.Data.StateChangeEventHandler((sender, e) =>
                //{
                //    if (e.CurrentState == System.Data.ConnectionState.Open)
                //        setContextInfo(connection, contextInfo);
                //});

                command.Connection = connection;
                await connection.TryOpenAsync();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    do
                    {
                        if (reader.HasRows && reader.FieldCount > 0)
                        {
                            using (var table = dataSet.Tables.Add())
                            {
                                table.Load(reader);
                            }
                        }
                        else
                            reader.Close();

                    } while (!reader.IsClosed);
                }

                //clearContextInfo(connection, contextInfo);
                connection.TryClose();
            }

            return dataSet;
        }

        protected System.Data.DataSet Execute(sql.SqlCommand command)   //, IContextInfo contextInfo
        {
            var dataSet = new System.Data.DataSet();

            using (var connection = new sql.SqlConnection(_connectionString))
            {
                //connection.StateChange += new System.Data.StateChangeEventHandler((sender, e) =>
                //{
                //    if (e.CurrentState == System.Data.ConnectionState.Open)
                //        setContextInfo(connection, contextInfo);

                //});

                command.Connection = connection;

                connection.TryOpen();

                using (var reader = command.ExecuteReader())
                {
                    do
                    {
                        if (reader.HasRows && reader.FieldCount > 0)
                        {
                            using (var table = dataSet.Tables.Add())
                            {
                                table.Load(reader);
                                dataSet.Tables.Add(table);
                            }
                        }
                        else
                            reader.Close();
                    }
                    while (!reader.IsClosed);
                }

                //clearContextInfo(connection, contextInfo);
                connection.TryClose();
            }



            return dataSet;
        }
        #endregion

        protected string ReplaceArabicWithPersianChars(string s)
             => string.IsNullOrWhiteSpace(s) ? s : s.Replace("ي", "ی").Replace("ك", "ک");

        public void BatchExcute(params sql.SqlCommand[] commands)   //IContextInfo contextInfo, 
        {
            if (commands?.Length < 1)
                return;

            using (var connection = new sql.SqlConnection(_connectionString))
            {
                //connection.StateChange += new System.Data.StateChangeEventHandler((sender, e) =>
                //{
                //    if (e.CurrentState == System.Data.ConnectionState.Open)
                //        setContextInfo(connection, contextInfo);

                //});

                connection.TryOpen();
                using (var tran = connection.BeginTransaction(System.Data.IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        foreach (var command in commands)
                        {
                            command.Connection = connection;
                            command.Transaction = tran;
                            command.ExecuteNonQuery();
                        }

                        tran.Commit();
                    }
                    catch (Exception e)
                    {
                        tran.Rollback();
                        throw e;
                    }
                    finally
                    {
                        //clearContextInfo(connection, contextInfo);
                        connection.TryClose();
                    }
                }

            }
        }

        public void BatchExcute(BulkCopyModel bulkCopyModel, params sql.SqlCommand[] commands)   //IContextInfo contextInfo, 
        {
            if (commands?.Length < 1)
                return;

            using (var connection = new sql.SqlConnection(_connectionString))
            {
                //connection.StateChange += new System.Data.StateChangeEventHandler((sender, e) =>
                //{
                //    if (e.CurrentState == System.Data.ConnectionState.Open)
                //        setContextInfo(connection, contextInfo);

                //});

                connection.TryOpen();
                using (var tran = connection.BeginTransaction(System.Data.IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        foreach (var command in commands)
                        {
                            command.Connection = connection;
                            command.Transaction = tran;
                            command.ExecuteNonQuery();
                        }

                        using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, tran))
                        {
                            bulkCopy.BatchSize = bulkCopyModel.BatchSize;    // ~ 100
                            bulkCopy.DestinationTableName = bulkCopyModel.TableName;
                            if(bulkCopyModel.ColumnMappings != null)
                                bulkCopyModel.ColumnMappings.ForEach(c => bulkCopy.ColumnMappings.Add(c));
                            var dataTable = bulkCopyModel.DataTable;
                            bulkCopy.BulkCopyTimeout = bulkCopyModel.Timeout;
                            bulkCopy.WriteToServer(dataTable);
                        }

                        tran.Commit();
                    }
                    catch (Exception e)
                    {
                        tran.Rollback();
                        throw e;
                    }
                    finally
                    {
                        //clearContextInfo(connection, contextInfo);
                        connection.TryClose();
                    }
                }

            }
        }

        public async Task BatchExcuteAsync(params sql.SqlCommand[] commands)  //IContextInfo contextInfo, 
        {
            if (commands?.Length < 1)
                return;

            using (var connection = new sql.SqlConnection(_connectionString))
            {
                //connection.StateChange += new System.Data.StateChangeEventHandler((sender, e) =>
                //{
                //    if (e.CurrentState == System.Data.ConnectionState.Open)
                //        setContextInfo(connection, contextInfo);

                //});

                await connection.TryOpenAsync();

                using (var tran = connection.BeginTransaction(System.Data.IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        foreach (var command in commands)
                        {
                            command.Connection = connection;
                            command.Transaction = tran;
                            await command.ExecuteNonQueryAsync();
                        }

                        tran.Commit();
                    }
                    catch (Exception e)
                    {
                        tran.Rollback();
                        throw e;
                    }
                    finally
                    {
                        //clearContextInfo(connection, contextInfo);
                        connection.TryClose();
                    }
                }

            }
        }

        public async Task BatchExcuteAsync(BulkCopyModel bulkCopyModel, params SqlCommand[] commands)   //IContextInfo contextInfo, 
        {
            if (commands?.Length < 1)
                return;

            using (var connection = new sql.SqlConnection(_connectionString))
            {
                //connection.StateChange += new System.Data.StateChangeEventHandler((sender, e) =>
                //{
                //    if (e.CurrentState == System.Data.ConnectionState.Open)
                //        setContextInfo(connection, contextInfo);

                //});

                connection.TryOpen();
                using (var tran = connection.BeginTransaction(System.Data.IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        foreach (var command in commands)
                        {
                            command.Connection = connection;
                            command.Transaction = tran;
                            await command.ExecuteNonQueryAsync();
                        }

                        using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, tran))
                        {
                            bulkCopy.BatchSize = bulkCopyModel.BatchSize;    // ~ 100
                            bulkCopy.DestinationTableName = bulkCopyModel.TableName;
                            if (bulkCopyModel.ColumnMappings != null)
                                bulkCopyModel.ColumnMappings.ForEach(c => bulkCopy.ColumnMappings.Add(c));
                            var dataTable = bulkCopyModel.DataTable;
                            bulkCopy.BulkCopyTimeout = bulkCopyModel.Timeout;
                            await bulkCopy.WriteToServerAsync(dataTable);
                        }

                        tran.Commit();
                    }
                    catch (Exception e)
                    {
                        tran.Rollback();
                        throw e;
                    }
                    finally
                    {
                        //clearContextInfo(connection, contextInfo);
                        connection.TryClose();
                    }
                }

            }
        }

    }
}

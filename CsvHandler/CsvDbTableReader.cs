using System.Collections.Generic;
using Foldda.Automation.Framework;
using System.Threading;
using Charian;
using System;
using System.Data.OleDb;
using System.Threading.Tasks;
using System.Linq;
using System.IO;

namespace Foldda.Automation.CsvHandler
{
    /**
     * 
     * 
     *          This DB handler requires installing System.Data.OleDb.dll (8.0) via NuGet
     *          whilst the project is targeting NetStandard 2.0
     * 
     * 
     * DbTableReader dynamically constructs a SQL select-query, and runs against a database table defined by a
     * connection string and a table-name parameter.
     * 
     * These parameters can be provided by it's Parent handler's output, or if unavailable, by the handler settings
     * 
     */
    public class CsvDbTableReader : BaseCsvDbTableHandler
    {
        public const string QUERY_WHERE_CLAUSE = "query-where-clause";
        public string QueryWhereClause { get; private set; }

        public CsvDbTableReader(ILoggingProvider logger) : base(logger) { }

        public override void SetParameters(IConfigProvider config)
        {
            try
            {
                base.SetParameters(config);
                QueryWhereClause = config.GetSettingValue(QUERY_WHERE_CLAUSE, string.Empty);
            }
            catch(Exception e)
            {
                Log(e);
                throw e;
            }
        }

        //part of the virtual CsvHandler's DataTransformationTask()
        protected override void ProcessEvent(HandlerEvent event1, DataContainer inputContainer, DataContainer outputContainer, CancellationToken cancellationToken)
        {
            try
            {
                //testing if the trigger contains 'file-read config instructions' in its context,
                if (!(event1.EventDetailsRda is DbTableConnectionConfig config))
                {
                    //if not, use the handler's local settings
                    Log($"Container has no file-download instrcution, local (FTP) config settings are used.");
                    config = LocalConfig;
                }

                TabularRecord.MetaData metaData = new TabularRecord.MetaData() { SourceId = config.DbTableName };
                if(_columnDefinitions.Count > 0)
                {
                    //if query specified columns
                    metaData.ColumnNames = _columnDefinitions.Keys.ToArray();
                }
                else
                {
                    //if query "*"
                    metaData.ColumnNames = _targetTableSchema.ToArray();
                }
                outputContainer.MetaData = metaData;

                int recordsRead = ReadFromDatabase(config, outputContainer, cancellationToken);
                Log($"Retrieved {recordsRead} records.");
            }
            catch (Exception e)
            {
                Log(e);
                throw e;
            }
        }

        /// <summary>
        /// It is recommended to utilize the pre- and post-processing stored-procs in the table reading, through ELT retrieving pattern, 
        /// 1) the pre-processing creates a temp 'staging' table, and retrieve the sourced data to this table
        /// 2) the main reading process download date from this table
        /// 3) the post-processing cleans/drops the staging temp table ready for the next read
        /// </summary>
        /// <param name="config"></param>
        /// <param name="outputContainer"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected virtual int ReadFromDatabase(DbTableConnectionConfig config, DataContainer outputContainer, CancellationToken cancellationToken)
        {
            try
            {
                using (OleDbConnection connection = new OleDbConnection(config.DbConnectionString))
                {
                    Deb($"OLE-DB connection string = [{config.DbConnectionString}]");
                    connection.Open();

                    //(optional) run pre-processing stored-proc if provided.
                    if (!string.IsNullOrEmpty(config.PreProcessingStoredProc))
                    {
                        base.RunStoredProc(connection, config.PreProcessingStoredProc, null, null, null, null);
                    }

                    using (var selectCommand = connection.CreateCommand())
                    {
                        //1. build the OleDB (SELECT) Command object based on container columns
                        string fields = "*";
                        if (_columnDefinitions.Count > 0)
                        {
                            fields = string.Join(",", _columnDefinitions.Keys);
                        }

                        selectCommand.CommandText = $"SELECT {fields} FROM {config.DbTableName} {QueryWhereClause}";
                        Log($"Executing query '{selectCommand.CommandText}'");

                        //2. run query to retrive records each record
                        using (OleDbDataReader reader = selectCommand.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var columnValues = new List<string>();

                                for (int colIndex = 0; colIndex < reader.FieldCount; colIndex++)
                                {
                                    string columnName = reader.GetName(colIndex);

                                    string columnValue;
                                    if (reader[columnName] == null || reader[columnName].GetType() == typeof(DBNull))
                                    {
                                        columnValue = string.Empty;
                                    }
                                    else if (_columnDefinitions.TryGetValue(columnName, out CsvColumnDataDefinition columnDef))
                                    {
                                        columnValue = CsvColumnDataDefinition.ApplyFormat(reader[columnName], columnDef);
                                    }
                                    else
                                    {
                                        //format as the default ToString(), can also apply formatting based on "reader.GetFieldType(col)"
                                        columnValue = reader[columnName].ToString();
                                    }

                                    columnValues.Add(columnValue);
                                }

                                //Deb($"Got row - '{string.Join(",", columnValues)}'");

                                outputContainer.Add(new TabularRecord(columnValues));
                            }
                        }

                    }

                    //(optional) run post-processing stored proc
                    if (!string.IsNullOrEmpty(config.PostProcessingStoredProc))
                    {
                        Log($"Executing post-processing storec-proc '{config.PostProcessingStoredProc}'");
                        base.RunStoredProc(connection, config.PostProcessingStoredProc, null, null, null, null);
                    }
                }

            }
            catch (Exception e)
            {
                Log($"Reading from database has encountered an exception -'{e.Message}'\nDetails:\n{e}");
            }

            return outputContainer.Records.Count;
        }
    }
}
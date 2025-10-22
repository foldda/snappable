﻿using System.Collections.Generic;
using Foldda.Automation.Framework;
using System.Threading;
using System;
using System.Threading.Tasks;
using Charian;
using System.Data;
using System.Data.OleDb;
using System.Linq;
using System.Globalization;
using System.IO;

namespace Foldda.Automation.CsvHandler
{
    //Write to MS SQL Server using OleDb Driver
    public class CsvDbTableWriter : BaseCsvDbTableHandler
    {

        /**
         * CsvDbTableWriter writes Cvs tabular data (based on the defined Csv-column-index to Db-table-column-name
         * mapping defined in the config file) to a database table.
         */

        public CsvDbTableWriter(ISnappableManager manager) : base(manager) { }

        /**
         * Defines each of the Csv columns and their corresponding database table column's name and data-type, in the
         * following specific format - 
         * 
         * <1-based Csv column index> | <db-table column's name>;<data-type>;<data-type parsing format>
         * 
         * eg "1|USER_AGE;int"
         * 
         * if unspecified, the default data type is varchar(max)
         * 
         * <Parameter>
         *  <Name>column-spec</Name>
         *  <Value>1|USER_AGE;int</Value>
         * </Parameter>
         * 
         * <Parameter>
         *  <Name>column-spec</Name>
         *  <Value>2|USER_HEIGHT;decimal</Value>
         * </Parameter>
         * <Parameter>
         *  <Name>column-spec</Name>
         *  <Value>3|ADDRESS;string;120</Value>
         * </Parameter>
         * 
         * <Parameter>
         *  <Name>column-spec</Name>
         *  <Value>4|USER_DOB;date-time;d/MM/yyyy H:mm</Value>
         * </Parameter>
         */

        /// <summary>
        /// Driven by the Handler-manager, this method processes a record inputContainer - passed in by the handler manager.
        /// Note this handler would deposite its output, if any, to a designated storage from the manager
        /// </summary>
        /// <param name="inputContainer">a inputContainer with a collection of records</param>
        /// <returns>a status integer</returns>
        public override Task<int> ProcessPipelineRecordContainer(RecordContainer inputContainer, CancellationToken cancellationToken)
        {
            if (inputContainer.Records.Count > 0)
            {

                int recordsWritten = 0;
                try
                {
                    if (inputContainer.MetaData is TabularRecord.MetaData metaData)
                    {
                        VerifyDBConfig(LocalConfig);
                        recordsWritten += this.WriteToDatabase(LocalConfig /**using local DB config**/, inputContainer.Records, metaData);
                    }
                }
                catch (Exception e)
                {
                    Log($"ERROR: Write inputContainer '{inputContainer.MetaData.ToRda().ScalarValue}' to database table [{LocalConfig.DbTableName}] failed with exception: {e.Message}");
                    Deb(e.StackTrace);
                }

                Log($"Total {recordsWritten} records processed.");
            }

            return Task.FromResult(0);    //output status
        }

        /// <summary>
        /// It is recommended to utilize the pre- and post-processing stored-procs in the table writing, through ELT loading pattern, 
        /// 1) the pre-processing creates a temp 'staging' table
        /// 2) the main writing process populates this table
        /// 3) the post-processing transforms and transfers the loaded data from the staging temp table to the target table
        /// </summary>
        /// <param name="config"></param>
        /// <param name="container"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected virtual int WriteToDatabase(DbTableConnectionConfig config, List<IRda> tabularRecords, TabularRecord.MetaData metaData)
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

                using (var insertCommand = connection.CreateCommand())
                {
                    //1. build the OleDB (INSERT) Command object based on inputContainer columns
                    System.Text.StringBuilder fields = new System.Text.StringBuilder();
                    System.Text.StringBuilder questionMarks = new System.Text.StringBuilder();

                    foreach (var column in _columnDefinitions.Values)
                    {
                        //construct column-definition for this column
                        //store the definition in the command's parameters
                        insertCommand.Parameters.Add(parameterName: $"@{column.Name}", oleDbType: column.DbType);
                        Log($"Added INSERT command parameter: column '{column.Name}', type {column.DbType}");

                        //construct the insert Sql query string
                        fields.Append(",").Append(column.Name);
                        questionMarks.Append(",?");
                    }

                    insertCommand.CommandText = $"INSERT INTO {config.DbTableName} ({fields.Remove(0, 1)}) VALUES ({questionMarks.Remove(0, 1)})";

                    //2. now use the prepared query to insert each record
                    int rowIndex = 0;
                    foreach (var line in tabularRecords)
                    {
                        TabularRecord row = line as TabularRecord;  //casting

                        foreach (var columnMappingDef in _columnDefinitions.Values)
                        {
                            //now we have the value, we then assign it to the target, with the correct data-type
                            int colIndex = columnMappingDef.CsvColumnIndex - 1; //convert to 0-based index
                            if(colIndex < 0 || colIndex >= row.ItemValues.Count )
                            {
                                //string is not empty, but parsing failed
                                string rowValues = string.Join("][", row.ItemValues);
                                throw new Exception($"Invalid column-index {colIndex} for data row [{rowValues}]");
                            }
                            //else ...
                            string columnValueInCsv = row.ItemValues[colIndex];  
                            object typedValue = columnMappingDef.ParseValueToColumnTypedObject(columnValueInCsv);
                            if (typedValue == DBNull.Value && !(string.IsNullOrEmpty(columnValueInCsv.Trim())))
                            {
                                //string is not empty, but parsing failed
                                throw new Exception($"Invalid data found in column '{ columnMappingDef.Name }', line #{rowIndex + 1} => [{ columnValueInCsv }]");
                            }
                            else
                            {
                                insertCommand.Parameters[$"@{columnMappingDef.Name}"].Value = typedValue;
                            }
                        }

                        //insert the line of records
                        insertCommand.ExecuteNonQuery();

                        rowIndex++;
                    }
                }

                //(optional) run post-processing stored-proc if provided.
                if (!string.IsNullOrEmpty(config.PostProcessingStoredProc))
                {
                    base.RunStoredProc(connection, config.PostProcessingStoredProc, null, null, null, null);
                }

            }

            return tabularRecords.Count;
        }
    }
}
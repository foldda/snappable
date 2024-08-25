using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Foldda.Automation.Framework;
using System.Text;
using System.Linq;
using Charian;
using System.Threading;

namespace Foldda.Automation.CsvHandler
{
    public abstract class BaseCsvHandler : BasicDataHandler
    {
        const string CSV_COLUMN_DELIMITER = "csv-delimiter";
        const string CSV_COLUMN_QUALIFIER = "csv-qualifier";
        const string CSV_FIRST_LINE_IS_HEADER = "csv-first-line-is-header";
        const string CSV_FIXED_COLUMNS_LENGTHS = "csv-fixed-columns-lengths";

        protected bool FirstLineIsHeader { get; private set; }

        internal TabularRecord.TabularRecordEncoding RecordEncoding { get; set; } = TabularRecord.DEFAULT_RECORD_ENCODING;


        public BaseCsvHandler(ILoggingProvider logger) : base(logger) { }

        public override AbstractCharStreamRecordScanner GetDefaultFileRecordScanner(ILoggingProvider loggingProvider)
        {
            return new TabularRecord.TabularRecordStreamScanner(loggingProvider, RecordEncoding);
        }

        public override void SetParameter(IConfigProvider config)
        {
            string columnFixedLengths = config.GetSettingValue(CSV_FIXED_COLUMNS_LENGTHS, null);
            if(!string.IsNullOrEmpty(columnFixedLengths))
            {
                string[] tokens = columnFixedLengths.Split(new char[] { ',', ';', '-' });
                int[] lengths = new int[tokens.Length];
                for(int i = 0; i < tokens.Length; i++)
                {
                    if(Int32.TryParse(tokens[i], out int clen) && clen >= 0)
                    {
                        lengths[i] = clen;
                    }
                    else
                    {
                        throw new Exception($"Parameter '{CSV_FIXED_COLUMNS_LENGTHS}' value '{columnFixedLengths}' is invalid. It should be a list of comma-separated integers");
                    }
                }

                RecordEncoding = new TabularRecord.TabularRecordEncoding(lengths);
            }
            else
            {
                char csvColumnDelimiter = config.GetSettingValue(CSV_COLUMN_DELIMITER, TabularRecord.TabularRecordEncoding.COMMA);
                char csvColumnQualifier = config.GetSettingValue(CSV_COLUMN_QUALIFIER, TabularRecord.TabularRecordEncoding.DOUBLE_QUOTE);    //default qualifier is double-quote

                RecordEncoding = new TabularRecord.TabularRecordEncoding(csvColumnDelimiter, csvColumnQualifier, csvColumnQualifier);
            }

            FirstLineIsHeader = config.GetSettingValue(CSV_FIRST_LINE_IS_HEADER, YES_STRING, true);
        }

        /// <summary>
        /// Determine what type the record is, then process accordingly.
        /// </summary>
        /// <param name="record"></param>
        /// <param name="inputContainer"></param>
        /// <param name="outputContainer"></param>
        /// <param name="cancellationToken"></param>
        protected sealed override Task ProcessRecord(IRda record, RecordContainer inputContainer, RecordContainer outputContainer, CancellationToken cancellationToken)
        {

            try
            {
                if (record is TabularRecord csvRecord)
                {
                    ProcessTabularRecord(csvRecord, inputContainer, outputContainer, cancellationToken);
                }
                else
                {
                    Log($"WARNING: Record type UNKNOWN and is ignored by this CSV data handler. Record => {record?.ToRda()}");
                }

            }
            catch (Exception e)
            {
                Logger.Log($"Failed converting input record to TabularRecord, record is skipped - {e.Message}.\n{e.StackTrace}");
            }

            return Task.Delay(50);
        }

        protected virtual void ProcessTabularRecord(TabularRecord record, RecordContainer inputContainer, RecordContainer outputContainer, CancellationToken cancellationToken)
        {
            //default is a pass-through
            outputContainer.Add(record);

            //force sub-class to implement
            //throw new NotImplementedException();
        }

    }
}




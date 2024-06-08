using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Foldda.DataAutomation.Framework;
using System.Text;
using System.Linq;
using Charian;
using System.Threading;

namespace Foldda.DataAutomation.CsvHandler
{
    public abstract class BaseCsvHandler : AbstractDataHandler
    {
        const string CSV_COLUMN_DELIMITER = "csv-delimiter";
        const string CSV_COLUMN_QUALIFIER = "csv-qualifier";
        const string CSV_FIRST_LINE_IS_HEADER = "csv-first-line-is-header";
        const string CSV_FIXED_COLUMNS_LENGTHS = "csv-fixed-columns-lengths";

        protected bool FirstLineIsHeader { get; private set; }

        internal TabularRecord.TabularRecordEncoding RecordEncoding { get; set; } = TabularRecord.DEFAULT_RECORD_ENCODING;


        public BaseCsvHandler(ILoggingProvider logger, DirectoryInfo homePath) : base(logger, homePath) { }

        public override AbstractCharStreamRecordScanner GetDefaultFileRecordScanner(ILoggingProvider loggingProvider)
        {
            return new TabularRecord.TabularRecordStreamScanner(loggingProvider, RecordEncoding);
        }

        public override void SetParameters(IConfigProvider config)
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
                        throw new Exception($"Parameter '{CSV_FIXED_COLUMNS_LENGTHS}' value '{columnFixedLengths}' is invalid. It should be a comma-separated list of integers");
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

        protected virtual void ProcessTabularRecord(TabularRecord record, Rda processingContext, DataContainer outputContainer, CancellationToken cancellationToken)
        {
            //default is a pass-through
            outputContainer.Add(record.ToRda());
        }

        public sealed override void ProcessRecord(Rda record, Rda processingContext, DataContainer outputContainer, CancellationToken cancellationToken)
        {
            try
            {
                ProcessTabularRecord(new TabularRecord(record), processingContext, outputContainer, cancellationToken);
            }
            catch (Exception e)
            {
                Logger.Log($"Failed converting input record to TabularRecord, record is skipped - {e.Message}.\n{e.StackTrace}");
            }
        }
    }
}




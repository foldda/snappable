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
    public abstract class BaseCsvHandler : BasicSnappable
    {
        const string CSV_COLUMN_DELIMITER = "csv-delimiter";
        const string CSV_COLUMN_QUALIFIER = "csv-qualifier";
        const string CSV_FIRST_LINE_IS_HEADER = "csv-first-line-is-header";
        const string CSV_FIXED_COLUMNS_LENGTHS = "csv-fixed-columns-lengths";

        protected bool FirstLineIsHeader { get; private set; }

        internal TabularRecord.TabularRecordEncoding CsvRecordEncoding { get; set; } = TabularRecord.DEFAULT_RECORD_ENCODING;


        public BaseCsvHandler(ISnappableManager manager) : base(manager) { }

        protected override AbstractCharStreamRecordScanner GetDefaultFileRecordScanner(ILoggingProvider loggingProvider)
        {
            return new TabularRecord.TabularRecordStreamScanner(loggingProvider, CsvRecordEncoding);
        }

        public override void Setup(IConfigProvider config)
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

                CsvRecordEncoding = new TabularRecord.TabularRecordEncoding(lengths);
            }
            else
            {
                char csvColumnDelimiter = config.GetSettingValue(CSV_COLUMN_DELIMITER, TabularRecord.TabularRecordEncoding.COMMA);
                char csvColumnQualifier = config.GetSettingValue(CSV_COLUMN_QUALIFIER, TabularRecord.TabularRecordEncoding.DOUBLE_QUOTE);    //default qualifier is double-quote

                CsvRecordEncoding = new TabularRecord.TabularRecordEncoding(csvColumnDelimiter, csvColumnQualifier, csvColumnQualifier);
            }

            FirstLineIsHeader = config.GetSettingValue(CSV_FIRST_LINE_IS_HEADER, YES_STRING, true);
        }

        /// <summary>
        /// Driven by the Handler-manager, this method processes a record inputContainer - passed in by the handler manager.
        /// Note this handler would deposite its output, if any, to a designated storage from the manager
        /// </summary>
        /// <param name="inputContainer">a inputContainer with a collection of records</param>
        /// <returns>a status integer</returns>
        public override async Task<int> ProcessPipelineRecordContainer(RecordContainer inputContainer, CancellationToken cancellationToken)
        {
            RecordContainer outputContainer = new RecordContainer()
            {
                MetaData = new RecordContainer.DefaultMetaData(this.UID, DateTime.UtcNow)
                {
                    OriginalMetaData = inputContainer.MetaData //keep the soure container's meta-data
                }
            };

            Manager.PipelineOutputDataStorage.Receive(inputContainer);    //default is pass-thru

            ///alternatively processing each record indivisually ... something like

            foreach (var record in inputContainer.Records)
            {
                if (record is TabularRecord csvRecord)
                {
                    await ProcessTabularRecord(csvRecord, outputContainer, CancellationToken.None);
                }
                else
                {
                    Log($"WARNING: Record of type {record.GetType().FullName} is ignored. Record => {record?.ToRda()}");
                }
            }

            Manager.PipelineOutputDataStorage.Receive(outputContainer);

            return 0;
        }

        protected virtual Task ProcessTabularRecord(TabularRecord record, RecordContainer outputContainer, CancellationToken cancellationToken)
        {
            //default is a pass-through
            outputContainer.Add(record);
            return Task.CompletedTask;

            //force sub-class to implement
            //throw new NotImplementedException();
        }

    }
}




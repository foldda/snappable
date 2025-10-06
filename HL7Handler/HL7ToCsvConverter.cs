using System.Collections.Generic;
using Foldda.Automation.Framework;
using System.Threading;
using Foldda.Automation.CsvHandler;
using System.IO;
using System.Threading.Tasks;

namespace Foldda.Automation.HL7Handler
{
    /**
     * HL7ToCsvConverter selects elements of a HL7 message to form rows of CSV data lines
     * The parameters of HL7ToCsvConverter handler specifies which HL7 message data elements 
     * are to be selected for as columns of a CSV row. 
     * 
     * If for a given HL7 message the combination of the selection criteria results
     * non-unique set of data elements, the conbination of selected elements' values are 
     * returned, meaning multiple CSV rows can be resulted from a single HL7 message. 
     * 
     */
    public class HL7ToCsvConverter : BaseHL7Handler
    {
        public const string CSV_COLUMN_HL7_ELEMENTS = "csv-column-hl7-elements";
        HL7Filter.SelectionPathDefilition DataElementSelectionPathDefinition { get; set; }

        public HL7ToCsvConverter(IHandlerManager manager) : base(manager) { }

        public override void Setup(IConfigProvider config)
        {
            /*
             * implied group hierarchy and option for filtering

                <Parameter>
                  <Name>csv-column-hl7-elements</Name>
                  <Value>MSH-10~PID-11.7==MAILING~PID-11.5~PID-9~ORC-1~OBR-1~OBX-11
                </Parameter>

            columns are defined (separated) by ~, and optionally each defined column can have filtering (eg PID-8==M, for male patient only)
            note also the use of filtering to eliminate duplicated output (ORC-1==1~OBR-1==1~OBX-1==1 in the example below )

            'MSH-10~PID-8==M~PID-5.1~PID-9~ORC-1==1~OBR-1==1~OBX-1==1~OBX-3~OBX-11'

             */
            string elementsSelectingRules = config.GetSettingValue(CSV_COLUMN_HL7_ELEMENTS, string.Empty);

            DataElementSelectionPathDefinition = new HL7Filter.SelectionPathDefilition(elementsSelectingRules);

        }

        //protected override Task ProcessInputHL7MessageRecord(HL7Message hl7, RecordContainer inputContainer, RecordContainer outputContainer, CancellationToken cancellationToken)
        //{
        //}


        protected override Task ProcessInputHL7MessageRecord(HL7Message hl7, RecordContainer outputContainer, CancellationToken cancellationToken)
        {
            List<List<string>> csvBlock = new List<List<string>>();
            foreach (var path in DataElementSelectionPathDefinition.GetQualifiedPaths(hl7.Segments))
            {
                csvBlock.AddRange(path.GetValuesCsv());
            }

            //add csv to the output container
            foreach (List<string> row in csvBlock)
            {
                TabularRecord tabularRecord = new TabularRecord(row);
                outputContainer.Add(tabularRecord);
            }

            return Task.Delay(50);
        }

    }
}
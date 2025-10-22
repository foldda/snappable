using Charian;
using Foldda.Automation.Framework;
using Foldda.Automation.EventHandler;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Foldda.Custom.Handler
{
    /// <summary>
    /// Class LookupToEventAdaptor illustrates how to convert incompatible data output from a handler to expected data input of another hander .
    /// 
    /// In this case, it converts (selects) the data elements from w SimpleWebServer's Lookup data array and creates a Event data with expected contents that drives
    /// a OsCommander handler.
    /// 
    /// </summary>
    public class LookupToCommandInputAdaptor : BasicDataHandler
    {
        public LookupToCommandInputAdaptor(ILoggingProvider logger) : base(logger) { }

        //configurable parameters re what value in lookup would be mapped to Cmd_exe, and cmd_arg etc
        const string LOOKUP_CMD_EXE = "cmd-exe";
        const string LOOKUP_CMD_ARG = "cmd-arg";

        //we'll be looking for values mapped from these keys in the Lookup data structure
        string CommandExe { get; set; }
        string CommandArg { get; set; }

        public override void SetParameter(IConfigProvider config)
        {
            CommandExe = config.GetSettingValue(LOOKUP_CMD_EXE, string.Empty);
            CommandArg = config.GetSettingValue(LOOKUP_CMD_ARG, string.Empty);
        }

        /// <summary>
        /// Check if the record is expected. Each handler can expect one or more types of records to handle.
        /// </summary>
        /// <param name="record"></param>
        /// <param name="outputContainer">This is where to deposite the produced (output) record if applicable.</param>
        /// <param name="cancellationToken"></param>
        protected override Task ProcessRecord(IRda record, RecordContainer inputContainer, RecordContainer outputContainer, CancellationToken cancellationToken)
        {
            // if the record is a Lookup object, we will use it to construct a command-input object
            // which is what OsCommander handler expecting
            if(record is DictionaryRda lookUp)
            {
                var result = new OsCommander.InputRecord()
                {
                    CMD_EXECUTABLE = lookUp.GetString(CommandExe),
                    ARGUMENTS = lookUp.GetString(CommandArg),
                };

                if (!string.IsNullOrEmpty(result.CMD_EXECUTABLE))
                {
                    outputContainer.Add(result);
                }
                else
                {
                    Log($"WARNING: expected input values, for keys '{CommandExe}' and '{CommandArg}', not found in Lookup.");
                }
            }

            return Task.Delay(50);
        }
    }
}

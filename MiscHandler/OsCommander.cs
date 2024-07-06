using System.Diagnostics;
using Foldda.Automation.Framework;
using System.Threading;
using System.Management.Automation;
using Charian;
using System.IO;
using System;
using System.Collections.Generic;

namespace Foldda.Automation.MiscHandler
{
    /**
     * Run the provided script thru the operating system
     * 
     */

    public class OsCommander : AbstractDataHandler
    {
        public class InputRecord : Rda
        {
            //these constants are used by getting config settings to construct a FtpDownloaderInput record
            public const string PARAM_CMD_EXECUTABLE = "cmd-executable";
            public const string PARAM_ARGUMENTS = "arguments";
            //addition arguments that can be optionally provided
            public const string PARAM_ARGUMENTS_2 = "arguments_2";
            public const string PARAM_ARGUMENTS_3 = "arguments_3";

            public InputRecord(Rda originalRda) : base(originalRda)
            {
            }

            public InputRecord() : base()
            {
            }

            public enum RDA_INDEX : int { CMD_EXECUTABLE, ARGUMENTS, ARGUMENTS_2, ARGUMENTS_3 }

            public string CMD_EXECUTABLE 
            {
                get => this[(int)RDA_INDEX.CMD_EXECUTABLE].ScalarValue;
                set => this[(int)RDA_INDEX.CMD_EXECUTABLE].ScalarValue = value;
            }

            public string ARGUMENTS
            {
                get => this[(int)RDA_INDEX.ARGUMENTS].ScalarValue;
                set => this[(int)RDA_INDEX.ARGUMENTS].ScalarValue = value;
            }
            //addition arguments that can be optionally provided
            public string ARGUMENTS_2
            {
                get => this[(int)RDA_INDEX.ARGUMENTS_2].ScalarValue;
                set => this[(int)RDA_INDEX.ARGUMENTS_2].ScalarValue = value;
            }
            public string ARGUMENTS_3
            {
                get => this[(int)RDA_INDEX.ARGUMENTS_3].ScalarValue;
                set => this[(int)RDA_INDEX.ARGUMENTS_3].ScalarValue = value;
            }
        }

        //this data record tells the downstream handler (eg a Csv-Reader where to pick up the resulted data)
        public class OutputRecord : HandlerEvent
        {
            public OutputRecord(string sourceId, DateTime time) : base(sourceId, time)
            {
                //
            }

            public string ExecutionOutput { get; set; }
        }

        public OsCommander(ILoggingProvider logger, DirectoryInfo homePath) : base(logger, homePath)
        {
        }

        public override void ProcessRecord(IRda eventTriggerRecord, DataContainer inputContainer, DataContainer outputContainer, CancellationToken cancellationToken)
        {
            try
            {
                Log($"Downloading triggered by {eventTriggerRecord}");

                //InputRecord commandConfig;
                //testing if the trigger contains 'download instructions' in its context,
                if(!(eventTriggerRecord is HandlerEvent evn) || !(evn.EventDetailsRda is InputRecord commandConfig) || string.IsNullOrEmpty(commandConfig.CMD_EXECUTABLE))
                {
                    Log($"Trigger event has no command-exectution instrcution in input record, local config settings are used.");
                    commandConfig = LocalConfig;
                }

                List<OutputRecord> recordsRead = RunCommand(commandConfig, cancellationToken);
            }
            catch (Exception e)
            {
                Log(e);
                throw e;
            }

        }

        private List<OutputRecord> RunCommand(InputRecord commandConfig, CancellationToken cancellationToken)
        {

            Process cmd = new Process();
            cmd.StartInfo.FileName = commandConfig.CMD_EXECUTABLE;
            cmd.StartInfo.Arguments = $"{commandConfig.ARGUMENTS} {commandConfig.ARGUMENTS_2} {commandConfig.ARGUMENTS_3}".Trim();
            Log($"Exec= '{cmd.StartInfo.FileName} {cmd.StartInfo.Arguments}'");

            cmd.StartInfo.RedirectStandardOutput = true;
            cmd.StartInfo.CreateNoWindow = true;
            cmd.StartInfo.UseShellExecute = false;
            //cmd.StartInfo.WorkingDirectory = @"C:\Windows\System32";

            cmd.Start();
            string output = cmd.StandardOutput.ReadToEnd();

            cmd.WaitForExit();

            Log(output);
            return new List<OutputRecord>()
            {
                new OutputRecord(this.GetType().Name, DateTime.Now) { ExecutionOutput = output}
            };
        }

        internal InputRecord LocalConfig { get; private set; }

        public override void SetParameters(IConfigProvider config)
        {
            LocalConfig = new InputRecord()
            {
                CMD_EXECUTABLE = config.GetSettingValue(InputRecord.PARAM_CMD_EXECUTABLE, string.Empty).Trim(),
                ARGUMENTS = config.GetSettingValue(InputRecord.PARAM_ARGUMENTS, string.Empty).Trim(),
                ARGUMENTS_2 = config.GetSettingValue(InputRecord.PARAM_ARGUMENTS_2, string.Empty).Trim(),
                ARGUMENTS_3 = config.GetSettingValue(InputRecord.PARAM_ARGUMENTS_3, string.Empty).Trim()
            };
        }
    }
}
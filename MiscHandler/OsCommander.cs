using System.Diagnostics;
using Foldda.DataAutomation.Framework;
using System.Threading;
using System.Management.Automation;
using Charian;
using System.IO;
using System;
using System.Collections.Generic;

namespace Foldda.DataAutomation.MiscHandler
{
    #region GetProcCommand

    /// <summary>
    /// Class that implements the GetProcCommand.
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "Proc")]
    public class GetProcCommand : Cmdlet
    {
        #region Cmdlet Overrides

        /// <summary>
        /// For each of the requested process names, retrieve and write
        /// the associated processes.
        /// </summary>
        protected override void ProcessRecord()
        {
            // Get the current processes.
            Process[] processes = Process.GetProcesses();

            // Write the processes to the pipeline making them available
            // to the next cmdlet. The second argument (true) tells the
            // system to enumerate the array, and send one process object
            // at a time to the pipeline.
            WriteObject(processes, true);
        }

        #endregion Overrides
    } // End GetProcCommand class.
    #endregion GetProcCommand

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

            public InputRecord()
            {
            }

            public enum RDA_INDEX : int { CMD_EXECUTABLE, ARGUMENTS, ARGUMENTS_2, ARGUMENTS_3 }

            public string CMD_EXECUTABLE
            {
                get => this[(int)RDA_INDEX.CMD_EXECUTABLE].ScalarValue;
                set => this[(int)RDA_INDEX.CMD_EXECUTABLE].ScalarValue = value.ToString();
            }
            public string ARGUMENTS
            {
                get => this[(int)RDA_INDEX.ARGUMENTS].ScalarValue;
                set => this[(int)RDA_INDEX.ARGUMENTS].ScalarValue = value.ToString();
            }

            //addition arguments that can be optionally provided
            public string ARGUMENTS_2
            {
                get => this[(int)RDA_INDEX.ARGUMENTS_2].ScalarValue;
                set => this[(int)RDA_INDEX.ARGUMENTS_2].ScalarValue = value.ToString();
            }
            public string ARGUMENTS_3
            {
                get => this[(int)RDA_INDEX.ARGUMENTS_3].ScalarValue;
                set => this[(int)RDA_INDEX.ARGUMENTS_3].ScalarValue = value.ToString();
            }
        }

        //this data record tells the downstream handler (eg a Csv-Reader where to pick up the resulted data)
        public class OutputRecord : HandlerEvent
        {
            public OutputRecord(string sourceId, DateTime time) : base(sourceId, time)
            {
                //
            }

            public string ExecutionOutput   //screen or log output, eg stdout
            {
                get => EventContextRda.ScalarValue;
                set => EventContextRda.ScalarValue = value;
            }
        }

        protected OsCommander(ILoggingProvider logger, DirectoryInfo homePath) : base(logger, homePath)
        {
        }

        public override void ProcessRecord(Rda eventTriggerRecord, Rda processingContext, DataContainer outputContainer, CancellationToken cancellationToken)
        {
            try
            {
                Log($"Downloading triggered by {eventTriggerRecord}");

                InputRecord commandConfig;
                //testing if the trigger contains 'download instructions' in its context,
                try
                {
                    HandlerEvent tigger = new HandlerEvent(eventTriggerRecord);
                    commandConfig = new InputRecord(tigger.EventContextRda);
                    if(string.IsNullOrEmpty(commandConfig.CMD_EXECUTABLE))
                    {
                        throw new Exception($"Parameter {InputRecord.PARAM_CMD_EXECUTABLE} is not provoded in input-record.");
                    }
                }
                catch(Exception e)
                {
                    //if not, use the handler's local settings
                    Log($"Trigger event has no command-exectution instrcution in input record - '{e.Message}', local config settings are used.");
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
            cmd.StartInfo.RedirectStandardInput = true;
            cmd.StartInfo.RedirectStandardOutput = true;
            cmd.StartInfo.CreateNoWindow = true;
            cmd.StartInfo.UseShellExecute = false;
            cmd.Start();

            cmd.StandardInput.WriteLine($"echo '{cmd.StartInfo.FileName} {cmd.StartInfo.Arguments}'");
            cmd.StandardInput.Flush();
            cmd.StandardInput.Close();
            cmd.WaitForExit();

            Log(cmd.StandardOutput.ReadToEnd());
            return new List<OutputRecord>()
            {
                new OutputRecord(this.GetType().Name, DateTime.Now) { ExecutionOutput = cmd.StandardOutput.ReadToEnd()}
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
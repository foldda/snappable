using System.Diagnostics;
using Foldda.Automation.Framework;
using System.Threading;
using System.Management.Automation;
using Charian;
using System.IO;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Foldda.Automation.EventHandler
{
    /**
     * Run the provided script thru the operating system
     * 
     */

    public class OsCommander : BasicDataHandler
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

        public OsCommander(ILoggingProvider logger) : base(logger)
        {
        }

        protected override Task ProcessRecord(IRda containerRecord, RecordContainer inputContainer, RecordContainer outputContainer, CancellationToken cancellationToken)
        {
            
            Log($"Handler processing triggered by {containerRecord}");

            //InputRecord commandConfig;
            //testing if the trigger contains 'download instructions' in its context,
            if(!(containerRecord is HandlerEvent evn) || !(evn.EventDetailsRda is InputRecord commandConfig) || string.IsNullOrEmpty(commandConfig.CMD_EXECUTABLE))
            {
                Log($"Trigger event has no command-exectution instrcution in input record, local config settings are used.");
                commandConfig = LocalConfig;
            }

            return RunCommand(commandConfig);
        }

        protected override Task ProcessHandlerEvent(HandlerEvent evn, CancellationToken cancellationToken)
        {
            Log($"Handler processing triggered by {evn}");

            //InputRecord commandConfig;
            //testing if the trigger contains 'download instructions' in its context,
            if(!(evn.EventDetailsRda is InputRecord commandConfig) || string.IsNullOrEmpty(commandConfig.CMD_EXECUTABLE))
            {
                Log($"Trigger event has no command-exectution instrcution in input record, local config settings are used.");
                commandConfig = LocalConfig;
            }

            return RunCommand(commandConfig);            //force sub-class to implement
        }

        private async Task RunCommand(InputRecord commandConfig)
        {
            await Task.Run(async () =>
            {
                try
                {
                    using (Process process = new Process())
                    {
                        process.StartInfo.FileName = commandConfig.CMD_EXECUTABLE;
                        process.StartInfo.Arguments = $"{commandConfig.ARGUMENTS} {commandConfig.ARGUMENTS_2} {commandConfig.ARGUMENTS_3}".Trim();
                        string fullCommand = $"{process.StartInfo.FileName} {process.StartInfo.Arguments}";

                        process.StartInfo.CreateNoWindow = true;
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.RedirectStandardOutput = true;
                        process.StartInfo.RedirectStandardError = true;

                        //an ill-formed command can cause handler's process hanging (when waiting for output),
                        //so below solution adds monitoring of the process with a timeout.
                        //https://stackoverflow.com/questions/139593/processstartinfo-hanging-on-waitforexit-why
                        StringBuilder output = new StringBuilder();
                        StringBuilder error = new StringBuilder();

                        using (AutoResetEvent outputWaitHandle = new AutoResetEvent(false))
                        using (AutoResetEvent errorWaitHandle = new AutoResetEvent(false))
                        {
                            process.OutputDataReceived += (sender, e) =>
                            {
                                if (e.Data == null)
                                {
                                    outputWaitHandle.Set();
                                }
                                else
                                {
                                    output.AppendLine(e.Data);
                                }
                            };
                            process.ErrorDataReceived += (sender, e) =>
                            {
                                if (e.Data == null)
                                {
                                    errorWaitHandle.Set();
                                }
                                else
                                {
                                    error.AppendLine(e.Data);
                                }
                            };

                            process.Start();

                            process.BeginOutputReadLine();
                            process.BeginErrorReadLine();

                            //WaitForExit WITH TIMEOUT
                            int TIMEOUT = 1000; //1 sec
                            if (process.WaitForExit(TIMEOUT) &&
                                outputWaitHandle.WaitOne(TIMEOUT) &&
                                errorWaitHandle.WaitOne(TIMEOUT))
                            {
                                // Process completed. Check process.ExitCode here.
                                if(process.ExitCode == 0)
                                {
                                    Log($"INFO: Executing command '{fullCommand}' successed. Output = {output}");
                                    OutputStorage.Receive(new OutputRecord(this.GetType().Name, DateTime.Now) { ExecutionOutput = output.ToString() });
                                }
                                else
                                {
                                    Log($"ERROR: Executing command '{fullCommand}' had an error. Output = {output}, Error= {error}");
                                }
                            }
                            else
                            {
                                // Timed out.
                                Log($"ERROR: Executing command '{fullCommand}' timed out and had no output. Please ensure the command's syntax is correct.");
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Log(e);
                    throw e;
                }
            });








        }

        internal InputRecord LocalConfig { get; private set; }

        public override void SetParameter(IConfigProvider config)
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
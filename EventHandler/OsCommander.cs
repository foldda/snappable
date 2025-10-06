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
using static Foldda.Automation.EventHandler.EmailSender;

namespace Foldda.Automation.EventHandler
{
    /**
     * Run the provided script thru the operating system
     * 
     */

    public class OsCommander : BasicDataHandler
    {
        public class CommandInputParameters : Rda
        {
            //these constants are used by getting config settings to construct a FtpDownloaderInput record
            public const string PARAM_CMD_EXECUTABLE = "cmd-executable";
            public const string PARAM_ARGUMENTS = "arguments";
            //addition arguments that can be optionally provided
            public const string PARAM_ARGUMENTS_2 = "arguments_2";
            public const string PARAM_ARGUMENTS_3 = "arguments_3";
            public const string PARAM_TIMEOUT_SEC = "timeout-sec";

            public CommandInputParameters(Rda originalRda) : base(originalRda)
            {
            }

            public CommandInputParameters() : base()
            {
            }

            public enum RDA_INDEX : int { CMD_EXECUTABLE, ARGUMENTS, ARGUMENTS_2, ARGUMENTS_3, TIMEOUT_SEC }

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

            public int TIMEOUT_SEC
            {
                get => int.TryParse(this[(int)RDA_INDEX.TIMEOUT_SEC].ScalarValue, out int timeoutSec) ? timeoutSec : DEFAULT_TIMEOUT;
                set => this[(int)RDA_INDEX.TIMEOUT_SEC].ScalarValue = value.ToString();
            }
        }

        //this data record tells the downstream handler (eg a Csv-Reader where to pick up the resulted data)
        public class CommandOutputRecord : MessageRda.HandlerEvent
        {
            public CommandOutputRecord(string sourceId, DateTime time, Rda output) : base(sourceId, time, output)
            {
                //
                ExecutionOutput = output.ScalarValue;
            }

            public string ExecutionOutput { get; }
        }

        public OsCommander(IHandlerManager manager) : base(manager)
        {
        }

        /// <summary>
        /// Process a record inputContainer - passed in by the handler manager.
        /// Note this handler would deposite its output, if any, to a designated storage from the manager
        /// </summary>
        /// <param name="inputContainer">a inputContainer with a collection of records</param>
        /// <returns>a status integer</returns>
        public override async Task<int> ProcessPipelineRecordContainer(RecordContainer inputContainer, CancellationToken cancellationToken)
        {

            ///alternatively processing each record indivisually ... something like

            foreach (var record in inputContainer.Records)
            {
                if (record is CommandInputParameters commandConfig2)
                {
                    if (string.IsNullOrEmpty(commandConfig2.CMD_EXECUTABLE))
                    {
                        Log($"Record has no command-exectution instrcution in input record, local config settings are used.");
                        commandConfig2 = LocalConfig;
                    }

                    await RunCommand(commandConfig2);
                }
            }

            return 0;
        }


        /// <summary>
        /// Process a handler message - passed in by the handler manager.
        /// Note this handler would deposite its output, if any , to designated storage(s) via the manager
        /// </summary>
        /// <param name="message">a handler message, can be an event, notification, or command, or other types</param>
        /// <returns>a status integer</returns>
        /// <param name="cancellationToken"></param>
        public override async Task<int> ProcessInboundMessage(MessageRda message, CancellationToken cancellationToken)
        {
            if (message is MessageRda.HandlerEvent handlerEvent && handlerEvent.EventDetailsRda is CommandInputParameters commandConfig)
            {
                if (string.IsNullOrEmpty(commandConfig.CMD_EXECUTABLE))
                {
                    Log($"Trigger event has no command-exectution instrcution in input record, local config settings are used.");
                    commandConfig = LocalConfig;
                }

                await RunCommand(commandConfig);

                return 0;
            }
            else if (message is MessageRda.HandlerNotification handlerNotification && handlerNotification.NotificationBodyRda is CommandInputParameters commandConfig2)
            {
                if (string.IsNullOrEmpty(commandConfig2.CMD_EXECUTABLE))
                {
                    Log($"Trigger event has no command-exectution instrcution in input record, local config settings are used.");
                    commandConfig2 = LocalConfig;
                }

                await RunCommand(commandConfig2);

                return 0;
            }
            else
            {
                return 1;
            }
        }


        public const int DEFAULT_TIMEOUT = 1; //1 sec

        private async Task RunCommand(CommandInputParameters commandConfig)
        {
            await Task.Run(() => {
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
                                    try
                                    {
                                        outputWaitHandle.Set();
                                    }
                                    catch(ObjectDisposedException)
                                    {
                                        Deb("outputWaitHandle disposed");
                                    }
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
                                    try
                                    {
                                        errorWaitHandle.Set();
                                    }
                                    catch(ObjectDisposedException)
                                    {
                                        return;
                                    }                                 
                                }
                                else
                                {
                                    error.AppendLine(e.Data);
                                }
                            };

                            Task.Delay(50).Wait(); //have a small delay waiting for the above threads to set the wait handles

                            //execute the command
                            process.Start();

                            //capture ouputs
                            process.BeginOutputReadLine();
                            process.BeginErrorReadLine();

                            int timeOutMiliSec = commandConfig.TIMEOUT_SEC * 1000;

                            //WaitForExit WITH TIMEOUT
                            if (process.WaitForExit(timeOutMiliSec) &&
                                outputWaitHandle.WaitOne(timeOutMiliSec) &&
                                errorWaitHandle.WaitOne(timeOutMiliSec))
                            {
                                // Process completed. Check process.ExitCode here.
                                if (process.ExitCode == 0)
                                {
                                    Log($"INFO: Executing command '{fullCommand}' successed. Output = {output}");
                                    HandlerManager.PostHandlerOutboundMessage(new CommandOutputRecord(this.GetType().Name, DateTime.Now, new Rda() { ScalarValue = output.ToString() }));
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

                return Task.CompletedTask;
            });

        }

        internal CommandInputParameters LocalConfig { get; private set; }

        public override void Setup(IConfigProvider config)
        {
            LocalConfig = new CommandInputParameters()
            {
                CMD_EXECUTABLE = config.GetSettingValue(CommandInputParameters.PARAM_CMD_EXECUTABLE, string.Empty).Trim(),
                ARGUMENTS = config.GetSettingValue(CommandInputParameters.PARAM_ARGUMENTS, string.Empty).Trim(),
                ARGUMENTS_2 = config.GetSettingValue(CommandInputParameters.PARAM_ARGUMENTS_2, string.Empty).Trim(),
                ARGUMENTS_3 = config.GetSettingValue(CommandInputParameters.PARAM_ARGUMENTS_3, string.Empty).Trim(),
                TIMEOUT_SEC = config.GetSettingValue(CommandInputParameters.PARAM_TIMEOUT_SEC, DEFAULT_TIMEOUT)
            };
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using System.Collections.ObjectModel;
//using Foldda.Util;
using System.Diagnostics;
using System.Security.Permissions;
using System.Reflection;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Security;
using Foldda.Automation.Util;


namespace Foldda.Automation.HandlerDevKit
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        [SecurityPermission(SecurityAction.Demand, Flags = SecurityPermissionFlag.ControlAppDomain)]
        static void Main(string[] args)
        {
            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += new UnhandledExceptionEventHandler(HandleUnHandledException);
            Application.ThreadException += new ThreadExceptionEventHandler(HandleThreadException);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);   //https://stackoverflow.com/questions/8283631/graphics-drawstring-vs-textrenderer-drawtextwhich-can-deliver-better-quality
            DevKitForm form = new DevKitForm();

            Application.Run(form);
        }

        public static string AssemblyDirectory
        {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
        }

        static void HandleUnHandledException(object sender, UnhandledExceptionEventArgs args)
        {
            Exception e = (Exception)args.ExceptionObject;
            //if( args.IsTerminating )
            Log(e);
        }

        static void HandleThreadException(object sender, ThreadExceptionEventArgs e)
        {
            Exception ex = e.Exception;
            Log(ex);
        }

        static void Log(Exception e)
        {
            string logFileName = $@"{AssemblyDirectory}\CRASH_{System.DateTime.Now:_yyyyMMdd}.log";
            //after rotating the log, content is cleared (FileMode.Truncate) before writing the next log line
            //FileShare.ReadWrite is required for Tailing program to read the log file at the same time
            using (var stream = new FileStream(logFileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, bufferSize: 1024, useAsync: true))
            {

                string logLine = System.String.Format("[{0:T}] {1}\n", System.DateTime.Now, $"Exception: {e.Message}\n{e.StackTrace}").Replace("\n", Environment.NewLine);

                byte[] bytes = System.Text.Encoding.Default.GetBytes(logLine);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush();
            }
        }

        //execute powershell command
        //this version does _not_ require using System.Management.Automation;
        //static void RunCommand(MainForm form, string cmd, string psScipt)
        //{
        //    ProcessStartInfo processInfo = new ProcessStartInfo
        //    {
        //        FileName = cmd,
        //        Arguments = psScipt,
        //        RedirectStandardError = true,
        //        RedirectStandardOutput = true,
        //        UseShellExecute = false,
        //        CreateNoWindow = true
        //    };

        //    using (var timeoutCancellationTokenSource = new CancellationTokenSource())
        //    {
        //        string file = "Handlers.dll";
        //        try
        //        {
        //            using (Stream stream = new FileStream(file, FileMode.Open))
        //            {
        //                form.Log($"Unblocking files not required.");
        //            }
        //        }
        //        catch(Exception e)
        //        {
        //            form.Log($"{e.Message} - {file} inaccessible - unblocking files ...");

        //            using (Process process = new Process() { StartInfo = processInfo })
        //            {
        //                using (timeoutCancellationTokenSource.Token.Register(() => process.Kill()))
        //                {
        //                    timeoutCancellationTokenSource.CancelAfter(3000);    //timeout incase Ps.invoke hangs
        //                    Task.Run(() =>
        //                    {
        //                        process.Start();
        //                        //read output
        //                        form.Log($"PS-run output - {process.StandardOutput.ReadToEnd()}");
        //                        //read errors
        //                        form.Log($"PS-run errors - {process.StandardError.ReadToEnd()}");
        //                    }, timeoutCancellationTokenSource.Token);
        //                }
        //            }
        //        }
        //    }
        //}


        //this version does _not_ require using System.Management.Automation;
        //static void RunCommand2(MainForm form, string cmd, string psScipt)
        //{
        //    ProcessStartInfo processInfo = new ProcessStartInfo
        //    {
        //        FileName = cmd,
        //        Arguments = psScipt,
        //        RedirectStandardError = true,
        //        RedirectStandardOutput = true,
        //        UseShellExecute = false,
        //        CreateNoWindow = true
        //    };
        //    //execute powershell script using script file
        //    //processInfo.Arguments = @"& {c:\temp\Get-EventLog.ps1}";

        //    //start powershell process using process start info
        //    using (Process process = new Process() { StartInfo = processInfo })
        //    {
        //        process.Start();

        //        //read output
        //        form.Log($"PS-run output - {process.StandardOutput.ReadToEnd()}");
        //        //read errors
        //        form.Log($"PS-run errors - {process.StandardError.ReadToEnd()}");
        //    }

        //}

        //static async Task InvokePowershellAsync(MainForm form, string psScipt, int timeout)
        //{
        //    using (var timeoutCancellationTokenSource = new CancellationTokenSource())
        //    {
        //        try
        //        {
        //            using (PowerShell PowerShellInstance = PowerShell.Create())
        //            {
        //                using (timeoutCancellationTokenSource.Token.Register(() => PowerShellInstance.Stop()))
        //                {
        //                    timeoutCancellationTokenSource.CancelAfter(timeout);    //timeout incase Ps.invoke hangs
        //                    await Task.Run(() =>
        //                    {
        //                        //https://blogs.msdn.microsoft.com/kebab/2014/04/28/executing-powershell-scripts-from-c/
        //                        PowerShellInstance.AddScript(psScipt);
        //                        PowerShellInstance.Invoke();    //this may hang
        //                        form.Log("Ps execution completed.");
        //                    }); //, timeoutCancellationTokenSource.Token);
        //                }
        //            }
        //        }
        //        catch(Exception e)
        //        {
        //            form.Log($"Error running ps. {e.Message}");
        //        }
        //    }
        //}
    }
}

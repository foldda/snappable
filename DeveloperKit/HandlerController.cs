using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using System.Threading;
using System.Diagnostics;

//independent to Handlers
using Foldda.Automation.Framework;

using Foldda.Automation.Util;
using Charian;
using System.Reflection;
using System.Windows.Forms;

namespace Foldda.Automation.HandlerDevKit
{
    /// <summary>
    /// 
    /// Adopting to the MVC pattern, HandlerController is the "controller" drives two "views" (HandlerLoggingPanel & HandlerSettingsPanel) over a "handler model" (HandlerModel).
    /// 
    /// HandlerController is also a basic runtime environment for hosting a Foldda Automation (FA) handler instance that is constructed by a supplied FA config file, and this
    /// instance, as it executes as it would in a FA runtime, updates the model object of the MVC (primarily the changing settings and logging properties).
    /// 
    /// </summary>
    class HandlerController
    {
        private HandlerModel _handlerModel = new HandlerModel.Dummy();

        public HandlerModel HandlerModel
        {
            get
            {
                return _handlerModel;
            }

            set
            {
                if (_handlerModel != value)
                {
                    //... assign new node.
                    _handlerModel = value;
                    _handlerModel.Touch();
                }
            }
        }

        //internal void MakeHandler(string handler, string handlerAssembly)
        //{
        //    Handler = (BasicDataHandler)CreateHandlerInstance(handler, handlerAssembly);
        //}

        private static readonly Dictionary<string, Assembly> AssemblyCache = new Dictionary<string, Assembly>();

        public BasicDataHandler CreateHandlerInstance(string fullTypeName, string handlerAssemblyName)
        {
            //, ILoggingProvider logger
            IDataHandler handler;
            string folderPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string assemblyPath = Path.Combine(folderPath, handlerAssemblyName);
            try
            {

                //Foldda supports the specifying Handler from a different/separate (3rd party) assembly ...
                //eg "ThirdParty.Handlers.dll" assembly containing class "ThirdParty.Handlers.WebServer"
                //in this case, the <Handler> tage would be specified as <Handler>ThirdParty.Handlers.WebServer</Handler>

                Assembly assembly;
                if (AssemblyCache.TryGetValue(assemblyPath, out assembly) == false)
                {
                    if (!File.Exists(assemblyPath))
                    {
                        throw new Exception($"Specified assembly file, '{assemblyPath}', does not exist.");
                    }

                    assembly = Assembly.LoadFile(assemblyPath);    //try loading the class from "configured" assembly
                    if (assembly == null)
                    {
                        throw new Exception($"ERROR: failed to load specified assembly {assemblyPath}.");
                    }
                    AssemblyCache.Add(assemblyPath, assembly);
                }

                Type type = assembly.GetType(fullTypeName); // full name - i.e. with namespace (perhaps concatenate)
                if (type == null)
                {
                    string message = $"{fullTypeName} cannot be located in assembly {assemblyPath}. These types are available in [{assembly.GetName()}]:";

                    foreach (Type t in assembly.GetTypes())
                    {
                        message += $"\t [{t.FullName}]";
                    }

                    throw new Exception(message);
                }
                else
                {
                    Log($"Type full name - {fullTypeName} located in assembly {assemblyPath}.");
                }
                //#if DEBUG
                //                Debug.Assert(type != null);
                //#endif
                handler = (IDataHandler)Activator.CreateInstance(type, new object[] { this.HandlerModel });

                Log($"Successfully instantiated class [{fullTypeName}] from assembly [{assemblyPath}]");
            }
            catch (Exception e)
            {
                Log(e.Message);
                Log($"ERROR: Handler class [{fullTypeName}] not found in assembly [{assemblyPath}], please ensure their names are correct in the 'node-config' file.");
                throw;
            }
            //#if DEBUG
            //            Debug.Assert(result != null);
            //#endif
            return (BasicDataHandler)handler;
        }

        private void Log(string v)
        {
            HandlerModel?.Log(v);
        }

        Task HandlerTask { get; set; }

        internal CancellationTokenSource HandlerStopCommandCancelSource = new CancellationTokenSource();   //triggerred by node-stop command

        internal void Start(CancellationToken appShutdown)
        {
            if (HandlerModel != null && !(HandlerModel is HandlerModel.Dummy))
            {
                HandlerStopCommandCancelSource = new CancellationTokenSource();

                CancellationTokenSource linkedHandlerStopCancellationTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(HandlerStopCommandCancelSource.Token, appShutdown);

                try
                {
                    Log($"Execute START command");

                    //DebugMode = RuntimeConstant.YES_STRING.Equals(NodeConfig.DebugMode?.ToUpper());
                    //_config = null; //force reload config.

                    //starting handler (node tasks)!!
                    HandlerTask = StartHandler(linkedHandlerStopCancellationTokenSource.Token);   //non-blocking because it doesn't wait()
                    
                    return;
                }
                catch (Exception e)
                {
                    HandlerStopCommandCancelSource.Cancel(); //stop any started processes

                    Log($"Node starting UNSUCESSFUL due to exception {e.Message}");
                    HandlerTask = Task.CompletedTask;
                    return;
                }

            }
            else
            {
                Log($"Node handler is not assigned.");
            }
        }

        public IDataStore InboundDataBuffer { get; } //used by folder-scanning processor, plus Inbound processors

        public IDataStore OutboundDataBuffer { get; } //used for collecting DataProcessingTask's output (which in-turn will be passed to the Output-consuming task.

        internal DevKitForm DevKitForm { get; }
        internal RichTextBox HandlerLoggingPanel { get; }
        internal ListView HandlerSettingsPanel { get; }

        public HandlerController(DevKitForm devKitForm, IDataStore inputStore, IDataStore outputStore, RichTextBox loggingPanel, ListView settingsPanel) //: base(form.Logger)
        {
            DevKitForm = devKitForm;
            HandlerLoggingPanel = loggingPanel;
            HandlerSettingsPanel = settingsPanel;

            InboundDataBuffer = inputStore;
            OutboundDataBuffer = outputStore;
        }

        internal BasicDataHandler Handler { get; private set; }  //the handler it manages

        //NB, input container/records count are implemented in the Receive(Container) method, which has no animation
        //called from the Node
        private Task StartHandler(CancellationToken cancellationToken)
        {
            var t = Task.Run(() =>
            {
                try
                {
                    HandlerModel.SetStateForCommand(HandlerModel.COMMAND_TYPE.NODE_CMD_START);   //setter will send node-state-change notification
                    //initialise the custom handler eg. with current parameters
                    Handler.Setup(HandlerModel.HandlerConfig, InboundDataBuffer, OutboundDataBuffer);

                    //handler moves data from inbound buffer to outbound buffer
                    Task dataHandlingTask = Handler.ProcessData(cancellationToken);

                    Log($"Handler '{this.Handler.GetType().Name}' tasks started.");
                    Task.WaitAll(dataHandlingTask);

                }
                catch (Exception e)
                {
                    Log($"ERROR: Handler operation is stopped due to exception - {e.Message}.");
                    Log($"\n{e.StackTrace}");
                }
                finally
                {
                    //don't set STATE here, let command and state-table to drive state 
                    Log($"Handler '{this.Handler.GetType().Name}' tasks completed/stopped.");
                }

            });

            return t;
        }

        internal async Task Stop()
        {
            try
            {
                Log($"Execute STOP command");
                HandlerModel.SetStateForCommand(HandlerModel.COMMAND_TYPE.NODE_CMD_STOP);   //setter will send node-state-change notification

                HandlerStopCommandCancelSource.Cancel(); //stop local issued "handler tasks"

                if (HandlerTask != null && await Task.WhenAny(HandlerTask, Task.Delay(5000)) != HandlerTask)
                {
                    Log("Handler didn't stop in time (5 sec)."); // timeout logic
                }
            }
            catch(ObjectDisposedException)
            {
                Log($"{nameof(ObjectDisposedException)} thrown in Stop().");
            }
            catch (OperationCanceledException)
            {
                Log($"{nameof(OperationCanceledException)} thrown in Stop().");
            }
            catch (Exception e)
            {
                throw e;
            }
            finally
            {
                HandlerStopCommandCancelSource.Dispose();  //we have to dispose the cts once it's cancelled.

                if (HandlerTask != null && HandlerTask?.Status != TaskStatus.RanToCompletion)
                {
                    Log($"Handler task wasn't stopped properly, status = {HandlerTask.Status}");
                }
            }
        }

        internal void UpdateCurrentNode(string fileName)
        {
            HandlerModel = new HandlerModel(new FileInfo(fileName)); ;

            Handler = CreateHandlerInstance(HandlerModel.HandlerConfig.Handler, HandlerModel.HandlerConfig.HandlerAssembly);

            HandlerModel.Touch();
        }

        internal void RePaint(DevKitForm form)
        {
            if(HandlerModel.Dirty)
            {
                form.DrawHandlerLogView(this.HandlerLoggingPanel, new HandlerView.LoggingPanel(HandlerModel));
                form.DrawHandlerSettingsListView(this.HandlerSettingsPanel, new HandlerView.HandlerConfigPanel(HandlerModel));
            }
        }

        internal void Reset()
        {
            HandlerModel = new HandlerModel.Dummy();
            HandlerModel.Touch();
            HandlerStopCommandCancelSource = new CancellationTokenSource();
        }
    }
}
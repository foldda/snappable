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

using Charian;
using System.Reflection;
using System.Windows.Forms;

namespace Foldda.Automation.HandlerDevKit
{
    /// <summary>
    /// 
    /// As part of the DevKit app that adopts to the MVC pattern, HandlerController is the "controller" drives two "views" (HandlerLoggingPanel & HandlerSettingsPanel) over a "handler model" (HandlerModel).
    /// 
    /// HandlerController implements the IDataHandlerManager interface that provides a basic runtime environment for hosting a Foldda Automation (FA) handler instance which is constructed using a supplied FA config file, 
    /// and this handler instance, as it executes in a FA runtime, updates the UI's model object of the MVC (primarily the changing settings and logging properties) via this controller-.
    /// 
    /// </summary>
    public class HandlerController : ISnappableManager
    {
        private HandlerModel _handlerModel = HandlerModel.DUMMY;

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
                    _handlerModel?.Touch();
                }
            }
        }

        public ILoggingProvider Logger => HandlerModel;

        public IDataStore PipelineInputDataStorage { get; set; } //used by folder-scanning processor, plus Inbound processors

        public IDataStore PipelineOutputDataStorage { get; set; } //used for collecting DataProcessingTask's output (which in-turn will be passed to the Output-consuming task.

        public IDataStore InboundMessageStorage { get; set; }  //a common space that anyone can have accessto, used for handlers to post public events for arbitrary recipients.


        //subscribers of the listener emitted events.
        public List<ISnappableEventListener> SnappableEventListeners { get; }

        /// <summary>
        /// For the managed listener to call and post its outbound events
        /// </summary>
        /// <param name=""></param>
        public void PostHandlerOutboundMessage(MessageRda handlerOutMessage)
        {
            //differentiate event and notification
            //for event, call the ProcessEvent below, which distribute the message to all subscribed listeners
            //for notification, deposite the message to runtime's message board.
            if (handlerOutMessage is MessageRda.HandlerEvent || handlerOutMessage is MessageRda.HandlerNotification )
            {
                Log($"INFO: handler posted a message - {handlerOutMessage}.");
            }
            else
            {
                throw new Exception("Unknown message type - skipped.");
            }
        }

        public void ProcessEvent(MessageRda.HandlerEvent evnt, CancellationToken cancellationToken)
        {
            if (!evnt.EventSourceId.Equals(Snappable.UID))
            {
                Snappable.ProcessInboundMessage(evnt, cancellationToken);
            }
        }

        public string UID => "HandlerController_UID";
        public string Name => "Handler Controller";



        //internal void MakeHandler(string handler, string handlerAssembly)
        //{
        //    Handler = (BasicDataHandler)CreateHandlerInstance(handler, handlerAssembly);
        //}

        private static readonly Dictionary<string, Assembly> AssemblyCache = new Dictionary<string, Assembly>();

        public BasicSnappable CreateHandlerInstance(string fullTypeName, string handlerAssemblyName)
        {
            //, ILoggingProvider logger
            BasicSnappable result;
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
                result = Activator.CreateInstance(type, [this]) as BasicSnappable;
                if (result == null) 
                { 
                    throw new Exception($"Activator.CreateInstance failed for instantiating an object of type '{fullTypeName}'"); 
                }

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
            return result;
        }

        private void Log(string v)
        {
            HandlerModel?.Log(v);
        }

        Task HandlerTask { get; set; }

        internal CancellationTokenSource HandlerStopCommandCancelSource = new CancellationTokenSource();   //triggerred by node-stop command

        internal void Start(CancellationToken appShutdown)
        {
            if (HandlerModel != null && !(HandlerModel == HandlerModel.DUMMY))
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


        internal DevKitForm DevKitForm { get; }
        internal RichTextBox HandlerLoggingPanel { get; }
        internal ListView HandlerSettingsPanel { get; }
        internal Button[] HandlerButtons { get; }
        internal int Index0 { get; }    //0-based index

        public HandlerController(DevKitForm devKitForm, 
            IDataStore inputStore, IDataStore outputStore, IDataStore eventStore, 
            RichTextBox loggingPanel, ListView settingsPanel, Button[] buttons, 
            int controllerIndex) //: base(form.Logger)
        {
            DevKitForm = devKitForm;
            HandlerLoggingPanel = loggingPanel;
            HandlerSettingsPanel = settingsPanel;
            HandlerButtons = buttons;

            PipelineInputDataStorage = inputStore;
            PipelineOutputDataStorage = outputStore;
            InboundMessageStorage = eventStore;
            Index0 = controllerIndex;

            var savedConfig = ConfigSettings.Get(DevKitForm.CONFIG_PATHS[Index0]);
            if(!string.IsNullOrEmpty(savedConfig))
            {
                UpdateCurrentNode(savedConfig);
            }
        }

        internal HandlerController ChildController => Index0 < DevKitForm.Controllers.Count - 1 ? DevKitForm.Controllers[Index0 + 1] : null;

        public ISnappable Snappable { get; private set; }  //the handler it manages
        public string Id => this.GetType().Name;


        public Task ManageHandlerDataPipeline(CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                try
                {
                    do
                    {
                        //assign input records to the listener
                        var handlerInput = PipelineInputDataStorage.CollectReceived();
                        foreach (var item in handlerInput)
                        {
                            int itemContainerCount = 0;
                            int itemRecordsCount = 1;
                            if (item is RecordContainer inputContainer)
                            {
                                itemContainerCount = 1;
                                itemRecordsCount = inputContainer.Records.Count;
                                await Snappable.ProcessPipelineRecordContainer(inputContainer, cancellationToken);
                            }

                            //TODO increment this nodeOrSolution's inbound trans counts
                        }

                        await Task.Delay(100);
                    }
                    while (cancellationToken.IsCancellationRequested == false);
                }
                catch (Exception e)
                {
                    Log($"Handler Data Pipeline Managing task stopped due to error - {e.Message}.");
                    Log(e.StackTrace);
                }
                finally
                {
                    Log($"Handler Data Pipeline Managing task completed.");
                }

            });
        }

        public Task ManageHandlerInboundMessages(CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                try
                {
                    do
                    {
                        //moves the listener's output to the down-stream nodes
                        var inboundMessages = InboundMessageStorage.CollectReceived();
                        foreach (var item in inboundMessages)
                        {
                            if (item is MessageRda handlerMessage)
                            {
                                await Snappable.ProcessInboundMessage(handlerMessage, cancellationToken);
                            }
                            else
                            {
                                Log($"ManageHandlerInboundMessages(): Message of type '{item.GetType().FullName}' is not supported by the framework.");
                            }

                            //increment this nodeOrSolution's inbound trans counts
                            //RuntimeEntity.NodeChangeMonitor.AddTransCounts(0, itemRecordsCount, 0);
                        }

                        await Task.Delay(100);
                    }
                    while (cancellationToken.IsCancellationRequested == false);
                }
                catch (Exception e)
                {
                    Log($"Handler Inbound Messages processing task stopped due to error - {e.Message}.");
                    Log(e.StackTrace);
                }
                finally
                {
                    Log($"Handler Inbound Messages processing task stopped.");
                }

            });
        }

        public virtual Task InitializeHandler(CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                try
                {
                    Log($"Handler initialization task started.");
                    await Snappable.Init(cancellationToken);
                }
                catch (Exception e)
                {
                    Log($"Handler initialization exception - {e.Message}.");
                    Log(e.StackTrace);
                }
                finally
                {
                    Log($"Handler initialization task completed.");
                }

            });
        }


        //NB, input inputContainer/records count are implemented in the Receive(Container) method, which has no animation
        //called from the Node
        private Task StartHandler(CancellationToken cancellationToken)
        {
            var t = Task.Run(() =>
            {
                try
                {
                    HandlerModel.SetStateForCommand(HandlerModel.COMMAND_TYPE.NODE_CMD_START);   //setter will send node-state-change notification
                    
                    //initialise the custom handler eg. with current parameters
                    Snappable.Setup(HandlerModel.HandlerConfig);

                    Task handlerManagerExtraTask = HandlerManagerTask(cancellationToken);

                    Task handlerInitializationTask = InitializeHandler(cancellationToken);

                    //handler moves data from inbound buffer to outbound buffer
                    Task handlerMessagesManagingTask = ManageHandlerInboundMessages(cancellationToken);


                    Task dataPipelineManagingTask = ManageHandlerDataPipeline(cancellationToken);

                    Log($"Handler '{this.Snappable.GetType().Name}' tasks started.");
                    Task.WaitAll(handlerInitializationTask, dataPipelineManagingTask, handlerMessagesManagingTask, handlerManagerExtraTask);

                }
                catch (Exception e)
                {
                    Log($"ERROR: Handler operation is stopped due to exception - {e.Message}.");
                    Log($"\n{e.StackTrace}");
                }
                finally
                {
                    //don't set STATE here, let command and state-table to drive state 
                    Log($"Handler '{this.Snappable.GetType().Name}' tasks completed/stopped.");
                }

            });

            return t;
        }

        //Framework task example - injecting heartbeat timer-events
        private Task HandlerManagerTask(CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                try
                {
                    do
                    {
                        //inject dummy "heart-beat" timer-events to the handler - which can choose not to handle this event.
                        Snappable?.ProcessInboundMessage(new MessageRda.HandlerEvent(this.Id, DateTime.Now, Rda.NULL), cancellationToken);
                        //this is one way the framework to drive handler's behaviors rather than just the start-stop operations.
                        await Task.Delay(1000);
                    }
                    while (cancellationToken.IsCancellationRequested == false);
                }
                catch (Exception e)
                {
                    Log($"Handler Manager task stopped unexpected due to error - {e.Message}.");
                    Log(e.StackTrace);
                }
                finally
                {
                    Log($"Handler Manager task completed.");
                }
            });
        }

        internal async Task Stop()
        {
            try
            {
                Log($"Execute STOP command");
                HandlerModel?.SetStateForCommand(HandlerModel.COMMAND_TYPE.NODE_CMD_STOP);   //setter will send node-state-change notification

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
            try
            {
                if(!File.Exists(fileName))
                {
                    throw new Exception($"Config file '{fileName}' not exist.");
                }

                HandlerModel = new HandlerModel(new FileInfo(fileName)); ;

                Snappable = CreateHandlerInstance(HandlerModel.HandlerConfig.Handler, HandlerModel.HandlerConfig.HandlerAssembly);

                HandlerModel.Touch();

                ConfigSettings.Save(DevKitForm.CONFIG_PATHS[Index0], fileName);
            }
            catch(Exception e)
            {
                Log(e.Message);
                ConfigSettings.Remove(DevKitForm.CONFIG_PATHS[Index0]);
            }

        }

        internal void RePaint(DevKitForm form)
        {
            if (HandlerModel != null && (HandlerModel == HandlerModel.DUMMY || HandlerModel.Dirty))
            {
                HandlerModel.Clean();
                form.DrawHandlerLogView(this.HandlerLoggingPanel, new HandlerView.LoggingPanel(HandlerModel));
                form.DrawHandlerSettingsListView(this.HandlerSettingsPanel, new HandlerView.HandlerConfigPanel(HandlerModel));
                form.DrawHandlerButtons(
                    HandlerButtons[(int)HandlerView.HandlerButtonsPanel.INDEX.LOAD], 
                    HandlerButtons[(int)HandlerView.HandlerButtonsPanel.INDEX.START],
                    HandlerButtons[(int)HandlerView.HandlerButtonsPanel.INDEX.STOP],
                    HandlerButtons[(int)HandlerView.HandlerButtonsPanel.INDEX.UNLOAD],
                    new HandlerView.HandlerButtonsPanel(HandlerModel));

                if(HandlerModel == HandlerModel.DUMMY) { HandlerModel = null; }
            }
        }

        internal void Reset()
        {
            HandlerModel = HandlerModel.DUMMY;
            HandlerModel.Touch();
            HandlerStopCommandCancelSource = new CancellationTokenSource();
        }

    }
}
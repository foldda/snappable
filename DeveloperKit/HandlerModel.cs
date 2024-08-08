using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Foldda.Automation.Util;
using Foldda.Automation.Framework;

namespace Foldda.Automation.HandlerDevKit
{


    public class HandlerModel : ILoggingProvider
    {    
        public enum ENTITY_STATE : int
        {
            // ---- these states responds to command
            NODE_STARTED,     //starts thread for receiving inbound and producing outbound
            // ---- these states do not have receive/dispose threads
            NODE_STOPPED,    //respond to "start" command, in state-transit table
        }

        public enum COMMAND_TYPE : int
        {
            NODE_CMD_START,    //
            NODE_CMD_RESTART,    //
            NODE_CMD_STOP,
            UNKNOWN_CMD
        }

        private HandlerModel() { }

        public class Dummy : HandlerModel
        {
            public Dummy() : base() { }


        }

        ILoggingProvider Logger { get; }

        private int _touched = 0;

        internal void Touch()
        {
            _touched++;
        }

        internal bool Dirty => _touched > 0;

        internal void Clean() { _touched = 0; }
 
        //CancellationToken token is passed-in from the root-node's constructor, and it's used by 
        //this root constructor to shutdown the whole tree.
        internal HandlerModel(FileInfo handlerConfigFile) 
        {

            /* if no exception thrown in the above steps, this node can be successfully constructed - config is valid */

            try
            {
                ResetConfig(handlerConfigFile.FullName);  //throws InvalidNodeFolderException if valid config is not found
                //change logging path from runtime log file to handler's log file
                HomePath = handlerConfigFile.DirectoryName;
                Logger = new FileLogger($@"{HomePath}\{DevKitForm.FOLDDA_LOG_FOLDER_NAME}\{HandlerConfig.Handler}");

                //this node is now constructed in its initial state
                Log($"Created handler with config: {HandlerConfig.Details()}.");

                //send structure message 
                UpdateSettingsDisplay();

            }
            catch (Exception e)
            {
                Log($"Error when try constructing a valid handler from config file '{handlerConfigFile?.Name}' - {e.Message}");

                //make sure error is exposed
                throw;
            }
        }

        private void UpdateSettingsDisplay()
        {
            Description = HandlerConfig.Description;
            Handler = HandlerConfig.Handler;
            Assembly = HandlerConfig.HandlerAssembly;
            AlertPatterns = new List<string>(DEFAULT_ALERT_PATTERN);
            AlertPatterns.AddRange(HandlerConfig.CustomAlertPatterns.Split(new char[] { ';', ','}));
            Parameters = new Parameter[HandlerConfig.Parameters.Length];
            for (int i = 0; i < HandlerConfig.Parameters.Length; i++)
            {
                Parameters[i] = new Parameter() { Name = HandlerConfig.Parameters[i].Name, Value = HandlerConfig.Parameters[i].Value };
            }

            Touch();
        }

        public static Dictionary<ENTITY_STATE, string> STATES = new Dictionary<ENTITY_STATE, string>()
                    {
                        {ENTITY_STATE.NODE_STARTED, "Started" },
                        {ENTITY_STATE.NODE_STOPPED, "Stopped" }
                    };

        ///FSM implement
        ///

        class EntityStateTransition
        {
            readonly ENTITY_STATE CurrentState;
            readonly COMMAND_TYPE Command;

            public EntityStateTransition(ENTITY_STATE currentState, COMMAND_TYPE command)
            {
                CurrentState = currentState;
                Command = command;
            }

            /*
             * The StateTransition class is used as key in the dictionary and equality of keys are important. 
             * Two distinct instances of StateTransition should be considered equal as long as they represent 
             * the same transition (e.g. CurrentState and Command are the same). To implement equality you have 
             * to override Equals as well as GetHashCode. In particular the dictionary will use the hash code 
             * and two equal objects must return the same hash code.
             */
            public override int GetHashCode()
            {
                return 17 + 31 * CurrentState.GetHashCode() + 31 * Command.GetHashCode();
            }

            public override bool Equals(object obj)
            {
                EntityStateTransition other = obj as EntityStateTransition;
                return other != null && this.CurrentState == other.CurrentState && this.Command == other.Command;
            }
        }

        private static readonly Dictionary<EntityStateTransition, ENTITY_STATE> TransitionRuleTable =
            new Dictionary<EntityStateTransition, ENTITY_STATE>
            {
                //node transitions
                { new EntityStateTransition(ENTITY_STATE.NODE_STOPPED, COMMAND_TYPE.NODE_CMD_START), ENTITY_STATE.NODE_STARTED },
                { new EntityStateTransition(ENTITY_STATE.NODE_STOPPED, COMMAND_TYPE.NODE_CMD_RESTART), ENTITY_STATE.NODE_STARTED },
                { new EntityStateTransition(ENTITY_STATE.NODE_STOPPED, COMMAND_TYPE.NODE_CMD_STOP), ENTITY_STATE.NODE_STOPPED },
                ////
                { new EntityStateTransition(ENTITY_STATE.NODE_STARTED, COMMAND_TYPE.NODE_CMD_RESTART), ENTITY_STATE.NODE_STARTED },
                { new EntityStateTransition(ENTITY_STATE.NODE_STARTED, COMMAND_TYPE.NODE_CMD_STOP), ENTITY_STATE.NODE_STOPPED },
            };

        private ENTITY_STATE _currentState = ENTITY_STATE.NODE_STOPPED;

        public ENTITY_STATE CurrentState
        {
            get
            {
                return _currentState;
            }

            internal set
            {
                if (_currentState != value)
                {
                    _currentState = value;
                    Touch();
                }
            }
        }

        public void SetStateForCommand(COMMAND_TYPE command)
        {
            EntityStateTransition transition = new EntityStateTransition(CurrentState, command);
            if (!TransitionRuleTable.TryGetValue(transition, out ENTITY_STATE nextState))
            {
                //do nothing if the transition is unexpected.
                Log($"Invalid transition: {CurrentState } -> {command}");
            }
            else
            {
                CurrentState = nextState;
            }
        }

        internal static readonly string DATE_FORMAT_STRING = "h:mm tt, MMM d";
        internal static readonly string NOT_AVAILABLE = " \u2013";

        internal static string FormatRefTime(DateTime? refTime)
        {
            if (refTime == null || refTime < DateTime.MinValue.AddMinutes(10))
            {
                return NOT_AVAILABLE;
            }
            else
            {
                return refTime?.ToString(DATE_FORMAT_STRING);
            }
        }

        public virtual NodeConfig HandlerConfig => _config;

        private NodeConfig _config = null;
        internal void ResetConfig(string configFileFullName)
        {
            try
            {
                _config = NodeConfig.GetConfig(configFileFullName);
            }
            catch (Exception e)
            {
                throw new Exception($"Config file '{configFileFullName}' is not found or is invalid. {e.Message}");
            }
        }

        public LoggingLevel LoggingThreshold { get => Logger.LoggingThreshold; set => Logger.LoggingThreshold = value; }

        public void Log(string v)
        {
            Log(v, LoggingLevel.Debug);
        }

        public void Log(string v, LoggingLevel loggingLevel)
        {
            Logger?.Log(v, loggingLevel);
            while (BufferredLogLines.Count > LOG_BUFFER_LENGTH)
            {
                BufferredLogLines.TryDequeue(out _);
            }

            BufferredLogLines.Enqueue($"[{DateTime.Now:T}] {v}");
            Touch();
        }

        public string Description { get; private set; } //= string.Empty;
        public string Handler { get; private set; }

        public string HandlerShortName
        {
            get
            {
                string[] tokens = Handler.Split('.');
                return tokens[tokens.Length - 1];
            }
        }

        public string Assembly { get; private set; }

        public List<string> AlertPatterns { get; private set; } = new List<string>();

        static List<string> DEFAULT_ALERT_PATTERN = new List<string> { "ERROR", "WARNING" };

        public Parameter[] Parameters { get; private set; } = new Parameter[0];

        const int LOG_BUFFER_LENGTH = 100;

        public ConcurrentQueue<string> BufferredLogLines { get; internal set; } = new ConcurrentQueue<string>();

        public string HomePath { get; set; } = string.Empty;

        public string ImageKey => HandlerStateString;
        public string HandlerStateString { get { return STATES[CurrentState]; } }

    }
}


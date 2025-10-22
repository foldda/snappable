using Charian;
using System.Threading;
using System.Threading.Tasks;

namespace Foldda.Automation.Framework
{
    /// <summary>
    /// ISnappable represents the abstraction of a unit of independent process which can be one these three types. 
    /// 
    /// X1) For gathering/generating input data of the data-processing chain (placing the produced data to the 'input storage')
    /// 2) For translating/transforming data based on the input from the 'input storage', and placing the result to the 'output storage'.
    /// X3) For dispatching processed data to an outside destination (eg database, file, network, etc)
    /// 
    /// Via its ISnappableManager, ISnappable interacts with its outside environment by -
    /// 
    /// 1) processing the data inside the input DataContainers that is passed to it, and in most cases disposes processed data to an output DataContainer
    /// 
    /// 2) create events that will be passed to its immediately connected members (parent & children), and respond to events that is passed to it.
    /// eg. timer events that triggers children to start a process, error event that alert parent to pause sending more data
    /// 
    /// 3) post messages to an (universal) IHandlerMessageBoard, and respond to messages that can be from any outside entity, such as from another hanlder, or from the runtime.
    /// eg. a runtime message can ask the handler to stop or start, an acknowledgement message to the hanlder at the beginning of a data-processing chain.
    /// 
    /// A data-processing handler class can implement (or extend the default behavior of ) any one of these methods.
    /// 
    /// A handler 'runtime' program achives its intended data-processing by providing data inputContainer storages (IReceiver objects), 
    /// and pipelines i.e. linkage from framework , between handlers (ISnappable).
    /// 
    /// </summary>
    public interface ISnappable
    {
        /// <summary>
        /// Unique ID used ub messaging for identifying sender and receiver.
        /// </summary>
        string UID { get; }

        /// <summary>
        /// For being used in where-ever the handler needs to be identified, eg in logging.
        /// </summary>
        string Name { get; }


        ISnappableManager Manager { get; }    //set by constructor


        /// <summary>
        /// Setting up the data-handler "worker" with its config, and its input storage where it takes records for processing, and its output storage where it places
        /// its output/processed data that will be sent to its default destinations (eg next stage of the data-processing flow). But alternatively or additionally
        /// a handler can also send data to so-called "off branch" channel - such as to its parent node's handler (in the case the previous data-processing 
        /// stage requires feedback), or to the runtime (in cases such as sending alters via the runtime).
        /// 
        /// In the case of sending data via the off-branch channel, the inputContainer being deposited must has appropriate Mete data for the Handler-Manager about where
        /// to deliver this inputContainer. Containers without delivery addressee in Meta-data will be disgarded. 
        ///
        /// </summary>
        /// <param name="config"></param>
        /// <param name="manager">Where the handler depends on for its data and event processing, including - inputStorage outputStorage, and eventStorage.</param>
        /// <returns></returns>        
        void Setup(IConfigProvider config);

        /// <summary>
        /// Process a record inputContainer - passed in by the handler manager.
        /// Note this handler would deposite its output, if any , to designated storage(s) via the manager
        /// </summary>
        /// <param name="inputContainer">a inputContainer with a collection of records</param>
        /// <returns>a status integer</returns>
        Task<int> ProcessPipelineRecordContainer(RecordContainer inputContainer, CancellationToken cancellationToken);

        /// <summary>
        /// Process a handler message - passed in by the handler manager.
        /// Note this handler would deposite its output, if any , to designated storage(s) via the manager
        /// </summary>
        /// <param name="message">a handler message, can be an event, notification, or command, or other types</param>
        /// <returns>a status integer</returns>
        /// <param name="cancellationToken"></param>
        Task<int> ProcessInboundMessage(MessageRda message, CancellationToken cancellationToken);

        /// <summary>
        /// Initialize this handler. For tasks that require initialization, eg. a network listener
        /// </summary>
        /// <param name="message">a handler message, can be an event, notification, or command, or other types</param>
        /// <returns>a status integer</returns>
        Task<int> Init(CancellationToken cancellationToken);

        AbstractCharStreamRecordScanner DefaultFileRecordScanner { get; }
    }
}

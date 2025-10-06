using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Foldda.Automation.Framework
{
    public interface IHandlerManager : IHandlerEventListener
    {
        /// <summary>
        /// These properties are for storing data for handler's data processing
        /// </summary>
        IDataStore PipelineInputDataStorage { get; set; }

        IDataStore PipelineOutputDataStorage { get; set; }

        IDataStore InboundMessageStorage { get; set; }

        /// <summary>
        /// These methods are for handler to communicate with the 'outside world' via messaging
        /// </summary>
        List<IHandlerEventListener> HandlerEventListeners { get; }

        /// <summary>
        /// For the managed handler to call and post its outbound messages including events and notifications
        /// </summary>
        /// <param name="handlerOutputMessage"></param>        
        void PostHandlerOutboundMessage(MessageRda handlerOutputMessage);

        /// <summary>
        /// For being controlled (start/stop) by runtime
        /// </summary>
        /// <param name="cancellationToken">run-time's cancelation token</param>
        /// <returns>The task for runtime to wait upon</returns>
        Task ManageHandlerDataPipeline(CancellationToken cancellationToken);

        /// <summary>
        /// For being controlled (start/stop) by runtime
        /// </summary>
        /// <param name="cancellationToken">run-time's cancelation token</param>
        /// <returns>The task for runtime to wait upon</returns>
        Task ManageHandlerInboundMessages(CancellationToken cancellationToken);



        //the handler (data and event processor) managed by this manager
        IDataHandler Handler { get; }
        ILoggingProvider HandlerLogger { get; }

        string UID { get; } //unique string identify this handler manager

        string Name { get; }    //reader-able name for printing (may not unique)
    }
}

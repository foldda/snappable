using Charian;
using System.Threading;
using System.Threading.Tasks;

namespace Foldda.Automation.Framework
{
    /// <summary>
    /// IDataHandler represents the abstraction of the One independent process. 
    /// 
    /// X1) For gathering/generating input data of the data-processing chain (placing the produced data to the 'input storage')
    /// 2) For translating/transforming data based on the input from the 'input storage', and placing the result to the 'output storage'.
    /// X3) For dispatching processed data to an outside destination (eg database, file, network, etc)
    /// 
    /// A data-processing handler class can implement (or extend the default behavior of ) any one of these methods.
    /// 
    /// A handler 'runtime' program achives its intended data-processing by providing data container storages (IReceiver objects), 
    /// and pipelines i.e. linkage from framework , between handlers (IDataHandler).
    /// 
    /// </summary>
    public interface IDataHandler
    {
        /// <summary>
        /// 0) For being used in where-ever the handler needs to be identified, eg in logging.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Setting up the data-handler "worker" with its config, and its input and output trays
        /// </summary>
        /// <param name="config"></param>
        /// <param name="inputStorage"></param>
        /// <param name="outputStorage"></param>
        /// <returns></returns>        
        void Setup(IConfigProvider config, IDataStore inputStorage, IDataStore ouputStorage);

        /// <summary>
        /// Here the handler "worker" take records/events from the 'input storage', and (optionally, if there is any output) placing the result to the 'output storage'.
        /// </summary>
        /// <param name="inputStorage"></param>
        /// <param name="outputStorage"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task ProcessData(CancellationToken cancellationToken);

    }
}

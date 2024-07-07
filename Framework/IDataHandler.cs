using Charian;
using System.Threading;
using System.Threading.Tasks;

namespace Foldda.Automation.Framework
{
    /// <summary>
    /// IDataHandler represents the abstraction of the three independent processes. 
    /// 
    /// 1) For gathering/generating input data of the data-processing chain (placing the produced data to the 'input storage')
    /// 2) For translating/transforming data based on the input from the 'input storage', and placing the result to the 'output storage'.
    /// 3) For dispatching processed data to an outside destination (eg database, file, network, etc)
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
        /// 1) For gathering/generating input data of the data-processing chain (placing the produced data to the 'input storage')
        /// </summary>
        /// <param name="inputStorage"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task InputCollectingProcess(IDataContainerStore inputStorage, CancellationToken cancellationToken);

        /// <summary>
        /// 2) For translating/transforming data based on the input from the 'input storage', and placing the result to the 'output storage'.
        /// </summary>
        /// <param name="inputStorage"></param>
        /// <param name="outputStorage"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task InputProcessingProcess(IDataContainerStore inputStorage, IDataContainerStore outputStorage, CancellationToken cancellationToken);

        /// <summary>
        /// 3) For dispatching processed data to an outside destination (eg database, file, network, etc)
        /// </summary>
        /// <param name="outputStorage">Data to be dispatched are taken from this storage</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task OutputDispatchingProcess(IDataContainerStore outputStorage, CancellationToken cancellationToken);
    }
}

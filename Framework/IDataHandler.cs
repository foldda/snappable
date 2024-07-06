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
    /// 3) For dispatching data to an outside destination (eg database, file, network, etc)
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
        /// 
        /// </summary>
        /// <param name="inputStorage"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task InputCollectingProcess(IDataContainerStore inputStorage, CancellationToken cancellationToken);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="inputStorage"></param>
        /// <param name="outputStorage"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task InputProcessingProcess(IDataContainerStore inputStorage, IDataContainerStore outputStorage, CancellationToken cancellationToken);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="outputStorage">Data to be dispatched are taken from this storage</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task OutputDispatchingProcess(IDataContainerStore outputStorage, CancellationToken cancellationToken);
    }
}

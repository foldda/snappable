using Charian;
using System.Threading;
using System.Threading.Tasks;

namespace Foldda.DataAutomation.Framework
{
    /// <summary>
    /// IDataHandler represents the abstraction of the three tasks. 
    /// 
    /// 1) For gathering/generating input data of the data-processing chain (placing the produced data to the 'input storage')
    /// 2) For translating/transforming data based on the input from the 'input storage', and placing the result to the 'output storage'.
    /// 3) For dispatching data to an outside destination (eg database, file, network, etc)
    /// 
    /// A data-processor class can implement (or extend the default behavior of ) any one of these methods.
    /// 
    /// </summary>
    public interface IDataHandler
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="producedInputStorage"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task InputProducingProcess(IDataReceiver producedInputStorage, CancellationToken cancellationToken);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="inputSourceStorage"></param>
        /// <param name="outputDestinationStorage"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task DataTransformationProcess(IDataReceiver inputSourceStorage, IDataReceiver outputDestinationStorage, CancellationToken cancellationToken);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="outputSourceStorage"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task OutputConsumingProcess(IDataReceiver outputSourceStorage, CancellationToken cancellationToken);
    }
}

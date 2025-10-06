using System.Threading;

namespace Foldda.Automation.Framework
{
    public interface IHandlerEventListener
    {
        void ProcessEvent(MessageRda.HandlerEvent evnt, CancellationToken cancellationToken);
    }
}
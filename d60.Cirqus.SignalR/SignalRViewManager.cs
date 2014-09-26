using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using d60.Cirqus.Events;
using d60.Cirqus.Views.ViewManagers;

namespace d60.Cirqus.SignalR
{
    /// <summary>
    /// Special SignalR view manager wrapper that can be used to publish view data to clients
    /// </summary>
    /// <typeparam name="TViewInstance"></typeparam>
    public class SignalRViewManager<TViewInstance> : IViewManager<TViewInstance> where TViewInstance : IViewInstance
    {
        readonly IViewManager<TViewInstance> _innerViewManager;

        public SignalRViewManager(IViewManager<TViewInstance> innerViewManager)
        {
            _innerViewManager = innerViewManager;

            _innerViewManager.Updated += instance =>
            {
                PublishUpdatedView(instance);

                Updated(instance);
            };
        }

        public long GetPosition(bool canGetFromCache = true)
        {
            return _innerViewManager.GetPosition(canGetFromCache);
        }

        public void Dispatch(IViewContext viewContext, IEnumerable<DomainEvent> batch)
        {
            _innerViewManager.Dispatch(viewContext, batch);
        }

        public Task WaitUntilProcessed(CommandProcessingResult result, TimeSpan timeout)
        {
            return _innerViewManager.WaitUntilProcessed(result, timeout);
        }

        public void Purge()
        {
            _innerViewManager.Purge();
        }

        public TViewInstance Load(string viewId)
        {
            return _innerViewManager.Load(viewId);
        }

        public event ViewInstanceUpdatedHandler<TViewInstance> Updated = delegate { };

        void PublishUpdatedView(TViewInstance instance)
        {
        }
    }
}

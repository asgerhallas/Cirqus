﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using d60.Cirqus.Aggregates;
using d60.Cirqus.Config.Configurers;
using d60.Cirqus.Events;
using d60.Cirqus.Views;

namespace d60.Cirqus.Diagnostics
{
    public static class DiagnosticsConfigurationExtensions
    {
        /// <summary>
        /// Hooks the given profiler into core operations, allowing for recording relevant execution times
        /// </summary>
        public static void AddProfiler(this OptionsConfigurationBuilder builder, IProfiler profiler)
        {
            var operationProfiler = new OperationProfiler(profiler);

            builder
                .Registrar
                .Register<IEventStore>(c =>
                {
                    var innerEventStore = c.Get<IEventStore>();
                    return new EventStoreDecorator(innerEventStore, operationProfiler);
                }, decorator: true);

            builder
                .Registrar
                .Register<IAggregateRootRepository>(c =>
                {
                    var innnerAggregateRootRepository = c.Get<IAggregateRootRepository>();
                    return new AggregateRootRepositoryDecorator(innnerAggregateRootRepository, operationProfiler);
                }, decorator: true);

            builder
                .Registrar
                .Register<IEventDispatcher>(c =>
                {
                    var innerEventDispatcher = c.Get<IEventDispatcher>();
                    return new EventDispatcherDecorator(innerEventDispatcher, operationProfiler);
                }, decorator: true);
        }

        class OperationProfiler
        {
            readonly IProfiler _profiler;

            public OperationProfiler(IProfiler profiler)
            {
                _profiler = profiler;
            }

            public void RecordAggregateRootGet(TimeSpan elapsed, Type type, Guid aggregateRootId)
            {
                _profiler.RecordAggregateRootGet(elapsed, type, aggregateRootId);
            }

            public void RecordAggregateRootExists(TimeSpan elapsed, Guid aggregateRootId)
            {
                _profiler.RecordAggregateRootExists(elapsed, aggregateRootId);
            }

            public void RecordEventBatchSave(TimeSpan elapsed, Guid batchId)
            {
                _profiler.RecordEventBatchSave(elapsed, batchId);
            }

            public void RecordGlobalSequenceNumberGetNext(TimeSpan elapsed)
            {
                _profiler.RecordGlobalSequenceNumberGetNext(elapsed);
            }

            public void RecordEventDispatch(TimeSpan elapsed)
            {
                _profiler.RecordEventDispatch(elapsed);
            }
        }

        class EventDispatcherDecorator : IEventDispatcher
        {
            readonly IEventDispatcher _innerEventDispatcher;
            readonly OperationProfiler _operationProfiler;

            public EventDispatcherDecorator(IEventDispatcher innerEventDispatcher, OperationProfiler operationProfiler)
            {
                _innerEventDispatcher = innerEventDispatcher;
                _operationProfiler = operationProfiler;
            }

            public void Initialize(IEventStore eventStore, bool purgeExistingViews = false)
            {
                _innerEventDispatcher.Initialize(eventStore, purgeExistingViews);
            }

            public void Dispatch(IEventStore eventStore, IEnumerable<DomainEvent> events)
            {
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    _innerEventDispatcher.Dispatch(eventStore, events);
                }
                finally
                {
                    _operationProfiler.RecordEventDispatch(stopwatch.Elapsed);
                }
            }
        }

        class AggregateRootRepositoryDecorator : IAggregateRootRepository
        {
            readonly IAggregateRootRepository _innnerAggregateRootRepository;
            readonly OperationProfiler _operationProfiler;

            public AggregateRootRepositoryDecorator(IAggregateRootRepository innnerAggregateRootRepository, OperationProfiler operationProfiler)
            {
                _innnerAggregateRootRepository = innnerAggregateRootRepository;
                _operationProfiler = operationProfiler;
            }

            public AggregateRootInfo<TAggregate> Get<TAggregate>(Guid aggregateRootId, IUnitOfWork unitOfWork, long maxGlobalSequenceNumber = long.MaxValue, bool createIfNotExists = false) where TAggregate : AggregateRoot, new()
            {
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    return _innnerAggregateRootRepository
                        .Get<TAggregate>(aggregateRootId, unitOfWork, maxGlobalSequenceNumber, createIfNotExists);
                }
                finally
                {
                    _operationProfiler.RecordAggregateRootGet(stopwatch.Elapsed, typeof(TAggregate), aggregateRootId);
                }
            }

            public bool Exists<TAggregate>(Guid aggregateRootId, long maxGlobalSequenceNumber = Int64.MaxValue, IUnitOfWork unitOfWork = null) where TAggregate : AggregateRoot
            {
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    return _innnerAggregateRootRepository
                        .Exists<TAggregate>(aggregateRootId, maxGlobalSequenceNumber, unitOfWork);
                }
                finally
                {
                    _operationProfiler.RecordAggregateRootExists(stopwatch.Elapsed, aggregateRootId);
                }
            }
        }

        class EventStoreDecorator : IEventStore
        {
            readonly IEventStore _innerEventStore;
            readonly OperationProfiler _operationProfiler;

            public EventStoreDecorator(IEventStore innerEventStore, OperationProfiler operationProfiler)
            {
                _innerEventStore = innerEventStore;
                _operationProfiler = operationProfiler;
            }

            public void Save(Guid batchId, IEnumerable<DomainEvent> batch)
            {
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    _innerEventStore.Save(batchId, batch);
                }
                finally
                {
                    _operationProfiler.RecordEventBatchSave(stopwatch.Elapsed, batchId);
                }
            }

            public IEnumerable<DomainEvent> Load(Guid aggregateRootId, long firstSeq = 0)
            {
                return _innerEventStore.Load(aggregateRootId, firstSeq);
            }

            public IEnumerable<DomainEvent> Stream(long globalSequenceNumber = 0)
            {
                return _innerEventStore.Stream(globalSequenceNumber);
            }

            public long GetNextGlobalSequenceNumber()
            {
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    return _innerEventStore.GetNextGlobalSequenceNumber();
                }
                finally
                {
                    _operationProfiler.RecordGlobalSequenceNumberGetNext(stopwatch.Elapsed);
                }
            }
        }
    }
}
﻿using System;
using d60.Cirqus.Aggregates;

namespace d60.Cirqus.Events
{
    /// <summary>
    /// A thing that is capable of collecting emitted events
    /// </summary>
    public interface IUnitOfWork
    {
        void AddEmittedEvent(DomainEvent e);

        void AddToCache<TAggregateRoot>(TAggregateRoot aggregateRoot, long globalSequenceNumberCutoff) where TAggregateRoot : AggregateRoot;

        bool Exists<TAggregateRoot>(Guid aggregateRootId, long globalSequenceNumberCutoff) where TAggregateRoot : AggregateRoot;

        AggregateRootInfo<TAggregateRoot> Get<TAggregateRoot>(Guid aggregateRootId, long globalSequenceNumberCutoff, bool createIfNotExists = false) where TAggregateRoot : AggregateRoot, new();
    }
}
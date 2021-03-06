﻿using System;
using d60.Cirqus.Aggregates;
using d60.Cirqus.Numbers;

namespace d60.Cirqus.Commands
{
    /// <summary>
    /// Ultimate command base class - don't derive off of this one directly, use <see cref="Command{TAggregateRoot}"/>
    /// </summary>
    public abstract class Command
    {
        protected Command()
        {
            Meta = new Metadata();
        }

        public Metadata Meta { get; private set; }

        public abstract void Execute(ICommandContext context);
    }

    /// <summary>
    /// Command base class that is mapped to one specific aggregate root instance for which the <seealso cref="Execute"/> method will be invoked
    /// </summary>
    /// <typeparam name="TAggregateRoot">Specifies the type of aggregate root that this command targets</typeparam>
    public abstract class Command<TAggregateRoot> : Command where TAggregateRoot : AggregateRoot, new()
    {
        protected Command(Guid aggregateRootId)
        {
            AggregateRootId = aggregateRootId;
        }
       
        public Guid AggregateRootId { get; private set; }

        public sealed override void Execute(ICommandContext context)
        {
            var aggregateRootInstance = context.Load<TAggregateRoot>(AggregateRootId);

            Execute(aggregateRootInstance);
        }

        public abstract void Execute(TAggregateRoot aggregateRoot);

        public override string ToString()
        {
            return string.Format("{0} => {1} {2}", GetType().Name, typeof(TAggregateRoot).Name, AggregateRootId);
        }
    }
}
﻿using System;
using System.Collections.Generic;
using System.Linq;
using d60.Cirqus.Events;
using d60.Cirqus.Exceptions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace d60.Cirqus.MongoDb.Events
{
    public class MongoDbEventStore : IEventStore
    {
        const string GlobalSeqUniquenessIndexName = "EnsureGlobalSeqUniqueness";
        const string SeqUniquenessIndexName = "EnsureSeqUniqueness";
        const string EventsDocPath = "Events";
        const string MetaDocPath = "Meta";

        static readonly string SeqNoDocPath = string.Format("{0}.{1}.{2}", EventsDocPath, MetaDocPath, DomainEvent.MetadataKeys.SequenceNumber);
        static readonly string GlobalSeqNoDocPath = string.Format("{0}.{1}.{2}", EventsDocPath, MetaDocPath, DomainEvent.MetadataKeys.GlobalSequenceNumber);
        static readonly string AggregateRootIdDocPath = string.Format("{0}.{1}.{2}", EventsDocPath, MetaDocPath, DomainEvent.MetadataKeys.AggregateRootId);

        readonly MongoDbSerializer _serializer = new MongoDbSerializer();

        readonly MongoCollection _eventBatches;

        public MongoDbEventStore(MongoDatabase database, string eventCollectionName, bool automaticallyCreateIndexes = true)
        {
            _eventBatches = database.GetCollection(eventCollectionName);

            if (automaticallyCreateIndexes)
            {
                _eventBatches.CreateIndex(IndexKeys.Ascending(GlobalSeqNoDocPath), IndexOptions.SetUnique(true).SetName(GlobalSeqUniquenessIndexName));
                _eventBatches.CreateIndex(IndexKeys.Ascending(AggregateRootIdDocPath, SeqNoDocPath), IndexOptions.SetUnique(true).SetName(SeqUniquenessIndexName));
            }
        }

        public void AddSerializationMutator(IJsonEventMutator mutator)
        {
            _serializer.EventSerializationMutators.Add(mutator);
        }

        public void AddDeserializationMutator(IJsonEventMutator mutator)
        {
            _serializer.EventDeserializationMutators.Add(mutator);
        }

        public IEnumerable<DomainEvent> Stream(long globalSequenceNumber = 0)
        {
            var globalSequenceNumberToQueryFor = globalSequenceNumber;

            return _eventBatches
                .FindAs<BsonDocument>(Query.GTE(GlobalSeqNoDocPath, globalSequenceNumberToQueryFor))
                .SelectMany(doc => doc[EventsDocPath].AsBsonArray)
                .Select(eventDoc => new
                {
                    GlobalSequenceNumber = GetLong(eventDoc[MetaDocPath][DomainEvent.MetadataKeys.GlobalSequenceNumber]),
                    EventDoc = eventDoc
                })
                .Where(a => a.GlobalSequenceNumber >= globalSequenceNumber)
                .Select(a => _serializer.Deserialize(a.EventDoc));
        }

        public IEnumerable<DomainEvent> Load(Guid aggregateRootId, long firstSeq = 0)
        {
            var criteria = Query.And(
                Query.EQ(AggregateRootIdDocPath, aggregateRootId.ToString()),
                Query.GTE(SeqNoDocPath, firstSeq));

            var docs = _eventBatches.FindAs<BsonDocument>(criteria);

            var eventsSatisfyingCriteria = docs
                .SelectMany(doc => doc[EventsDocPath].AsBsonArray)
                .Select(e => new
                {
                    Event = e,
                    SequenceNumber = GetLong(e[MetaDocPath][DomainEvent.MetadataKeys.SequenceNumber]),
                    AggregateRootId = GetAggregateRootIdOrDefault(e)
                })
                .Where(e => e.AggregateRootId == aggregateRootId && e.SequenceNumber >= firstSeq);

            return eventsSatisfyingCriteria
                .OrderBy(e => e.SequenceNumber)
                .Select(e => _serializer.Deserialize(e.Event));
        }

        public void Save(Guid batchId, IEnumerable<DomainEvent> batch)
        {
            var events = batch.ToList();

            if (!events.Any())
            {
                throw new InvalidOperationException(string.Format("Attempted to save batch {0}, but the batch of events was empty!", batchId));
            }

            events.ForEach(e => _serializer.EnsureSerializability(e));

            var nextGlobalSeqNo = GetNextGlobalSequenceNumber();

            foreach (var e in events)
            {
                e.Meta[DomainEvent.MetadataKeys.GlobalSequenceNumber] = nextGlobalSeqNo++;
                e.Meta[DomainEvent.MetadataKeys.BatchId] = batchId;
            }

            EventValidation.ValidateBatchIntegrity(batchId, events);

            var doc = new BsonDocument
            {
                {"_id", batchId.ToString()},
                {EventsDocPath, GetEvents(events)}
            };

            try
            {
                _eventBatches.Save(doc);
            }
            catch (MongoDuplicateKeyException exception)
            {
                throw new ConcurrencyException(batchId, events, exception);
            }
        }

        public long GetNextGlobalSequenceNumber()
        {
            var doc = _eventBatches
                .FindAllAs<BsonDocument>()
                .SetSortOrder(SortBy.Descending(GlobalSeqNoDocPath))
                .SetLimit(1)
                .SingleOrDefault();

            return doc == null
                ? 0
                : doc[EventsDocPath].AsBsonArray
                    .Select(e => e[MetaDocPath][DomainEvent.MetadataKeys.GlobalSequenceNumber].ToInt64())
                    .Max() + 1;
        }

        long GetLong(BsonValue bsonValue)
        {
            if (bsonValue.IsInt32)
                return bsonValue.ToInt32();

            if (bsonValue.IsInt64)
                return bsonValue.ToInt64();

            throw new FormatException(string.Format("Could not intepret BSON value '{0}' as int or long - its type is '{1}'",
                bsonValue, bsonValue.BsonType));
        }

        Guid GetAggregateRootIdOrDefault(BsonValue e)
        {
            var metaDoc = e[MetaDocPath].AsBsonDocument;

            return new Guid(metaDoc.GetValue(DomainEvent.MetadataKeys.AggregateRootId, Guid.Empty.ToString()).ToString());
        }

        BsonValue GetEvents(IEnumerable<DomainEvent> events)
        {
            var array = new BsonArray();

            foreach (var e in events)
            {
                array.Add(_serializer.Serialize(e));
            }

            return array;
        }
    }

    class EventBatch
    {
        [BsonId]
        public string BatchId { get; set; }

        public List<DomainEvent> Events { get; set; }
    }
}
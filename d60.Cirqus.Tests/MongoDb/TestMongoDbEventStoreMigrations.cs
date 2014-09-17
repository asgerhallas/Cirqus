using System;
using System.Linq;
using d60.Cirqus.Events;
using d60.Cirqus.MongoDb.Events;
using d60.Cirqus.Numbers;
using NUnit.Framework;

namespace d60.Cirqus.Tests.MongoDb
{
    [TestFixture, Category(TestCategories.MongoDb)]
    public class TestMongoDbEventStoreMigrations : FixtureBase
    {
        MongoDbEventStore _eventStore;

        protected override void DoSetUp()
        {
            _eventStore = new MongoDbEventStore(MongoHelper.InitializeTestDatabase(), "Events");
        }

        [Test]
        public void DoesNotDoAnythingWithOrdinaryEvents()
        {
            // arrange
            var anEvent = new OrdinaryEvent
            {
                Meta =
                {
                    {DomainEvent.MetadataKeys.AggregateRootId, Guid.NewGuid().ToString()},
                    {DomainEvent.MetadataKeys.SequenceNumber, 1}
                }
            };

            // act
            _eventStore.Save(Guid.NewGuid(), new DomainEvent[] { anEvent });

            // assert

        }

        class OrdinaryEvent : DomainEvent { }

        [Test]
        public void CanMigrateEvents()
        {
            // arrange
            var aggregateRootId = Guid.NewGuid();

            var oldCrappyEvent = new OldCrappyEventV1
            {
                SomeField = "whatever",
                Meta =
                {
                    {DomainEvent.MetadataKeys.AggregateRootId, aggregateRootId.ToString()},
                    {DomainEvent.MetadataKeys.SequenceNumber, 1}
                }
            };

            // act
            _eventStore.Save(Guid.NewGuid(), new DomainEvent[] { oldCrappyEvent });
            var events = _eventStore.Load(aggregateRootId).ToList();

            // assert
            var domainEvent = events.Single();

            Assert.That(domainEvent, Is.TypeOf<OldCrappyEventV2>());
            Assert.That(((OldCrappyEventV2) domainEvent).SomeField, Is.EqualTo("whatever"));
        }

        [Meta("Version", 1)]
        class OldCrappyEventV1 : DomainEvent
        {
            public string SomeField { get; set; }
        }

        [Meta("Version", 2)]
        class OldCrappyEventV2 : DomainEvent
        {
            public string SomeField { get; set; }
        }
    }
}
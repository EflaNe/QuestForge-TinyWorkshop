using NUnit.Framework;
using TinyWorkshop.QuestCore;
namespace TinyWorkshop.Tests
{
    public sealed class QuestCoreTests
    {
        [Test]
        public void LockedQuestIgnoresEventsAndRejectsReward()
        {
            var bus = new GameEventPublisher();
            using (var quest = new WorkshopQuest(bus, 3))
            {
                bus.Publish(new GameEvent(GameEventKind.PartProduced, 1));
                Assert.AreEqual(0, quest.Produced);
                Assert.IsFalse(quest.ClaimReward());
                Assert.AreEqual("Locked", quest.StateName);
            }
        }
        [Test]
        public void RepeatedEventsAdvanceEachObjectiveOnlyOnce()
        {
            var bus = new GameEventPublisher();
            using (var quest = new WorkshopQuest(bus, 3))
            {
                quest.Start();
                for (int i = 0; i < 10; i++)
                    bus.Publish(new GameEvent(GameEventKind.PartProduced, 1));
                Assert.AreEqual(1, quest.Produced);
                Assert.AreEqual(0, quest.Collected);
                Assert.AreEqual("Active", quest.StateName);
            }
        }
        [Test]
        public void AllObjectivesRequiredAndRewardClaimedExactlyOnce()
        {
            var bus = new GameEventPublisher();
            using (var quest = new WorkshopQuest(bus, 3))
            {
                Assert.IsTrue(quest.Start());
                Assert.IsFalse(quest.Start());
                for (int id = 1; id <= 3; id++)
                {
                    bus.Publish(new GameEvent(GameEventKind.PartProduced, id));
                    bus.Publish(new GameEvent(GameEventKind.PartCollected, id));
                }
                Assert.AreEqual("Active", quest.StateName);
                Assert.IsFalse(quest.ClaimReward());
                for (int id = 1; id <= 3; id++)
                    bus.Publish(new GameEvent(GameEventKind.PartDelivered, id));
                Assert.AreEqual("Completed", quest.StateName);
                Assert.IsTrue(quest.ClaimReward());
                Assert.IsFalse(quest.ClaimReward());
                Assert.AreEqual("RewardClaimed", quest.StateName);
                bus.Publish(new GameEvent(GameEventKind.PartProduced, 4));
                Assert.AreEqual(3, quest.Produced);
            }
        }
        [Test]
        public void DisposeRemovesObserverAndDisablesQuest()
        {
            var bus = new GameEventPublisher();
            var quest = new WorkshopQuest(bus, 3);
            Assert.AreEqual(1, bus.ObserverCount);
            quest.Dispose();
            quest.Dispose();
            Assert.AreEqual(0, bus.ObserverCount);
            Assert.IsFalse(quest.Start());
        }
        [Test]
        public void FactoriesCreateMatchingObjectiveKinds()
        {
            Assert.AreEqual(GameEventKind.PartProduced, new ProduceObjectiveCreator().Create(3).Kind);
            Assert.AreEqual(GameEventKind.PartCollected, new CollectObjectiveCreator().Create(3).Kind);
            Assert.AreEqual(GameEventKind.PartDelivered, new DeliverObjectiveCreator().Create(3).Kind);
            Assert.IsInstanceOf<ProduceObjective>(new ProduceObjectiveCreator().Create(3));
            Assert.IsInstanceOf<CollectObjective>(new CollectObjectiveCreator().Create(3));
            Assert.IsInstanceOf<DeliverObjective>(new DeliverObjectiveCreator().Create(3));
        }
        [Test] public void InvalidTargetIsRejected() => Assert.Throws<System.ArgumentOutOfRangeException>(() => new ProduceObjectiveCreator().Create(0));
        [Test] public void EmptyCompositeIsNotACompletedQuest() => Assert.IsFalse(new QuestGroup().IsComplete);
    }
}

using System;
using TinyWorkshop.QuestCore;

static class Checks
{
    static int count;
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); count++; }
    static void Main()
    {
        var bus = new GameEventPublisher();
        using (var quest = new WorkshopQuest(bus, 3))
        {
            bus.Publish(new GameEvent(GameEventKind.PartProduced, 1));
            Check(quest.Produced == 0, "Locked quest ignores events");
            Check(!quest.ClaimReward(), "No early reward");
            Check(quest.Start(), "Start allowed once");
            Check(!quest.Start(), "Cannot restart active quest");
            bus.Publish(new GameEvent(GameEventKind.PartCollected, 99));
            Check(quest.Produced == 0 && quest.Collected == 1, "Objectives filter event kind");
            for (int i = 1; i <= 3; i++)
            {
                foreach (GameEventKind kind in Enum.GetValues(typeof(GameEventKind)))
                { bus.Publish(new GameEvent(kind, i)); bus.Publish(new GameEvent(kind, i)); }
            }
            Check(quest.Produced == 3 && quest.Collected == 3 && quest.Delivered == 3, "Counts cap and deduplicate");
            Check(quest.StateName == "Completed", "Composite completes all three");
            Check(quest.ClaimReward(), "Reward after completion");
            Check(!quest.ClaimReward() && quest.StateName == "RewardClaimed", "Reward only once");
        }
        Check(bus.ObserverCount == 0, "Dispose unsubscribes");
        using (var quest = new WorkshopQuest(bus, 3))
        {
            quest.Start();
            for (int i = 0; i < 3; i++) bus.Publish(new GameEvent(GameEventKind.PartProduced, i));
            Check(quest.StateName == "Active" && !quest.ClaimReward(), "Production alone is insufficient");
        }
        Check(new ProduceObjectiveCreator().Create(3).Kind == GameEventKind.PartProduced, "Produce factory");
        Check(new CollectObjectiveCreator().Create(3).Kind == GameEventKind.PartCollected, "Collect factory");
        Check(new DeliverObjectiveCreator().Create(3).Kind == GameEventKind.PartDelivered, "Deliver factory");
        Check(new ProduceObjectiveCreator().Create(3) is ProduceObjective, "Concrete produce product");
        Check(new CollectObjectiveCreator().Create(3) is CollectObjective, "Concrete collect product");
        Check(new DeliverObjectiveCreator().Create(3) is DeliverObjective, "Concrete deliver product");
        Console.WriteLine($"PASS: {count} QuestCore behavioral checks");
    }
}

namespace TinyWorkshop.QuestCore
{
    // [KALIP: Factory Method] Rolü: Concrete Creator
    // Create() çağrılınca CollectObjective üretir.
    public sealed class CollectObjectiveCreator : ObjectiveCreator
    {
        public override Objective Create(int target) => new CollectObjective(target);
    }
}

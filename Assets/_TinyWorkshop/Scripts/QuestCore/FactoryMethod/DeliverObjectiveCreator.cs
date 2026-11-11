namespace TinyWorkshop.QuestCore
{
    // [KALIP: Factory Method] Rolü: Concrete Creator
    // Create() çağrılınca DeliverObjective üretir.
    public sealed class DeliverObjectiveCreator : ObjectiveCreator
    {
        public override Objective Create(int target) => new DeliverObjective(target);
    }
}

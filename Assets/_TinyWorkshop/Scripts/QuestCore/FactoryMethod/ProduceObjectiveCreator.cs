namespace TinyWorkshop.QuestCore
{
    // [KALIP: Factory Method] Rolü: Concrete Creator
    // Create() çağrılınca ProduceObjective üretir.
    public sealed class ProduceObjectiveCreator : ObjectiveCreator
    {
        public override Objective Create(int target) => new ProduceObjective(target);
    }
}

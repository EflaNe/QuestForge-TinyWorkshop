namespace TinyWorkshop.QuestCore
{
    // [KALIP: Factory Method] Rolü: Concrete Product
    // "N dişli üret" hedefi: sadece PartProduced olaylarını sayar.
    public sealed class ProduceObjective : Objective
    {
        public ProduceObjective(int target) : base(GameEventKind.PartProduced, target) { }
    }
}

namespace TinyWorkshop.QuestCore
{
    // [KALIP: Factory Method] Rolü: Concrete Product
    // "N dişli teslim et" hedefi: sadece PartDelivered olaylarını sayar.
    public sealed class DeliverObjective : Objective
    {
        public DeliverObjective(int target) : base(GameEventKind.PartDelivered, target) { }
    }
}

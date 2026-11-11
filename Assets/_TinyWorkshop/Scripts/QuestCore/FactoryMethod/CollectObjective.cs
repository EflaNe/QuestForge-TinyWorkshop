namespace TinyWorkshop.QuestCore
{
    // [KALIP: Factory Method] Rolü: Concrete Product
    // "N dişli topla" hedefi: sadece PartCollected olaylarını sayar.
    public sealed class CollectObjective : Objective
    {
        public CollectObjective(int target) : base(GameEventKind.PartCollected, target) { }
    }
}

namespace TinyWorkshop.QuestCore
{
    // [KALIP: State] Rolü: Concrete State - Kilitli (başlangıç durumu)
    // Olaylar sayılmaz. Sadece Start() izinli: Locked -> Active
    public sealed class LockedState : QuestState
    {
        public override string Name => "Locked";
        public override bool Start(WorkshopQuest quest)
        {
            quest.Transition(new ActiveState());
            return true;
        }
    }
}

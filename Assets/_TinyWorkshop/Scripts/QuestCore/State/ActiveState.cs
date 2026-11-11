namespace TinyWorkshop.QuestCore
{
    // [KALIP: State] Rolü: Concrete State - Aktif
    // Olaylar hedefleri ilerletir. Hepsi bitince: Active -> Completed
    public sealed class ActiveState : QuestState
    {
        public override string Name => "Active";
        public override void OnEvent(WorkshopQuest quest, GameEvent value)
        {
            quest.AdvanceObjectives(value);
            if (quest.IsComplete) // Composite'e sorulur: tüm hedefler tamam mı?
                quest.Transition(new CompletedState());
        }
    }
}

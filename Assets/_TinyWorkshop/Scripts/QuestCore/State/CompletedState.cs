namespace TinyWorkshop.QuestCore
{
    // [KALIP: State] Rolü: Concrete State - Tamamlandı (ödül bekliyor)
    // Sadece ClaimReward() izinli: Completed -> RewardClaimed
    public sealed class CompletedState : QuestState
    {
        public override string Name => "Completed";
        public override bool ClaimReward(WorkshopQuest quest)
        {
            quest.Transition(new RewardClaimedState());
            return true;
        }
    }
}

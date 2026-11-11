namespace TinyWorkshop.QuestCore
{
    // [KALIP: State] Rolü: Concrete State - Ödül alındı (son durum)
    // Hiçbir şeyi override etmez: tüm istekler reddedilir.
    public sealed class RewardClaimedState : QuestState
    {
        public override string Name => "RewardClaimed";
    }
}

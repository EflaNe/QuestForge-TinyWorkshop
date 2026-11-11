namespace TinyWorkshop.QuestCore
{
    // [KALIP: State] Rolü: State (durum arayüzü)
    // Görevin (WorkshopQuest = Context) her isteği o anki duruma devredilir.
    public interface IQuestState
    {
        string Name
        {
            get;
        }
        bool Start(WorkshopQuest quest);
        bool ClaimReward(WorkshopQuest quest);
        void OnEvent(WorkshopQuest quest, GameEvent value);
    }
    // Ortak taban: varsayılan cevap "hayır / hiçbir şey yapma".
    // Her somut durum sadece kendisinde izinli olan işlemi override eder.
    public abstract class QuestState : IQuestState
    {
        public abstract string Name
        {
            get;
        }
        public virtual bool Start(WorkshopQuest quest) => false;
        public virtual bool ClaimReward(WorkshopQuest quest) => false;
        public virtual void OnEvent(WorkshopQuest quest, GameEvent value)
        {
        }
    }
}

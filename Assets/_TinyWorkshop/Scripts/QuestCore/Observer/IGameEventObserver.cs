namespace TinyWorkshop.QuestCore
{
    // [KALIP: Observer] Rolü: Observer (dinleyici arayüzü)
    // Uygulayanlar: WorkshopQuest ve Objective.
    public interface IGameEventObserver
    {
        void OnGameEvent(GameEvent value);
    }
}

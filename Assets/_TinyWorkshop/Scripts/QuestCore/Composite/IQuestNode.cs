namespace TinyWorkshop.QuestCore
{
    // [KALIP: Composite] Rolü: Component (ortak arayüz)
    // Tek hedef (Objective = Leaf) ve hedef grubu (QuestGroup = Composite)
    // bu arayüzle aynı şekilde kullanılır: "tamamlandın mı?"
    public interface IQuestNode
    {
        bool IsComplete
        {
            get;
        }
    }
}

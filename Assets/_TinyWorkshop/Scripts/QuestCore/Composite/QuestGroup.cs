namespace TinyWorkshop.QuestCore
{
    // [KALIP: Composite] Rolü: Composite (bileşik düğüm)
    // Çocukları IQuestNode olduğu için hem hedef hem başka grup tutabilir.
    // Demoda: QuestGroup(Üret, Topla, Teslim Et)
    public sealed class QuestGroup : IQuestNode
    {
        readonly IQuestNode[] children;
        public QuestGroup(params IQuestNode[] children)
        {
            this.children = children;
        }
        // Grup, ancak tüm çocukları tamamsa tamamdır.
        public bool IsComplete
        {
            get
            {
                if (children.Length == 0) // Boş grup bilerek "tamam" sayılmaz
                    return false;
                foreach (var child in children)
                    if (!child.IsComplete) // Soruyu çocuklara devreder
                        return false;
                return true;
            }
        }
    }
}

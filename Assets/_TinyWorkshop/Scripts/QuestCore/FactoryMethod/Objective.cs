using System;
using System.Collections.Generic;
namespace TinyWorkshop.QuestCore
{
    // Tek bir hedef. Üç kalıpta birden rol alır:
    //  - Factory Method: Product (fabrikaların ürettiği soyut tip)
    //  - Composite: Leaf (yaprak), IQuestNode'u uygular
    //  - Observer: Observer, IGameEventObserver'ı uygular
    public abstract class Objective : IQuestNode, IGameEventObserver
    {
        // Sayılan dişli kimlikleri. HashSet: aynı dişli iki kez sayılmaz.
        readonly HashSet<int> seen = new HashSet<int>();
        public GameEventKind Kind // Bu hedefin dinlediği olay türü
        {
            get;
        }
        public int Target // Kaç tane gerekli (demoda 3)
        {
            get;
        }
        public int Count => seen.Count;
        // Composite yaprağı: kendi sayacına bakarak tamam mı söyler.
        public bool IsComplete => Count >= Target;
        protected Objective(GameEventKind kind, int target)
        {
            if (target < 1)
                throw new ArgumentOutOfRangeException(nameof(target));
            Kind = kind;
            Target = target;
        }
        // Observer: olay geldiğinde sadece kendi türündeyse sayar.
        public void OnGameEvent(GameEvent value)
        {
            if (value.Kind == Kind && !IsComplete)
                seen.Add(value.PartId);
        }
    }
}

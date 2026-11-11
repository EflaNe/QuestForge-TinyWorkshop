using System;
namespace TinyWorkshop.QuestCore
{
    // Görevin kendisi. Dört kalıbı bir araya getirir:
    //  - Factory Method: hedefleri oluşturucularla üretir (kurucu)
    //  - Composite: hedefleri bir QuestGroup'ta toplar (IsComplete)
    //  - Observer: yayıncıya abone olur (OnGameEvent)
    //  - State: State kalıbındaki Context; istekleri o anki duruma devreder
    public sealed class WorkshopQuest : IGameEventObserver, IDisposable
    {
        readonly GameEventPublisher publisher;
        readonly Objective produce, collect, deliver;
        readonly QuestGroup group;
        IQuestState state = new LockedState(); // Başlangıç durumu: Kilitli
        bool disposed;
        public event Action Changed;
        public int Produced => produce.Count;
        public int Collected => collect.Count;
        public int Delivered => deliver.Count;
        public bool IsComplete => group.IsComplete; // Composite: gruba sorulur
        public string StateName => state.Name;
        public WorkshopQuest(GameEventPublisher publisher, int target)
        {
            this.publisher = publisher;
            // Factory Method: somut hedefleri oluşturucular üretir.
            produce = new ProduceObjectiveCreator().Create(target);
            collect = new CollectObjectiveCreator().Create(target);
            deliver = new DeliverObjectiveCreator().Create(target);
            // Composite: üç hedef tek bir grup olarak ele alınır.
            group = new QuestGroup(produce, collect, deliver);
            // Observer: oyun olaylarını dinlemek için abone ol.
            publisher.Subscribe(this);
        }
        // State: kararı görev değil, o anki durum nesnesi verir.
        public bool Start() => !disposed && state.Start(this);
        public bool ClaimReward() => !disposed && state.ClaimReward(this);
        // Observer: yayıncı bir olay bildirdiğinde çağrılır.
        public void OnGameEvent(GameEvent value)
        {
            if (!disposed)
            {
                state.OnEvent(this, value); // State: olayı duruma devret
                Changed?.Invoke();
            }
        }
        // Sadece ActiveState çağırır: olayı her hedefe iletir,
        // her hedef yalnızca kendi türündeki olayı sayar.
        internal void AdvanceObjectives(GameEvent value)
        {
            produce.OnGameEvent(value);
            collect.OnGameEvent(value);
            deliver.OnGameEvent(value);
        }
        // State: durum geçişi. internal = sadece durum sınıfları çağırabilir.
        internal void Transition(IQuestState next)
        {
            state = next;
            Changed?.Invoke();
        }
        // Sahne kapanınca abonelikten çık (Observer'da sızıntıyı önler).
        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            publisher.Unsubscribe(this);
            Changed = null;
        }
    }
}

using System.Collections.Generic;
namespace TinyWorkshop.QuestCore
{
    // [KALIP: Observer] Rolü: Subject (yayıncı)
    // Oyun nesneleri (makine, dişli, istasyon) olayı buraya yayınlar;
    // görev sistemi abone olarak dinler. İki taraf birbirini tanımaz.
    public sealed class GameEventPublisher
    {
        readonly List<IGameEventObserver> observers = new List<IGameEventObserver>();
        public int ObserverCount => observers.Count;
        // Abone ol (aynı abone iki kez eklenmez).
        public void Subscribe(IGameEventObserver observer)
        {
            if (!observers.Contains(observer))
                observers.Add(observer);
        }
        // Abonelikten çık (görev Dispose edilince çağrılır).
        public void Unsubscribe(IGameEventObserver observer) => observers.Remove(observer);
        // Olayı tüm abonelere bildir.
        // ToArray(): bildirim sırasında biri abonelikten çıkarsa liste bozulmasın.
        public void Publish(GameEvent value)
        {
            foreach (var observer in observers.ToArray())
                observer.OnGameEvent(value);
        }
    }
}

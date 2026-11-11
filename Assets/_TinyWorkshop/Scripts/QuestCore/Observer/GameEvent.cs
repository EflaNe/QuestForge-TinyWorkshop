namespace TinyWorkshop.QuestCore
{
    // [KALIP: Observer] Yayınlanan olayın türü.
    public enum GameEventKind
    {
        PartProduced,  // Makine dişli üretti
        PartCollected, // Robot dişliyi topladı
        PartDelivered  // Dişli istasyona teslim edildi
    }
    // [KALIP: Observer] Abonelere gönderilen bildirim verisi.
    // PartId: dişlinin kimliği, aynı dişlinin iki kez sayılmasını önler.
    public readonly struct GameEvent
    {
        public GameEventKind Kind
        {
            get;
        }
        public int PartId
        {
            get;
        }
        public GameEvent(GameEventKind kind, int partId)
        {
            Kind = kind;
            PartId = partId;
        }
    }
}

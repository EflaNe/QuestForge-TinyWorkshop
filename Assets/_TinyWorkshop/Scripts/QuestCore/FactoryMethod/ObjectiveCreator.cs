namespace TinyWorkshop.QuestCore
{
    // [KALIP: Factory Method] Rolü: Creator (soyut oluşturucu)
    // Hangi somut hedefin üretileceğine alt sınıflar karar verir.
    // Kullanan kod sadece bu soyut tipi ve soyut Objective'i bilir.
    public abstract class ObjectiveCreator
    {
        // Fabrika metodu: dönüş tipi soyut Objective (Product).
        public abstract Objective Create(int target);
    }
}

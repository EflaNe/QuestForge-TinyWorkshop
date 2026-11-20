# Kurulum ve Çalıştırma Dokümanı

QuestForge · Tiny Workshop · EBLG443 Tasarım Kalıpları

Projeyi üç yoldan çalıştırabilirsiniz. Yalnızca oyunu denemek için **Yol A** yeterlidir; kaynak kodu ve testleri incelemek için **Yol B**, Unity kurmadan kalıp mantığını doğrulamak için **Yol C** kullanılır.

---

## Yol A · Hazır Windows sürümü (Unity gerekmez)

**Gereksinim:** Windows 10/11, 64 bit, DirectX 11/12 destekli ekran kartı.

1. GitHub deposunda **Releases** bölümünden en son sürümün `TinyWorkshop-Windows64.zip` dosyasını indirin.
2. Zip'i bir klasöre çıkarın. `TinyWorkshop.exe` dosyası `TinyWorkshop_Data`, `MonoBleedingEdge` ve `D3D12` klasörleriyle **aynı yerde kalmalıdır**.
3. `TinyWorkshop.exe` dosyasını çalıştırın. Windows SmartScreen uyarısı çıkarsa *Ek bilgi › Yine de çalıştır* seçin (imzasız öğrenci derlemesi).
4. Oyun `01_OneButton` sahnesiyle açılır. **F4** ile doğrudan görev sistemini kullanan ana sahneye (`04_MainGame`) geçin.

## Yol B · Unity Editor ile kaynaktan

**Gereksinimler**

| Araç | Sürüm |
| --- | --- |
| Unity Hub | güncel |
| Unity Editor | **6000.3.23f1** (Unity 6) · *Windows Build Support (Mono)* modülü |
| Git | herhangi bir güncel sürüm |

Paketler (`Packages/manifest.json` ile otomatik gelir): URP 2D, Input System, Test Framework, TextMeshPro, 2D Sprite.

**Adımlar**

```powershell
git clone https://github.com/EflaNe/QuestForge-TinyWorkshop.git
```

1. Unity Hub › **Add › Add project from disk** › klonlanan klasörü seçin.
2. Editor sürümü olarak **6000.3.23f1** seçin ve açın. İlk açılışta `Library/` oluşturulduğu için birkaç dakika sürebilir.
3. `Assets/_TinyWorkshop/Scenes/04_MainGame.unity` sahnesini açıp **Play**'e basın.
4. (İsteğe bağlı) Sahneler kod ile üretilir: **Tiny Workshop › Build or Rebuild All Scenes** menüsü dört sahneyi ve prefabları yeniden oluşturur.

**Windows derlemesi almak:** *File › Build Profiles › Windows* › **Build**. Sahne listesi hazırdır (01–04).

## Yol C · Unity olmadan kalıp çekirdeği (.NET)

QuestCore saf C# olduğu için Unity'siz derlenip çalıştırılabilir.

**Gereksinim:** [.NET 8 SDK](https://dotnet.microsoft.com/download)

```powershell
dotnet run --project Tools/QuestCore.Checks
# Beklenen çıktı: PASS: 17 QuestCore behavioral checks
```

Bu proje `Assets/_TinyWorkshop/Scripts/QuestCore/**/*.cs` dosyalarını doğrudan derler; yani Unity'deki ile **aynı kaynak** test edilir.

---

## Oynanış (04_MainGame)

1. **E** · vardiyayı başlat (*Locked → Active*)
2. Makinenin turuncu düğmesine **3 kez tıkla** · dişli üretilir (`PartProduced`)
3. **Space** ile zıpla, dişlilere dokun · toplanır (`PartCollected`)
4. Sağdaki istasyonda **E** · teslim (`PartDelivered`); 3/3 · 3/3 · 3/3 olunca *Completed*
5. Tekrar **E** · ödül alınır (*RewardClaimed*)

| Tuş | İşlev |
| --- | --- |
| A / D, ← / → | Hareket |
| Space | Zıpla |
| E | Etkileşim |
| R | Sahneyi yeniden başlat |
| Tab | Menü |
| F1 – F4 | Sahne değiştir (01 Tek Düğme · 02 Hareket · 03 Fizik · 04 Ana Oyun) |

## Sık karşılaşılan sorunlar

| Belirti | Çözüm |
| --- | --- |
| Proje açılırken "different Editor version" uyarısı | Hub'dan 6000.3.23f1 kurun; başka 6000.x sürümleri genelde çalışır ama test edilmemiştir. |
| Sahne pembe/boş görünüyor | Doğru sahneyi açtığınızdan emin olun (`Assets/_TinyWorkshop/Scenes`); gerekirse *Tiny Workshop › Build or Rebuild All Scenes*. |
| Klavye girdisi çalışmıyor | *Project Settings › Player › Active Input Handling* = **Input System Package (New)** olmalı (depoda böyle ayarlıdır). |
| `.exe` açılmıyor | Zip'in tamamen çıkarıldığından ve `TinyWorkshop_Data` klasörünün yanında olduğundan emin olun. |
| `dotnet run` bulunamadı | .NET 8 SDK kurun, terminali yeniden açın. |

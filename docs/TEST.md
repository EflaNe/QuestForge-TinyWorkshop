# Test Dokümanı

QuestForge · Tiny Workshop · EBLG443 Tasarım Kalıpları

Testler üç katmandadır: kalıp çekirdeğinin birim testleri (Unity EditMode ve .NET), sahne bütünlüğü testleri (EditMode) ve oyunu gerçekten oynatan entegrasyon testleri (PlayMode).

## 1. Son çalıştırma sonuçları

**Tarih:** 20.11.2026 · **Ortam:** Windows 11, Unity 6000.3.23f1, .NET 8 SDK

| Paket | Toplam | Geçen | Kalan | Süre |
| --- | ---: | ---: | ---: | ---: |
| Unity EditMode (`TinyWorkshop.EditModeTests`) | 11 | 11 | 0 | 2,3 sn |
| Unity PlayMode (`TinyWorkshop.PlayModeTests`) | 13 | 13 | 0 | 37,7 sn |
| .NET (`Tools/QuestCore.Checks`) | 17 kontrol | 17 | 0 | < 1 sn |
| **Toplam** | **41** | **41** | **0** | |

## 2. Testleri çalıştırma

**Unity içinde**

1. Projeyi Unity 6000.3.23f1 ile açın (bkz. [KURULUM.md](KURULUM.md)).
2. **Window › General › Test Runner**.
3. **EditMode** sekmesi › *Run All* → 11 test.
4. **PlayMode** sekmesi › *Run All* → 13 test (sahneleri sırayla yükler, yaklaşık 40 sn).

**Komut satırından (Unity Editor kapalıyken)**

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath . `
  -runTests -testPlatform EditMode -testResults results-editmode.xml
& "C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath . `
  -runTests -testPlatform PlayMode -testResults results-playmode.xml
```

**Unity olmadan (yalnızca kalıp çekirdeği)**

```powershell
dotnet run --project Tools/QuestCore.Checks
# PASS: 17 QuestCore behavioral checks
```

## 3. Kalıp birim testleri · `Tests/EditMode/QuestCoreTests.cs`

Her test, bir kalıbın sorumlu olduğu kuralı doğrular. Hepsi saf C#'tır; sahne yüklemez.

| # | Test | Kalıp | Senaryo | Beklenen sonuç |
| --- | --- | --- | --- | --- |
| T1 | `FactoriesCreateMatchingObjectiveKinds` | Factory Method | Her `ConcreteCreator.Create(3)` çağrılır | Doğru somut ürün (`ProduceObjective`, `CollectObjective`, `DeliverObjective`) ve doğru olay türü döner |
| T2 | `InvalidTargetIsRejected` | Factory Method | `Create(0)` | `ArgumentOutOfRangeException` |
| T3 | `EmptyCompositeIsNotACompletedQuest` | Composite | Çocuksuz `QuestGroup` | `IsComplete == false` |
| T4 | `AllObjectivesRequiredAndRewardClaimedExactlyOnce` | Composite + State | Üret ve topla 3/3, teslim 0/3; sonra teslim 3/3 | Teslim bitmeden *Active* kalır ve ödül reddedilir; üçü bitince *Completed*; ödül yalnızca bir kez alınır; *RewardClaimed* durumunda yeni olay sayılmaz |
| T5 | `RepeatedEventsAdvanceEachObjectiveOnlyOnce` | Observer | Aynı dişli için 10 kez `PartProduced` yayınlanır | `Produced == 1`, diğer hedefler etkilenmez |
| T6 | `DisposeRemovesObserverAndDisablesQuest` | Observer | Görev iki kez `Dispose` edilir | Yayıncıda abone kalmaz (`ObserverCount == 0`), görev artık başlatılamaz |
| T7 | `LockedQuestIgnoresEventsAndRejectsReward` | State | Başlatılmamış göreve olay gelir, ödül istenir | Olay sayılmaz, ödül reddedilir, durum *Locked* |

`Tools/QuestCore.Checks/Program.cs` aynı kuralları (ve ek olarak "aktif görev yeniden başlatılamaz", "yalnızca üretim yetmez") 17 kontrol ile Unity dışında tekrar doğrular.

## 4. Sahne bütünlüğü · `Tests/EditMode/SceneIntegrityTests.cs`

| # | Test | Senaryo | Beklenen sonuç |
| --- | --- | --- | --- |
| S1–S4 | `SavedSceneHasRequiredReferencesAndPersistentNavigation("01…04")` | Her kayıtlı sahne açılır | `WorkshopSession`, HUD, geri bildirim, kamera ve dişli prefabı bağlı; hiçbir nesnede eksik script yok; her düğmenin kalıcı `onClick` bağlantısı var; ana sahnede robot, makine ve istasyon bağlı |

## 5. Oyun entegrasyon testleri · `Tests/PlayMode/GameplayTests.cs`

Testler sahneyi gerçekten yükler, girdiyi kodla verir ve fizik adımlarını bekler.

| # | Test | Neyi doğrular |
| --- | --- | --- |
| P1 | `MainGameCompletesThroughRealProductionPickupAndDelivery` | **Dört kalıbın uçtan uca akışı:** makine dişli üretir → robot dişlileri toplar → istasyonda teslim eder → ödül alınır. Görev *Locked → Active → Completed → RewardClaimed* geçişlerini sırasıyla yapar |
| P2 | `MainGameGearsLandOnPlatformsAndStay` | Üretilen dişliler platformlara düşer ve ulaşılabilir kalır |
| P3 | `OneButtonRequiresNinePressesAndCompletes` | 01 sahnesi: dokuz basışta tamamlanır, öncesinde tamamlanmaz |
| P4 | `MovementAcceleratesBrakesAndHasVariableJump` | İvmelenme, frenleme ve değişken zıplama yüksekliği |
| P5 | `CoyoteTimeAcceptsJumpAfterLeavingLedge` | Kenardan düştükten kısa süre sonra zıplama kabul edilir |
| P6 | `JumpBufferAcceptsPressBeforeLanding` | Yere inmeden hemen önce basılan zıplama kaybolmaz |
| P7 | `FallingRobotReturnsToSafePosition` | Boşluğa düşen robot güvenli noktaya döner |
| P8 | `SafePositionTracksLastPlatformDuringMovement` | Güvenli nokta son basılan platformla güncellenir |
| P9–P11 | `MovementCourseCompletesWith{Normal,Heavy,Light}Profile` | 02 parkuru üç hareket profiliyle de bitirilebilir |
| P12–P13 | `Physics{Natural,Designed}ModeCanHitAllThreeTargets` | 03 sahnesinde her iki fizik modunda üç hedef de vurulabilir |

## 6. Elle kabul testi (Windows derlemesi)

| # | Adım | Beklenen |
| --- | --- | --- |
| M1 | `TinyWorkshop.exe` çalıştırılır, **F4** | Ana sahne açılır, görev *Locked* |
| M2 | Makine düğmesine tıklanır (görev başlamadan) | Görev ilerlemez |
| M3 | **E** | Görev *Active*, hedefler 0/3 |
| M4 | Düğmeye 3 tık × 3 | *Üret 3/3* |
| M5 | Aynı dişliye tekrar dokunulur | Sayaç artmaz |
| M6 | 3 dişli toplanıp istasyonda **E** | *Teslim 3/3*, görev *Completed* |
| M7 | **E** | *RewardClaimed*; tekrar **E** ödülü ikinci kez vermez |
| M8 | **R** | Görev sıfırdan *Locked* başlar |

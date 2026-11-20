# Tasarım Kalıpları · Sınıf Diyagramları

QuestForge · EBLG443 Tasarım Kalıpları

Dört GoF kalıbı `Assets/_TinyWorkshop/Scripts/QuestCore/` altında, kalıp adıyla klasörlenmiştir. Her dosyanın başında `// [KALIP: <ad>] Rolü: <rol>` yorumu vardır; kodda aramak için:

```powershell
git grep -n "\[KALIP:"
```

| Grup | Kalıp | Klasör | Çözdüğü problem |
| --- | --- | --- | --- |
| Yaratımsal | Factory Method | `QuestCore/FactoryMethod/` | Görev, somut hedef sınıflarını bilmeden hedef üretir |
| Yapısal | Composite | `QuestCore/Composite/` | Tek hedef ve hedef grubu aynı arayüzle "tamam mı?" sorusuna cevap verir |
| Davranışsal | Observer | `QuestCore/Observer/` | Oyun nesneleri ve görev sistemi birbirini tanımadan haberleşir |
| Davranışsal | State | `QuestCore/State/` | Görev durumuna göre değişen davranış `if/else` yerine durum sınıflarındadır |

Öneri belgesindeki "eşya toplama / bölgeye ulaşma / düşman yenme" hedefleri, demodaki atölye temasına **üret / topla / teslim et** olarak uyarlanmıştır; kalıplar ve rolleri birebir aynıdır.

---

## 1. Factory Method

```mermaid
classDiagram
    class ObjectiveCreator {
        <<abstract>>
        +Create(int target) Objective*
    }
    class ProduceObjectiveCreator
    class CollectObjectiveCreator
    class DeliverObjectiveCreator
    class Objective {
        <<abstract>>
        +Kind GameEventKind
        +Target int
        +IsComplete bool
    }
    ObjectiveCreator <|-- ProduceObjectiveCreator
    ObjectiveCreator <|-- CollectObjectiveCreator
    ObjectiveCreator <|-- DeliverObjectiveCreator
    Objective <|-- ProduceObjective
    Objective <|-- CollectObjective
    Objective <|-- DeliverObjective
    ProduceObjectiveCreator ..> ProduceObjective : creates
    CollectObjectiveCreator ..> CollectObjective : creates
    DeliverObjectiveCreator ..> DeliverObjective : creates
    WorkshopQuest ..> ObjectiveCreator : client
```

| Rol | Tip | Dosya |
| --- | --- | --- |
| Creator | `ObjectiveCreator` | `FactoryMethod/ObjectiveCreator.cs` |
| ConcreteCreator | `ProduceObjectiveCreator`, `CollectObjectiveCreator`, `DeliverObjectiveCreator` | `FactoryMethod/*Creator.cs` |
| Product | `Objective` | `FactoryMethod/Objective.cs` |
| ConcreteProduct | `ProduceObjective`, `CollectObjective`, `DeliverObjective` | `FactoryMethod/*Objective.cs` |
| Client | `WorkshopQuest` (kurucu) | `QuestCore/WorkshopQuest.cs` |

**Kalıp kaldırılırsa:** `WorkshopQuest` her hedef türü için `new` ile somut sınıfa bağlanır; yeni hedef eklemek görev sınıfını değiştirmeyi gerektirir.

## 2. Composite

```mermaid
classDiagram
    class IQuestNode {
        <<interface>>
        +IsComplete bool
    }
    class Objective {
        <<abstract>>
        +IsComplete bool
    }
    class QuestGroup {
        -IQuestNode[] children
        +IsComplete bool
    }
    IQuestNode <|.. Objective : Leaf
    IQuestNode <|.. QuestGroup : Composite
    QuestGroup o-- "0..*" IQuestNode : children
```

| Rol | Tip | Dosya |
| --- | --- | --- |
| Component | `IQuestNode` | `Composite/IQuestNode.cs` |
| Leaf | `Objective` (+ alt sınıfları) | `FactoryMethod/Objective.cs` |
| Composite | `QuestGroup` | `Composite/QuestGroup.cs` |

**Kalıp kaldırılırsa:** tamamlanma kontrolü `produce.IsComplete && collect.IsComplete && ...` olarak sabit yazılır; iç içe alt görevler desteklenemez.

## 3. Observer

```mermaid
classDiagram
    class GameEventPublisher {
        -List~IGameEventObserver~ observers
        +Subscribe(IGameEventObserver)
        +Unsubscribe(IGameEventObserver)
        +Publish(GameEvent)
    }
    class IGameEventObserver {
        <<interface>>
        +OnGameEvent(GameEvent)
    }
    class GameEvent {
        +Kind GameEventKind
        +PartId int
    }
    GameEventPublisher o-- "0..*" IGameEventObserver
    IGameEventObserver <|.. WorkshopQuest
    IGameEventObserver <|.. Objective
    GameEventPublisher ..> GameEvent
    WorkshopSession ..> GameEventPublisher : Publish(...)
    WorkshopMachine ..> WorkshopSession : Produce()
    Gear ..> WorkshopSession : TryCollect()
    RepairStation ..> WorkshopSession : Deliver()
```

Unity tarafında olayları `WorkshopSession` yayınlar (`Produce` → `PartProduced`, `TryCollect` → `PartCollected`, `Deliver` → `PartDelivered`); makine, dişli ve istasyon yalnızca oturumu çağırır, görev sınıfını hiç tanımaz.

| Rol | Tip | Dosya |
| --- | --- | --- |
| Subject | `GameEventPublisher` | `Observer/GameEventPublisher.cs` |
| Observer | `IGameEventObserver` | `Observer/IGameEventObserver.cs` |
| ConcreteObserver | `WorkshopQuest`, `Objective` | `QuestCore/WorkshopQuest.cs`, `FactoryMethod/Objective.cs` |
| Bildirim | `GameEvent` | `Observer/GameEvent.cs` |

Abone listesi ve bildirim döngüsü elle yazılmıştır; C#'ın `event`/`IObservable<T>` yapısı kalıp için **kullanılmamıştır** (yalnızca HUD'u yenilemek için `WorkshopQuest.Changed` vardır, kalıp sayımına dâhil değildir).

**Kalıp kaldırılırsa:** oyun tarafı `WorkshopQuest`'i doğrudan çağırmak zorunda kalır; görev sistemi Unity nesnelerine bağımlı olur.

## 4. State

```mermaid
classDiagram
    class IQuestState {
        <<interface>>
        +Name string
        +Start(WorkshopQuest) bool
        +ClaimReward(WorkshopQuest) bool
        +OnEvent(WorkshopQuest, GameEvent)
    }
    class QuestState {
        <<abstract>>
    }
    class WorkshopQuest {
        -IQuestState state
        +Start() bool
        +ClaimReward() bool
        +OnGameEvent(GameEvent)
        ~Transition(IQuestState)
    }
    IQuestState <|.. QuestState
    QuestState <|-- LockedState
    QuestState <|-- ActiveState
    QuestState <|-- CompletedState
    QuestState <|-- RewardClaimedState
    WorkshopQuest o-- IQuestState : state
```

```mermaid
stateDiagram-v2
    [*] --> Locked
    Locked --> Active : Start()
    Active --> Completed : QuestGroup.IsComplete
    Completed --> RewardClaimed : ClaimReward()
    RewardClaimed --> [*]
```

| Rol | Tip | Dosya |
| --- | --- | --- |
| Context | `WorkshopQuest` | `QuestCore/WorkshopQuest.cs` |
| State | `IQuestState` (+ varsayılan davranışlı `QuestState`) | `State/IQuestState.cs` |
| ConcreteState | `LockedState`, `ActiveState`, `CompletedState`, `RewardClaimedState` | `State/*State.cs` |

**Kalıp kaldırılırsa:** `Start`, `ClaimReward` ve `OnGameEvent` metotlarının her biri `if (state == "...")` dallanmasına döner.

## 5. Kalıpların birlikte çalışması (bir dişli toplanınca)

```mermaid
sequenceDiagram
    participant G as Gear / WorkshopSession (Unity)
    participant P as GameEventPublisher
    participant Q as WorkshopQuest
    participant S as ActiveState
    participant O as Objective x3
    participant C as QuestGroup
    G->>P: Publish(PartCollected, id)
    P->>Q: OnGameEvent(e)
    Q->>S: OnEvent(quest, e)
    S->>Q: AdvanceObjectives(e)
    Q->>O: OnGameEvent(e) (yalnızca kendi türünü sayar)
    S->>C: quest.IsComplete
    C-->>S: tüm çocuklar tamam mı?
    S->>Q: Transition(new CompletedState())
```

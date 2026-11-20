using System.Collections.Generic;
using TinyWorkshop.QuestCore;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace TinyWorkshop
{
    // One per scene. Owns the shared state (gears, quest, coins, profiles) and the scene's rules;
    // components report to it and the HUD reads from it. Keyboard shortcuts live in WorkshopHotkeys.
    public sealed class WorkshopSession : MonoBehaviour
    {
        const int BagCapacity = 3;
        const int GearsPerShift = 3;
        const int LabGearLimit = 18;
        const int TargetsToHit = 3;
        static readonly string[] SceneNames = { "01_OneButton", "02_Movement", "03_Physics", "04_MainGame" };

        [Header("Scene")]
        public WorkshopMode mode;
        public RobotController2D Robot;
        public WorkshopMachine Machine;
        public RepairStation Station;
        public WorkshopFeedback Feedback;
        public WorkshopHud Hud;
        public Camera worldCamera;
        public SpriteRenderer[] lamps, lampGlows;

        [Header("Profiles (keys 1 / 2 / 3)")]
        public MovementProfile[] movementProfiles;
        public ProductionProfile[] productionProfiles;

        [Header("Gears")]
        public Gear gearPrefab;
        public Vector2 safeGearPoint = new Vector2(-3.8f, -3.3f);
        [Tooltip("Main game: landing spots (gear centre) cycled per produced gear; mostly platforms so jumping matters.")]
        public Vector2[] gearDrops = { new Vector2(-1.65f, -2.42f), new Vector2(3.5f, -2.17f), new Vector2(.9f, -1.32f) };
        [Tooltip("Extra height of the launch arc above the higher of outlet / landing spot.")]
        public float dropArcHeight = 1.6f;

        [Header("Physics lab")]
        public Transform launcher;
        public Vector2 launchVelocity = new Vector2(4, 6);

        [Header("Economy (main game)")]
        public int coinsPerGear = 10;
        public int rewardCoins = 50;

        readonly List<int> carried = new List<int>();
        readonly List<Gear> worldGears = new List<Gear>();
        GameEventPublisher publisher;
        WorkshopQuest quest;
        RobotPresentation robotLook;
        int nextGearId, produced, hitTargets;
        float nextLaunchTime, toastUntil, rewardClaimedAt;
        string toast;

        // ---------------------------------------------------------------- state for HUD / tests
        public int ProfileIndex { get; private set; }
        public bool DemoComplete { get; private set; }
        public bool DesignedPhysics { get; private set; }
        public int Coins { get; private set; }
        public bool QuestActive => quest != null && quest.StateName == "Active";
        public bool RewardClaimed => quest != null && quest.StateName == "RewardClaimed";
        public string QuestState => quest?.StateName ?? "";
        public int Produced => quest?.Produced ?? produced;
        public int Collected => quest?.Collected ?? 0;
        public int Delivered => quest?.Delivered ?? 0;
        public int Carried => carried.Count;
        public bool CanCarryMore => QuestActive && carried.Count < BagCapacity;
        public int WorldGearCount => worldGears.Count;
        public IReadOnlyList<Gear> WorldGears => worldGears;
        public int HitTargets => hitTargets;
        public string Toast => Time.time < toastUntil ? toast : "";

        // ---------------------------------------------------------------- lifecycle
        void Awake()
        {
            publisher = new GameEventPublisher();
            if (mode == WorkshopMode.MainGame)
                quest = new WorkshopQuest(publisher, GearsPerShift);
            if (Robot)
            {
                robotLook = Robot.GetComponent<RobotPresentation>();
                Robot.Respawned += OnRobotRespawned;
            }
        }

        void Start()
        {
            SelectProfile(0);
            Feedback.Play("start", .45f);
        }

        void OnDestroy()
        {
            quest?.Dispose();
            if (Robot)
                Robot.Respawned -= OnRobotRespawned;
        }

        void Update()
        {
            if (RewardClaimed)
                LightUpLamps();
            Hud.Refresh();
        }

        void OnRobotRespawned() => Notify("Güvenli noktaya döndün.");

        // Workshop "wakes up": lamps turn on one after another.
        void LightUpLamps()
        {
            for (int i = 0; i < lamps.Length; i++)
            {
                float t = Mathf.Clamp01((Time.time - rewardClaimedAt - i * .24f) * 3);
                lamps[i].color = mode == WorkshopMode.MainGame ? new Color(1, .86f, .55f, t * .55f) : Color.Lerp(new Color(.5f, .6f, .55f), Color.white, t);
                lampGlows[i].color = new Color(1, .79f, .4f, t * .16f);
            }
        }

        // ---------------------------------------------------------------- main game: E key
        public void Interact()
        {
            if (quest == null)
                return;
            if (quest.StateName == "Locked")
                StartShift();
            else if (!Station || !Station.IsNear)
                Notify("Teslim etmek için sağdaki tamir istasyonuna yaklaş.");
            else if (quest.StateName == "Completed")
                ClaimReward();
            else
                Station.Interact();
        }

        void StartShift()
        {
            quest.Start();
            Feedback.Play("start");
            Feedback.Shake(.06f);
            Feedback.Burst(Robot.transform.position, 10, new Color(1, .8f, .45f));
        }

        void ClaimReward()
        {
            if (!quest.ClaimReward())
                return;
            rewardClaimedAt = Time.time;
            Coins += rewardCoins;
            Hud.Floater(Station.transform.position + Vector3.up * 3.2f, "+" + rewardCoins, new Color(1, .82f, .3f));
            RobotEmote(1.4f);
            Feedback.Play("success");
            Feedback.Burst(Station.transform.position, 36, new Color(.7f, 1, .65f));
            Feedback.Shake(.15f);
            Feedback.Hitstop(.1f);
            Station.Punch(.3f);
        }

        // ---------------------------------------------------------------- gears: produce, collect, deliver
        public void Produce(Vector2 position, Vector2 velocity)
        {
            if (worldGears.Count >= GearsPerShift)
                return;
            SpawnGear(position, velocity, mode == WorkshopMode.MainGame);
            produced++;
            publisher.Publish(new GameEvent(GameEventKind.PartProduced, nextGearId));
            if (mode == WorkshopMode.OneButton && produced >= GearsPerShift)
                CompleteDemo();
        }

        // Ballistic launch that lands on the next drop spot (low, right, middle platform).
        // Pick the arc's apex height, then: vy = sqrt(2 g h), flight time = rise + fall, vx = dx / time.
        public Vector2 DropVelocity(Vector2 from)
        {
            if (gearDrops == null || gearDrops.Length == 0)
                return launchVelocity;
            var to = gearDrops[produced % gearDrops.Length] + new Vector2(Random.Range(-.25f, .25f), 0);
            float gravityScale = gearPrefab ? gearPrefab.GetComponent<Rigidbody2D>().gravityScale : 1;
            float g = Mathf.Abs(Physics2D.gravity.y) * gravityScale;
            float apex = Mathf.Max(from.y, to.y) + dropArcHeight + Mathf.Abs(to.x - from.x) * .06f;
            float vy = Mathf.Sqrt(2 * g * (apex - from.y));
            float time = vy / g + Mathf.Sqrt(2 * (apex - to.y) / g);
            return new Vector2((to.x - from.x) / time, vy);
        }

        Gear SpawnGear(Vector2 position, Vector2 velocity, bool collectable)
        {
            var gear = Instantiate(gearPrefab, position, Quaternion.identity);
            gear.name = "Gear_" + (++nextGearId);
            gear.Initialize(this, nextGearId, velocity, collectable);
            worldGears.Add(gear);
            return gear;
        }

        public bool TryCollect(Gear gear)
        {
            if (!CanCarryMore || !worldGears.Contains(gear))
                return false;
            carried.Add(gear.Id);
            worldGears.Remove(gear);
            publisher.Publish(new GameEvent(GameEventKind.PartCollected, gear.Id));
            if (robotLook)
                robotLook.PickupReaction();
            return true;
        }

        public void RemoveWorldGear(Gear gear) => worldGears.Remove(gear);

        public void Deliver()
        {
            if (!QuestActive)
                return;
            if (carried.Count == 0)
            {
                Deny("Çantanda dişli yok. Önce dişlileri topla.");
                return;
            }
            int count = carried.Count;
            foreach (int id in carried)
                publisher.Publish(new GameEvent(GameEventKind.PartDelivered, id));
            carried.Clear();
            PlayDeliveryAnimation(count);
            RobotEmote(1);
            Notify(quest.StateName == "Completed" ? "Tamir tamam! Atölyeyi uyandırmak için tekrar E." : "Dişliler teslim edildi.");
        }

        // Progress is already recorded; the gears visibly fly into the station one by one as the payoff.
        void PlayDeliveryAnimation(int count)
        {
            var gearSprite = gearPrefab ? gearPrefab.GetComponentInChildren<SpriteRenderer>().sprite : null;
            var stationSprite = Station.GetComponent<SpriteRenderer>();
            Vector2 from = Robot.transform.position + Vector3.up * .4f;
            Vector2 to = stationSprite ? (Vector2)stationSprite.bounds.center : (Vector2)Station.transform.position + Vector2.up * .3f;
            for (int i = 0; i < count; i++)
            {
                int index = i;
                Feedback.FlyTo(gearSprite, from, to, i * .12f, () => OnGearArrived(to, index));
            }
        }

        void OnGearArrived(Vector2 at, int index)
        {
            if (!Station)
                return;
            Station.Confirm();
            Coins += coinsPerGear;
            Hud.Floater(at + Vector2.up * 1.4f, "+" + coinsPerGear, new Color(1, .82f, .3f));
            Feedback.Play("deliver", .8f, 1 + index * .15f);
            Feedback.Burst(at, 10, new Color(.65f, 1, .7f));
            Feedback.Shake(.05f);
        }

        void RobotEmote(float duration)
        {
            if (robotLook)
                robotLook.Emote(duration);
        }

        // ---------------------------------------------------------------- physics lab
        public void Launch()
        {
            if (Time.time < nextLaunchTime || DemoComplete)
                return;
            if (worldGears.Count >= LabGearLimit)
                RemoveOldestGear();
            nextLaunchTime = Time.time + .35f;
            SpawnGear(launcher.position, launchVelocity, false);
            Feedback.Play("produce", .6f);
            Feedback.Burst(launcher.position, 8, new Color(1, .75f, .4f));
        }

        void RemoveOldestGear()
        {
            var gear = worldGears[0];
            worldGears.RemoveAt(0);
            if (gear)
                Destroy(gear.gameObject);
        }

        public void HitTarget()
        {
            hitTargets++;
            Feedback.Play("collect");
            if (hitTargets >= TargetsToHit)
                CompleteDemo();
        }

        public void TogglePhysics()
        {
            DesignedPhysics = !DesignedPhysics;
            Notify(DesignedPhysics ? "Tasarlanmış fizik: çarpışma sonrası hız düzeltilir." : "Doğal fizik: Physics Material 2D sekmesi.");
        }

        // ---------------------------------------------------------------- labs: settings
        public void CompleteDemo()
        {
            if (DemoComplete)
                return;
            DemoComplete = true;
            Feedback.Play("success", .7f);
        }

        // Same index picks the movement feel and the production rhythm (Normal / second / third).
        public void SelectProfile(int index)
        {
            ProfileIndex = Mathf.Clamp(index, 0, 2);
            if (Robot && ProfileIndex < movementProfiles.Length)
                Robot.SetProfile(movementProfiles[ProfileIndex]);
            if (Machine && ProfileIndex < productionProfiles.Length)
                Machine.SetProfile(productionProfiles[ProfileIndex]);
        }

        public void ToggleAssistance()
        {
            if (!Robot)
                return;
            Robot.assistance = !Robot.assistance;
            Notify(Robot.assistance ? "Coyote time ve jump buffer açık." : "Coyote time ve jump buffer kapalı.");
        }

        // ---------------------------------------------------------------- messages
        public void Notify(string message)
        {
            toast = message;
            toastUntil = Time.time + 2.8f;
        }

        public void Deny(string message)
        {
            Notify(message);
            Feedback.Play("denied", .6f);
        }

        // ---------------------------------------------------------------- scenes (also bound to UI buttons)
        public void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        public void OpenScene(int index) => SceneManager.LoadScene(SceneNames[index]);
        public void OpenProduction() => OpenScene(0);
        public void OpenMovement() => OpenScene(1);
        public void OpenPhysics() => OpenScene(2);
        public void OpenMain() => OpenScene(3);
        public void SelectNormal() => SelectProfile(0);
        public void SelectSecond() => SelectProfile(1);
        public void SelectThird() => SelectProfile(2);
    }
}

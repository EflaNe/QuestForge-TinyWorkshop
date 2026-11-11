using System.Collections.Generic;
using TinyWorkshop.QuestCore;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
namespace TinyWorkshop
{
    public sealed class WorkshopSession : MonoBehaviour
    {
        public WorkshopMode mode;
        public RobotController2D Robot;
        public WorkshopMachine Machine;
        public RepairStation Station;
        public WorkshopFeedback Feedback;
        public WorkshopHud Hud;
        public Camera worldCamera;
        public Gear gearPrefab;
        public Transform launcher;
        public Vector2 safeGearPoint = new Vector2(-3.8f, -3.3f);
        public SpriteRenderer[] lamps, lampGlows;
        public Vector2 launchVelocity = new Vector2(4, 6);
        [Tooltip("Main game: landing spots (gear centre) cycled per produced gear; mostly platforms so jumping matters.")]
        public Vector2[] gearDrops = { new Vector2(-1.65f, -2.42f), new Vector2(3.5f, -2.17f), new Vector2(.9f, -1.32f) };
        [Tooltip("Extra height of the launch arc above the higher of outlet / landing spot.")]
        public float dropArcHeight = 1.6f;
        readonly List<int> carried = new List<int>();
        readonly List<Gear> worldGears = new List<Gear>();
        GameEventPublisher publisher;
        WorkshopQuest quest;
        int nextId, produced, hitTargets;
        float launchAt, toastUntil, completedAt;
        string toast;
        public bool DemoComplete
        {
            get; private set;
        }
        public bool DesignedPhysics
        {
            get; private set;
        }
        public bool QuestActive => quest != null && quest.StateName == "Active";
        public string QuestState => quest?.StateName ?? "";
        public int Produced => quest?.Produced ?? produced;
        public int Collected => quest?.Collected ?? 0;
        public int Delivered => quest?.Delivered ?? 0;
        public int Carried => carried.Count;
        public int WorldGearCount => worldGears.Count;
        public bool CanCarryMore => QuestActive && carried.Count < 3;
        public IReadOnlyList<Gear> WorldGears => worldGears;
        public int HitTargets => hitTargets;
        [Tooltip("Main game: coins per delivered gear and for waking the workshop (display economy, GDD hook).")]
        public int coinsPerGear = 10, rewardCoins = 50;
        public int Coins
        {
            get; private set;
        }
        public string Toast => Time.time < toastUntil ? toast : "";
        public bool RewardClaimed => quest != null && quest.StateName == "RewardClaimed";
        void Awake()
        {
            publisher = new GameEventPublisher();
            if (mode == WorkshopMode.MainGame)
                quest = new WorkshopQuest(publisher, 3);
        }
        void Start()
        {
            Feedback.Play("start", .45f);
        }
        void OnDestroy()
        {
            quest?.Dispose();
        }
        void Update()
        {
            var k = Keyboard.current;
            if (k != null)
            {
                if (k.f1Key.wasPressedThisFrame)
                    OpenScene(0);
                if (k.f2Key.wasPressedThisFrame)
                    OpenScene(1);
                if (k.f3Key.wasPressedThisFrame)
                    OpenScene(2);
                if (k.f4Key.wasPressedThisFrame)
                    OpenScene(3);
                if (k.rKey.wasPressedThisFrame && mode != WorkshopMode.OneButton)
                    Restart();
                if (k.mKey.wasPressedThisFrame)
                    Feedback.ToggleMute();
                if (k.vKey.wasPressedThisFrame)
                    Feedback.shakeEnabled = !Feedback.shakeEnabled;
                if (k.tabKey.wasPressedThisFrame || k.escapeKey.wasPressedThisFrame)
                    Hud.ToggleMenu();
                if (mode == WorkshopMode.MainGame && k.eKey.wasPressedThisFrame)
                    Interact();
                if (mode == WorkshopMode.Physics)
                {
                    if (k.tKey.wasPressedThisFrame)
                        TogglePhysics();
                    if (k.spaceKey.wasPressedThisFrame)
                        Launch();
                }
                if (mode == WorkshopMode.Movement || mode == WorkshopMode.OneButton)
                {
                    if (k.digit1Key.wasPressedThisFrame)
                        SelectProfile(0);
                    if (k.digit2Key.wasPressedThisFrame)
                        SelectProfile(1);
                    if (k.digit3Key.wasPressedThisFrame)
                        SelectProfile(2);
                    if (mode == WorkshopMode.Movement && k.gKey.wasPressedThisFrame)
                        ToggleAssistance();
                }
            }
            if (RewardClaimed)
            {
                for (int i = 0; i < lamps.Length; i++)
                {
                    float t = Mathf.Clamp01((Time.time - completedAt - i * .24f) * 3);
                    lamps[i].color = mode == WorkshopMode.MainGame ? new Color(1, .86f, .55f, t * .55f) : Color.Lerp(new Color(.5f, .6f, .55f), Color.white, t);
                    lampGlows[i].color = new Color(1, .79f, .4f, t * .16f);
                }
            }
            Hud.Refresh();
        }
        public void Interact()
        {
            if (quest == null)
                return;
            if (quest.StateName == "Locked")
            {
                quest.Start();
                Feedback.Play("start");
                Feedback.Shake(.06f);
                Feedback.Burst(Robot.transform.position, 10, new Color(1, .8f, .45f));
                return;
            }
            if (Station && Station.IsNear)
            {
                if (quest.StateName == "Completed")
                {
                    if (quest.ClaimReward())
                    {
                        completedAt = Time.time;
                        Coins += rewardCoins;
                        Hud.Floater(Station.transform.position + Vector3.up * 3.2f, "+" + rewardCoins, new Color(1, .82f, .3f));
                        Robot.Emote(1.4f);
                        Feedback.Play("success");
                        Feedback.Burst(Station.transform.position, 36, new Color(.7f, 1, .65f));
                        Feedback.Shake(.15f);
                        Feedback.Hitstop(.1f);
                        Station.Punch(.3f);
                    }
                }
                else
                    Station.Interact();
            }
            else
                Notify("Teslim etmek için sağdaki tamir istasyonuna yaklaş.");
        }
        public void Produce(Vector2 position, Vector2 velocity)
        {
            if (worldGears.Count >= 3)
                return;
            SpawnGear(position, velocity, mode == WorkshopMode.MainGame);
            produced++;
            publisher.Publish(new GameEvent(GameEventKind.PartProduced, nextId));
            if (mode == WorkshopMode.OneButton && produced >= 3)
                CompleteDemo();
        }
        // Ballistic launch that lands on the next drop spot. Gear settles on contact (see Gear), so the
        // spot is where the player has to go: floor, low platform, high platform.
        public Vector2 DropVelocity(Vector2 from)
        {
            if (gearDrops == null || gearDrops.Length == 0)
                return launchVelocity;
            var to = gearDrops[produced % gearDrops.Length] + new Vector2(Random.Range(-.25f, .25f), 0);
            float g = Mathf.Abs(Physics2D.gravity.y) * (gearPrefab ? gearPrefab.GetComponent<Rigidbody2D>().gravityScale : 1);
            float apex = Mathf.Max(from.y, to.y) + dropArcHeight + Mathf.Abs(to.x - from.x) * .06f;
            float vy = Mathf.Sqrt(2 * g * (apex - from.y));
            float time = vy / g + Mathf.Sqrt(2 * (apex - to.y) / g);
            return new Vector2((to.x - from.x) / time, vy);
        }
        Gear SpawnGear(Vector2 position, Vector2 velocity, bool collectable)
        {
            var gear = Instantiate(gearPrefab, position, Quaternion.identity);
            gear.name = "Gear_" + (++nextId);
            gear.Initialize(this, nextId, velocity, collectable);
            worldGears.Add(gear);
            return gear;
        }
        public bool TryCollect(Gear gear)
        {
            if (!QuestActive || carried.Count >= 3 || !worldGears.Contains(gear))
                return false;
            carried.Add(gear.Id);
            worldGears.Remove(gear);
            publisher.Publish(new GameEvent(GameEventKind.PartCollected, gear.Id));
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
            // Progress is already recorded; the gears visibly fly in one by one for the "payoff".
            var gearSprite = gearPrefab ? gearPrefab.GetComponentInChildren<SpriteRenderer>().sprite : null;
            var stationSprite = Station.GetComponent<SpriteRenderer>();
            Vector2 from = Robot.transform.position + Vector3.up * .4f, to = stationSprite ? (Vector2)stationSprite.bounds.center : (Vector2)Station.transform.position + Vector2.up * .3f;
            for (int i = 0; i < count; i++)
            {
                int n = i;
                Feedback.FlyTo(gearSprite, from, to, i * .12f, () =>
                {
                    if (!Station)
                        return;
                    Station.Confirm();
                    Coins += coinsPerGear;
                    Hud.Floater(to + Vector2.up * 1.4f, "+" + coinsPerGear, new Color(1, .82f, .3f));
                    Feedback.Play("deliver", .8f, 1 + n * .15f);
                    Feedback.Burst(to, 10, new Color(.65f, 1, .7f));
                    Feedback.Shake(.05f);
                });
            }
            Robot.Emote(1);
            Notify(quest.StateName == "Completed" ? "Tamir tamam! Atölyeyi uyandırmak için tekrar E." : "Dişliler teslim edildi.");
        }
        public void Launch()
        {
            if (Time.time < launchAt || DemoComplete)
                return;
            if (worldGears.Count >= 18)
            {
                RemoveOldestGear();
            }
            launchAt = Time.time + .35f;
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
            if (hitTargets >= 3)
                CompleteDemo();
        }
        public void CompleteDemo()
        {
            if (DemoComplete)
                return;
            DemoComplete = true;
            Feedback.Play("success", .7f);
        }
        public void TogglePhysics()
        {
            DesignedPhysics = !DesignedPhysics;
            Notify(DesignedPhysics ? "Tasarlanmış fizik: çarpışma sonrası hız düzeltilir." : "Doğal fizik: Physics Material 2D sekmesi.");
        }
        public void ToggleAssistance()
        {
            if (Robot)
            {
                Robot.assistance = !Robot.assistance;
                Notify(Robot.assistance ? "Coyote time ve jump buffer açık." : "Coyote time ve jump buffer kapalı.");
            }
        }
        public void SelectProfile(int index)
        {
            if (Robot)
                Robot.SelectProfile(index);
            if (Machine)
                Machine.SelectProfile(index);
        }
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
        public void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        public void OpenProduction() => OpenScene(0);
        public void OpenMovement() => OpenScene(1);
        public void OpenPhysics() => OpenScene(2);
        public void OpenMain() => OpenScene(3);
        public void SelectNormal() => SelectProfile(0);
        public void SelectSecond() => SelectProfile(1);
        public void SelectThird() => SelectProfile(2);
        public void OpenScene(int index) => SceneManager.LoadScene(new[] { "01_OneButton", "02_Movement", "03_Physics", "04_MainGame" }[index]);
    }
}

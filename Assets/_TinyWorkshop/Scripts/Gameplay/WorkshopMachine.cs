using System;
using UnityEngine;
namespace TinyWorkshop
{
    // The one-button production machine: every press adds energy, reaching the threshold produces a gear.
    // Input comes from MachineInput; animation and sound live in MachinePresentation.
    public sealed class WorkshopMachine : MonoBehaviour
    {
        const int GearQuota = 3;   // gears per shift / lab run, and max gears lying around

        public WorkshopSession session;
        [SerializeField] ProductionProfile profile;
        [Tooltip("Where produced gears are launched from.")]
        public Transform outlet;
        [Tooltip("Main game: the robot must be this close to use the machine.")]
        public float interactionRange = 2.4f;

        public event Action Pressed, Produced;

        public ProductionProfile Profile => profile;
        public int Energy { get; private set; }
        // Unscaled times, so animations keep running during hitstop.
        public float LastPressTime { get; private set; } = float.NegativeInfinity;
        public float LastProduceTime { get; private set; } = float.NegativeInfinity;

        public bool IsNear => !session.Robot || Vector2.Distance(session.Robot.transform.position, transform.position) < interactionRange;
        bool IsMainGame => session.mode == WorkshopMode.MainGame;

        // True when a press would be accepted (used for the "press me" hints).
        public bool IsReady => !IsMainGame || (session.QuestActive && IsNear && session.Produced < GearQuota && session.WorldGearCount < GearQuota);

        float nextPressTime;

        public void SetProfile(ProductionProfile value)
        {
            profile = value;
            Energy = 0;
        }

        public bool TryPress()
        {
            if (Time.unscaledTime < nextPressTime || !CanPress())
                return false;
            nextPressTime = Time.unscaledTime + profile.cooldown;
            LastPressTime = Time.unscaledTime;
            Energy += profile.energyPerPress;
            Pressed?.Invoke();
            if (Energy >= profile.threshold)
                ProduceGear();
            return true;
        }

        // Every refusal tells the player why.
        bool CanPress()
        {
            if (IsMainGame && !session.QuestActive)
                return Refuse(session.QuestState == "Locked" ? "Önce E ile vardiyayı başlat." : "Üretim tamamlandı.");
            if (!IsNear)
                return Refuse("Makineye yaklaş, sonra tıkla.");
            if (session.Produced >= GearQuota)
            {
                session.Notify("Üretim tamam! Dişlileri tamir istasyonuna götür.");
                return false;
            }
            if (session.WorldGearCount >= GearQuota)
                return Refuse("Önce yerdeki dişlileri topla.");
            return true;
        }

        bool Refuse(string reason)
        {
            session.Deny(reason);
            return false;
        }

        void ProduceGear()
        {
            Energy = 0;
            LastProduceTime = Time.unscaledTime;
            session.Produce(outlet.position, LaunchVelocity());
            Produced?.Invoke();
        }

        // Main game aims at the next landing spot; the lab uses a simple fan of three arcs.
        Vector2 LaunchVelocity()
        {
            if (IsMainGame)
                return session.DropVelocity(outlet.position);
            return new Vector2(3.5f + (session.Produced % 3) * .6f, 6);
        }
    }
}

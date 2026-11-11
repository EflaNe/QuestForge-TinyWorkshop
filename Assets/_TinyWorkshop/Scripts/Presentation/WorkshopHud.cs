using System.Collections;
using UnityEngine;
using UnityEngine.UI;
namespace TinyWorkshop
{
    public sealed class WorkshopHud : MonoBehaviour
    {
        public WorkshopSession session;
        public Text title, subtitle, objective, status, hint, toast, controls, profileLabel, victoryTitle, victoryText, soundLabel, menuHint;
        public GameObject victoryPanel, menuPanel;
        public Image[] goalPips;
        [Tooltip("Main game: the hint pill floats above the object it refers to instead of sitting mid-screen.")]
        public bool hintFollowsWorld;

        [Header("Main game HUD (built by WorkshopSceneBuilder)")]
        public RectTransform objectiveCard;
        public Text objectiveTitle, objectiveStep;
        public RectTransform objectiveFill;
        public Text stageLabel;
        public Image[] stageNodes;
        public Sprite nodeDone, nodeCurrent, nodeEmpty;
        public RectTransform gift;
        public Text coinText, gearText, shakeLabel;
        public RectTransform coinCounter, gearCounter;
        public GameObject missionsPanel, upgradesPanel, settingsPanel;
        public RectTransform[] missionFills;
        public Text[] missionCounts;
        public Font floaterFont;

        static readonly string[] Titles = { "ÜRETİM TEZGAHI", "ROBOT PARKURU", "FİZİK LABORATUVARI", "ATÖLYEYİ UYANDIR" };
        static readonly string[] Subtitles = { "01 / TEK TUŞ · RİTİM VE ÜRETİM", "02 / HAREKET · ÜÇ PLATFORM", "03 / FİZİK · ÇARPIŞMA VE SEKME", "04 / TINY WORKSHOP · İLK VARDİYA" };
        static readonly string[] StepNames = { "Dişli üret", "Dişlileri topla", "Teslim et" };
        const string Done = "#8fd6a0", Current = "#f2b25c", Pending = "#6f8583";
        float controlsAlpha = 1, shownFill, cardPunch, coinPunch, gearPunch;
        float[] shownMission = new float[3], nodePunch = new float[3];
        int lastCoins, lastCarried, lastProgress = -1, lastStep = -1;

        GameObject[] Panels => new[] { menuPanel, missionsPanel, upgradesPanel, settingsPanel };
        public bool AnyPanelOpen
        {
            get
            {
                foreach (var p in Panels)
                    if (p && p.activeSelf)
                        return true;
                return false;
            }
        }
        // Tab / Esc: close whatever is open, otherwise open the home menu.
        public void ToggleMenu()
        {
            if (AnyPanelOpen)
                ClosePanels();
            else
                Open(menuPanel);
        }
        public void ClosePanels()
        {
            foreach (var p in Panels)
                if (p)
                    p.SetActive(false);
        }
        void Open(GameObject panel)
        {
            bool wasOpen = panel && panel.activeSelf;
            ClosePanels();
            if (panel && !wasOpen)
            {
                panel.SetActive(true);
                StartCoroutine(PopIn((RectTransform)panel.transform));
                session.Feedback.Play("press", .5f, 1.3f);
            }
        }
        public void OpenHome() => Open(menuPanel);
        public void OpenMissions() => Open(missionsPanel);
        public void OpenUpgrades() => Open(upgradesPanel);
        public void OpenSettings() => Open(settingsPanel);
        public void UpgradeLocked()
        {
            session.Deny("Geliştirmeler yakında açılacak.");
        }
        public void ToggleShake() => session.Feedback.ToggleShake();

        public void Refresh()
        {
            int mode = (int)session.mode;
            if (title)
                title.text = Titles[mode];
            if (subtitle)
                subtitle.text = Subtitles[mode];
            if (soundLabel)
                soundLabel.text = session.mode == WorkshopMode.MainGame ? (session.Feedback.Muted ? "Ses: Kapalı  [M]" : "Ses: Açık  [M]") : session.Feedback.Muted ? "SES: KAPALI [M]" : "SES: AÇIK [M]";
            if (shakeLabel)
                shakeLabel.text = session.Feedback.shakeEnabled ? "Sarsıntı: Açık  [V]" : "Sarsıntı: Kapalı  [V]";
            toast.text = session.Toast;
            bool win = session.RewardClaimed || session.DemoComplete;
            victoryPanel.SetActive(win);
            hint.transform.parent.gameObject.SetActive(!win);
            toast.gameObject.SetActive(!win);
            victoryTitle.text = session.RewardClaimed ? (session.mode == WorkshopMode.MainGame ? "Atölye Uyandı!" : "ATÖLYE YENİDEN CANLANDI") : session.mode == WorkshopMode.Movement ? "GÜZEL SIÇRAYIŞ!" : "TEST TAMAMLANDI";
            victoryText.text = session.RewardClaimed ? "Üç küçük dişli. Yeni bir başlangıç." : "Bir sonraki laboratuvara geç veya yeniden dene.";
            if (session.mode == WorkshopMode.MainGame)
            {
                if (objectiveCard)
                    RefreshMainHud();
                else
                    RefreshMain();
            }
            else if (session.mode == WorkshopMode.OneButton)
            {
                objective.text = $"ÜRETİLEN DİŞLİ   {session.Produced} / 3";
                status.text = $"ENERJİ   {session.Machine.Energy} / {session.Machine.Settings.threshold}";
                controls.text = "SPACE  BAS / ÜRET          1 2 3  RİTİM PROFİLİ          TAB  MENÜ";
                hint.text = "SPACE  ·  HER ÜÇ BASIŞTA BİR DİŞLİ";
                profileLabel.text = "RİTİM: " + new[] { "NORMAL", "SAKİN", "TELAŞLI" }[session.Machine.ProfileIndex];
            }
            else if (session.mode == WorkshopMode.Movement)
            {
                objective.text = "ÜÇ PLATFORMU AŞ  →  YEŞİL HEDEF";
                status.text = session.Robot.Grounded ? "ZEMİNDE" : "HAVADA";
                controls.text = "A D  HAREKET     SPACE  ZIPLA     1 2 3  PROFİL     G  YARDIM     TAB  MENÜ";
                hint.text = "KISA BAS: ALÇAK  ·  BASILI TUT: YÜKSEK";
                profileLabel.text = "HİS: " + new[] { "NORMAL", "AĞIR", "HAFİF" }[session.Robot.ProfileIndex] + "   /   COYOTE + BUFFER: " + (session.Robot.assistance ? "AÇIK" : "KAPALI");
            }
            else
            {
                objective.text = $"VURULAN HEDEF   {session.HitTargets} / 3";
                status.text = session.DesignedPhysics ? "TASARLANMIŞ FİZİK" : "DOĞAL FİZİK";
                controls.text = "SPACE  FIRLAT          T  FİZİK MODU          R  YENİDEN KUR          TAB  MENÜ";
                hint.text = "SPACE  ·  SEKME YÜZEYİNDEN HEDEFLERE";
                profileLabel.text = session.DesignedPhysics ? "BUMPER: TEMAS SONRASI YÖN / HIZ DÜZELTME" : "PHYSICS MATERIAL 2D: DOĞAL SEKME";
            }
            FitHint();
        }

        // ---------------------------------------------------------------- redesigned main game HUD
        void RefreshMainHud()
        {
            int p = session.Produced, c = session.Collected, d = session.Delivered;
            string state = session.QuestState;
            bool locked = state == "Locked", completed = state == "Completed", claimed = session.RewardClaimed;
            int step = p < 3 ? 0 : c < 3 ? 1 : d < 3 ? 2 : 3;
            int[] values = { p, c, d };
            float dt = Time.unscaledDeltaTime;

            // Objective card: current step and its own progress bar.
            float fill;
            if (locked)
            {
                objectiveTitle.text = "Vardiya Başlıyor";
                objectiveStep.text = "Başlamak için E'ye bas";
                fill = 0;
            }
            else if (claimed)
            {
                objectiveTitle.text = "Atölye Uyandı!";
                objectiveStep.text = "Tüm görevler tamam";
                fill = 1;
            }
            else if (completed || step == 3)
            {
                objectiveTitle.text = "Tamir Tamam!";
                objectiveStep.text = "Ödülü almak için istasyonda E";
                fill = 1;
            }
            else
            {
                objectiveTitle.text = "Görev: Atölyeyi Onar";
                objectiveStep.text = $"{StepNames[step]}   ({values[step]} / 3)";
                fill = values[step] / 3f;
            }
            int progress = p + c + d + (locked ? -1 : 0) + (claimed ? 10 : 0);
            if (lastProgress >= 0 && progress != lastProgress)
                cardPunch = 1;
            lastProgress = progress;
            if (step != lastStep && lastStep >= 0 && lastStep < 3)
                nodePunch[lastStep] = 1;
            lastStep = step;
            shownFill = Mathf.MoveTowards(shownFill, fill, dt * 2.5f);
            if (fill < shownFill - .5f)
                shownFill = fill;   // new step: bar restarts from empty
            SetFill(objectiveFill, shownFill);
            cardPunch = Mathf.MoveTowards(cardPunch, 0, dt * 4);
            objectiveCard.localScale = Vector3.one * (1 + Punch(cardPunch) * .045f);

            // Stage track: Üret / Topla / Teslim + reward gift.
            stageLabel.text = locked ? "Hazır" : step >= 3 ? (claimed ? "Bitti!" : "Ödül!") : $"Adım {step + 1}/3";
            for (int i = 0; i < stageNodes.Length; i++)
            {
                bool done = values[i] >= 3, current = !locked && i == step;
                stageNodes[i].sprite = done ? nodeDone : current ? nodeCurrent : nodeEmpty;
                nodePunch[i] = Mathf.MoveTowards(nodePunch[i], 0, dt * 3);
                float breathe = current ? Mathf.Sin(Time.unscaledTime * 4) * .05f : 0;
                stageNodes[i].rectTransform.localScale = Vector3.one * (1 + breathe + Punch(nodePunch[i]) * .35f);
            }
            if (gift)
            {
                bool ready = completed && !claimed;
                float wob = ready ? Mathf.Sin(Time.unscaledTime * 9) * 9 : 0;
                gift.localRotation = Quaternion.Euler(0, 0, wob);
                gift.localScale = Vector3.one * (ready ? 1.1f + Mathf.Sin(Time.unscaledTime * 5) * .06f : 1);
            }

            // Resource counters.
            if (session.Coins != lastCoins)
                coinPunch = 1;
            if (session.Carried != lastCarried)
                gearPunch = 1;
            lastCoins = session.Coins;
            lastCarried = session.Carried;
            coinText.text = session.Coins.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));
            gearText.text = $"{session.Carried}/3";
            coinPunch = Mathf.MoveTowards(coinPunch, 0, dt * 4);
            gearPunch = Mathf.MoveTowards(gearPunch, 0, dt * 4);
            coinCounter.localScale = Vector3.one * (1 + Punch(coinPunch) * .12f);
            gearCounter.localScale = Vector3.one * (1 + Punch(gearPunch) * .12f);

            // Missions window rows.
            for (int i = 0; i < 3 && i < missionFills.Length; i++)
            {
                shownMission[i] = Mathf.MoveTowards(shownMission[i], values[i] / 3f, dt * 2);
                SetFill(missionFills[i], shownMission[i]);
                missionCounts[i].text = $"{values[i]} / 3";
            }
            PlaceMainHint(locked, p);
        }
        static float Punch(float t) => Mathf.Sin(t * Mathf.PI) * t;
        static void SetFill(RectTransform fillRect, float value)
        {
            var track = (RectTransform)fillRect.parent;
            float min = track.rect.height;
            fillRect.gameObject.SetActive(value > .001f);
            fillRect.sizeDelta = new Vector2(Mathf.Lerp(min, track.rect.width, Mathf.Clamp01(value)), fillRect.sizeDelta.y);
        }
        void PlaceMainHint(bool locked, int p)
        {
            var station = session.Station;
            var machine = session.Machine;
            var stationTop = station.GetComponent<SpriteRenderer>() ? station.GetComponent<SpriteRenderer>().bounds.max.y + .2f : station.transform.position.y + 1.6f;
            Vector3 stationAnchor = new Vector3(station.transform.position.x, stationTop);
            Vector3 machineAnchor = machine.buttonVisual ? machine.buttonVisual.position + Vector3.up * 2.55f : machine.transform.position + Vector3.up * 1.9f;
            Vector3 anchor;
            if (locked)
            {
                hint.text = "E  ·  Vardiyayı başlat";
                anchor = session.Robot.transform.position + Vector3.up * 2.2f;
            }
            else if (session.QuestState == "Completed")
            {
                hint.text = station.IsNear ? "E  ·  Atölyeyi uyandır" : "İstasyona dön  →";
                anchor = stationAnchor;
            }
            else if (station.IsNear && session.Carried > 0)
            {
                hint.text = "E  ·  Teslim et";
                anchor = stationAnchor;
            }
            else if (session.WorldGearCount > 0 && session.CanCarryMore && (p >= 3 || session.WorldGearCount >= 3 || !machine.IsNear))
            {
                var gear = NearestGear();
                if (!gear)
                    return;
                bool high = gear.transform.position.y > session.Robot.transform.position.y + .6f;
                hint.text = high ? "SPACE  ·  Zıpla, topla" : "Dişliyi topla";
                anchor = gear.transform.position + Vector3.up * .8f;
            }
            else if (p < 3)
            {
                hint.text = machine.IsNear ? "Tıkla  ·  Kırmızı düğme" : "←  Makineye yaklaş";
                anchor = machineAnchor;
            }
            else
            {
                hint.text = "E  ·  İstasyonda teslim et";
                anchor = stationAnchor;
            }
            PlaceAt(anchor);
        }

        // Rising "+10" style numbers over world positions.
        public void Floater(Vector3 world, string text, Color color)
        {
            if (!floaterFont || !session.worldCamera || !isActiveAndEnabled)
                return;
            StartCoroutine(FloatRoutine(world, text, color));
        }
        IEnumerator FloatRoutine(Vector3 world, string text, Color color)
        {
            var go = new GameObject("Floater", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.sizeDelta = new Vector2(160, 40);
            var label = go.AddComponent<Text>();
            label.font = floaterFont;
            label.fontSize = 26;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            label.text = text;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(.12f, .16f, .14f, .9f);
            outline.effectDistance = new Vector2(2, -2);
            var canvas = (RectTransform)transform;
            const float duration = .9f;
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                var screen = session.worldCamera.WorldToScreenPoint(world);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen, null, out var local);
                float k = t / duration;
                rt.anchoredPosition = local + Vector2.up * (70 * (1 - (1 - k) * (1 - k)));
                float pop = k < .15f ? Mathf.Lerp(.4f, 1.25f, k / .15f) : Mathf.Lerp(1.25f, 1, Mathf.Clamp01((k - .15f) / .15f));
                rt.localScale = Vector3.one * pop;
                label.color = new Color(color.r, color.g, color.b, Mathf.Clamp01((1 - k) / .35f));
                yield return null;
            }
            Destroy(go);
        }
        IEnumerator PopIn(RectTransform panel)
        {
            for (float t = 0; t < .18f; t += Time.unscaledDeltaTime)
            {
                float k = t / .18f;
                panel.localScale = Vector3.one * (k < .7f ? Mathf.Lerp(.85f, 1.04f, k / .7f) : Mathf.Lerp(1.04f, 1, (k - .7f) / .3f));
                yield return null;
            }
            panel.localScale = Vector3.one;
        }

        // ---------------------------------------------------------------- legacy text HUD (kept for old scenes)
        void RefreshMain()
        {
            int p = session.Produced, c = session.Collected, d = session.Delivered;
            bool locked = session.QuestState == "Locked";
            string current = p < 3 ? "p" : c < 3 ? "c" : d < 3 ? "d" : "";
            objective.text = Step("ÜRET", p, current == "p", locked) + "\n" + Step("TOPLA", c, current == "c", locked) + "\n" + Step("TESLİM", d, current == "d", locked);
            status.text = "ÇANTA";
            for (int i = 0; i < goalPips.Length; i++)
                goalPips[i].color = i < session.Carried ? new Color(.98f, .72f, .35f) : new Color(.22f, .3f, .3f);
            profileLabel.text = "";
            controlsAlpha = Mathf.MoveTowards(controlsAlpha, p == 0 ? 1 : 0, Time.unscaledDeltaTime * 1.5f);
            controls.text = "A D  HAREKET     SPACE  ZIPLA     SOL TIK  ÜRET     E  ETKİLEŞİM";
            controls.color = new Color(controls.color.r, controls.color.g, controls.color.b, .75f * controlsAlpha);
            PlaceMainHint(locked, p);
        }
        static string Step(string label, int value, bool current, bool locked)
        {
            string color = value >= 3 ? Done : current && !locked ? Current : Pending;
            string marker = value >= 3 ? "●" : current && !locked ? "›" : "·";
            return $"<color={color}>{marker}  {label}  {value}/3</color>";
        }
        Gear NearestGear()
        {
            Gear best = null;
            float bestDistance = float.MaxValue;
            var robot = (Vector2)session.Robot.transform.position;
            foreach (var g in session.WorldGears)
            {
                if (!g)
                    continue;
                float distance = ((Vector2)g.transform.position - robot).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = g;
                }
            }
            return best;
        }
        void PlaceAt(Vector3 world)
        {
            if (!hintFollowsWorld)
                return;
            var panel = (RectTransform)hint.transform.parent;
            var canvas = (RectTransform)transform;
            var screen = session.worldCamera.WorldToScreenPoint(world);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen, null, out var local))
                return;
            // Keep the pill on screen, below the top HUD and above the bottom bar.
            var half = canvas.rect.size * .5f;
            float w = panel.sizeDelta.x * .5f;
            local.x = Mathf.Clamp(local.x, -half.x + w + 12, half.x - w - 12);
            local.y = Mathf.Clamp(local.y, -half.y + 110, half.y - 130);
            local.y += Mathf.Sin(Time.unscaledTime * 3) * 3;
            panel.anchoredPosition = Vector2.Lerp(panel.anchoredPosition, local, panel.anchoredPosition == Vector2.zero ? 1 : Mathf.Clamp01(Time.unscaledDeltaTime * 14));
        }
        void FitHint()
        {
            var panel = (RectTransform)hint.transform.parent;
            panel.sizeDelta = new Vector2(Mathf.Max(120, hint.preferredWidth + 48), panel.sizeDelta.y);
            hint.rectTransform.sizeDelta = new Vector2(panel.sizeDelta.x - 8, hint.rectTransform.sizeDelta.y);
        }
    }
}

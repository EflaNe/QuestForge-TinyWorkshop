using System.Collections;
using UnityEngine;
namespace TinyWorkshop
{
    public sealed class WorkshopFeedback : MonoBehaviour
    {
        public FeedbackProfile profile;
        public AudioClip[] clips;
        public AudioClip musicClip, ambienceClip;
        public Sprite sparkSprite;
        [Tooltip("Soft round sprite for steam/dust puffs; falls back to the spark sprite.")]
        public Sprite puffSprite;
        public Camera worldCamera;
        public bool shakeEnabled = true;
        public bool Muted
        {
            get; private set;
        }
        AudioSource effects, music, ambience;
        Vector3 cameraHome;
        float shake, dustAt;
        void Awake()
        {
            effects = gameObject.AddComponent<AudioSource>();
            effects.playOnAwake = false;
            effects.volume = profile.effectsVolume;
            music = gameObject.AddComponent<AudioSource>();
            music.clip = musicClip;
            music.loop = true;
            music.volume = profile.musicVolume;
            music.Play();
            if (ambienceClip)
            {
                ambience = gameObject.AddComponent<AudioSource>();
                ambience.clip = ambienceClip;
                ambience.loop = true;
                ambience.volume = .07f;
                ambience.Play();
            }
            cameraHome = worldCamera.transform.position;
        }
        public void Play(string cue, float volume = 1, float pitch = 0)
        {
            if (Muted)
                return;
            foreach (var clip in clips)
                if (clip && clip.name == cue)
                {
                    effects.pitch = pitch > 0 ? pitch : cue == "success" || cue == "start" ? 1 : Random.Range(.95f, 1.05f);
                    effects.PlayOneShot(clip, volume);
                    return;
                }
        }
        // Brief freeze on impactful moments (gear produced, pickup). Uses real time so it cannot get stuck.
        public void Hitstop(float seconds)
        {
            if (seconds <= 0 || !isActiveAndEnabled)
                return;
            StopCoroutine(nameof(HitstopRoutine));
            StartCoroutine(nameof(HitstopRoutine), seconds);
        }
        IEnumerator HitstopRoutine(float seconds)
        {
            Time.timeScale = .05f;
            yield return new WaitForSecondsRealtime(seconds);
            Time.timeScale = 1;
        }
        void OnDestroy() => Time.timeScale = 1;
        // A sprite that arcs from one point to another, then runs a callback (e.g. gears flying into the station).
        public void FlyTo(Sprite sprite, Vector2 from, Vector2 to, float delay, System.Action arrived)
        {
            StartCoroutine(Fly(sprite, from, to, delay, arrived));
        }
        IEnumerator Fly(Sprite sprite, Vector2 from, Vector2 to, float delay, System.Action arrived)
        {
            yield return new WaitForSeconds(delay);
            var go = new GameObject("FlyingIcon");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 30;
            const float duration = .32f;
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                var p = Vector2.Lerp(from, to, k * k) + Vector2.up * Mathf.Sin(k * Mathf.PI) * 1.1f;
                go.transform.position = p;
                go.transform.localScale = Vector3.one * Mathf.Lerp(1.15f, .7f, k);
                go.transform.Rotate(0, 0, -900 * Time.deltaTime);
                yield return null;
            }
            Destroy(go);
            arrived?.Invoke();
        }
        public void ToggleMute()
        {
            Muted = !Muted;
            effects.mute = Muted;
            music.mute = Muted;
            if (ambience)
                ambience.mute = Muted;
        }
        public void ToggleShake() => shakeEnabled = !shakeEnabled;
        public void Shake(float strength)
        {
            if (shakeEnabled)
                shake = Mathf.Min(strength, profile.shakeStrength);
        }
        void LateUpdate()
        {
            shake = Mathf.MoveTowards(shake, 0, Time.deltaTime * .35f);
            worldCamera.transform.position = cameraHome + (Vector3)(Random.insideUnitCircle * (shakeEnabled ? shake : 0));
            if (Time.time > dustAt)
            {
                dustAt = Time.time + 1.6f;
                StartCoroutine(Particle(new Vector2(Random.Range(-9, 9), Random.Range(-2, 3.5f)), new Vector2(.08f, .08f), new Color(.9f, .85f, .6f, .1f), 3f, .45f));
            }
        }
        // Soft white puff (gear spawn, landing): grows, drifts up and fades.
        public void Puff(Vector2 position, int count)
        {
            for (int i = 0; i < count; i++)
                StartCoroutine(PuffParticle(position + Random.insideUnitCircle * .15f, new Vector2(Random.Range(-.6f, .6f), Random.Range(.4f, 1.3f)), Random.Range(.35f, .6f)));
        }
        IEnumerator PuffParticle(Vector2 position, Vector2 velocity, float duration)
        {
            var go = new GameObject("Puff");
            go.transform.position = position;
            var sprite = go.AddComponent<SpriteRenderer>();
            sprite.sprite = puffSprite ? puffSprite : sparkSprite;
            sprite.sortingOrder = 24;
            float size = puffSprite ? Random.Range(.35f, .55f) : 1;
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                go.transform.position += (Vector3)velocity * Time.deltaTime;
                velocity *= 1 - Time.deltaTime * 3;
                go.transform.localScale = Vector3.one * size * Mathf.Lerp(.5f, 1.3f, Mathf.Sqrt(k));
                sprite.color = new Color(1, 1, 1, .8f * (1 - k));
                yield return null;
            }
            Destroy(go);
        }
        public void Burst(Vector2 position, int count, Color color)
        {
            for (int i = 0; i < count; i++)
                StartCoroutine(Particle(position, Random.insideUnitCircle * Random.Range(.5f, 2.5f), color, Random.Range(.2f, .55f), 1));
        }
        IEnumerator Particle(Vector2 position, Vector2 velocity, Color color, float duration, float scale)
        {
            var go = new GameObject("Spark");
            go.transform.position = position;
            go.transform.localScale = Vector3.one * scale;
            var sprite = go.AddComponent<SpriteRenderer>();
            sprite.sprite = sparkSprite;
            sprite.color = color;
            sprite.sortingOrder = 25;
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                go.transform.position += (Vector3)velocity * Time.deltaTime;
                velocity.y -= Time.deltaTime * 2;
                sprite.color = new Color(color.r, color.g, color.b, color.a * (1 - t / duration));
                yield return null;
            }
            Destroy(go);
        }
    }
}

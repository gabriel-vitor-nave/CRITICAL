using System;
using TMPro;
using UnityEngine;
using Critical.Feedback;

public class PlayerHealth : MonoBehaviour
{
    public int HP = 6;
    public int maxHP = 6;
    public TMP_Text hpText;
    [Min(0f)] public float invulnerabilityDuration = 0.65f;
    public bool IsDead => HP <= 0;
    public event Action<int, int> HealthChanged;
    public event Action Died;

    private float invulnerableUntil;
    private SpriteRenderer[] sprites;
    private Color[] colors;
    private bool deathRaised;

    private void Awake()
    {
        maxHP = Mathf.Max(1, maxHP);
        HP = Mathf.Clamp(HP, 1, maxHP);
        sprites = GetComponentsInChildren<SpriteRenderer>();
        colors = new Color[sprites.Length];
        for (int i = 0; i < sprites.Length; i++) colors[i] = sprites[i].color;
    }

    private void Start() => Refresh();

    private void Update()
    {
        float remaining = invulnerableUntil - Time.time;
        for (int i = 0; i < sprites.Length; i++)
            if (sprites[i] != null)
                sprites[i].color = remaining > 0f
                    ? Color.Lerp(colors[i], new Color(1f, 0.32f, 0.32f, 0.5f), 0.6f + 0.4f * Mathf.Sin(remaining * 48f))
                    : colors[i];
    }

    public void ChangeHP(int amount)
    {
        if (IsDead || amount == 0 || (amount < 0 && Time.time < invulnerableUntil)) return;
        int previous = HP;
        HP = (int)Math.Max(0L, Math.Min((long)maxHP, (long)HP + amount));
        if (HP == previous) return;
        if (HP < previous)
        {
            invulnerableUntil = Time.time + invulnerabilityDuration;
            WorldFeedback.Burst(transform.position, new Color(1f, 0.24f, 0.3f), 10);
            WorldFeedback.Number(transform.position + Vector3.up * 0.5f, (HP - previous).ToString(), new Color(1f, 0.34f, 0.34f));
            var flow = FindObjectOfType<Critical.Procedural.ExplorationFlow>();
            if (flow != null) flow.Shake(0.16f);
        }
        else WorldFeedback.Number(transform.position + Vector3.up * 0.5f, "+" + (HP - previous), new Color(0.45f, 1f, 0.65f));
        Refresh();
        if (IsDead && !deathRaised)
        {
            deathRaised = true;
            var movement = GetComponent<PlayerMovement>();
            if (movement != null) movement.enabled = false;
            var body = GetComponent<Rigidbody2D>();
            if (body != null) body.velocity = Vector2.zero;
            Died?.Invoke();
        }
    }

    public void Refresh()
    {
        maxHP = Mathf.Max(1, maxHP);
        HP = Mathf.Clamp(HP, 0, maxHP);
        if (hpText != null) hpText.text = "HP: " + HP + " / " + maxHP;
        HealthChanged?.Invoke(HP, maxHP);
    }
}

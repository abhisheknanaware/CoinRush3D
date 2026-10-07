using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("HUD")]
    public GameObject hud;
    public TMP_Text scoreText;
    public TMP_Text healthText;
    public TMP_Text coinsText;
    public TMP_Text timeText;
    public TMP_Text speedText;
    public TMP_Text toastText;
    public TMP_Text lockHint;
    public Image damageFlash;

    [Header("Screens")]
    public GameObject startScreen;
    public GameObject winScreen;
    public TMP_Text winScore;
    public TMP_Text winCoins;
    public TMP_Text winTime;
    public GameObject gameOverScreen;
    public TMP_Text gameOverTitle;
    public TMP_Text gameOverScore;
    public TMP_Text gameOverCoins;

    GameManager gm;
    int lastSecond = -1;
    Coroutine toastRoutine;

    void Start()
    {
        gm = GameManager.Instance;
        gm.StateChanged += OnStateChanged;
        OnStateChanged(gm.State);
        if (damageFlash) damageFlash.color = Color.clear;
        if (toastText) toastText.alpha = 0f;
    }

    void OnDestroy()
    {
        if (gm) gm.StateChanged -= OnStateChanged;
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.mKey.wasPressedThisFrame && gm.audioManager) gm.audioManager.ToggleMute();
        if (gm.State != GameState.Playing) return;

        scoreText.text = $"Score: {gm.Score}";
        healthText.text = $"Health: {gm.Health}";
        coinsText.text = $"Coins: {gm.CoinsCollected} / {gm.TotalCoins}";
        speedText.text = $"Speed: {gm.player.walkSpeed:0}";

        int sec = Mathf.CeilToInt(gm.TimeLeft);
        if (sec != lastSecond)
        {
            lastSecond = sec;
            timeText.text = $"Time: {sec}";
            timeText.color = sec <= 10 ? new Color(1f, 0.35f, 0.35f) : Color.white;
        }
        if (lockHint) lockHint.gameObject.SetActive(Cursor.lockState != CursorLockMode.Locked);
    }

    void OnStateChanged(GameState state)
    {
        hud.SetActive(state != GameState.Start);
        startScreen.SetActive(state == GameState.Start);
        winScreen.SetActive(false);
        gameOverScreen.SetActive(false);

        if (state == GameState.Won)
        {
            winScore.text = $"Final Score: <color=#ffd54a>{gm.Score}</color>";
            winCoins.text = $"Coins Collected: <color=#ffd54a>{gm.CoinsCollected} / {gm.TotalCoins}</color>";
            winTime.text = $"Time Remaining: <color=#ffd54a>{Mathf.CeilToInt(gm.TimeLeft)}s</color>";
            StartCoroutine(ShowLater(winScreen, 0.9f));
        }
        else if (state == GameState.GameOver || state == GameState.TimeUp)
        {
            gameOverTitle.text = state == GameState.TimeUp ? "TIME UP!" : "GAME OVER";
            gameOverScore.text = $"Score: <color=#ffd54a>{gm.Score}</color>";
            gameOverCoins.text = $"Coins: <color=#ffd54a>{gm.CoinsCollected} / {gm.TotalCoins}</color>";
            StartCoroutine(ShowLater(gameOverScreen, 0.5f));
        }
        if (lockHint) lockHint.gameObject.SetActive(false);
    }

    IEnumerator ShowLater(GameObject screen, float delay)
    {
        yield return new WaitForSeconds(delay);
        screen.SetActive(true);
        Transform card = screen.transform.GetChild(0);
        for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.3f;
            card.localScale = Vector3.one * Mathf.Lerp(0.7f, 1f, 1f - (1f - k) * (1f - k));
            yield return null;
        }
        card.localScale = Vector3.one;
    }

    public void OnStartClicked() => gm.StartGame();
    public void OnRestartClicked() => gm.Restart();

    public void FlashDamage()
    {
        if (damageFlash) StartCoroutine(Flash());
    }

    IEnumerator Flash()
    {
        for (float t = 0f; t < 0.45f; t += Time.deltaTime)
        {
            damageFlash.color = new Color(1f, 0f, 0f, 0.45f * (1f - t / 0.45f));
            yield return null;
        }
        damageFlash.color = Color.clear;
    }

    public void Bump(TMP_Text text)
    {
        if (text) StartCoroutine(BumpRoutine(text.transform));
    }

    static IEnumerator BumpRoutine(Transform t)
    {
        for (float x = 0f; x < 0.3f; x += Time.deltaTime)
        {
            t.localScale = Vector3.one * (1f + 0.18f * Mathf.Sin(x / 0.3f * Mathf.PI));
            yield return null;
        }
        t.localScale = Vector3.one;
    }

    public void ShowToast(string message)
    {
        if (!toastText) return;
        if (toastRoutine != null) StopCoroutine(toastRoutine);
        toastRoutine = StartCoroutine(Toast(message));
    }

    IEnumerator Toast(string message)
    {
        toastText.text = message;
        toastText.alpha = 1f;
        yield return new WaitForSeconds(2f);
        for (float t = 0f; t < 0.4f; t += Time.deltaTime)
        {
            toastText.alpha = 1f - t / 0.4f;
            yield return null;
        }
        toastText.alpha = 0f;
    }
}

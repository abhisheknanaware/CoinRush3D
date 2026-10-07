using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameState { Start, Playing, Won, GameOver, TimeUp }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    static bool startImmediately;

    [Header("Rules")]
    public float levelTime = 60f;
    public int startingHealth = 3;
    public int coinValue = 10;
    public float invulnerableTime = 1.5f;
    public float knockbackForce = 9f;

    [Header("References")]
    public PlayerController player;
    public UIManager ui;
    public AudioManager audioManager;
    public Transform coinsRoot;

    [Header("Effects")]
    public ParticleSystem coinBurstPrefab;
    public ParticleSystem hitBurstPrefab;
    public ParticleSystem victoryPrefab;

    public GameState State { get; private set; } = GameState.Start;
    public int Score { get; private set; }
    public int CoinsCollected { get; private set; }
    public int TotalCoins { get; private set; }
    public int Health { get; private set; }
    public float TimeLeft { get; private set; }

    public event Action<GameState> StateChanged;

    float invulnerableUntil;

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
        TotalCoins = coinsRoot ? coinsRoot.GetComponentsInChildren<Coin>().Length : 0;
        Health = startingHealth;
        TimeLeft = levelTime;
    }

    void Start()
    {
        if (startImmediately)
        {
            startImmediately = false;
            StartGame();
        }
        else
        {
            SetState(GameState.Start);
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void StartGame()
    {
        Score = 0;
        CoinsCollected = 0;
        Health = startingHealth;
        TimeLeft = levelTime;
        invulnerableUntil = 0f;
        SetState(GameState.Playing);
        if (audioManager) audioManager.StartMusic();
        if (ui) ui.ShowToast("Find the coins!");
    }

    public void Restart()
    {
        startImmediately = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void Update()
    {
        if (State != GameState.Playing) return;
        TimeLeft = Mathf.Max(0f, TimeLeft - Time.deltaTime);
        if (TimeLeft <= 0f) EndGame(GameState.TimeUp);
    }

    public void CollectCoin(Coin coin)
    {
        if (State != GameState.Playing) return;
        Score += coinValue;
        CoinsCollected++;
        Spawn(coinBurstPrefab, coin.transform.position, 1.5f);
        if (audioManager) audioManager.PlayCoin();
        Destroy(coin.gameObject);
        if (ui)
        {
            ui.Bump(ui.scoreText);
            if (CoinsCollected == TotalCoins) ui.ShowToast("All coins collected! Head to the portal!");
        }
    }

    public bool DamagePlayer(Vector3 sourcePosition)
    {
        if (State != GameState.Playing || Time.time < invulnerableUntil) return false;
        invulnerableUntil = Time.time + invulnerableTime;
        Health = Mathf.Max(0, Health - 1);

        Vector3 away = player.transform.position - sourcePosition;
        away.y = 0f;
        player.Knockback(away.normalized * knockbackForce);
        Spawn(hitBurstPrefab, Vector3.Lerp(player.transform.position, sourcePosition, 0.5f) + Vector3.up, 1.5f);
        if (audioManager) audioManager.PlayHit();
        if (ui)
        {
            ui.FlashDamage();
            ui.Bump(ui.healthText);
        }
        if (Health <= 0) EndGame(GameState.GameOver);
        return true;
    }

    public void ReachPortal(Vector3 portalPosition)
    {
        if (State != GameState.Playing) return;
        Spawn(victoryPrefab, portalPosition + Vector3.up * 0.5f, 4f);
        Spawn(victoryPrefab, player.transform.position + player.transform.forward * 2f, 4f);
        EndGame(GameState.Won);
    }

    void EndGame(GameState result)
    {
        SetState(result);
        if (audioManager)
        {
            audioManager.StopMusic();
            if (result == GameState.Won) audioManager.PlayWin();
            else audioManager.PlayLose();
        }
    }

    void SetState(GameState state)
    {
        State = state;
        bool playing = state == GameState.Playing;
        Cursor.lockState = playing ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !playing;
        StateChanged?.Invoke(state);
    }

    static void Spawn(ParticleSystem prefab, Vector3 position, float life)
    {
        if (!prefab) return;
        ParticleSystem fx = Instantiate(prefab, position, Quaternion.identity);
        fx.Play(true);
        Destroy(fx.gameObject, life);
    }
}

using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("References")]
    public PlayerHealth playerHealth;
    public GameHUD hud;

    [Header("Wave Settings")]
    public float waveDuration = 30f;

    private bool gameOver;
    private float timeElapsed;
    private int currentWave = 1;
    private int enemyLevel = 1;
    private float waveTimer;

    public float TimeElapsed => timeElapsed;
    public int CurrentWave => currentWave;
    public int EnemyLevel => enemyLevel;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Application.runInBackground = true;
    }

    private void Start()
    {
        if (playerHealth != null)
            playerHealth.OnPlayerDied += HandlePlayerDeath;

        waveTimer = waveDuration;
    }

    private void Update()
    {
        if (gameOver) return;

        timeElapsed += Time.deltaTime;

        waveTimer -= Time.deltaTime;
        if (waveTimer <= 0f)
        {
            currentWave++;
            enemyLevel = 1 + (currentWave - 1) / 2;
            waveTimer = waveDuration;
        }

        if (hud != null && playerHealth != null)
            hud.UpdateHealthBar(playerHealth.currentHealth, playerHealth.maxHealth);
    }

    private void HandlePlayerDeath()
    {
        gameOver = true;
        if (hud != null)
            hud.ShowGameOver();
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }
}

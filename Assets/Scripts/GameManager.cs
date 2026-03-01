using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("References")]
    public PlayerHealth playerHealth;
    public GameHUD hud;

    private bool gameOver;

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
    }

    private void Update()
    {
        if (gameOver) return;

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

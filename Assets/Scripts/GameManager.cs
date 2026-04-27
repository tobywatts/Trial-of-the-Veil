using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum StagePhase { Waves, BossFight, Completed }

    [Header("References")]
    public PlayerHealth playerHealth;
    public GameHUD hud;

    [Header("Stage Progression")]
    [Tooltip("Kills to finish a stage's wave phase. Hitting this triggers the boss fight.")]
    public int firstStageKills = 10;
    [Tooltip("Per-stage increment on firstStageKills. 0 keeps every stage equal.")]
    public int stageKillsIncrement = 0;
    [Tooltip("Highest stage that runs in this scene. The portal spawns after its boss dies.")]
    public int finalLocalStage = 3;
    [Tooltip("Spawn a portal after every boss kill, not just the final-stage boss.")]
    public bool spawnPortalAfterEveryBoss = false;
    [Tooltip("Skip the final stage's boss fight and spawn the portal on kill quota. Used by level3.")]
    public bool skipBossOnFinalStage = false;
    [Tooltip("Spawn the boss shortly after scene start with no wave phase. Used by level4.")]
    public bool autoSpawnBossOnStart = false;

    [Header("Portal to Next Level")]
    [Tooltip("Scene loaded when the player enters the portal.")]
    public string nextSceneName = "level2";
    [Tooltip("Optional portal prefab. If unset, a placeholder portal is built at runtime.")]
    public GameObject portalPrefab;
    [Tooltip("Teleport the player to nextScenePlayerPosition after the next scene loads.")]
    public bool overrideNextScenePlayerPosition = true;
    [Tooltip("World position to drop the player at in the next scene.")]
    public Vector3 nextScenePlayerPosition = new Vector3(515.469971f, 0.800000012f, 517.150024f);

    private bool gameOver;
    private float timeElapsed;
    private int currentStage = 1;
    private int currentStageKills;
    private StagePhase phase = StagePhase.Waves;
    private int bossesDefeated;

    public float TimeElapsed => timeElapsed;
    public int CurrentStage => currentStage;
    public int CurrentWave => currentStage;
    public int EnemyLevel => 1 + Mathf.FloorToInt(timeElapsed / 30f);
    public int CurrentStageKills => currentStageKills;
    public int KillsForCurrentStage => KillsRequiredForStage(currentStage);
    public float StageProgress => KillsForCurrentStage > 0 ? Mathf.Clamp01((float)currentStageKills / KillsForCurrentStage) : 0f;
    public bool GameOver => gameOver;
    public StagePhase Phase => phase;
    public bool IsInBossFight => phase == StagePhase.BossFight;
    public bool IsCompleted => phase == StagePhase.Completed;
    public int BossesDefeated => bossesDefeated;

    public int KillsRequiredForStage(int stage)
    {
        return firstStageKills + Mathf.Max(0, stage - 1) * stageKillsIncrement;
    }

    public void RegisterEnemyKill()
    {
        if (gameOver) return;
        if (phase != StagePhase.Waves) return; // kills only count during the wave phase
        currentStageKills++;
        if (currentStageKills >= KillsForCurrentStage)
        {
            // No-boss path: final stage completes on kill quota and spawns the portal directly.
            if (skipBossOnFinalStage && currentStage >= finalLocalStage)
            {
                phase = StagePhase.Completed;
                Vector3 portalPos = playerHealth != null
                    ? playerHealth.transform.position + playerHealth.transform.forward * 2f
                    : Vector3.zero;
                SpawnPortal(portalPos);
                return;
            }
            phase = StagePhase.BossFight;
            var spawner = FindFirstObjectByType<EnemySpawner>();
            if (spawner != null) spawner.SpawnBoss();
            else Debug.LogWarning("Wave threshold reached but no EnemySpawner found to spawn the boss.");
        }
    }

    public void RegisterBossKill(Vector3 worldPos)
    {
        if (gameOver) return;
        if (phase != StagePhase.BossFight) return;
        bossesDefeated++;

        bool atFinalLocalStage = currentStage >= finalLocalStage;

        if (atFinalLocalStage)
        {
            phase = StagePhase.Completed;
            // No next scene means this was the final boss; show victory. gameOver also halts spawners and AI.
            if (string.IsNullOrEmpty(nextSceneName))
            {
                gameOver = true;
                if (hud != null) hud.ShowVictory();
            }
            else
            {
                SpawnPortal(worldPos);
            }
        }
        else
        {
            // Boss-after-every-stage mode: spawn a portal but still advance so the player can keep clearing.
            if (spawnPortalAfterEveryBoss) SpawnPortal(worldPos);
            currentStage++;
            currentStageKills = 0;
            phase = StagePhase.Waves;
        }
    }

    public void RestoreState(float time, int stage, int stageKills, int bossKills, StagePhase phaseToRestore)
    {
        timeElapsed = Mathf.Max(0f, time);
        currentStage = Mathf.Max(1, stage);
        currentStageKills = Mathf.Max(0, stageKills);
        bossesDefeated = Mathf.Max(0, bossKills);
        phase = phaseToRestore;
    }

    private void SpawnPortal(Vector3 worldPos)
    {
        GameObject portalGO;
        if (portalPrefab != null)
        {
            portalGO = Instantiate(portalPrefab, worldPos + Vector3.up * 1.5f, Quaternion.identity);
        }
        else
        {
            portalGO = Portal.CreateDefault(worldPos + Vector3.up * 1.5f);
        }
        Portal portal = portalGO.GetComponent<Portal>();
        if (portal == null) portal = portalGO.AddComponent<Portal>();
        portal.targetScene = nextSceneName;
        portal.overrideSpawnPosition = overrideNextScenePlayerPosition;
        portal.spawnPositionInTargetScene = nextScenePlayerPosition;
        Debug.Log($"Portal spawned at {worldPos} to '{nextSceneName}' (target spawn={nextScenePlayerPosition}).");
    }

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

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        StartSceneMusic();
        EnsureSpawners();
        EnsureUiPrefabs();
        EnsurePlayerComponents();

        // Apply after every Start has run; PlayerHealth.Start resets currentHealth and would clobber the snapshot.
        if (PlayerRunState.HasSnapshot)
            StartCoroutine(ApplyRunStateNextFrame());

        if (autoSpawnBossOnStart)
            StartCoroutine(AutoSpawnBossNextFrame());
    }

    private void StartSceneMusic()
    {
        // PlayMusic is a no-op for an already-playing clip, so re-entering a level won't restart the track.
        string sceneName = SceneManager.GetActiveScene().name;
        switch (sceneName)
        {
            case "level1": SoundFx.PlayMusic(SoundFx.StageOneMusic); break;
            case "level2": SoundFx.PlayMusic(SoundFx.StageTwoMusic); break;
            case "level3": SoundFx.PlayMusic(SoundFx.StageThreeMusic); break;
            case "level4": SoundFx.PlayMusic(SoundFx.BossMusic); break;
            default: SoundFx.StopMusic(); break;
        }
    }

    private void EnsureSpawners()
    {
        if (FindFirstObjectByType<EnemySpawner>() == null)
        {
            GameObject spawnerGO = new GameObject("EnemySpawner (auto)");
            spawnerGO.AddComponent<EnemySpawner>();
            Debug.Log("Auto-created EnemySpawner");
        }

        if (FindFirstObjectByType<ChestSpawner>() == null)
        {
            GameObject chestSpawnerGO = new GameObject("ChestSpawner (auto)");
            chestSpawnerGO.AddComponent<ChestSpawner>();
            Debug.Log("Auto-created ChestSpawner");
        }
    }

    private void EnsureUiPrefabs()
    {
        if (FindFirstObjectByType<PauseMenu>() == null)
        {
            // Prefer the Resources/UI prefab; fall back to a script-only GameObject if it's missing.
            PauseMenu prefab = Resources.Load<PauseMenu>("UI/PauseMenu");
            if (prefab != null) Instantiate(prefab).gameObject.name = "PauseMenu";
            else new GameObject("PauseMenu (auto)").AddComponent<PauseMenu>();
        }

        if (hud == null)
        {
            GameHUD hudPrefab = Resources.Load<GameHUD>("UI/GameHUD");
            if (hudPrefab != null)
            {
                hud = Instantiate(hudPrefab);
                hud.gameObject.name = "GameHUD";
            }
        }
    }

    private void EnsurePlayerComponents()
    {
        if (playerHealth != null && playerHealth.GetComponent<PlayerWeapon>() == null)
        {
            playerHealth.gameObject.AddComponent<PlayerWeapon>();
            Debug.Log("Auto-added PlayerWeapon (magic attack) to player.");
        }

        if (playerHealth != null && playerHealth.GetComponent<PlayerVisualSwap>() == null)
        {
            playerHealth.gameObject.AddComponent<PlayerVisualSwap>();
            Debug.Log("Auto-added PlayerVisualSwap to player. Drag the GanzSe modular character prefab into its 'Character Prefab' slot.");
        }

        if (playerHealth != null && playerHealth.GetComponent<PlayerCombatStance>() == null)
            playerHealth.gameObject.AddComponent<PlayerCombatStance>();

        if (playerHealth != null && playerHealth.GetComponent<PlayerLevel>() == null)
            playerHealth.gameObject.AddComponent<PlayerLevel>();

        if (playerHealth != null && playerHealth.GetComponent<PlayerHeadLookAt>() == null)
            playerHealth.gameObject.AddComponent<PlayerHeadLookAt>();

        if (playerHealth != null && playerHealth.GetComponent<PlayerCastSpineTwist>() == null)
            playerHealth.gameObject.AddComponent<PlayerCastSpineTwist>();
    }

    private IEnumerator AutoSpawnBossNextFrame()
    {
        // Wait two frames so PlayerRunState.Apply has positioned the player; the boss spawns off player.forward.
        yield return null;
        yield return null;
        if (gameOver) yield break;
        var spawner = FindFirstObjectByType<EnemySpawner>();
        if (spawner == null) { Debug.LogWarning("autoSpawnBossOnStart=true but no EnemySpawner."); yield break; }
        phase = StagePhase.BossFight;
        spawner.SpawnBoss();
    }

    private IEnumerator ApplyRunStateNextFrame()
    {
        yield return null;
        PlayerRunState.Apply(this);
    }

    private void Update()
    {
        if (gameOver) return;

        timeElapsed += Time.deltaTime;

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

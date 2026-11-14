using UnityEngine;

/// <summary>
/// On-screen HUD displaying active arrow count, health, dash readiness, and controls.
/// Works instantly using OnGUI without requiring prefab/canvas setup.
/// </summary>
public class ChaosHUD : MonoBehaviour
{
    private PlayerHealth _playerHealth;
    private PlayerMovement _playerMovement;
    private ArrowShooter _arrowShooter;
    private EnemySpawner _enemySpawner;
    private GameLoopManager _gameLoop;
    private GUIStyle _titleStyle;
    private GUIStyle _subStyle;
    private GUIStyle _boxStyle;
    private GUIStyle _bannerStyle;

    private void Start()
    {
        var player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            _playerHealth = player.GetComponent<PlayerHealth>();
            _playerMovement = player.GetComponent<PlayerMovement>();
            _arrowShooter = player.GetComponent<ArrowShooter>();
        }
        _enemySpawner = Object.FindAnyObjectByType<EnemySpawner>();
        _gameLoop = GameLoopManager.Instance;
    }

    private void OnGUI()
    {
        InitStyles();

        if (_enemySpawner == null)
        {
            _enemySpawner = Object.FindAnyObjectByType<EnemySpawner>();
        }
        if (_gameLoop == null)
        {
            _gameLoop = GameLoopManager.Instance;
        }

        // Top-Center: Area Objective & Gateway Notification
        if (_gameLoop != null)
        {
            GUILayout.BeginArea(new Rect((Screen.width - 400) * 0.5f, 18, 400, 85), _boxStyle);
            GUI.color = new Color(1f, 0.85f, 0.2f);
            GUILayout.Label($"AREA {_gameLoop.currentArea}: {_gameLoop.CurrentAreaName}", _titleStyle);
            GUI.color = Color.white;

            if (_gameLoop.isObjectiveComplete)
            {
                GUI.color = Color.cyan;
                GUILayout.Label("★ GATEWAY UNLOCKED! ENTER PORTAL ★", _titleStyle);
                GUI.color = Color.white;
            }
            else
            {
                GUILayout.Label($"OBJECTIVE: Defeat {_gameLoop.targetQuota} Hostiles ({_gameLoop.enemiesDefeatedInArea} / {_gameLoop.targetQuota})", _subStyle);
                Rect progRect = GUILayoutUtility.GetRect(380, 10);
                GUI.Box(progRect, "");
                Rect fill = new Rect(progRect.x, progRect.y, progRect.width * _gameLoop.ProgressNormalized, progRect.height);
                DrawColorRect(fill, new Color(0.95f, 0.35f, 0.2f));
            }
            GUILayout.EndArea();

            // Center Screen Dramatic Announcement Banner
            if (_gameLoop.HasActiveBanner)
            {
                Rect bannerRect = new Rect((Screen.width - 640) * 0.5f, Screen.height * 0.26f, 640, 85);
                GUI.Box(bannerRect, "", _boxStyle);
                GUI.color = new Color(1f, 0.92f, 0.35f);
                GUI.Label(bannerRect, _gameLoop.currentBanner, _bannerStyle);
                GUI.color = Color.white;
            }
        }

        // Top-Left: Health & Arrow Chaos Counter
        GUILayout.BeginArea(new Rect(20, 20, 290, 220), _boxStyle);

        // Active Arrows Counter
        int arrowCount = ArrowShooter.ActiveArrowCount;
        Color arrowColor = arrowCount > 15 ? Color.red : (arrowCount > 7 ? new Color(1f, 0.5f, 0.1f) : Color.white);
        GUI.color = arrowColor;
        GUILayout.Label($"⚡ ACTIVE ARROWS: {arrowCount}", _titleStyle);
        GUI.color = Color.white;

        // Active Enemies
        if (_enemySpawner != null)
        {
            GUI.color = new Color(1f, 0.45f, 0.4f);
            GUILayout.Label($"💀 ENEMIES ALIVE: {_enemySpawner.ActiveEnemyCount}", _subStyle);
            GUI.color = Color.white;
        }

        GUILayout.Space(4);

        // Player Health Bar
        if (_playerHealth != null)
        {
            float hpPct = Mathf.Clamp01(_playerHealth.currentHealth / _playerHealth.maxHealth);
            GUILayout.Label($"HEALTH: {_playerHealth.currentHealth:F0} / {_playerHealth.maxHealth:F0}", _subStyle);

            Rect barRect = GUILayoutUtility.GetRect(250, 14);
            GUI.Box(barRect, "");
            Rect fillRect = new Rect(barRect.x, barRect.y, barRect.width * hpPct, barRect.height);
            Color hpColor = Color.Lerp(Color.red, Color.cyan, hpPct);
            DrawColorRect(fillRect, hpColor);
        }

        GUILayout.Space(5);

        // Bow Draw Tension Bar
        if (_arrowShooter != null)
        {
            float drawPct = _arrowShooter.DrawProgress;
            string stateLabel = _arrowShooter.CurrentState == ArrowShooter.BowState.FullyDrawn ? "BOW: [ FULL TENSION! ]" :
                                (_arrowShooter.CurrentState == ArrowShooter.BowState.Drawing ? "BOW: [ DRAWING... ]" : "BOW: [ NOCKED & READY ]");
            Color bowColor = _arrowShooter.CurrentState == ArrowShooter.BowState.FullyDrawn ? new Color(1f, 0.85f, 0.2f) :
                             (_arrowShooter.CurrentState == ArrowShooter.BowState.Drawing ? new Color(1f, 0.55f, 0.15f) : Color.white);
            GUI.color = bowColor;
            GUILayout.Label(stateLabel, _subStyle);
            GUI.color = Color.white;

            Rect bowBarRect = GUILayoutUtility.GetRect(250, 10);
            GUI.Box(bowBarRect, "");
            Rect bowFillRect = new Rect(bowBarRect.x, bowBarRect.y, bowBarRect.width * drawPct, bowBarRect.height);
            Color drawColor = Color.Lerp(new Color(1f, 0.5f, 0.1f), new Color(1f, 0.9f, 0.2f), drawPct);
            DrawColorRect(bowFillRect, drawColor);
        }

        GUILayout.Space(5);

        // Dash status
        if (_playerMovement != null)
        {
            string dashText = _playerMovement.IsDashing ? "DASH: [ ACTIVE! ]" : "DASH: [ READY - Space / RMB ]";
            GUI.color = _playerMovement.IsDashing ? Color.cyan : Color.white;
            GUILayout.Label(dashText, _subStyle);
            GUI.color = Color.white;
        }

        GUILayout.EndArea();

        // Top-Right: Controls Legend
        GUILayout.BeginArea(new Rect(Screen.width - 290, 20, 270, 130), _boxStyle);
        GUILayout.Label("TOP-DOWN ARCHERY", _titleStyle);
        GUILayout.Label("W A S D : Move freely (360°)", _subStyle);
        GUILayout.Label("Mouse : Aim Bow", _subStyle);
        GUILayout.Label("Left Click : 1 Arrow per Click (Hold to Aim)", _subStyle);
        GUILayout.Label("Space / Shift / RMB : Evasive Dash", _subStyle);
        GUILayout.EndArea();
    }

    private void InitStyles()
    {
        if (_titleStyle == null)
        {
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
            _titleStyle.normal.textColor = Color.white;

            _subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Normal
            };
            _subStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(10, 10, 8, 8)
            };

            _bannerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _bannerStyle.normal.textColor = Color.white;
        }
    }

    private void DrawColorRect(Rect rect, Color color)
    {
        Color prev = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = prev;
    }
}

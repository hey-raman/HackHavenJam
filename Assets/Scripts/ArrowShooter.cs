using System.Collections;
using UnityEngine;

/// <summary>
/// Authentic arcade-style Bow & Arrow weapon system for fast-paced 2.5D action.
/// Strictly enforces ONE ARROW PER ONE CLICK:
/// - Press & hold LMB: draws bow and holds at full tension (no auto-firing).
/// - Release LMB (or quick-click): looses exactly one arrow.
/// - Holding LMB after an arrow is loosed does NOT fire additional arrows.
/// - Every arrow requires an explicit click.
/// </summary>
public class ArrowShooter : MonoBehaviour
{
    public enum BowState
    {
        Idle,           // Resting with arrow nocked
        Drawing,        // String and arrow pulling back under tension
        FullyDrawn,     // Maximum draw power reached, holding tension
        Releasing,      // Snap forward, arrow launched, string vibrating
        Recovering      // Quick nocking of the next arrow
    }

    [Header("Archery Timing (Arcade Cadence)")]
    [Tooltip("Time in seconds to reach full draw power.")]
    public float fullDrawDuration = 0.20f;

    [Tooltip("Minimum draw time for quick snap-shots.")]
    public float minDrawDuration = 0.06f;

    [Tooltip("Brief recovery time after firing before the next arrow can be nocked.")]
    public float recoveryDuration = 0.05f;

    [Header("Arrow Power & Speed")]
    [Tooltip("Speed of a fully drawn arrow.")]
    public float maxArrowSpeed = 46f;

    [Tooltip("Speed of a quick snap-shot arrow.")]
    public float minArrowSpeed = 36f;

    [Tooltip("Damage of a fully drawn arrow.")]
    public float maxDamage = 38f;

    [Tooltip("Damage of a quick snap-shot arrow.")]
    public float minDamage = 22f;

    [Tooltip("Kinetic recoil impulse pushed back against the player on release.")]
    public float baseRecoilForce = 4.2f;

    [Header("Aiming & Visuals")]
    [Tooltip("Draw a predictive laser guide showing aim path and first bounce.")]
    public bool showAimGuide = true;

    [Tooltip("Color of the aim guide.")]
    public Color aimColor = new Color(1f, 0.6f, 0.15f, 0.65f);

    [Tooltip("Screen shake intensity on full draw release.")]
    public float maxFireShake = 0.14f;

    // References
    private PlayerMovement _movement;
    private LineRenderer _aimLine;
    private Transform _bowPivot;
    private Transform _bowRoot;
    private LineRenderer _bowstring;
    private Transform _nockedArrow;
    private Camera _mainCam;

    // State machine
    private BowState _state = BowState.Idle;
    private float _drawTimer = 0f;
    private float _stateTimer = 0f;
    private Vector2 _aimDirection = Vector2.right;

    // Single-shot click enforcement
    private bool _hasFiredThisClick = false;
    private bool _bufferedClick = false;

    // Bowstring animation positions (local to _bowRoot)
    private readonly Vector3 _upperTip = new Vector3(0.06f, 0.52f, 0f);
    private readonly Vector3 _lowerTip = new Vector3(0.06f, -0.52f, 0f);
    private readonly float _restingNockX = 0.06f;
    private readonly float _drawnNockX = -0.32f;
    private float _currentNockX = 0.06f;

    // Global active arrow counter
    public static int ActiveArrowCount { get; private set; } = 0;
    public static void RegisterArrow() => ActiveArrowCount++;
    public static void UnregisterArrow() => ActiveArrowCount = Mathf.Max(0, ActiveArrowCount - 1);

    private void Awake()
    {
        _movement = GetComponent<PlayerMovement>();
        _mainCam = Camera.main;

        SetupAimGuide();
        SetupRecurveBow();
    }

    private void SetupAimGuide()
    {
        if (!showAimGuide) return;

        _aimLine = GetComponent<LineRenderer>();
        if (_aimLine == null)
        {
            _aimLine = gameObject.AddComponent<LineRenderer>();
            _aimLine.useWorldSpace = true;
            _aimLine.startWidth = 0.045f;
            _aimLine.endWidth = 0.015f;
            _aimLine.material = new Material(Shader.Find("Sprites/Default"));
            _aimLine.startColor = aimColor;
            _aimLine.endColor = new Color(aimColor.r, aimColor.g, aimColor.b, 0.05f);
            _aimLine.positionCount = 3;
        }
    }

    private void SetupRecurveBow()
    {
        // 1. Pivot container on player
        GameObject bowPivotObj = new GameObject("BowPivot");
        bowPivotObj.transform.SetParent(transform);
        bowPivotObj.transform.localPosition = Vector3.zero;
        _bowPivot = bowPivotObj.transform;

        // 2. Bow Root container placed in front of player
        GameObject bowRootObj = new GameObject("BowRoot");
        bowRootObj.transform.SetParent(_bowPivot);
        bowRootObj.transform.localPosition = new Vector3(0.55f, 0f, 0f);
        bowRootObj.transform.localRotation = Quaternion.identity;
        _bowRoot = bowRootObj.transform;

        Material bowWoodMat = new Material(Shader.Find("Sprites/Default"));
        bowWoodMat.color = new Color(0.35f, 0.18f, 0.08f, 1f); // Rich dark mahogany wood

        Material bowGripMat = new Material(Shader.Find("Sprites/Default"));
        bowGripMat.color = new Color(0.18f, 0.14f, 0.12f, 1f); // Leather wrapped grip

        Material goldAccentMat = new Material(Shader.Find("Sprites/Default"));
        goldAccentMat.color = new Color(1f, 0.82f, 0.25f, 1f); // Golden tips

        // Center Riser / Grip
        GameObject grip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        grip.name = "Grip";
        grip.transform.SetParent(_bowRoot);
        grip.transform.localPosition = Vector3.zero;
        grip.transform.localRotation = Quaternion.identity;
        grip.transform.localScale = new Vector3(0.08f, 0.14f, 0.08f);
        Destroy(grip.GetComponent<Collider>());
        grip.GetComponent<MeshRenderer>().material = bowGripMat;

        // Upper Limb
        GameObject upperLimb = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        upperLimb.name = "UpperLimb";
        upperLimb.transform.SetParent(_bowRoot);
        upperLimb.transform.localPosition = new Vector3(0.01f, 0.28f, 0f);
        upperLimb.transform.localRotation = Quaternion.Euler(0, 0, -12f);
        upperLimb.transform.localScale = new Vector3(0.05f, 0.26f, 0.05f);
        Destroy(upperLimb.GetComponent<Collider>());
        upperLimb.GetComponent<MeshRenderer>().material = bowWoodMat;

        // Upper Tip (Recurve)
        GameObject upperTipObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        upperTipObj.name = "UpperTip";
        upperTipObj.transform.SetParent(_bowRoot);
        upperTipObj.transform.localPosition = _upperTip;
        upperTipObj.transform.localScale = new Vector3(0.07f, 0.07f, 0.07f);
        Destroy(upperTipObj.GetComponent<Collider>());
        upperTipObj.GetComponent<MeshRenderer>().material = goldAccentMat;

        // Lower Limb
        GameObject lowerLimb = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        lowerLimb.name = "LowerLimb";
        lowerLimb.transform.SetParent(_bowRoot);
        lowerLimb.transform.localPosition = new Vector3(0.01f, -0.28f, 0f);
        lowerLimb.transform.localRotation = Quaternion.Euler(0, 0, 12f);
        lowerLimb.transform.localScale = new Vector3(0.05f, 0.26f, 0.05f);
        Destroy(lowerLimb.GetComponent<Collider>());
        lowerLimb.GetComponent<MeshRenderer>().material = bowWoodMat;

        // Lower Tip (Recurve)
        GameObject lowerTipObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        lowerTipObj.name = "LowerTip";
        lowerTipObj.transform.SetParent(_bowRoot);
        lowerTipObj.transform.localPosition = _lowerTip;
        lowerTipObj.transform.localScale = new Vector3(0.07f, 0.07f, 0.07f);
        Destroy(lowerTipObj.GetComponent<Collider>());
        lowerTipObj.GetComponent<MeshRenderer>().material = goldAccentMat;

        // 3. Dynamic Bowstring (LineRenderer with 3 vertices)
        GameObject stringObj = new GameObject("Bowstring");
        stringObj.transform.SetParent(_bowRoot);
        stringObj.transform.localPosition = Vector3.zero;
        stringObj.transform.localRotation = Quaternion.identity;

        _bowstring = stringObj.AddComponent<LineRenderer>();
        _bowstring.useWorldSpace = false;
        _bowstring.startWidth = 0.022f;
        _bowstring.endWidth = 0.022f;
        _bowstring.material = new Material(Shader.Find("Sprites/Default"));
        _bowstring.startColor = new Color(0.95f, 0.95f, 0.9f, 0.95f);
        _bowstring.endColor = new Color(0.95f, 0.95f, 0.9f, 0.95f);
        _bowstring.positionCount = 3;

        UpdateBowstringPositions(_restingNockX);

        // 4. Nocked Arrow visual resting on the bow
        _nockedArrow = BuildArrowVisual("NockedArrow", _bowRoot).transform;
        _nockedArrow.localPosition = new Vector3(_restingNockX + 0.35f, 0f, 0f);
        _nockedArrow.localRotation = Quaternion.identity;
    }

    private void Update()
    {
        UpdateAimDirection();

        // One-shot input handling
        bool fireDown = Input.GetMouseButtonDown(0) || Input.GetButtonDown("Fire1") || Input.GetKeyDown(KeyCode.J);
        bool fireHeld = Input.GetMouseButton(0) || Input.GetButton("Fire1") || Input.GetKey(KeyCode.J);
        bool fireUp = Input.GetMouseButtonUp(0) || Input.GetButtonUp("Fire1") || Input.GetKeyUp(KeyCode.J);

        // Reset click-fired flag when button is completely released
        if (!fireHeld)
        {
            _hasFiredThisClick = false;
        }

        UpdateBowStateMachine(fireDown, fireHeld, fireUp);
        UpdateAimGuideVisual();
    }

    private void UpdateAimDirection()
    {
        if (_mainCam == null) _mainCam = Camera.main;
        if (_mainCam != null)
        {
            Plane plane = new Plane(Vector3.forward, Vector3.zero);
            Ray ray = _mainCam.ScreenPointToRay(Input.mousePosition);

            if (plane.Raycast(ray, out float enter))
            {
                Vector3 worldMouse = ray.GetPoint(enter);
                Vector2 diff = (Vector2)worldMouse - (Vector2)transform.position;
                if (diff.sqrMagnitude > 0.01f)
                {
                    _aimDirection = diff.normalized;
                }
            }
        }

        // Rotate entire bow pivot toward aim direction
        if (_bowPivot != null)
        {
            float angle = Mathf.Atan2(_aimDirection.y, _aimDirection.x) * Mathf.Rad2Deg;
            _bowPivot.rotation = Quaternion.Euler(0, 0, angle);
        }
    }

    private void UpdateBowStateMachine(bool fireDown, bool fireHeld, bool fireUp)
    {
        switch (_state)
        {
            case BowState.Idle:
                _nockedArrow.gameObject.SetActive(true);
                _currentNockX = Mathf.MoveTowards(_currentNockX, _restingNockX, Time.deltaTime * 18f);
                UpdateBowstringPositions(_currentNockX);
                _nockedArrow.localPosition = new Vector3(_currentNockX + 0.35f, 0f, 0f);

                // Start drawing ONLY on a new click, or buffered click
                if ((fireDown && !_hasFiredThisClick) || _bufferedClick)
                {
                    _bufferedClick = false;
                    _state = BowState.Drawing;
                    _drawTimer = 0f;
                    SoundEffects.PlayBowDraw();
                }
                break;

            case BowState.Drawing:
                _drawTimer += Time.deltaTime;
                float drawT = Mathf.Clamp01(_drawTimer / fullDrawDuration);

                // Smoothly pull bowstring and arrow backward
                _currentNockX = Mathf.Lerp(_restingNockX, _drawnNockX, Mathf.SmoothStep(0f, 1f, drawT));
                UpdateBowstringPositions(_currentNockX);
                _nockedArrow.localPosition = new Vector3(_currentNockX + 0.35f, 0f, 0f);

                if (drawT >= 1f)
                {
                    _state = BowState.FullyDrawn;
                    _stateTimer = 0f;
                }

                // If released during draw: loose arrow with current tension (at least minDraw power)
                if (fireUp && !_hasFiredThisClick)
                {
                    float loosePower = Mathf.Max(drawT, 0.35f);
                    LooseArrow(loosePower);
                }
                break;

            case BowState.FullyDrawn:
                // Held at maximum draw tension — WILL NOT auto-fire! Holds until release.
                _currentNockX = _drawnNockX;
                UpdateBowstringPositions(_currentNockX);
                _nockedArrow.localPosition = new Vector3(_currentNockX + 0.35f, 0f, 0f);

                // Loose on button release
                if (fireUp && !_hasFiredThisClick)
                {
                    LooseArrow(1.0f);
                }
                break;

            case BowState.Releasing:
                // Fast bowstring snap rebound vibration
                _stateTimer += Time.deltaTime;
                float snapT = _stateTimer / 0.07f;
                if (snapT < 1f)
                {
                    float vibration = Mathf.Sin(snapT * Mathf.PI * 4f) * (1f - snapT) * 0.08f;
                    _currentNockX = _restingNockX + vibration;
                    UpdateBowstringPositions(_currentNockX);
                }
                else
                {
                    _currentNockX = _restingNockX;
                    UpdateBowstringPositions(_restingNockX);
                    _state = BowState.Recovering;
                    _stateTimer = 0f;
                }

                // Buffer new click if pressed during release
                if (fireDown)
                {
                    _bufferedClick = true;
                }
                break;

            case BowState.Recovering:
                _stateTimer += Time.deltaTime;

                // Buffer new click if pressed during recovery
                if (fireDown)
                {
                    _bufferedClick = true;
                }

                if (_stateTimer >= recoveryDuration)
                {
                    _nockedArrow.gameObject.SetActive(true);
                    _nockedArrow.localPosition = new Vector3(_restingNockX + 0.35f, 0f, 0f);

                    // If a new click was clicked/buffered, start next draw; otherwise return to Idle
                    if (_bufferedClick)
                    {
                        _bufferedClick = false;
                        _state = BowState.Drawing;
                        _drawTimer = 0f;
                        SoundEffects.PlayBowDraw();
                    }
                    else
                    {
                        _state = BowState.Idle;
                    }
                }
                break;
        }
    }

    private void UpdateBowstringPositions(float nockX)
    {
        if (_bowstring == null) return;
        _bowstring.SetPosition(0, _upperTip);
        _bowstring.SetPosition(1, new Vector3(nockX, 0f, 0f));
        _bowstring.SetPosition(2, _lowerTip);
    }

    private void LooseArrow(float drawPower)
    {
        _state = BowState.Releasing;
        _stateTimer = 0f;
        _hasFiredThisClick = true; // Prevents any further arrows from this click
        _nockedArrow.gameObject.SetActive(false);

        // Calculate power-scaled arrow stats
        float speed = Mathf.Lerp(minArrowSpeed, maxArrowSpeed, drawPower);
        float damage = Mathf.Lerp(minDamage, maxDamage, drawPower);
        float recoil = baseRecoilForce * Mathf.Lerp(0.6f, 1.0f, drawPower);
        float shake = maxFireShake * Mathf.Lerp(0.5f, 1.0f, drawPower);

        // Spawn world projectile
        Vector3 spawnPos = _bowRoot.position + (Vector3)(_aimDirection * 0.15f);
        spawnPos.z = 0f;

        GameObject arrowObj = BuildArrowVisual("RicochetArrow", null);
        arrowObj.transform.position = spawnPos;

        RicochetArrow arrow = arrowObj.AddComponent<RicochetArrow>();
        arrow.speed = speed;
        arrow.damage = damage;
        arrow.Initialize(_aimDirection, transform, speed, damage);

        // Physical recoil against player
        if (_movement != null)
        {
            _movement.ApplyKnockback(-_aimDirection * recoil);
        }

        // Camera micro-shake & procedural bow twang
        CameraFollow.Shake(shake, 0.08f);
        SoundEffects.PlayBowRelease(drawPower);

        // Visual arrow launch shockwave
        SpawnLaunchEffect(spawnPos, _aimDirection, drawPower);
    }

    private GameObject BuildArrowVisual(string name, Transform parent)
    {
        GameObject arrow = new GameObject(name);
        if (parent != null)
        {
            arrow.transform.SetParent(parent);
            arrow.transform.localPosition = Vector3.zero;
            arrow.transform.localRotation = Quaternion.identity;
        }

        // Materials
        Material shaftMat = new Material(Shader.Find("Sprites/Default"));
        shaftMat.color = new Color(0.42f, 0.26f, 0.15f, 1f); // Cedar wood shaft

        Material headMat = new Material(Shader.Find("Sprites/Default"));
        headMat.color = new Color(0.95f, 0.65f, 0.15f, 1f); // Gleaming golden steel arrowhead

        Material featherMat = new Material(Shader.Find("Sprites/Default"));
        featherMat.color = new Color(0.92f, 0.22f, 0.18f, 1f); // Crimson fletching feathers

        // 1. Shaft (length 0.65m)
        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shaft.name = "Shaft";
        shaft.transform.SetParent(arrow.transform);
        shaft.transform.localPosition = Vector3.zero;
        shaft.transform.localRotation = Quaternion.Euler(0, 0, 90);
        shaft.transform.localScale = new Vector3(0.045f, 0.32f, 0.045f);
        Destroy(shaft.GetComponent<Collider>());
        shaft.GetComponent<MeshRenderer>().material = shaftMat;

        // 2. Arrowhead (Pointed tip)
        GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        tip.name = "Arrowhead";
        tip.transform.SetParent(arrow.transform);
        tip.transform.localPosition = new Vector3(0.33f, 0f, 0f);
        tip.transform.localScale = new Vector3(0.12f, 0.09f, 0.09f);
        Destroy(tip.GetComponent<Collider>());
        tip.GetComponent<MeshRenderer>().material = headMat;

        // 3. Fletching Feathers (Top and Bottom fins at tail)
        GameObject featherTop = GameObject.CreatePrimitive(PrimitiveType.Cube);
        featherTop.name = "FeatherTop";
        featherTop.transform.SetParent(arrow.transform);
        featherTop.transform.localPosition = new Vector3(-0.24f, 0.055f, 0f);
        featherTop.transform.localRotation = Quaternion.Euler(0, 0, -25f);
        featherTop.transform.localScale = new Vector3(0.11f, 0.035f, 0.012f);
        Destroy(featherTop.GetComponent<Collider>());
        featherTop.GetComponent<MeshRenderer>().material = featherMat;

        GameObject featherBottom = GameObject.CreatePrimitive(PrimitiveType.Cube);
        featherBottom.name = "FeatherBottom";
        featherBottom.transform.SetParent(arrow.transform);
        featherBottom.transform.localPosition = new Vector3(-0.24f, -0.055f, 0f);
        featherBottom.transform.localRotation = Quaternion.Euler(0, 0, 25f);
        featherBottom.transform.localScale = new Vector3(0.11f, 0.035f, 0.012f);
        Destroy(featherBottom.GetComponent<Collider>());
        featherBottom.GetComponent<MeshRenderer>().material = featherMat;

        return arrow;
    }

    private void SpawnLaunchEffect(Vector3 position, Vector2 direction, float drawPower)
    {
        GameObject burst = new GameObject("BowLaunchEffect");
        burst.transform.position = position;

        ParticleSystem ps = burst.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 0.22f;
        main.startSpeed = Mathf.Lerp(6f, 14f, drawPower);
        main.startSize = Mathf.Lerp(0.08f, 0.16f, drawPower);
        main.startColor = new Color(1f, 0.85f, 0.4f, 0.9f);
        main.stopAction = ParticleSystemStopAction.Destroy;
        main.loop = false;

        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, Mathf.RoundToInt(12 * drawPower)) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 18f;
        shape.radius = 0.08f;
        shape.rotation = new Vector3(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

        var rend = burst.GetComponent<ParticleSystemRenderer>();
        rend.material = new Material(Shader.Find("Sprites/Default"));

        ps.Play();
        Destroy(burst, 0.35f);
    }

    private void UpdateAimGuideVisual()
    {
        if (_aimLine == null || !showAimGuide) return;

        Vector2 start = (Vector2)_bowRoot.position + (_aimDirection * 0.2f);
        float castDistance = Mathf.Lerp(10f, 18f, DrawProgress);

        float startW = Mathf.Lerp(0.02f, 0.05f, DrawProgress);
        _aimLine.startWidth = startW;
        _aimLine.endWidth = 0.012f;

        Color currentColor = Color.Lerp(new Color(1f, 0.7f, 0.2f, 0.35f), aimColor, DrawProgress);
        _aimLine.startColor = currentColor;
        _aimLine.endColor = new Color(currentColor.r, currentColor.g, currentColor.b, 0.05f);

        RaycastHit2D hit = Physics2D.Raycast(start, _aimDirection, castDistance);
        if (hit.collider != null && hit.collider.gameObject != gameObject)
        {
            Vector2 hitPoint = hit.point;
            Vector2 normal = hit.normal;
            if (normal.sqrMagnitude < 0.001f)
            {
                Vector2 closest = hit.collider.ClosestPoint(start);
                Vector2 outNorm = (start - closest).normalized;
                normal = outNorm.sqrMagnitude > 0.001f ? outNorm : -_aimDirection;
            }

            Vector2 reflectDir = Vector2.Reflect(_aimDirection, normal);
            Vector2 endReflect = hitPoint + (reflectDir * 3.5f);

            _aimLine.positionCount = 3;
            _aimLine.SetPosition(0, new Vector3(start.x, start.y, 0f));
            _aimLine.SetPosition(1, new Vector3(hitPoint.x, hitPoint.y, 0f));
            _aimLine.SetPosition(2, new Vector3(endReflect.x, endReflect.y, 0f));
        }
        else
        {
            Vector2 end = start + (_aimDirection * castDistance);
            _aimLine.positionCount = 2;
            _aimLine.SetPosition(0, new Vector3(start.x, start.y, 0f));
            _aimLine.SetPosition(1, new Vector3(end.x, end.y, 0f));
        }
    }

    // Public getters for HUD & systems
    public BowState CurrentState => _state;
    public float DrawProgress => _state == BowState.FullyDrawn ? 1f : (_state == BowState.Drawing ? Mathf.Clamp01(_drawTimer / fullDrawDuration) : 0f);
    public bool IsDrawing => _state == BowState.Drawing || _state == BowState.FullyDrawn;
}

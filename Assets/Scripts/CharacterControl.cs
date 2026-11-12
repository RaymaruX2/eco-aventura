using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Control del jugador (gato).
// Mantiene la misma API publica que la version original (jumpForce, speed, runSpeed,
// resumeScreen, moving, resume) para no romper escenas ni otros scripts.
//
// Parametros del Animator (player-idle-1.controller) que alimenta este script:
//   speed (float)          velocidad horizontal real en el suelo -> Idle <-> Run
//   runMultiplier (float)  velocidad de reproduccion de Run, proporcional a la velocidad
//   grounded (bool)        en el suelo (con un pequeno margen para no parpadear en juntas de tiles)
//   vSpeed (float)         velocidad vertical -> Jump -> Fall
//   jump (trigger)         inicio de salto
//   climbing (bool)        trepando una escalera
//   climbMultiplier(float) 0 = trepar en pausa (quieto en la escalera), 1 = animando
//   dead / death           los gestiona Death.cs
[RequireComponent(typeof(Rigidbody2D))]
public class CharacterControl : MonoBehaviour
{
    [Header("Movimiento")]
    public float jumpForce = 2.0f;
    public float speed = 1.0f;
    public float runSpeed = 3.0f;  // Velocidad al mantener Shift

    [Tooltip("Aceleracion en suelo (unidades/s^2). Valores altos = respuesta mas inmediata.")]
    [SerializeField] private float groundAcceleration = 55f;
    [Tooltip("Frenado en suelo al soltar la direccion (evita el deslizamiento).")]
    [SerializeField] private float groundDeceleration = 70f;
    [Tooltip("Aceleracion en el aire (control aereo).")]
    [SerializeField] private float airAcceleration = 35f;

    [Header("Salto")]
    [Tooltip("Tiempo de gracia para saltar justo despues de salir de un borde.")]
    [SerializeField] private float coyoteTime = 0.1f;
    [Tooltip("Si se pulsa saltar un poco antes de aterrizar, el salto se ejecuta al tocar el suelo.")]
    [SerializeField] private float jumpBufferTime = 0.12f;
    [Tooltip("Multiplicador de gravedad al caer: caida mas firme y menos 'flotante'.")]
    [SerializeField] private float fallGravityMultiplier = 1.5f;
    [Tooltip("Al soltar el boton de salto durante la subida se corta el salto (altura variable).")]
    [SerializeField] private float jumpCutMultiplier = 0.5f;
    [SerializeField] private float maxFallSpeed = 14f;

    [Header("Doble salto")]
    [SerializeField] private bool permitirDobleSalto = true;
    [Tooltip("Saltos extra en el aire. Se recargan al tocar el suelo, al agarrarse a una pared o al pisar un enemigo.")]
    [SerializeField] private int saltosAereos = 1;
    [SerializeField] private float fuerzaDobleSalto = 6.3f;

    [Header("Agarre a paredes (opcional)")]
    [SerializeField] private bool permitirAgarrePared = true;
    [Tooltip("Tiempo (s) que el gato se queda quieto sujeto a la pared.")]
    [SerializeField] private float tiempoSujecion = 0.6f;
    [Tooltip("Tiempo maximo (s) en la pared; despues cae y no puede volver a agarrar esa pared hasta tocar el suelo.")]
    [SerializeField] private float tiempoMaxPared = 1.5f;
    [Tooltip("Velocidad a la que resbala cuando termina el tiempo de sujecion.")]
    [SerializeField] private float velocidadDeslizamiento = 1.5f;
    [Tooltip("Impulso del salto desde la pared (x: alejandose de la pared, y: hacia arriba).")]
    [SerializeField] private Vector2 impulsoSaltoPared = new Vector2(6f, 6.8f);
    [Tooltip("Tras saltar desde la pared se ignora la direccion un instante para que el gato se separe de ella.")]
    [SerializeField] private float bloqueoControlSaltoPared = 0.18f;

    [Header("Escalera")]
    [SerializeField] private float climbSpeedFactor = 1.5f;

    [Header("Fisica")]
    [SerializeField] private float baseGravityScale = 0.9f;

    [Header("Animacion")]
    [Tooltip("Velocidad horizontal minima para considerar que el gato camina.")]
    [SerializeField] private float walkThreshold = 0.15f;
    [Tooltip("Margen (s) durante el que se sigue considerando 'en el suelo' para la animacion; evita parpadeos a Fall en juntas o bordes.")]
    [SerializeField] private float groundedAnimGrace = 0.08f;

    [SerializeField] public GameObject resumeScreen;

    public static bool moving;
    public static bool resume = false;

    private Rigidbody2D _rigidbody2D;
    private Animator anim;
    private SpriteRenderer _spriteRenderer;
    private PlayerAttack _attack;

    private float _currentSpeed;
    private float inputX;
    private float inputY;
    private bool jumpCutRequested;
    private float lastGroundedTime = -10f;
    private float lastJumpPressedTime = -10f;
    private float lastJumpTime = -10f;
    private bool grounded;
    private bool isJumping;
    private bool deathHandled;

    // Doble salto
    private int airJumpsLeft;
    private bool jumpPressedThisFrame;
    // Pared
    private int contactWallDir;          // -1 pared a la izquierda, 1 a la derecha, 0 ninguna (este paso de fisica)
    private bool wallGrabbing;
    private int wallDir;
    private float wallGrabStart;
    private float lastWallContactTime;
    private float wallRegrabBlockedUntil;
    private int exhaustedWallDir;        // pared agotada (no se puede volver a agarrar hasta tocar suelo)
    private float inputLockUntil;

    public bool IsWallGrabbing => wallGrabbing;
    public bool IsGrounded => grounded;

    // Sonido y efectos
    private bool estabaEnSuelo = true;
    private float velocidadCaidaMax;
    private float proximoPaso;
    private float proximoPeldano;
    private Vector2 Pies => (Vector2)transform.position + new Vector2(0f, -0.9f);

    private readonly ContactPoint2D[] contacts = new ContactPoint2D[16];
    private readonly RaycastHit2D[] castHits = new RaycastHit2D[8];
    private ContactFilter2D groundFilter;

    [Header("Pegado al suelo (pendientes)")]
    [Tooltip("Distancia maxima por debajo a la que se 'pega' al suelo al bajar rampas o escalones pequenos.")]
    [SerializeField] private float groundSnapDistance = 0.3f;
    [Tooltip("Tiempo (s) tras perder el contacto durante el que se intenta el pegado.")]
    [SerializeField] private float groundSnapGrace = 0.12f;

    private static readonly int SpeedHash = Animator.StringToHash("speed");
    private static readonly int RunMultiplierHash = Animator.StringToHash("runMultiplier");
    private static readonly int GroundedHash = Animator.StringToHash("grounded");
    private static readonly int VSpeedHash = Animator.StringToHash("vSpeed");
    private static readonly int JumpHash = Animator.StringToHash("jump");
    private static readonly int ClimbingHash = Animator.StringToHash("climbing");
    private static readonly int ClimbMultiplierHash = Animator.StringToHash("climbMultiplier");
    private static readonly int DoubleJumpHash = Animator.StringToHash("doubleJump");
    private static readonly int WallGrabHash = Animator.StringToHash("wallGrab");

    // Tiempo minimo tras saltar durante el que se ignora el contacto con el suelo
    // (el contacto del paso de fisica anterior aun existe y anularia el salto).
    private const float JumpGroundIgnoreTime = 0.1f;

    private void Awake()
    {
        anim = GetComponent<Animator>();
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _attack = GetComponent<PlayerAttack>();
    }

    private bool IsAttacking => _attack != null && _attack.IsAttacking;

    private void Start()
    {
        // Las variables estaticas sobreviven entre escenas: se reinician al empezar cada nivel.
        moving = true;
        resume = false;
        Climb.isClimbing = false;
        Climb.isLadder = false;
        Death.isDeath = false;
        Death.isAttack = false;
        Enemy.isEnemyDeath = false;

        // Interpolacion: elimina el temblor del sprite cuando la camara sigue al jugador.
        _rigidbody2D.interpolation = RigidbodyInterpolation2D.Interpolate;
        _rigidbody2D.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        _rigidbody2D.freezeRotation = true;
        _rigidbody2D.gravityScale = baseGravityScale;

        // runSpeed nunca debe ser menor que speed (en el prefab estaba 3 < 4 y Shift ralentizaba).
        if (runSpeed < speed) runSpeed = speed * 1.5f;
        _currentSpeed = speed;
    }

    private void Update()
    {
        HandlePauseInput();

        bool canControl = moving && !resume && !Death.isDeath;
        if (!canControl)
        {
            inputX = 0f;
            inputY = 0f;
            UpdateAnimator();
            return;
        }

        inputX = Input.GetAxisRaw("Horizontal");
        inputY = Input.GetAxisRaw("Vertical");
        _currentSpeed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? runSpeed : speed;

        bool jumpPressed = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow);
        bool jumpReleased = Input.GetKeyUp(KeyCode.Space) || Input.GetKeyUp(KeyCode.W) || Input.GetKeyUp(KeyCode.UpArrow);

        // --- Escalera ---
        if (Climb.isLadder && !Climb.isClimbing && Mathf.Abs(inputY) > 0.1f && !(grounded && inputY < 0f))
        {
            StartClimbing();
            jumpPressed = false;
        }

        if (Climb.isClimbing)
        {
            bool jumpOff = Input.GetKeyDown(KeyCode.Space);
            if (jumpOff || Mathf.Abs(inputX) > 0.1f || (grounded && inputY < 0f))
            {
                StopClimbing();
                if (jumpOff)
                {
                    // Saltar desde la escalera: se permite como si estuviera en el suelo.
                    lastJumpPressedTime = Time.time;
                    lastGroundedTime = Time.time;
                }
            }
        }
        else if (jumpPressed)
        {
            lastJumpPressedTime = Time.time;
            jumpPressedThisFrame = true;
        }

        if (jumpReleased && isJumping) jumpCutRequested = true;

        // --- Orientacion del sprite (solo con entrada real, nunca por inercia) ---
        // Durante un ataque la direccion queda fija para que el zarpazo no cambie de lado.
        // Sujeto a una pared mira hacia fuera (el dibujo tiene la espalda contra la pared).
        if (wallGrabbing)
        {
            _spriteRenderer.flipX = wallDir > 0;
        }
        else if (!IsAttacking)
        {
            if (inputX > 0.01f) _spriteRenderer.flipX = false;
            else if (inputX < -0.01f) _spriteRenderer.flipX = true;
        }

        SonidosMovimiento();
        UpdateAnimator();
    }

    private void FixedUpdate()
    {
        UpdateGrounded();

        if (Death.isDeath)
        {
            HandleDeathPhysics();
            return;
        }
        deathHandled = false;
        moving = true;

        // Rebote al pisar a un enemigo.
        if (Enemy.isEnemyDeath)
        {
            Enemy.isEnemyDeath = false;
            if (Climb.isClimbing) StopClimbing();
            StopWallGrab(false);
            airJumpsLeft = saltosAereos;      // pisar a un enemigo recarga el doble salto
            _rigidbody2D.velocity = new Vector2(_rigidbody2D.velocity.x, jumpForce);
            isJumping = true;
            lastJumpTime = Time.time;
            jumpCutRequested = false;
            if (anim != null) anim.SetTrigger(JumpHash);
        }

        if (Climb.isClimbing)
        {
            _rigidbody2D.gravityScale = 0f;
            _rigidbody2D.velocity = new Vector2(0f, inputY * climbSpeedFactor * speed);
            return;
        }

        bool wantsJump = Time.time - lastJumpPressedTime <= jumpBufferTime;

        // --- Agarre a paredes ---
        if (wallGrabbing)
        {
            if (wantsJump) { WallJump(); }
            else if (UpdateWallGrab()) return;      // sigue sujeto: la fisica ya esta aplicada
        }
        else if (CanStartWallGrab())
        {
            StartWallGrab();
            if (UpdateWallGrab()) return;
        }

        // --- Movimiento horizontal con aceleracion / desaceleracion ---
        float targetX = inputX * _currentSpeed;
        // Al atacar en el suelo el gato se "planta" para golpear (conserva algo de inercia).
        if (IsAttacking && grounded) targetX *= _attack.GroundSpeedFactor;
        float accel;
        if (grounded)
            accel = Mathf.Abs(targetX) > 0.01f ? groundAcceleration : groundDeceleration;
        else
            accel = airAcceleration;
        // Justo despues de saltar desde una pared se conserva el impulso (el gato se separa de ella).
        float newX = Time.time < inputLockUntil
            ? _rigidbody2D.velocity.x
            : Mathf.MoveTowards(_rigidbody2D.velocity.x, targetX, accel * Time.fixedDeltaTime);

        float newY = _rigidbody2D.velocity.y;

        // --- Salto con coyote time y buffer ---
        bool canJump = Time.time - lastGroundedTime <= coyoteTime && !isJumping;
        if (wantsJump && !canJump && CanDoubleJump())
        {
            newY = fuerzaDobleSalto;
            airJumpsLeft--;
            isJumping = true;
            lastJumpTime = Time.time;
            lastJumpPressedTime = -10f;
            jumpCutRequested = false;
            jumpPressedThisFrame = false;
            if (anim != null) { anim.ResetTrigger(JumpHash); anim.SetTrigger(DoubleJumpHash); }
            Sonido.Reproducir("jugador/doble_salto");
            EfectosFX.Chispas(Pies, new Color(1f, 1f, 0.85f), 6);
        }
        else if (wantsJump && canJump)
        {
            newY = jumpForce;
            isJumping = true;
            grounded = false;
            lastJumpTime = Time.time;
            lastJumpPressedTime = -10f;
            lastGroundedTime = -10f;
            jumpCutRequested = false;
            if (_attack != null) _attack.Cancel();
            if (anim != null) anim.SetTrigger(JumpHash);
            Sonido.Reproducir("jugador/salto");
            EfectosFX.Polvo(Pies, 5);
        }

        // Altura de salto variable.
        if (jumpCutRequested)
        {
            if (newY > 0f) newY *= jumpCutMultiplier;
            jumpCutRequested = false;
        }

        // Gravedad mas fuerte en la caida para un salto con mas peso.
        _rigidbody2D.gravityScale = newY < 0f ? baseGravityScale * fallGravityMultiplier : baseGravityScale;
        if (newY < -maxFallSpeed) newY = -maxFallSpeed;

        _rigidbody2D.velocity = new Vector2(newX, newY);
        jumpPressedThisFrame = false;
    }

    // ------------------------------------------------------------------ doble salto
    private bool CanDoubleJump()
    {
        if (!permitirDobleSalto || airJumpsLeft <= 0 || grounded || Climb.isClimbing || wallGrabbing) return false;
        // Solo con una pulsacion hecha EN el aire (no con el buffer de aterrizaje)...
        if (!jumpPressedThisFrame && Time.time - lastJumpPressedTime > 0.02f) return false;
        // ...y si el suelo esta a punto de llegar, el salto se guarda para el aterrizaje.
        if (_rigidbody2D.velocity.y < 0f && TryFindGroundBelow(0.45f, out _)) return false;
        return true;
    }

    // ------------------------------------------------------------------ paredes
    private bool CanStartWallGrab()
    {
        if (!permitirAgarrePared || grounded || Climb.isClimbing || Death.isDeath || IsAttacking) return false;
        if (contactWallDir == 0 || Time.time < wallRegrabBlockedUntil) return false;
        if (contactWallDir == exhaustedWallDir) return false;
        // Hay que empujar hacia la pared (saltar hacia ella), no basta con rozarla.
        if (inputX * contactWallDir < 0.1f) return false;
        // No se agarra subiendo a toda velocidad (dejaria el salto a medias).
        return _rigidbody2D.velocity.y <= 2.5f;
    }

    private void StartWallGrab()
    {
        wallGrabbing = true;
        wallDir = contactWallDir;
        wallGrabStart = Time.time;
        lastWallContactTime = Time.time;
        airJumpsLeft = saltosAereos;      // la pared recarga el doble salto
        isJumping = false;
        Sonido.Reproducir("jugador/agarre_pared");
        EfectosFX.Polvo((Vector2)transform.position + new Vector2(wallDir * 0.65f, 0f), 4);
        jumpCutRequested = false;
        if (_attack != null) _attack.Cancel();
    }

    // Devuelve true mientras sigue sujeto.
    private bool UpdateWallGrab()
    {
        float t = Time.time - wallGrabStart;
        if (contactWallDir == wallDir) lastWallContactTime = Time.time;

        bool lostWall = Time.time - lastWallContactTime > 0.08f;
        bool letGo = inputX * wallDir < -0.1f || inputY < -0.5f;
        bool timeOut = t > tiempoMaxPared;

        if (grounded || lostWall || letGo || timeOut)
        {
            StopWallGrab(timeOut);
            return false;
        }

        float vy = t < tiempoSujecion ? 0f : -velocidadDeslizamiento;
        _rigidbody2D.gravityScale = 0f;
        // Un pequeno empuje hacia la pared mantiene el contacto.
        _rigidbody2D.velocity = new Vector2(wallDir * 0.6f, vy);
        return true;
    }

    private void StopWallGrab(bool exhausted)
    {
        if (!wallGrabbing) return;
        wallGrabbing = false;
        _rigidbody2D.gravityScale = baseGravityScale;
        wallRegrabBlockedUntil = Time.time + 0.25f;
        if (exhausted) exhaustedWallDir = wallDir;
    }

    private void WallJump()
    {
        int away = -wallDir;
        StopWallGrab(false);
        _rigidbody2D.gravityScale = baseGravityScale;
        _rigidbody2D.velocity = new Vector2(away * impulsoSaltoPared.x, impulsoSaltoPared.y);
        inputLockUntil = Time.time + bloqueoControlSaltoPared;
        isJumping = true;
        lastJumpTime = Time.time;
        lastJumpPressedTime = -10f;
        jumpCutRequested = false;
        jumpPressedThisFrame = false;
        _spriteRenderer.flipX = away < 0;
        if (anim != null) anim.SetTrigger(JumpHash);
        Sonido.Reproducir("jugador/salto_pared");
        EfectosFX.Polvo((Vector2)transform.position + new Vector2(-away * 0.65f, -0.3f), 5, away);
    }

    private void UpdateGrounded()
    {
        grounded = false;
        contactWallDir = 0;

        int count = _rigidbody2D.GetContacts(contacts);
        bool ignoreGround = Time.time - lastJumpTime <= JumpGroundIgnoreTime;
        for (int i = 0; i < count; i++)
        {
            Collider2D other = contacts[i].collider;
            if (other == null || other.isTrigger || other.CompareTag("Enemy")) continue;
            Vector2 n = contacts[i].normal;
            // Solo cuenta como suelo una superficie bajo los pies (no paredes ni techos).
            // Justo despues de saltar se ignoran los contactos del paso anterior.
            if (n.y > 0.6f && !ignoreGround) grounded = true;
            // Pared valida para agarrarse: superficie vertical del terreno (tag Ground),
            // no plataformas de un solo sentido ni objetos.
            else if (Mathf.Abs(n.x) > 0.7f && n.y < 0.35f && other.CompareTag("Ground") && !other.usedByEffector)
                contactWallDir = n.x > 0f ? -1 : 1;
        }

        // Pegado al suelo en pendientes y pequenos desniveles: al bajar una rampa corriendo, la
        // velocidad horizontal "despega" al gato de la superficie en cada paso de fisica y el
        // Animator alternaba Run/Fall. Si acaba de estar en el suelo, no ha saltado y hay suelo
        // muy cerca por debajo, se mantiene sobre la superficie.
        if (!grounded && !isJumping && !Climb.isClimbing && !Death.isDeath &&
            Time.time - lastGroundedTime <= groundSnapGrace && _rigidbody2D.velocity.y <= 0.1f)
        {
            if (TryFindGroundBelow(groundSnapDistance, out float distance))
            {
                grounded = true;
                if (distance > 0.02f) _rigidbody2D.position += Vector2.down * (distance - 0.01f);
                Vector2 v = _rigidbody2D.velocity;
                if (v.y > 0f) v.y = 0f;
                _rigidbody2D.velocity = v;
            }
        }

        // Aterrizaje: sonido y polvo segun la velocidad de la caida.
        if (!grounded) velocidadCaidaMax = Mathf.Min(velocidadCaidaMax, _rigidbody2D.velocity.y);
        else if (!estabaEnSuelo && !Death.isDeath)
        {
            if (velocidadCaidaMax < -11f) { Sonido.Reproducir("jugador/aterrizaje_fuerte"); EfectosFX.Polvo(Pies, 10); EfectosFX.Sacudir(0.12f, 0.15f); }
            else if (velocidadCaidaMax < -3f) { Sonido.Reproducir("jugador/aterrizaje"); EfectosFX.Polvo(Pies, 6); }
            velocidadCaidaMax = 0f;
        }
        estabaEnSuelo = grounded;

        if (grounded)
        {
            lastGroundedTime = Time.time;
            airJumpsLeft = saltosAereos;      // tocar el suelo recarga el doble salto
            exhaustedWallDir = 0;             // y permite volver a agarrar cualquier pared
            if (isJumping && _rigidbody2D.velocity.y <= 0.01f)
            {
                isJumping = false;
                // Un trigger de salto no consumido (p. ej. pulsado mientras trepaba)
                // no debe dispararse mas tarde al aterrizar.
                if (anim != null) anim.ResetTrigger(JumpHash);
            }
        }
    }

    private void SonidosMovimiento()
    {
        float vx = Mathf.Abs(_rigidbody2D.velocity.x);
        if (grounded && !wallGrabbing && !Climb.isClimbing && vx > walkThreshold * 2f && Time.time >= proximoPaso)
        {
            Sonido.Reproducir("jugador/paso", null, vx > speed * 1.2f ? 1f : 0.7f);
            // Ritmo de pasos proporcional a la velocidad (corriendo, pasos mas seguidos).
            proximoPaso = Time.time + Mathf.Clamp(1.3f / vx, 0.18f, 0.4f);
            if (vx > speed * 1.2f && Random.value < 0.35f) EfectosFX.Polvo(Pies, 2, -Mathf.Sign(_rigidbody2D.velocity.x));
        }
        if (Climb.isClimbing && Mathf.Abs(inputY) > 0.1f && Time.time >= proximoPeldano)
        {
            Sonido.Reproducir("jugador/escalera");
            proximoPeldano = Time.time + 0.22f;
        }
    }

    private bool TryFindGroundBelow(float maxDistance, out float distance)
    {
        distance = 0f;
        groundFilter.useTriggers = false;
        int n = _rigidbody2D.Cast(Vector2.down, groundFilter, castHits, maxDistance);
        float best = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            RaycastHit2D h = castHits[i];
            if (h.collider == null || h.collider.isTrigger || h.collider.CompareTag("Enemy")) continue;
            if (h.normal.y > 0.6f && h.distance < best) best = h.distance;
        }
        if (best == float.MaxValue) return false;
        distance = best;
        return true;
    }

    private void UpdateAnimator()
    {
        if (anim == null) return;

        float vx = _rigidbody2D.velocity.x;
        float vy = _rigidbody2D.velocity.y;
        bool climbing = Climb.isClimbing;

        // "En el suelo" para la animacion: suelo real o un instante de margen
        // (salvo si acaba de saltar), para que pasar por una junta no muestre Fall.
        bool animGrounded = grounded ||
                            (!isJumping && Time.time - lastGroundedTime < groundedAnimGrace && vy > -1f);

        float horizontal = Mathf.Abs(vx);
        anim.SetBool(GroundedHash, animGrounded && !climbing && !wallGrabbing);
        anim.SetBool(WallGrabHash, wallGrabbing);
        anim.SetFloat(VSpeedHash, vy);
        anim.SetFloat(SpeedHash, animGrounded && horizontal > walkThreshold ? horizontal : 0f);
        // Las patas avanzan al ritmo del desplazamiento: andar = 1x, correr con Shift = mas rapido.
        anim.SetFloat(RunMultiplierHash, Mathf.Clamp(horizontal / Mathf.Max(0.01f, speed), 0.5f, 2f));

        anim.SetBool(ClimbingHash, climbing);
        anim.SetFloat(ClimbMultiplierHash, climbing && Mathf.Abs(inputY) > 0.1f ? 1f : 0f);
    }

    private void StartClimbing()
    {
        StopWallGrab(false);
        airJumpsLeft = saltosAereos;
        Climb.isClimbing = true;
        isJumping = false;
        jumpCutRequested = false;
        lastJumpPressedTime = -10f;
        _rigidbody2D.velocity = Vector2.zero;
        if (anim != null) anim.ResetTrigger(JumpHash);
    }

    private void StopClimbing()
    {
        Climb.isClimbing = false;
        _rigidbody2D.gravityScale = baseGravityScale;
    }

    private void HandleDeathPhysics()
    {
        moving = false;
        _rigidbody2D.gravityScale = baseGravityScale;
        if (!deathHandled)
        {
            // Pequeno salto de "golpe" al recibir dano.
            deathHandled = true;
            Climb.isClimbing = false;
            wallGrabbing = false;
            inputLockUntil = 0f;
            isJumping = false;
            jumpCutRequested = false;
            lastJumpPressedTime = -10f;
            if (anim != null) anim.ResetTrigger(JumpHash);
            _rigidbody2D.velocity = new Vector2(0f, jumpForce * 0.6f);
        }
        else
        {
            _rigidbody2D.velocity = new Vector2(0f, Mathf.Max(_rigidbody2D.velocity.y, -maxFallSpeed * 0.5f));
        }
    }

    private void HandlePauseInput()
    {
        if (!Input.GetKeyDown(KeyCode.Escape) || Transicion.Ocupada) return;

        if (!resume)
        {
            // No permitir pausar sobre la pantalla de Game Over o de victoria (timeScale ya en 0).
            if (Time.timeScale == 0f || HealthSystem.health <= 0) return;
            resume = true;
            Time.timeScale = 0;
            Sonido.Reproducir("interfaz/pausa_entrar");
            if (resumeScreen != null) resumeScreen.SetActive(true);
        }
        else
        {
            resume = false;
            Time.timeScale = 1;
            Sonido.Reproducir("interfaz/pausa_salir");
            if (resumeScreen != null) resumeScreen.SetActive(false);
        }
    }
}

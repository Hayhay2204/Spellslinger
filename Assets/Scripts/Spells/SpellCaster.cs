using System.Collections.Generic;
using SpellSlinger.Gestures;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpellSlinger
{
    // The main spell casting mechanic from the design doc:
    //   Q         start targeting (time slows down, the camera stops and a cursor shows up)
    //   Hold LMB  draw the spell (you can do more than one line)
    //   RMB       cast the spell at wherever the cursor is
    //   Esc / Q   cancel without casting
    public class SpellCaster : MonoBehaviour
    {
        [Tooltip("How fast the cursor moves when you move the mouse")]
        public float drawSensitivity = 1f;
        [Tooltip("How close the drawing has to be to a glyph for it to count (0 to 1)")]
        public float minScore = 0.72f;
        [Tooltip("Anything drawn shorter than this (in pixels) gets ignored")]
        public float minStrokeLength = 90f;
        [Tooltip("How far in front of the camera the lines get drawn")]
        public float strokeDistance = 1f;
        [Tooltip("How slow time goes while targeting, 1 means normal speed")]
        public float targetingTimeScale = 0.3f;

        public PlayerController player;
        public Wand wand;

        [Header("Spell prefabs")]
        public LightningArc lightningPrefab;
        public Projectile fireballPrefab;
        public Projectile boulderPrefab;
        public ShieldBubble windWardPrefab;
        public VineField vinesPrefab;
        [Tooltip("Material used for all the little spark bursts when spells hit things")]
        public Material sparkMaterial;

        public bool IsTargeting { get; private set; }
        public bool IsDrawing { get; private set; }
        // Stops you from targeting, like when you're in a conversation
        public bool Blocked { get; set; }
        // Where the drawing cursor is on the screen in pixels
        public Vector2 CursorPos { get; private set; }
        public string Message { get; private set; }
        public Color MessageColor { get; private set; }
        public float MessageTime { get; private set; } = -99f;
        // What it thinks you're drawing right now, this gets shown on the HUD
        public string LiveGuess { get; private set; }

        Camera cam;
        GestureRecognizer recognizer;
        readonly List<List<Vector2>> strokes = new();
        readonly List<LineRenderer> lines = new();
        Material lineMat;
        float lineFade, nextGuessTime;
        Color lineColor;
        static readonly Color DrawColor = new(1.6f, 1.2f, 3.2f, 1f);

        void Start()
        {
            cam = player.Cam;
            if (sparkMaterial) FX.SparkMaterial = sparkMaterial;
            recognizer = GestureLibrary.Build();
            foreach (var spell in SpellBook.All)
                spell.Icon = GestureLibrary.RenderIcon(GestureLibrary.Canonical[spell.Name], spell.Color);
            lineMat = FX.Additive(DrawColor, shared: false);
        }

        void OnDisable() => GameTime.Targeting = false;

        void Update()
        {
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            if (mouse == null || kb == null) return;

            UpdateLineFade();

            if (player.IsDead || Blocked)
            {
                if (IsTargeting) ExitTargeting();
                return;
            }

            if (Cursor.lockState != CursorLockMode.Locked)
            {
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
                return;
            }

            if (!IsTargeting)
            {
                if (kb.qKey.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame) EnterTargeting();
                else if (kb.escapeKey.wasPressedThisFrame)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                return;
            }

            // While targeting
            if (kb.escapeKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame)
            {
                ExitTargeting();
                FadeLines(new Color(0.5f, 0.5f, 0.5f));
                return;
            }

            Vector2 p = CursorPos + mouse.delta.ReadValue() * drawSensitivity;
            p.x = Mathf.Clamp(p.x, 0f, Screen.width);
            p.y = Mathf.Clamp(p.y, 0f, Screen.height);
            CursorPos = p;
            wand.AimAt(StrokeToWorld(CursorPos));

            if (mouse.leftButton.wasPressedThisFrame) BeginStroke();
            if (IsDrawing)
            {
                var stroke = strokes[strokes.Count - 1];
                if ((CursorPos - stroke[stroke.Count - 1]).sqrMagnitude > 9f) AddPoint(CursorPos);
                // keep guessing while you draw so the line can change colour as the shape forms
                if (Time.unscaledTime >= nextGuessTime) UpdateLiveGuess();
                if (mouse.leftButton.wasReleasedThisFrame)
                {
                    IsDrawing = false;
                    wand.SetDrawing(false);
                    UpdateLiveGuess();
                }
            }

            if (mouse.rightButton.wasPressedThisFrame) Cast();
        }

        void EnterTargeting()
        {
            IsTargeting = true;
            player.LookLocked = true;
            GameTime.TargetingScale = targetingTimeScale;
            GameTime.Targeting = true;
            ClearLines();
            CursorPos = new Vector2(Screen.width, Screen.height) * 0.5f;
            LiveGuess = null;
            SFX.Play2D(SFX.Whoosh, 0.25f, 0f);
        }

        void ExitTargeting()
        {
            IsTargeting = false;
            IsDrawing = false;
            player.LookLocked = false;
            GameTime.Targeting = false;
            wand.StopAiming();
            wand.SetDrawing(false);
            LiveGuess = null;
        }

        void BeginStroke()
        {
            IsDrawing = true;
            lineFade = 1f;
            if (strokes.Count == 0) lineMat.SetColor("_BaseColor", DrawColor); // later strokes keep the guessed colour
            strokes.Add(new List<Vector2>());
            var line = FX.Line(cam.transform, lineMat, 0.008f * strokeDistance, false);
            line.gameObject.layer = gameObject.layer;
            lines.Add(line);
            AddPoint(CursorPos);
            wand.SetDrawing(true);
        }

        void AddPoint(Vector2 screen)
        {
            var stroke = strokes[strokes.Count - 1];
            var line = lines[lines.Count - 1];
            stroke.Add(screen);
            line.positionCount = stroke.Count;
            // The lines are parented to the camera so they need camera local positions
            line.SetPosition(stroke.Count - 1, cam.transform.InverseTransformPoint(StrokeToWorld(screen)));
        }

        Vector3 StrokeToWorld(Vector2 screen) => cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, strokeDistance));

        List<Vector2> AllPoints()
        {
            var all = new List<Vector2>();
            foreach (var s in strokes) all.AddRange(s);
            return all;
        }

        void UpdateLiveGuess()
        {
            nextGuessTime = Time.unscaledTime + 0.1f;
            var pts = AllPoints();
            if (pts.Count < 6 || GestureRecognizer.PathLength(pts) < minStrokeLength)
            {
                LiveGuess = null;
                lineMat.SetColor("_BaseColor", DrawColor);
                return;
            }
            var r = GestureLibrary.Disambiguate(pts, recognizer.Recognize(pts));
            LiveGuess = r.Score >= minScore ? r.Name : "???";
            TintLine(r.Score >= minScore ? SpellBook.Get(r.Name) : null);
        }

        // Colours the line you're drawing to match the spell it looks like. Spells you haven't
        // learned yet show their colour faded out so you can tell it wont work
        void TintLine(SpellDef spell)
        {
            Color c = DrawColor;
            if (spell != null)
            {
                c = spell.Color * 3.5f;
                if (!spell.Known) c = Color.Lerp(spell.Color, Color.gray, 0.6f) * 1.2f;
                c.a = 1f;
            }
            lineMat.SetColor("_BaseColor", c);
        }

        void Cast()
        {
            var pts = AllPoints();
            ExitTargeting();

            if (pts.Count < 6 || GestureRecognizer.PathLength(pts) < minStrokeLength)
            {
                FadeLines(new Color(0.5f, 0.5f, 0.5f));
                return;
            }

            var result = GestureLibrary.Disambiguate(pts, recognizer.Recognize(pts));
            var spell = result.Score >= minScore ? SpellBook.Get(result.Name) : null;
            Debug.Log($"Spell guess: {result.Name} ({result.Score:P0}){(spell == null ? ", not close enough" : "")}");

            if (spell == null) { Fizzle("The glyph falters..."); return; }
            if (!spell.Known) { Fizzle($"You don't know {spell.Name} yet - seek out the {spell.Sect}"); return; }
            if (!player.TrySpendMana(spell.ManaCost)) { Fizzle($"Not enough mana for {spell.Name}"); return; }

            // Shoot towards wherever the cursor was when you right clicked
            Ray ray = cam.ScreenPointToRay(CursorPos);
            Vector3 target = Physics.Raycast(ray, out var hit, 250f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                ? hit.point
                : ray.GetPoint(80f);

            wand.Flash(spell.Color);
            spell.Cast(new CastContext { Player = player, Caster = this, Wand = wand, Origin = wand.Tip.position, Target = target, AimRay = ray });
            ShowMessage($"{spell.Name}!  <size=22>({result.Score:P0} match)</size>", spell.Color);
            FadeLines(spell.Color * 3f);
        }

        void Fizzle(string message)
        {
            ShowMessage(message, new Color(0.75f, 0.75f, 0.8f));
            FX.Burst(wand.Tip.position, new Color(0.6f, 0.6f, 0.7f), 12, 1.2f, 0.05f, 0.4f, 0.3f);
            SFX.Play2D(SFX.Fizzle, 0.6f);
            FadeLines(new Color(0.6f, 0.6f, 0.6f));
        }

        void FadeLines(Color c)
        {
            lineColor = c;
            lineFade = 0.999f;
        }

        void ClearLines()
        {
            foreach (var l in lines) if (l) Destroy(l.gameObject);
            lines.Clear();
            strokes.Clear();
            lineFade = 0f;
        }

        void UpdateLineFade()
        {
            if (IsTargeting || lineFade <= 0f) return;
            lineFade = Mathf.MoveTowards(lineFade, 0f, Time.unscaledDeltaTime * 2.5f);
            lineMat.SetColor("_BaseColor", lineColor * lineFade);
            if (lineFade <= 0f) ClearLines();
        }

        public void ShowMessage(string msg, Color color)
        {
            Message = msg;
            MessageColor = color;
            MessageTime = Time.unscaledTime;
        }
    }
}

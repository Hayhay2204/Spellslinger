using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpellSlinger
{
    // All the UI for the prototype (health, mana, spellbook, compass, map, dialogue, boss bar etc).
    // Its using OnGUI for now so I could get it working quickly, will probably move it to proper UI later
    public class GameHUD : MonoBehaviour
    {
        public PlayerController player;
        public SpellCaster caster;
        public DialogueSystem dialogue;
        [Tooltip("Picture of the map that shows up when you press M")]
        public Texture2D mapTexture;

        struct PopupText { public Vector3 pos; public string text; public Color color; public float time; }
        static readonly List<PopupText> popups = new();
        static string bannerTitle, bannerSub;
        static Color bannerColor;
        static float bannerTime = -99f;

        const float RefHeight = 1080f;
        GUIStyle label, small, big, center, title, boxText;
        Zone currentZone;
        bool mapOpen;
        bool bossDefeated;
        float defeatedTime;

        public static void Popup(Vector3 worldPos, string text, Color color) =>
            popups.Add(new PopupText { pos = worldPos + Random.insideUnitSphere * 0.3f, text = text, color = color, time = Time.unscaledTime });

        public static void Banner(string titleText, string subtitle, Color color)
        {
            bannerTitle = titleText;
            bannerSub = subtitle;
            bannerColor = color;
            bannerTime = Time.unscaledTime;
        }

        void OnEnable() => MagicEater.Defeated += OnBossDefeated;
        void OnDisable() => MagicEater.Defeated -= OnBossDefeated;
        void OnDestroy() { popups.Clear(); bannerTime = -99f; }
        void OnBossDefeated() { bossDefeated = true; defeatedTime = Time.unscaledTime; }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !player) return;

            // Show the zone name when you walk into a new area
            var zone = World.ZoneAt(player.transform.position);
            if (zone != null && zone != currentZone && (currentZone == null || zone.zoneName != currentZone.zoneName))
                Banner(zone.zoneName, zone.subtitle, zone.mapColor);
            currentZone = zone;

            // Open and close the map
            if (kb.mKey.wasPressedThisFrame && !dialogue.Active && !caster.IsTargeting && !player.IsDead) SetMap(!mapOpen);
            else if (mapOpen && kb.escapeKey.wasPressedThisFrame) SetMap(false);

            // Cheat that unlocks every spell, makes testing and showing the game easier
            if (kb.semicolonKey.wasPressedThisFrame)
            {
                PlayerMagic.LearnAll();
                Banner("All magic learned", "(prototype shortcut)", Color.white);
            }
        }

        void SetMap(bool open)
        {
            mapOpen = open;
            player.MovementLocked = open;
            caster.Blocked = open;
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
        }

        void InitStyles()
        {
            if (label != null) return;
            label = new GUIStyle(GUI.skin.label) { fontSize = 22, richText = true };
            label.normal.textColor = Color.white;
            small = new GUIStyle(label) { fontSize = 17 };
            small.normal.textColor = new Color(1f, 1f, 1f, 0.8f);
            big = new GUIStyle(label) { fontSize = 60, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            title = new GUIStyle(label) { fontSize = 26, alignment = TextAnchor.MiddleCenter };
            center = new GUIStyle(label) { fontSize = 32, alignment = TextAnchor.MiddleCenter };
            boxText = new GUIStyle(label) { fontSize = 26, wordWrap = true, alignment = TextAnchor.UpperLeft };
        }

        void OnGUI()
        {
            if (!player || !caster) return;
            InitStyles();

            float scale = Screen.height / RefHeight;
            float W = Screen.width / scale, H = RefHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            if (player.DamageFlash > 0f) Rect(new Rect(0, 0, W, H), new Color(0.8f, 0f, 0.1f, player.DamageFlash * 0.3f));
            if (caster.IsTargeting) DrawTargetingOverlay(W, H, scale);

            DrawNameplates(scale);
            DrawEnemyBars(scale);
            DrawPopups(scale);
            DrawCompass(W);
            DrawBossBar(W);
            DrawObjective();
            DrawVitals(H);
            DrawSpellbook(W);
            DrawCastMessage(W, H);
            DrawBanner(W, H);
            DrawInteractPrompt(W, H);
            DrawDialogue(W, H);

            if (player.IsDead) DrawDeath(W, H);
            else if (mapOpen) DrawMap(W, H);
            else if (Cursor.lockState != CursorLockMode.Locked && !dialogue.Active)
            {
                Rect(new Rect(0, H / 2f - 50, W, 100), new Color(0f, 0f, 0f, 0.5f));
                center.normal.textColor = Color.white;
                GUI.Label(new Rect(0, H / 2f - 50, W, 100), "Click to play", center);
            }

            if (bossDefeated && Time.unscaledTime - defeatedTime > 4f) DrawVictory(W, H);

            if (!caster.IsTargeting && !dialogue.Active && !mapOpen)
            {
                Rect(new Rect(0, H - 34, W, 34), new Color(0f, 0f, 0f, 0.35f));
                GUI.Label(new Rect(0, H - 34, W, 34),
                    "<b>Q</b> ready wand  ·  <b>E</b> talk  ·  <b>M</b> map  ·  <b>WASD</b> move  ·  <b>Shift</b> sprint  ·  <b>Space</b> jump  ·  <b>;</b> learn all (debug)",
                    new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow });
            }
        }

        // Drawing each part of the HUD

        void DrawTargetingOverlay(float W, float H, float scale)
        {
            // Purple border so you can tell you're in casting mode
            for (int i = 0; i < 6; i++)
            {
                float t = i * 18f;
                var c = new Color(0.35f, 0.15f, 0.6f, 0.07f);
                Rect(new Rect(0, t, W, 18), c);
                Rect(new Rect(0, H - t - 18, W, 18), c);
                Rect(new Rect(t, 0, 18, H), c);
                Rect(new Rect(W - t - 18, 0, 18, H), c);
            }
            Vector2 cur = caster.CursorPos / scale;
            Vector2 cp = new(cur.x, H - cur.y);
            Dot(cp, caster.IsDrawing ? 24f : 18f, new Color(1f, 0.85f, 1f, 1f));
            if (!caster.IsDrawing)
            {
                // Crosshair on the cursor, the spell goes wherever this is pointing when you right click
                var tick = new Color(1f, 0.85f, 1f, 0.8f);
                Rect(new Rect(cp.x - 20, cp.y - 1, 10, 2), tick);
                Rect(new Rect(cp.x + 10, cp.y - 1, 10, 2), tick);
                Rect(new Rect(cp.x - 1, cp.y - 20, 2, 10), tick);
                Rect(new Rect(cp.x - 1, cp.y + 10, 2, 10), tick);
            }

            string guess = caster.LiveGuess == null ? "" :
                caster.LiveGuess == "???" ? "  ·  <color=#aaaaaa>unrecognised glyph</color>" :
                $"  ·  <b>{caster.LiveGuess}</b>";
            GUI.Label(new Rect(0, H - 110, W, 40),
                $"Hold <b>LMB</b> to draw  ·  move the cursor onto a target  ·  <b>RMB</b> cast  ·  <b>Esc</b>/<b>Q</b> cancel{guess}",
                new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow });
        }

        void DrawVitals(float H)
        {
            Bar(new Rect(30, H - 100, 380, 26), player.Health / player.maxHealth, new Color(0.85f, 0.15f, 0.25f), $"HP {Mathf.CeilToInt(player.Health)}");
            Bar(new Rect(30, H - 66, 380, 26), player.Mana / player.maxMana, new Color(0.35f, 0.45f, 1f), $"MANA {Mathf.FloorToInt(player.Mana)}");
            if (player.ShieldActive) GUI.Label(new Rect(30, H - 138, 380, 30), "<color=#cdf3ff>Wind Ward active</color>", label);
            else if (player.IsRooted) GUI.Label(new Rect(30, H - 138, 380, 30), "<color=#79ff5a>Rooted!</color>", label);
        }

        void DrawSpellbook(float W)
        {
            float x = W - 300, y = 24;
            Rect(new Rect(x - 12, y - 10, 290, 34 + SpellBook.All.Count * 80), new Color(0.05f, 0.02f, 0.1f, 0.55f));
            GUI.Label(new Rect(x, y - 6, 270, 30), $"<b>SPELLBOOK</b>  <size=15>({PlayerMagic.Count}/{SpellBook.All.Count})</size>", small);
            y += 28;
            var boss = MagicEater.Instance;
            foreach (var s in SpellBook.All)
            {
                var prev = GUI.color;
                bool known = s.Known;
                GUI.color = !known ? new Color(1f, 1f, 1f, 0.25f) : player.Mana >= s.ManaCost ? Color.white : new Color(1f, 1f, 1f, 0.5f);
                if (s.Icon) GUI.DrawTexture(new Rect(x, y, 66, 66), s.Icon);
                GUI.color = prev;
                string hex = ColorUtility.ToHtmlStringRGB(s.Color);
                bool eaten = boss && !boss.IsDead && boss.IsImmune(s.Element);
                string status = eaten ? " <size=14><color=#ff7777>(eaten)</color></size>" : "";
                if (known)
                {
                    GUI.Label(new Rect(x + 74, y + 2, 210, 30), $"<b><color=#{hex}>{s.Name}</color></b> <size=15>{s.ManaCost:0}</size>{status}", label);
                    GUI.Label(new Rect(x + 74, y + 30, 210, 40), $"{s.Glyph} · {s.Description}", small);
                }
                else
                {
                    GUI.Label(new Rect(x + 74, y + 2, 210, 30), $"<color=#888888><b>???</b></color>{status}", label);
                    GUI.Label(new Rect(x + 74, y + 30, 210, 40), $"<color=#999999>Learn from the {s.Sect}</color>", small);
                }
                y += 80;
            }
        }

        void DrawCompass(float W)
        {
            const float width = 640f, y = 14f;
            float cx = W / 2f;
            Rect(new Rect(cx - width / 2f, y, width, 34), new Color(0f, 0f, 0f, 0.45f));
            float yaw = player.transform.eulerAngles.y;
            var style = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = 20 };

            void Mark(float bearing, string text, Color c, string sub = null)
            {
                float delta = Mathf.DeltaAngle(yaw, bearing);
                if (Mathf.Abs(delta) > 90f) return;
                float x = cx + delta / 90f * (width / 2f);
                style.normal.textColor = c;
                GUI.Label(new Rect(x - 60, y, 120, 34), text, style);
                if (sub != null)
                {
                    var s = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontSize = 14 };
                    s.normal.textColor = c;
                    GUI.Label(new Rect(x - 60, y + 30, 120, 20), sub, s);
                }
            }

            Mark(0f, "<b>N</b>", Color.white);
            Mark(90f, "E", Color.white);
            Mark(180f, "S", Color.white);
            Mark(270f, "W", Color.white);

            Vector3 p = player.transform.position;
            float Bearing(Vector3 to) => Mathf.Atan2(to.x - p.x, to.z - p.z) * Mathf.Rad2Deg;
            string Dist(Vector3 to) => $"{Mathf.RoundToInt(Vector2.Distance(new Vector2(p.x, p.z), new Vector2(to.x, to.z)))}m";

            foreach (var npc in NPC.All)
            {
                if (!npc.IsMentor || PlayerMagic.Knows(npc.teachesElement)) continue;
                var spell = SpellBook.Get(npc.teachesElement);
                Mark(Bearing(npc.transform.position), "◆", spell.Color, Dist(npc.transform.position));
            }
            var boss = MagicEater.Instance;
            if (boss && !boss.IsDead) Mark(Bearing(boss.Home), "<b>MAW</b>", new Color(1f, 0.8f, 0.2f), Dist(boss.Home));
        }

        void DrawObjective()
        {
            string main, sub;
            var boss = MagicEater.Instance;
            if (bossDefeated) { main = "The Magic Eater is slain"; sub = "Magic can be whole again."; }
            else if (PlayerMagic.Count < SpellBook.All.Count)
            {
                main = $"Learn the magic of the sects ({PlayerMagic.Count}/{SpellBook.All.Count})";
                sub = "The Magic Eater is already immune to your Storm magic. Follow the ◆ markers.";
            }
            else { main = "Face the Magic Eater at the Maw"; sub = "It will devour a new magic every time it loses a quarter of its strength."; }

            if (boss && boss.IsAwake && !boss.IsDead)
            {
                bool canHurt = false;
                foreach (var s in SpellBook.All)
                    if (s.Known && !boss.IsImmune(s.Element)) canHurt = true;
                if (!canHurt) sub = "<color=#ff8888>It has devoured every magic you know. Retreat and learn more!</color>";
            }

            Rect(new Rect(20, 20, 470, 78), new Color(0f, 0f, 0f, 0.4f));
            GUI.Label(new Rect(32, 26, 450, 30), $"<b>{main}</b>", label);
            GUI.Label(new Rect(32, 54, 450, 44), sub, new GUIStyle(small) { wordWrap = true, fontSize = 15 });
        }

        void DrawBossBar(float W)
        {
            var boss = MagicEater.Instance;
            if (!boss || boss.IsDead || !boss.IsAwake) return;
            const float width = 700f;
            float x = W / 2f - width / 2f, y = 72f;
            GUI.Label(new Rect(x, y, width, 30), "<b>THE MAGIC EATER</b>", new GUIStyle(label) { alignment = TextAnchor.MiddleCenter });
            var r = new Rect(x, y + 32, width, 20);
            Rect(r, new Color(0f, 0f, 0f, 0.6f));
            Rect(new Rect(r.x + 2, r.y + 2, (r.width - 4) * boss.Health / boss.maxHealth, r.height - 4), Color.Lerp(new Color(0.5f, 0.1f, 0.7f), boss.CoreColor, 0.5f));
            foreach (float t in new[] { 0.25f, 0.5f, 0.75f }) Rect(new Rect(r.x + r.width * t - 1, r.y - 3, 2, r.height + 6), new Color(1f, 1f, 1f, 0.8f));

            var parts = new List<string>();
            foreach (var e in boss.EatenOrder)
            {
                var s = SpellBook.Get(e);
                parts.Add($"<color=#{ColorUtility.ToHtmlStringRGB(s.Color)}>{s.Sect}</color>");
            }
            GUI.Label(new Rect(x, y + 54, width, 26), "Immune to: " + string.Join(", ", parts), new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });
        }

        void DrawCastMessage(float W, float H)
        {
            float age = Time.unscaledTime - caster.MessageTime;
            if (age > 2f) return;
            var c = caster.MessageColor;
            c.a = Mathf.Clamp01(2f - age);
            center.normal.textColor = c;
            GUI.Label(new Rect(0, H * 0.64f, W, 60), caster.Message, center);
        }

        void DrawBanner(float W, float H)
        {
            float age = Time.unscaledTime - bannerTime;
            if (age > 3.5f) return;
            float a = Mathf.Clamp01(age * 3f) * Mathf.Clamp01(3.5f - age);
            var c = bannerColor;
            c.a = a;
            big.normal.textColor = c;
            GUI.Label(new Rect(0, H * 0.2f, W, 90), bannerTitle, big);
            title.normal.textColor = new Color(1f, 1f, 1f, a * 0.9f);
            GUI.Label(new Rect(0, H * 0.2f + 80, W, 40), bannerSub, title);
        }

        void DrawNameplates(float scale)
        {
            var cam = player.Cam;
            var style = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = 20 };
            var sub = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontSize = 15 };
            foreach (var npc in NPC.All)
            {
                float d = Vector3.Distance(npc.transform.position, player.transform.position);
                if (d > 25f) continue;
                Vector3 sp = cam.WorldToScreenPoint(npc.HeadPosition + Vector3.up * (npc.IsMentor ? 1.3f : 0.5f));
                if (sp.z <= 0f) continue;
                Vector2 p = new(sp.x / scale, RefHeight - sp.y / scale);
                style.normal.textColor = new Color(1f, 1f, 1f, Mathf.Clamp01((25f - d) / 5f));
                GUI.Label(new Rect(p.x - 200, p.y - 40, 400, 26), $"<b>{npc.displayName}</b>", style);
                string t = npc.title;
                if (npc.IsMentor && !PlayerMagic.Knows(npc.teachesElement))
                {
                    var s = SpellBook.Get(npc.teachesElement);
                    t += $"  ·  <color=#{ColorUtility.ToHtmlStringRGB(s.Color)}>teaches {s.Name}</color>";
                }
                GUI.Label(new Rect(p.x - 200, p.y - 16, 400, 22), t, sub);
            }
        }

        void DrawInteractPrompt(float W, float H)
        {
            if (dialogue.Active || !dialogue.InRange) return;
            string text = $"<b>[E]</b> Talk to {dialogue.InRange.displayName}";
            Rect(new Rect(W / 2f - 220, H * 0.58f, 440, 44), new Color(0f, 0f, 0f, 0.55f));
            GUI.Label(new Rect(W / 2f - 220, H * 0.58f, 440, 44), text, new GUIStyle(label) { alignment = TextAnchor.MiddleCenter });
        }

        void DrawDialogue(float W, float H)
        {
            if (!dialogue.Active) return;
            var box = new Rect(W / 2f - 560, H - 290, 1120, 210);
            Rect(box, new Color(0.03f, 0.02f, 0.07f, 0.85f));
            Rect(new Rect(box.x, box.y, box.width, 4), dialogue.SpeakerColor);
            string hex = ColorUtility.ToHtmlStringRGB(Color.Lerp(dialogue.SpeakerColor, Color.white, 0.4f));
            GUI.Label(new Rect(box.x + 30, box.y + 16, box.width - 60, 34), $"<b><color=#{hex}>{dialogue.Speaker}</color></b>  <size=17><color=#aaaaaa>{dialogue.SpeakerTitle}</color></size>", label);
            string line = dialogue.CurrentLine;
            int n = Mathf.Clamp(dialogue.VisibleChars, 0, line.Length);
            GUI.Label(new Rect(box.x + 30, box.y + 58, box.width - 60, 120), line.Substring(0, n), boxText);
            if (n >= line.Length)
                GUI.Label(new Rect(box.xMax - 260, box.yMax - 38, 240, 30), "<b>[E]</b> continue", new GUIStyle(small) { alignment = TextAnchor.MiddleRight });
        }

        void DrawMap(float W, float H)
        {
            Rect(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.7f));
            float size = 820f;
            var r = new Rect(W / 2f - size / 2f, H / 2f - size / 2f + 20f, size, size);
            if (mapTexture) GUI.DrawTexture(r, mapTexture);

            Vector2 ToMap(Vector3 w) => new(r.x + (w.x / World.Size + 0.5f) * r.width, r.y + (0.5f - w.z / World.Size) * r.height);
            Vector3 ToWorld(Vector2 m) => new((m.x - r.x) / r.width * World.Size - World.Size / 2f, 0f, (0.5f - (m.y - r.y) / r.height) * World.Size);

            var labelStyle = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontSize = 16, fontStyle = FontStyle.Bold };
            var seen = new HashSet<string>();
            foreach (var z in Zone.All)
            {
                if (!z.showOnMap) continue;
                Vector2 m = ToMap(z.transform.position);
                Dot(m, 14f, z.mapColor);
                if (!seen.Add(z.zoneName)) continue;
                labelStyle.normal.textColor = Color.Lerp(z.mapColor, Color.white, 0.5f);
                GUI.Label(new Rect(m.x - 120, m.y + 6, 240, 22), z.floating ? z.zoneName + " (floating)" : z.zoneName, labelStyle);
            }
            foreach (var npc in NPC.All)
            {
                if (!npc.IsMentor || PlayerMagic.Knows(npc.teachesElement)) continue;
                Dot(ToMap(npc.transform.position), 20f, SpellBook.Get(npc.teachesElement).Color);
            }

            // Arrow showing where the player is
            Vector2 pm = ToMap(player.transform.position);
            var prevMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(player.transform.eulerAngles.y, pm * (Screen.height / RefHeight));
            GUI.Label(new Rect(pm.x - 20, pm.y - 22, 40, 40), "<b>▲</b>", new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = 28 });
            GUI.matrix = prevMatrix;

            GUI.Label(new Rect(0, r.y - 50, W, 40), "<b>MAP</b>   <size=17>(click to fast travel - prototype)   ·   M / Esc to close</size>", new GUIStyle(label) { alignment = TextAnchor.MiddleCenter });

            var e = Event.current;
            if (e.type == EventType.MouseDown && r.Contains(e.mousePosition))
            {
                Vector3 dest = ToWorld(e.mousePosition);
                if (World.HeightAt(dest) > 1f)
                {
                    player.Teleport(World.OnGround(dest, 0.5f));
                    SetMap(false);
                }
                e.Use();
            }
        }

        void DrawDeath(float W, float H)
        {
            Rect(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.55f));
            big.normal.textColor = new Color(1f, 0.4f, 0.5f);
            GUI.Label(new Rect(0, H * 0.38f, W, 100), "Your light fades...", big);
            center.normal.textColor = Color.white;
            GUI.Label(new Rect(0, H * 0.52f, W, 60), "Press R to wake at the Storm Spire", center);
        }

        void DrawVictory(float W, float H)
        {
            Rect(new Rect(0, H * 0.72f, W, 90), new Color(0f, 0f, 0f, 0.55f));
            center.normal.textColor = new Color(1f, 0.95f, 0.7f);
            GUI.Label(new Rect(0, H * 0.72f, W, 90), "VICTORY  -  you mastered every school of magic.  Thanks for playing the prototype!", center);
        }

        void DrawEnemyBars(float scale)
        {
            var cam = player.Cam;
            foreach (var c in Combatant.All)
            {
                if (c is not Enemy e || e.Health >= e.maxHealth) continue;
                Vector3 sp = cam.WorldToScreenPoint(e.transform.position + Vector3.up * (e.radius + 0.35f));
                if (sp.z <= 0f || sp.z > 60f) continue;
                float w = Mathf.Clamp(900f / sp.z, 30f, 90f);
                Vector2 p = new(sp.x / scale, RefHeight - sp.y / scale);
                var r = new Rect(p.x - w / 2f, p.y, w, 7f);
                Rect(r, new Color(0f, 0f, 0f, 0.6f));
                Rect(new Rect(r.x, r.y, r.width * Mathf.Clamp01(e.Health / e.maxHealth), r.height),
                    e.IsRooted ? new Color(0.4f, 0.9f, 0.3f) : new Color(0.8f, 0.3f, 1f));
            }
        }

        void DrawPopups(float scale)
        {
            var cam = player.Cam;
            var style = new GUIStyle(label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                float age = Time.unscaledTime - popups[i].time;
                if (age > 1f) { popups.RemoveAt(i); continue; }
                Vector3 sp = cam.WorldToScreenPoint(popups[i].pos + Vector3.up * age * 1.2f);
                if (sp.z <= 0f) continue;
                var c = popups[i].color;
                c.a = 1f - age;
                style.normal.textColor = c;
                GUI.Label(new Rect(sp.x / scale - 80, RefHeight - sp.y / scale - 15, 160, 30), popups[i].text, style);
            }
        }

        void Bar(Rect r, float t, Color color, string text)
        {
            Rect(r, new Color(0f, 0f, 0f, 0.55f));
            Rect(new Rect(r.x + 2, r.y + 2, (r.width - 4) * Mathf.Clamp01(t), r.height - 4), color);
            GUI.Label(new Rect(r.x + 10, r.y - 1, r.width, r.height), $"<size=17><b>{text}</b></size>", label);
        }

        static void Rect(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        static void Dot(Vector2 centre, float size, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(centre.x - size / 2f, centre.y - size / 2f, size, size), FX.SoftDot);
            GUI.color = prev;
        }
    }
}

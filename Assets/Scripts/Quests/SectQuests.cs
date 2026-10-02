using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpellSlinger
{
    // The other way to beat the game: getting the sects to work together.
    //   Pyromancers (Trial by Fire): burn out the husk nest closest to their camp
    //   Druids (Mothers Wound): cast vines on three drained crystals so they heal
    //   Earth + Air (Ground and Sky): get the Earth Guild to stop building at the crater, with the
    //   Air Mages paying them off with a skystone. Both sects join when its sorted
    // The mentors only talk about this after theyve taught you their spell
    public class SectQuests : MonoBehaviour
    {
        public struct Objective
        {
            public string text;
            public Vector3? target;
            public Color color;
        }

        enum Stage { NotStarted, Active, ReadyToReturn, Done }

        public static SectQuests Instance { get; private set; }

        [Tooltip("How many drained crystals the druids want healed")]
        public int crystalsNeeded = 3;
        [Tooltip("How close the vines have to be to a crystal to heal it")]
        public float healRange = 6f;
        [Tooltip("How far from the boss the allies stand during the fight")]
        public float allyRingRadius = 32f;

        Stage fire, nature;
        int sky; // 0 not started, 1 talk to Petra, 2 back to Wren, 3 skystone to Petra, 4 back to Wren, 5 done
        int healedCount;

        NPC ignar, bramble, petra, wren;
        EnemySpawner nest;
        readonly List<Transform> crystals = new();
        readonly HashSet<Transform> healed = new();
        GameObject scaffolds;
        readonly Dictionary<NPC, (Vector3 pos, Quaternion rot)> allyHomes = new();

        void Awake() => Instance = this;

        void Start()
        {
            foreach (var npc in NPC.All.Where(n => n.teaches))
            {
                switch (npc.teachesElement)
                {
                    case Element.Fire: ignar = npc; break;
                    case Element.Nature: bramble = npc; break;
                    case Element.Earth: petra = npc; break;
                    case Element.Air: wren = npc; break;
                }
            }
            if (ignar)
                nest = FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None)
                    .OrderBy(s => Vector3.Distance(s.transform.position, ignar.transform.position)).FirstOrDefault();
            foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t.name == "Drained Crystal") crystals.Add(t);
            scaffolds = GameObject.Find("Crater Construction");

            NPC.LinesOverride = Lines;
            DialogueSystem.ConversationFinished += OnTalked;
            VineField.Grew += OnVinesGrew;
            if (nest) nest.Cleared += OnNestCleared;
            MagicEater.Awakened += GatherAllies;
            PlayerController.Respawned += SendAlliesHome;
        }

        void OnDestroy()
        {
            if (NPC.LinesOverride == Lines) NPC.LinesOverride = null;
            DialogueSystem.ConversationFinished -= OnTalked;
            VineField.Grew -= OnVinesGrew;
            if (nest) nest.Cleared -= OnNestCleared;
            MagicEater.Awakened -= GatherAllies;
            PlayerController.Respawned -= SendAlliesHome;
        }

        // ---------------------------------------------------------------------
        // What the mentors say
        // ---------------------------------------------------------------------

        string[] Lines(NPC npc)
        {
            if (!npc.teaches || !PlayerMagic.Knows(npc.teachesElement)) return null; // they teach you first

            if (npc == ignar) return fire switch
            {
                Stage.NotStarted => new[]
                {
                    "You want the Pyromancers to fight beside you? Ha!",
                    "The strong dont follow the weak. So prove youre strong.",
                    "Theres a husk nest in the Devoured Lands, closest one to our camp. Burn it out. All of it.",
                    "Do that, and my warbands will follow you into the Maw itself.",
                },
                Stage.Active => new[] { "The nest still stands. Strength isnt talk, little storm." },
                Stage.ReadyToReturn => new[]
                {
                    "You actually did it. Fine. Strength is strength, wherever it comes from.",
                    "When you go for the beast, the Pyromancers burn beside you.",
                },
                _ => new[] { "Well be there when you face the beast. Dont make us wait." },
            };

            if (npc == bramble) return nature switch
            {
                Stage.NotStarted => new[]
                {
                    "You want Mothers children to fight? We dont fight, child. We heal.",
                    "But she is wounded. The drained crystals in the Devoured Lands are where the beast fed on her.",
                    $"Wrap {crystalsNeeded} of them in vines. Let her roots reach them and drink.",
                    "Heal her, and she will lend you her strength when the beast comes.",
                },
                Stage.Active => new[] { $"I can still feel the wounds. {crystalsNeeded - healedCount} more crystals, child." },
                Stage.ReadyToReturn => new[]
                {
                    "I felt it. She breathes easier.",
                    "The Druids will come when you face the beast. Mother insists.",
                },
                _ => new[] { "Mother is with you, child." },
            };

            if (npc == wren) return sky switch
            {
                0 => new[]
                {
                    "You ask us to stand beside the sects that drove us into the sky?",
                    "We would... if the Earth Mages stopped building toward our crater. Every season their scaffolds creep closer.",
                    "Speak to the Guildmaster. If she promises to leave the Sundering alone, the wind will answer your call.",
                },
                1 => new[] { "Speak to the Guildmaster in Terrabourne first." },
                2 => new[]
                {
                    "Payment. Of course.",
                    "Take this skystone. Its a piece of our old valley, the first stone we ever lifted. Its worth more than her whole city.",
                    "Give it to her, and let this be the end of it.",
                },
                3 => new[] { "Take the skystone to the Guildmaster." },
                4 => new[]
                {
                    "She agreed? After all these years...",
                    "Then the wind is yours. When you face the beast, we will keep you safe.",
                },
                _ => new[] { "The wind is with you." },
            };

            if (npc == petra) return sky switch
            {
                1 => new[]
                {
                    "Stop building at the crater? Do you know what that land is worth?",
                    "...Fine. But that floating rock is OUR land, technically. They owe us for it.",
                    "Tell them to pay what they owe, and Ill pull my builders back. A deals a deal.",
                },
                2 => new[] { "No payment, no deal. Go and see the air folk." },
                3 => new[]
                {
                    "Is that... a piece of the original valley? Do you know what collectors would pay for this?",
                    "Alright. The scaffolds come down. And when you go for the beast, the Earth Guild goes with you. Its good for business.",
                    "Tell the air folk... tell them were square.",
                },
                4 or 5 => new[] { "The Earth Guild stands with you. Dont tell anyone I said that for free." },
                _ => null, // not involved yet, so she just says her normal lines
            };

            return null;
        }

        void OnTalked(NPC npc)
        {
            if (!npc.teaches || !PlayerMagic.Knows(npc.teachesElement)) return;

            if (npc == ignar)
            {
                if (fire == Stage.NotStarted)
                {
                    fire = Stage.Active;
                    if (nest && nest.IsCleared) nest.Populate(); // so theres actually something to burn
                    QuestUpdate("Trial by Fire", "Destroy the husk nest", SpellBook.Get(Element.Fire).Color);
                }
                else if (fire == Stage.ReadyToReturn) Join(Element.Fire, ref fire, npc);
            }
            else if (npc == bramble)
            {
                if (nature == Stage.NotStarted)
                {
                    nature = Stage.Active;
                    QuestUpdate("Mothers Wound", $"Cast Vines on {crystalsNeeded} drained crystals", SpellBook.Get(Element.Nature).Color);
                }
                else if (nature == Stage.ReadyToReturn) Join(Element.Nature, ref nature, npc);
            }
            else if (npc == wren)
            {
                if (sky == 0) { sky = 1; QuestUpdate("Ground and Sky", "Talk to the Guildmaster in Terrabourne", SpellBook.Get(Element.Air).Color); }
                else if (sky == 2) { sky = 3; QuestUpdate("Ground and Sky", "Received a Skystone. Take it to the Guildmaster", SpellBook.Get(Element.Air).Color); }
                else if (sky == 4)
                {
                    sky = 5;
                    Pledge(Element.Air, npc);
                }
            }
            else if (npc == petra)
            {
                if (sky == 1) { sky = 2; QuestUpdate("Ground and Sky", "Tell the Air Mages what the Guild wants", SpellBook.Get(Element.Earth).Color); }
                else if (sky == 3)
                {
                    sky = 4;
                    if (scaffolds) scaffolds.SetActive(false); // they actually take the scaffolding down
                    Pledge(Element.Earth, npc);
                }
            }
        }

        void Join(Element element, ref Stage stage, NPC npc)
        {
            stage = Stage.Done;
            Pledge(element, npc);
        }

        void Pledge(Element element, NPC npc)
        {
            Alliance.Pledge(element);
            npc.PlayTeach();
            var spell = SpellBook.Get(element);
            FX.Burst(npc.transform.position + Vector3.up * 1.5f, spell.Color, 80, 5f, 0.15f, 1.2f, -0.5f);
            SFX.Play2D(SFX.Chime, 1f, 0f);
            string united = Alliance.AllUnited ? "Every sect stands with you" : $"{Alliance.Count} of {Alliance.SectCount} sects united";
            GameHUD.Banner($"THE {spell.Sect.ToUpper()} JOIN YOU", united, spell.Color);
        }

        static void QuestUpdate(string quest, string objective, Color color) => GameHUD.Banner(quest, objective, color);

        // ---------------------------------------------------------------------
        // Quest progress
        // ---------------------------------------------------------------------

        void OnNestCleared(EnemySpawner spawner)
        {
            if (fire != Stage.Active) return;
            fire = Stage.ReadyToReturn;
            QuestUpdate("Nest destroyed", "Return to Warlord Ignar", SpellBook.Get(Element.Fire).Color);
        }

        void OnVinesGrew(Vector3 pos)
        {
            if (nature != Stage.Active) return;
            foreach (var crystal in crystals)
            {
                if (!crystal || healed.Contains(crystal)) continue;
                Vector3 c = crystal.position;
                if (Vector2.Distance(new Vector2(c.x, c.z), new Vector2(pos.x, pos.z)) > healRange) continue;
                Heal(crystal);
                break;
            }
        }

        void Heal(Transform crystal)
        {
            healed.Add(crystal);
            healedCount++;
            var color = SpellBook.Get(Element.Nature).Color;
            if (crystal.TryGetComponent(out Renderer r)) r.material = FX.Glow(color * 2.5f, false);
            FX.Burst(crystal.position + Vector3.up, color, 60, 4f, 0.15f, 1.5f, -0.6f);
            FX.PointLight(crystal, Vector3.up, color, 2f, 8f);
            SFX.PlayAt(SFX.Shimmer, crystal.position, 1f);

            if (healedCount >= crystalsNeeded)
            {
                nature = Stage.ReadyToReturn;
                QuestUpdate("Mother breathes again", "Return to Elder Bramble", color);
            }
            else QuestUpdate("Crystal healed", $"{healedCount} of {crystalsNeeded}", color);
        }

        // ---------------------------------------------------------------------
        // What the HUD shows
        // ---------------------------------------------------------------------

        public List<Objective> Objectives
        {
            get
            {
                var list = new List<Objective>();
                var fireCol = SpellBook.Get(Element.Fire).Color;
                var natureCol = SpellBook.Get(Element.Nature).Color;
                var skyCol = SpellBook.Get(Element.Air).Color;

                if (fire == Stage.Active && nest) list.Add(new Objective { text = "Trial by Fire: destroy the husk nest", target = nest.transform.position, color = fireCol });
                if (fire == Stage.ReadyToReturn && ignar) list.Add(new Objective { text = "Trial by Fire: return to Warlord Ignar", target = ignar.transform.position, color = fireCol });

                if (nature == Stage.Active)
                {
                    var next = crystals.Where(c => c && !healed.Contains(c)).OrderBy(c => DistanceToPlayer(c.position)).FirstOrDefault();
                    list.Add(new Objective { text = $"Mothers Wound: vine the drained crystals ({healedCount}/{crystalsNeeded})", target = next ? next.position : null, color = natureCol });
                }
                if (nature == Stage.ReadyToReturn && bramble) list.Add(new Objective { text = "Mothers Wound: return to Elder Bramble", target = bramble.transform.position, color = natureCol });

                if ((sky == 1 || sky == 3) && petra) list.Add(new Objective { text = sky == 1 ? "Ground and Sky: talk to the Guildmaster" : "Ground and Sky: give the Guildmaster the skystone", target = petra.transform.position, color = skyCol });
                if ((sky == 2 || sky == 4) && wren) list.Add(new Objective { text = sky == 2 ? "Ground and Sky: tell Elder Wren the Guilds price" : "Ground and Sky: tell Elder Wren the news", target = wren.transform.position, color = skyCol });
                return list;
            }
        }

        static float DistanceToPlayer(Vector3 p)
        {
            var player = PlayerController.Instance;
            return player ? Vector3.Distance(player.transform.position, p) : 0f;
        }

        // ---------------------------------------------------------------------
        // The boss fight. Every sect thats joined you shows up and helps
        // ---------------------------------------------------------------------

        void GatherAllies()
        {
            var boss = MagicEater.Instance;
            if (!boss || Alliance.Count == 0) return;

            var allies = new[] { ignar, petra, wren, bramble }.Where(n => n && Alliance.Has(n.teachesElement)).ToList();
            for (int i = 0; i < allies.Count; i++)
            {
                var npc = allies[i];
                if (!allyHomes.ContainsKey(npc)) allyHomes[npc] = (npc.transform.position, npc.transform.rotation);

                // spread them round the south side of the arena, where the player comes in
                float angle = (-90f + (i - (allies.Count - 1) * 0.5f) * 35f) * Mathf.Deg2Rad;
                Vector3 spot = boss.Home + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * allyRingRadius;
                npc.transform.position = World.OnGround(spot);
                FX.Burst(npc.transform.position + Vector3.up, SpellBook.Get(npc.teachesElement).Color, 50, 5f, 0.2f, 1f);

                if (!npc.TryGetComponent(out AllyCaster ally)) ally = npc.gameObject.AddComponent<AllyCaster>();
                ally.element = npc.teachesElement;
            }
            GameHUD.Banner("YOUR ALLIES ARRIVE", $"{Alliance.Count} of {Alliance.SectCount} sects fight beside you", new Color(1f, 0.95f, 0.7f));
        }

        void SendAlliesHome()
        {
            foreach (var pair in allyHomes)
            {
                if (!pair.Key) continue;
                pair.Key.transform.SetPositionAndRotation(pair.Value.pos, pair.Value.rot);
                if (pair.Key.TryGetComponent(out AllyCaster ally)) Destroy(ally);
            }
            allyHomes.Clear();
        }
    }
}

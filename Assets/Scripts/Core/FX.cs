using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpellSlinger
{
    // Makes materials, particles and lights through code so I didnt need any art assets for the prototype.
    // These should get swapped out for proper assets later on
    public static class FX
    {
        static readonly Dictionary<string, Material> cache = new();
        static Texture2D softDot;

        public static Texture2D SoftDot => softDot != null ? softDot : (softDot = MakeSoftDot(64));

        static Shader FindShader(string name)
        {
            var s = Shader.Find(name);
            return s != null ? s : Shader.Find("Sprites/Default");
        }

        static bool TryCached(string key, bool shared, out Material m)
        {
            m = null;
            return shared && cache.TryGetValue(key, out m) && m != null;
        }

        public static Material Lit(Color color, float smoothness = 0.25f, Color? emission = null, bool shared = true)
        {
            string key = $"lit{color}{smoothness}{emission}";
            if (TryCached(key, shared, out var m)) return m;

            m = new Material(FindShader("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
            }
            if (shared) cache[key] = m;
            return m;
        }

        // Unlit material, if you use colour values above 1 it glows because of the bloom
        public static Material Glow(Color hdr, bool shared = true)
        {
            string key = $"glow{hdr}";
            if (TryCached(key, shared, out var m)) return m;

            m = new Material(FindShader("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", hdr);
            if (shared) cache[key] = m;
            return m;
        }

        // See through glowing material for magic effects, particles and lines
        public static Material Additive(Color hdr, Texture tex = null, bool doubleSided = false, bool shared = true)
        {
            string key = $"add{hdr}{(tex ? tex.GetInstanceID() : 0)}{doubleSided}";
            if (TryCached(key, shared, out var m)) return m;

            m = new Material(FindShader("Universal Render Pipeline/Particles/Unlit"));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 2f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.One);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", doubleSided ? (float)CullMode.Off : (float)CullMode.Back);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetColor("_BaseColor", hdr);
            if (tex) m.SetTexture("_BaseMap", tex);
            if (shared) cache[key] = m;
            return m;
        }

        // If this is set (the SpellCaster sets it from a saved material) all the sparks use it,
        // otherwise a material gets made in code
        public static Material SparkMaterial { get; set; }

        public static Material ParticleMaterial
        {
            get
            {
                if (SparkMaterial) return SparkMaterial;
                var m = Additive(new Color(2.4f, 2.4f, 2.4f, 1f), SoftDot);
                if (string.IsNullOrEmpty(m.name)) m.name = "Glow Particle";
                return m;
            }
        }

        public static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale,
            Material mat, bool keepCollider = false, bool castShadows = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            else if (type == PrimitiveType.Cylinder)
            {
                // Cylinders come with a capsule collider which doesnt work for flat things like the island, so use a mesh collider
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            }
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var r = go.GetComponent<Renderer>();
            if (mat) r.sharedMaterial = mat;
            if (!castShadows) r.shadowCastingMode = ShadowCastingMode.Off;
            if (parent) go.layer = parent.gameObject.layer;
            return go;
        }

        static ParticleSystem NewSystem(string name, Vector3 pos, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            if (parent) go.layer = parent.gameObject.layer;
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = FadeOut();
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = ParticleMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return ps;
        }

        // Quick burst of sparks that deletes itself after
        public static ParticleSystem Burst(Vector3 pos, Color color, int count, float speed, float size, float lifetime, float gravity = 0f)
        {
            var ps = NewSystem("Burst", pos, null);
            var main = ps.main;
            main.duration = 0.1f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.5f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.25f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.4f, size);
            main.startColor = color;
            main.gravityModifier = gravity;
            main.maxParticles = count;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            ps.Play();
            return ps;
        }

        // Particle emitter that keeps going, used for trails, the wand sparkles and background effects
        public static ParticleSystem Emitter(Transform parent, Color a, Color b, float ratePerSecond, float ratePerMeter,
            float size, float lifetime, float speed, float shapeRadius = 0.05f)
        {
            var ps = NewSystem("Emitter", parent.position, parent);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true; // so it starts playing by itself when its saved in the scene
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.5f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startColor = new ParticleSystem.MinMaxGradient(a, b);
            main.maxParticles = 500;
            var em = ps.emission;
            em.rateOverTime = ratePerSecond;
            em.rateOverDistance = ratePerMeter;
            var shape = ps.shape;
            shape.radius = shapeRadius;
            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            ps.Play();
            return ps;
        }

        // Takes the particles off the object so they can fade out properly instead of just disappearing
        public static void Detach(ParticleSystem ps)
        {
            ps.transform.SetParent(null, true);
            var main = ps.main;
            main.stopAction = ParticleSystemStopAction.Destroy;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        public static Light PointLight(Transform parent, Vector3 localPos, Color color, float intensity, float range)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        // Quick flash of light for when something hits
        public static void Flash(Vector3 pos, Color color, float intensity, float range, float duration)
        {
            var l = PointLight(null, pos, color, intensity, range);
            l.gameObject.AddComponent<FadeLight>().duration = duration;
        }

        public static LineRenderer Line(Transform parent, Material mat, float width, bool worldSpace)
        {
            var go = new GameObject("Line");
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = mat;
            lr.widthMultiplier = width;
            lr.useWorldSpace = worldSpace;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 3;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.positionCount = 0;
            return lr;
        }

        static Gradient FadeOut()
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        static Texture2D MakeSoftDot(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                float a = Mathf.Clamp01(1f - d);
                px[y * size + x] = new Color(1f, 1f, 1f, a * a);
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}

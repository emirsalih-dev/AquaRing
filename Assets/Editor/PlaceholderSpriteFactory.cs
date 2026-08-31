using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Aquaring.EditorTools
{
    /// <summary>
    /// Generates the throw-away placeholder art for the prototype (ring, peg, water
    /// tank, jet button, soft shadow) as PNG sprites under <c>Assets/Sprites</c>.
    /// Everything is drawn procedurally so the repo carries no binary art and the
    /// look can be tweaked from code. Replace these with real art later – the
    /// object sizes and pivots are what the scene relies on, not the pixels.
    /// </summary>
    public static class PlaceholderSpriteFactory
    {
        public const string SpriteFolder = "Assets/Sprites";
        public const int PixelsPerUnit = 512; // 512 px sprite == 1 world unit before scaling

        public static Sprite Ring   => LoadOrCreate("aquaring_ring",   512, DrawRing);
        public static Sprite Peg    => LoadOrCreate("aquaring_peg",    512, DrawPeg);
        public static Sprite Tank   => LoadOrCreate("aquaring_tank",   512, DrawTank);
        public static Sprite Button => LoadOrCreate("aquaring_button", 256, DrawButton);
        public static Sprite Shadow => LoadOrCreate("aquaring_shadow", 256, DrawShadow);

        // ----------------------------------------------------------------- infra

        private static Sprite LoadOrCreate(string name, int size, Func<float, float, Color> shader)
        {
            string path = $"{SpriteFolder}/{name}.png";

            if (!AssetDatabase.IsValidFolder(SpriteFolder))
                AssetDatabase.CreateFolder("Assets", "Sprites");

            if (File.Exists(path))
            {
                var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (existing != null) return existing;
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size;
                float v = (y + 0.5f) / size;
                pixels[y * size + x] = shader(u, v);
            }
            tex.SetPixels(pixels);
            tex.Apply();

            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            ConfigureImporter(path);

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void ConfigureImporter(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        // --------------------------------------------------------------- helpers

        private static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static float Sat(float x) => Mathf.Clamp01(x);

        private static Color Lerp(Color a, Color b, float t) => Color.Lerp(a, b, Sat(t));

        /// <summary>Distance from point p to the vertical segment a..b (all in 0..1 space).</summary>
        private static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Sat(Vector2.Dot(p - a, ab) / Vector2.Dot(ab, ab));
            return Vector2.Distance(p, a + ab * t);
        }

        // ------------------------------------------------------------- the shapes

        private static readonly Vector2 Light = new Vector2(-0.55f, 0.83f).normalized;

        private static Color DrawRing(float u, float v)
        {
            Vector2 p = new Vector2(u - 0.5f, v - 0.5f);
            float r = p.magnitude;

            const float outer = 0.46f, inner = 0.27f, aa = 0.010f;
            float coverage = Smooth(outer + aa, outer - aa, r) *
                             Smooth(inner - aa, inner + aa, r);
            if (coverage <= 0f) return Color.clear;

            float mid = (outer + inner) * 0.5f;
            float halfTube = (outer - inner) * 0.5f;
            Vector2 dir = r > 1e-4f ? p / r : Vector2.up;
            float tube = Mathf.Clamp((r - mid) / halfTube, -1f, 1f);   // -1 inner .. +1 outer
            Vector2 n = dir * tube;                                    // fake surface normal

            float diff = Sat(Vector2.Dot(n, Light)) * 0.85f + 0.35f;
            Color baseCol = new Color(0.12f, 0.72f, 0.80f);
            Color c = baseCol * diff;

            float spec = Mathf.Pow(Sat(Vector2.Dot(n, Light)), 10f);
            c += Color.white * (spec * 0.55f);

            // darken the very inner lip for a bit of depth
            c *= Lerp(new Color(0.75f, 0.75f, 0.75f), Color.white, Smooth(inner, mid, r));

            c.a = coverage;
            return c;
        }

        private static Color DrawPeg(float u, float v)
        {
            Vector2 p = new Vector2(u, v);
            const float top = 0.11f, bot = 0.95f, halfW = 0.085f, aa = 0.008f;

            float d = DistToSegment(p, new Vector2(0.5f, top), new Vector2(0.5f, bot));
            float coverage = Smooth(halfW, halfW - aa, d);
            if (coverage <= 0f) return Color.clear;

            // cylinder cross-section shading (bright centre, dark rims)
            float across = Mathf.Clamp((u - 0.5f) / halfW, -1f, 1f);
            float round = Mathf.Sqrt(Mathf.Max(0f, 1f - across * across));
            float shade = 0.30f + 0.80f * round;

            // light bias toward the top-left
            shade *= Mathf.Lerp(0.78f, 1.08f, Sat(v));

            Color baseCol = new Color(0.95f, 0.78f, 0.32f); // brass / gold peg
            Color c = baseCol * shade;

            // specular streak
            float streak = Mathf.Pow(Sat(1f - Mathf.Abs(across + 0.35f)), 6f);
            c += Color.white * (streak * 0.45f);

            // contact shadow where it meets the tank floor
            c *= Lerp(new Color(0.55f, 0.55f, 0.55f), Color.white, Smooth(bot, bot - 0.22f, v));

            c.a = coverage;
            return c;
        }

        private static Color DrawTank(float u, float v)
        {
            const float margin = 0.02f, corner = 0.07f, aa = 0.006f;

            // rounded-rect signed distance (outside positive)
            Vector2 q = new Vector2(
                Mathf.Abs(u - 0.5f) - (0.5f - margin - corner),
                Mathf.Abs(v - 0.5f) - (0.5f - margin - corner));
            float sd = Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - corner;

            float inside = Smooth(0f, -aa, sd);
            if (inside <= 0f) return Color.clear;

            // water gradient, kept semi-transparent so it reads like glass + water
            Color topCol = new Color(0.55f, 0.85f, 0.96f, 0.32f);
            Color botCol = new Color(0.10f, 0.42f, 0.74f, 0.55f);
            Color c = Lerp(botCol, topCol, v);

            // bright "water line" band near the top
            float line = Mathf.Exp(-Mathf.Pow((v - 0.82f) / 0.03f, 2f));
            c.r += line * 0.25f; c.g += line * 0.25f; c.b += line * 0.25f; c.a += line * 0.20f;

            // glass rim stroke
            float stroke = Smooth(-0.045f, -0.02f, sd) * Smooth(0f, -aa, sd);
            c = Lerp(c, new Color(0.85f, 0.96f, 1f, 0.95f), stroke);

            c.a *= inside;
            return c;
        }

        private static Color DrawButton(float u, float v)
        {
            Vector2 p = new Vector2(u - 0.5f, v - 0.5f);
            float r = p.magnitude;
            float ring = Smooth(0.48f, 0.44f, r) * Smooth(0.30f, 0.34f, r);
            float fill = Smooth(0.34f, 0.30f, r) * 0.35f;
            float a = Sat(ring + fill);
            return new Color(1f, 1f, 1f, a);
        }

        private static Color DrawShadow(float u, float v)
        {
            Vector2 p = new Vector2(u - 0.5f, v - 0.5f);
            float r = p.magnitude;
            float a = Smooth(0.5f, 0.05f, r);
            return new Color(0f, 0f, 0f, a);
        }
    }
}

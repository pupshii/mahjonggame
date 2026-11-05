using MahjongNight.Core;
using UnityEngine;

namespace MahjongNight.Game
{
    public enum TileLook
    {
        Normal,
        Highlight   // e.g. the discard that can be claimed
    }

    /// <summary>
    /// Builds greybox tiles from primitives: an ivory body, a green back half, and a text
    /// label on the face. The face is the tile's local -Z side, so an unrotated tile shows
    /// its face to a camera looking along +Z.
    /// </summary>
    public sealed class TileFactory
    {
        public const float Width = 0.045f;
        public const float Height = 0.06f;
        public const float Depth = 0.032f;

        private readonly Material faceMaterial;
        private readonly Material highlightMaterial;
        private readonly Material backMaterial;
        private readonly Font font;

        public TileFactory()
        {
            faceMaterial = CreateMaterial(new Color(0.96f, 0.94f, 0.86f));
            highlightMaterial = CreateMaterial(new Color(1f, 0.85f, 0.35f));
            backMaterial = CreateMaterial(new Color(0.12f, 0.45f, 0.3f));
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        /// <summary>A lit material that works in the built-in pipeline or URP.</summary>
        public static Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            var material = new Material(shader);
            material.color = color;
            return material;
        }

        /// <param name="showFace">False for hidden tiles: no label is created at all, so nothing can leak.</param>
        /// <param name="scale">Uniform scale, e.g. smaller tiles for the wall.</param>
        public TileView Create(Tile tile, bool showFace, Transform parent, TileLook look = TileLook.Normal, float scale = 1f)
        {
            var root = new GameObject(showFace ? tile.ToString() : "Tile");
            root.transform.SetParent(parent, false);
            root.transform.localScale = Vector3.one * scale;

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(Width, Height, Depth);
            body.GetComponent<Renderer>().sharedMaterial = look == TileLook.Highlight ? highlightMaterial : faceMaterial;

            GameObject back = GameObject.CreatePrimitive(PrimitiveType.Cube);
            back.name = "Back";
            Object.Destroy(back.GetComponent<Collider>());
            back.transform.SetParent(root.transform, false);
            back.transform.localPosition = new Vector3(0f, 0f, Depth * 0.26f);
            back.transform.localScale = new Vector3(Width * 1.02f, Height * 1.02f, Depth * 0.5f);
            back.GetComponent<Renderer>().sharedMaterial = backMaterial;

            if (showFace)
                AddLabel(root.transform, tile);

            var view = root.AddComponent<TileView>();
            view.Tile = tile;
            return view;
        }

        private void AddLabel(Transform tileRoot, Tile tile)
        {
            var label = new GameObject("Label");
            label.transform.SetParent(tileRoot, false);
            label.transform.localPosition = new Vector3(0f, 0f, -Depth * 0.5f - 0.0006f);

            var text = label.AddComponent<TextMesh>();
            text.font = font;
            text.text = LabelFor(tile);
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 48;
            text.characterSize = 0.0032f;
            text.lineSpacing = 0.9f;
            text.color = ColorFor(tile);
            label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        /// <summary>Greybox face text. Real tile art arrives in M8.</summary>
        public static string LabelFor(Tile tile)
        {
            switch (tile.Suit)
            {
                case TileSuit.Dots: return tile.Rank + "\nDot";
                case TileSuit.Bamboo: return tile.Rank + "\nBam";
                case TileSuit.Characters: return tile.Rank + "\nChr";
                case TileSuit.Wind: return new[] { "E", "S", "W", "N" }[tile.Rank - 1] + "\nWind";
                case TileSuit.Dragon: return new[] { "Red", "Grn", "Wht" }[tile.Rank - 1] + "\nDrgn";
                case TileSuit.Flower: return "F" + tile.Rank + "\nFlwr";
                case TileSuit.Season: return "S" + tile.Rank + "\nSesn";
                default: return "?";
            }
        }

        public static Color ColorFor(Tile tile)
        {
            switch (tile.Suit)
            {
                case TileSuit.Dots: return new Color(0.1f, 0.25f, 0.75f);
                case TileSuit.Bamboo: return new Color(0.05f, 0.5f, 0.2f);
                case TileSuit.Characters: return new Color(0.75f, 0.1f, 0.1f);
                case TileSuit.Wind: return new Color(0.1f, 0.1f, 0.1f);
                case TileSuit.Dragon:
                    return tile.Rank == 1 ? new Color(0.8f, 0.05f, 0.05f)
                        : tile.Rank == 2 ? new Color(0.05f, 0.55f, 0.15f)
                        : new Color(0.2f, 0.35f, 0.7f);
                default: return new Color(0.5f, 0.15f, 0.6f);
            }
        }
    }
}

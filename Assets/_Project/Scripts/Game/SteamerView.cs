using MahjongNight.Core;
using UnityEngine;

namespace MahjongNight.Game
{
    /// <summary>Marks a dumpling so clicks can be mapped back to its slot.</summary>
    public sealed class DumplingView : MonoBehaviour
    {
        public int Slot { get; set; }
    }

    /// <summary>
    /// Greybox bamboo steamer in the middle of the table: a cylinder with one sphere per
    /// dumpling. Eaten dumplings disappear; the basket flashes red when the pepper is eaten.
    /// The pepper's position is never shown — the view only knows which slots are eaten.
    /// </summary>
    public sealed class SteamerView
    {
        private const float BasketRadius = 0.1f;
        private const float BasketHeight = 0.03f;

        private readonly GameObject root;
        private readonly Renderer basket;
        private readonly Material basketMaterial;
        private readonly Material flashMaterial;
        private readonly DumplingView[] dumplings;
        private DumplingView hovered;

        public SteamerView(Transform parent, int size)
        {
            root = new GameObject("Steamer");
            root.transform.SetParent(parent, false);

            basketMaterial = TileFactory.CreateMaterial(new Color(0.62f, 0.45f, 0.24f));
            flashMaterial = TileFactory.CreateMaterial(new Color(0.9f, 0.12f, 0.05f));
            Material dumplingMaterial = TileFactory.CreateMaterial(new Color(0.97f, 0.94f, 0.85f));

            GameObject basketGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            basketGo.name = "Basket";
            Object.Destroy(basketGo.GetComponent<Collider>()); // only dumplings are clickable
            basketGo.transform.SetParent(root.transform, false);
            basketGo.transform.localPosition = new Vector3(0f, BasketHeight / 2f, 0f);
            basketGo.transform.localScale = new Vector3(BasketRadius * 2f, BasketHeight / 2f, BasketRadius * 2f);
            basket = basketGo.GetComponent<Renderer>();
            basket.sharedMaterial = basketMaterial;

            dumplings = new DumplingView[size];
            float ring = size == 1 ? 0f : BasketRadius * 0.6f;
            for (int i = 0; i < size; i++)
            {
                float angle = i * Mathf.PI * 2f / size;
                GameObject d = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                d.name = "Dumpling" + i;
                d.transform.SetParent(root.transform, false);
                d.transform.localPosition = new Vector3(Mathf.Sin(angle) * ring, BasketHeight + 0.016f, -Mathf.Cos(angle) * ring);
                d.transform.localScale = new Vector3(0.042f, 0.032f, 0.042f);
                d.GetComponent<Renderer>().sharedMaterial = dumplingMaterial;
                dumplings[i] = d.AddComponent<DumplingView>();
                dumplings[i].Slot = i;
            }
        }

        public void SetVisible(bool visible) => root.SetActive(visible);

        /// <summary>Shows exactly the uneaten dumplings of <paramref name="steamer"/>.</summary>
        public void Show(Steamer steamer)
        {
            for (int i = 0; i < dumplings.Length; i++)
                dumplings[i].gameObject.SetActive(i < steamer.Size && !steamer.IsEaten(i));
        }

        public void SetFlash(bool on) => basket.sharedMaterial = on ? flashMaterial : basketMaterial;

        /// <summary>Enlarges the hovered dumpling (null to clear).</summary>
        public void SetHover(DumplingView dumpling)
        {
            if (dumpling == hovered)
                return;
            if (hovered != null)
                hovered.transform.localScale = new Vector3(0.042f, 0.032f, 0.042f);
            hovered = dumpling;
            if (hovered != null)
                hovered.transform.localScale = new Vector3(0.055f, 0.042f, 0.055f);
        }
    }
}

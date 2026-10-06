using System.Collections.Generic;
using UnityEngine;

namespace PawTown
{
    /// <summary>
    /// Wardrobe catalog + applying the equipped look to the real 3D cat (on the Pet):
    /// fur recolours the cat's body / stripe / belly materials, collars recolour the collar, hats are small toon meshes
    /// on the head bone, trails are a particle sparkle behind her while she walks, face looks swap the eyes
    /// (happy ^^ / hearts) or add glasses on the head bone.
    /// </summary>
    public class PawCosmetics : MonoBehaviour
    {
        public enum Slot { Fur, Hat, Collar, Trail, Face }
        public enum Rarity { Common, Rare, Epic, Legendary }

        public class Item
        {
            public string id, name, icon;
            public Slot slot;
            public Rarity rarity;
            public int coins, fish;          // price (one of them), 0/0 = reward only
            public Color a, b, c;            // fur: body / stripe / belly; collar: a; hat: a (+b accent); trail: a, b
            public int pattern;              // fur: 0 none, 1 calico, 2 tortie, 3 galaxy, 4 colour points
            public Color pa, pb;             // fur pattern colours (patch / fleck / nebula / point, and second patch / star)
            public string shape;             // hat: cap / crown / chef / party / flowers; face: eyes / happy / heart / specs / shades
        }

        static Color H(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var c); return c; }

        public static readonly List<Item> Catalog = new List<Item>
        {
            // fur (body / stripe / belly sampled from each painted wardrobe icon so the 3D cat matches its picture)
            new Item { id = "fur_orange", name = "Orange Tabby", icon = "fur_orange", slot = Slot.Fur, a = H("FEA242"), b = H("E47A2B"), c = H("FFF0DC") },
            new Item { id = "fur_cream", name = "Creamsicle", icon = "fur_cream", slot = Slot.Fur, coins = 200, a = H("FFDCA7"), b = H("F6B06A"), c = H("FFF4E2") },
            new Item { id = "fur_ginger_white", name = "Ginger Socks", icon = "fur_ginger_white", slot = Slot.Fur, coins = 200, a = H("F2913C"), b = H("D66C23"), c = H("FFF6E8") },
            new Item { id = "fur_grey", name = "Smoky Grey", icon = "fur_grey", slot = Slot.Fur, rarity = Rarity.Rare, coins = 300, a = H("A39C98"), b = H("6E6764"), c = H("F4EEE4") },
            new Item { id = "fur_calico", name = "Calico", icon = "fur_calico", slot = Slot.Fur, rarity = Rarity.Rare, coins = 400, a = H("FFF6E8"), b = H("F3A04A"), c = H("FFFFFF"), pattern = 1, pa = H("F5A04A"), pb = H("3F3A37") },
            new Item { id = "fur_white", name = "Snowball", icon = "fur_white", slot = Slot.Fur, rarity = Rarity.Rare, fish = 15, a = H("FFF6E6"), b = H("F0E4D2"), c = H("FFFFFF") },
            new Item { id = "fur_tuxedo", name = "Tuxedo", icon = "fur_tuxedo", slot = Slot.Fur, rarity = Rarity.Epic, fish = 25, a = H("3A3233"), b = H("2A2424"), c = H("FFF6E8") },
            new Item { id = "fur_siamese", name = "Siamese", icon = "fur_siamese", slot = Slot.Fur, rarity = Rarity.Epic, a = H("FBE5C8"), b = H("E8CBA6"), c = H("FFF4E4"), pattern = 4, pa = H("5E3928"), pb = H("5E3928") },
            new Item { id = "fur_tortie", name = "Tortoiseshell", icon = "fur_tortie", slot = Slot.Fur, rarity = Rarity.Legendary, a = H("4E3428"), b = H("3A2820"), c = H("EBC08A"), pattern = 2, pa = H("D9803A"), pb = H("F2C38A") },
            new Item { id = "fur_bluerussian", name = "Blue Russian", icon = "fur_bluerussian", slot = Slot.Fur, rarity = Rarity.Rare, coins = 350, a = H("8E8F9C"), b = H("74748A"), c = H("A9A8B8") },
            new Item { id = "fur_strawberry", name = "Strawberry Milk", icon = "fur_strawberry", slot = Slot.Fur, rarity = Rarity.Epic, fish = 30, a = H("FFBCB8"), b = H("F7A3A8"), c = H("FFF4EA") },
            new Item { id = "fur_midnight", name = "Midnight", icon = "fur_midnight", slot = Slot.Fur, rarity = Rarity.Epic, fish = 25, a = H("3A3233"), b = H("2A2426"), c = H("4A3E3E") },
            new Item { id = "fur_honey", name = "Honey Gold", icon = "fur_honey", slot = Slot.Fur, rarity = Rarity.Rare, coins = 350, a = H("FEC66C"), b = H("E98E42"), c = H("FFF0D2") },
            new Item { id = "fur_lilac", name = "Lilac Point", icon = "fur_lilac", slot = Slot.Fur, rarity = Rarity.Rare, coins = 450, a = H("F8EEE4"), b = H("E6D8D0"), c = H("FFF8F0"), pattern = 4, pa = H("BDB0C6"), pb = H("BDB0C6") },
            new Item { id = "fur_mint", name = "Mint Dream", icon = "fur_mint", slot = Slot.Fur, rarity = Rarity.Epic, fish = 28, a = H("C9DDB0"), b = H("AEC596"), c = H("FFF8EC") },
            new Item { id = "fur_galaxy", name = "Galaxy", icon = "fur_galaxy", slot = Slot.Fur, rarity = Rarity.Legendary, fish = 60, a = H("2C2A63"), b = H("221F50"), c = H("3D3A80"), pattern = 3, pa = H("7A55C8"), pb = H("FFE58A") },
            // hats
            new Item { id = "hat_none", name = "No Hat", icon = "tile_item", slot = Slot.Hat },
            new Item { id = "hat_blue", name = "Delivery Cap", icon = "hat_blue", slot = Slot.Hat, coins = 150, shape = "cap", a = H("3E72C4"), b = H("FFFFFF") },
            new Item { id = "hat_red", name = "Red Cap", icon = "hat_red", slot = Slot.Hat, coins = 150, shape = "cap", a = H("D9483B"), b = H("FFFFFF") },
            new Item { id = "hat_green", name = "Forest Cap", icon = "hat_green", slot = Slot.Hat, rarity = Rarity.Rare, coins = 250, shape = "cap", a = H("3F6E5A"), b = H("E8C24A") },
            new Item { id = "hat_navy", name = "Night Cap", icon = "hat_navy", slot = Slot.Hat, rarity = Rarity.Rare, fish = 10, shape = "cap", a = H("2E3A6E"), b = H("C46AB0") },
            new Item { id = "hat_crown", name = "Golden Crown", icon = "hat_crown", slot = Slot.Hat, rarity = Rarity.Epic, shape = "crown", a = H("F2B33D"), b = H("E0473A") },
            new Item { id = "hat_crown2", name = "Royal Crown", icon = "hat_crown2", slot = Slot.Hat, rarity = Rarity.Legendary, fish = 60, shape = "crown", a = H("F7C531"), b = H("3E8FD9") },
            new Item { id = "hat_chef", name = "Baker's Hat", icon = "hat_chef", slot = Slot.Hat, coins = 220, shape = "chef", a = H("FAF7F0"), b = H("F0964A") },
            new Item { id = "hat_party", name = "Party Hat", icon = "hat_party", slot = Slot.Hat, rarity = Rarity.Rare, coins = 320, shape = "party", a = H("5CB0DE"), b = H("FFD65C") },
            new Item { id = "hat_beanie", name = "Cozy Beanie", icon = "hat_beanie", slot = Slot.Hat, coins = 180, shape = "beanie", a = H("E8873A"), b = H("FFF1D8") },
            new Item { id = "hat_witch", name = "Witch Hat", icon = "hat_witch", slot = Slot.Hat, rarity = Rarity.Rare, coins = 300, shape = "witch", a = H("6B3F8F"), b = H("F2B33D") },
            new Item { id = "hat_beret", name = "Painter's Beret", icon = "hat_beret", slot = Slot.Hat, coins = 160, shape = "beret", a = H("D9433A"), b = H("D9433A") },
            new Item { id = "hat_straw", name = "Sun Hat", icon = "hat_straw", slot = Slot.Hat, coins = 200, shape = "straw", a = H("E8C98A"), b = H("4F86C9") },
            new Item { id = "hat_cowboy", name = "Cowboy Hat", icon = "hat_cowboy", slot = Slot.Hat, rarity = Rarity.Rare, coins = 280, shape = "cowboy", a = H("8A5A3B"), b = H("5C3A25") },
            new Item { id = "hat_pirate", name = "Pirate Hat", icon = "hat_pirate", slot = Slot.Hat, rarity = Rarity.Rare, fish = 12, shape = "pirate", a = H("2B2626"), b = H("F2C14E") },
            new Item { id = "hat_santa", name = "Santa Hat", icon = "hat_santa", slot = Slot.Hat, rarity = Rarity.Rare, coins = 260, shape = "santa", a = H("D93B36"), b = H("FFF6EA") },
            new Item { id = "hat_tophat", name = "Fancy Top Hat", icon = "hat_tophat", slot = Slot.Hat, rarity = Rarity.Epic, fish = 20, shape = "tophat", a = H("2A2628"), b = H("F4A7B4") },
            new Item { id = "hat_propeller", name = "Propeller Cap", icon = "hat_propeller", slot = Slot.Hat, rarity = Rarity.Rare, coins = 240, shape = "propeller", a = H("3F7FD1"), b = H("F7C531") },
            new Item { id = "hat_frog", name = "Froggy Hat", icon = "hat_frog", slot = Slot.Hat, rarity = Rarity.Epic, fish = 22, shape = "frog", a = H("7DBE5C"), b = H("FFFFFF") },
            new Item { id = "hat_mushroom", name = "Mushroom Cap", icon = "hat_mushroom", slot = Slot.Hat, rarity = Rarity.Rare, coins = 300, shape = "mushroom", a = H("D9413A"), b = H("FFF6EA") },
            new Item { id = "hat_viking", name = "Viking Helmet", icon = "hat_viking", slot = Slot.Hat, rarity = Rarity.Epic, fish = 24, shape = "viking", a = H("8E8A86"), b = H("D9A33B") },
            new Item { id = "hat_grad", name = "Grad Cap", icon = "hat_grad", slot = Slot.Hat, rarity = Rarity.Rare, coins = 260, shape = "grad", a = H("2A2628"), b = H("F2B33D") },
            new Item { id = "hat_halo", name = "Angel Halo", icon = "hat_halo", slot = Slot.Hat, rarity = Rarity.Legendary, fish = 50, shape = "halo", a = H("FFD95C"), b = H("FFF3B0") },
            new Item { id = "hat_bunny", name = "Bunny Ears", icon = "hat_bunny", slot = Slot.Hat, rarity = Rarity.Rare, coins = 220, shape = "bunny", a = H("FFF8F0"), b = H("F4A7B4") },
            new Item { id = "hat_headphones", name = "Headphones", icon = "hat_headphones", slot = Slot.Hat, rarity = Rarity.Epic, fish = 26, shape = "headphones", a = H("E8873A"), b = H("4A3327") },
            new Item { id = "hat_flowers", name = "Flower Crown", icon = "hat_flowers", slot = Slot.Hat, rarity = Rarity.Epic, fish = 18, shape = "flowers", a = H("60A85C"), b = H("F6A0C4") },
            // collars
            new Item { id = "col_red", name = "Red Bell", icon = "col_bow", slot = Slot.Collar, a = H("E8433A") },
            new Item { id = "col_ring", name = "Teal Ring", icon = "col_ring", slot = Slot.Collar, coins = 100, a = H("3FA39A"), shape = "ring" },
            new Item { id = "col_tag", name = "Sunset Tag", icon = "col_tag", slot = Slot.Collar, coins = 120, a = H("F08A3A"), shape = "tag" },
            new Item { id = "col_pink", name = "Pink Ribbon", icon = "col_pink", slot = Slot.Collar, rarity = Rarity.Rare, fish = 8, a = H("F29BB8"), shape = "bow" },
            new Item { id = "col_bandana", name = "Paw Bandana", icon = "col_bandana", slot = Slot.Collar, coins = 160, shape = "bandana", a = H("3F6FB8"), b = H("3F6FB8") },
            new Item { id = "col_bowtie", name = "Bow Tie", icon = "col_bowtie", slot = Slot.Collar, rarity = Rarity.Rare, coins = 240, shape = "bowtie", a = H("2A2628"), b = H("2A2628") },
            new Item { id = "col_pearl", name = "Pearl Necklace", icon = "col_pearl", slot = Slot.Collar, rarity = Rarity.Epic, fish = 16, shape = "pearl", a = H("F6F1EA"), b = H("FFFFFF") },
            new Item { id = "col_scarf", name = "Cozy Scarf", icon = "col_scarf", slot = Slot.Collar, rarity = Rarity.Rare, coins = 220, shape = "scarf", a = H("C9342F"), b = H("C9342F") },
            // trails
            new Item { id = "trail_none", name = "No Trail", icon = "tile_item", slot = Slot.Trail },
            new Item { id = "trail_yarn", name = "Yarn Puffs", icon = "trail_yarn", slot = Slot.Trail, coins = 300, a = H("F29BB8"), b = H("E58AB4") },
            new Item { id = "trail_flower", name = "Marigolds", icon = "trail_flower", slot = Slot.Trail, rarity = Rarity.Rare, coins = 450, a = H("F5A23C"), b = H("FFD65C") },
            new Item { id = "trail_blossom", name = "Blossoms", icon = "trail_blossom", slot = Slot.Trail, rarity = Rarity.Epic, a = H("F7A1C4"), b = H("FFFFFF") },
            new Item { id = "trail_clover", name = "Lucky Clover", icon = "trail_clover", slot = Slot.Trail, rarity = Rarity.Legendary, a = H("5DB86A"), b = H("C9F27A") },
            new Item { id = "trail_bubbles", name = "Bubbles", icon = "trail_bubbles", slot = Slot.Trail, rarity = Rarity.Rare, coins = 400, a = H("BFE6FF"), b = H("F3D9FF") },
            new Item { id = "trail_hearts", name = "Little Hearts", icon = "trail_hearts", slot = Slot.Trail, rarity = Rarity.Rare, fish = 10, a = H("F26D8F"), b = H("FFB3C6") },
            new Item { id = "trail_stars", name = "Star Dust", icon = "trail_stars", slot = Slot.Trail, rarity = Rarity.Epic, fish = 18, a = H("FFD24A"), b = H("FFF3A8") },
            new Item { id = "trail_butterfly", name = "Butterflies", icon = "trail_butterfly", slot = Slot.Trail, rarity = Rarity.Epic, fish = 22, a = H("F7B6CF"), b = H("FFF3A8") },
            new Item { id = "trail_moon", name = "Moonbeams", icon = "trail_moon", slot = Slot.Trail, rarity = Rarity.Legendary, fish = 40, a = H("FFE38A"), b = H("D9C8FF") },
            new Item { id = "trail_koi", name = "Lucky Koi", icon = "trail_koi", slot = Slot.Trail, rarity = Rarity.Epic, fish = 24, a = H("F7A23C"), b = H("8FD3FF") },
            new Item { id = "trail_notes", name = "Music Notes", icon = "trail_notes", slot = Slot.Trail, rarity = Rarity.Epic, fish = 20, a = H("6C8EF2"), b = H("F27BB0") },
            // face looks
            new Item { id = "face_default", name = "Sparkly Eyes", icon = "face_default", slot = Slot.Face, shape = "eyes" },
            new Item { id = "face_happy", name = "Happy Eyes", icon = "face_happy", slot = Slot.Face, coins = 150, shape = "happy", a = H("2B2024") },
            new Item { id = "face_specs", name = "Round Specs", icon = "face_specs", slot = Slot.Face, coins = 250, shape = "specs", a = H("7A4A28") },
            new Item { id = "face_heart", name = "Heart Eyes", icon = "face_heart", slot = Slot.Face, rarity = Rarity.Rare, fish = 12, shape = "heart", a = H("E8486A"), b = H("FFFFFF") },
            new Item { id = "face_shades", name = "Cool Shades", icon = "face_shades", slot = Slot.Face, rarity = Rarity.Epic, fish = 22, shape = "shades", a = H("26232F"), b = H("FFFFFF") },
            new Item { id = "face_sleepy", name = "Sleepy", icon = "face_sleepy", slot = Slot.Face, coins = 120, shape = "sleepy", a = H("2B2024") },
            new Item { id = "face_wink", name = "Wink and Blep", icon = "face_wink", slot = Slot.Face, coins = 160, shape = "wink", a = H("1E1A1D"), b = H("F27B8F") },
            new Item { id = "face_stars", name = "Star Eyes", icon = "face_stars", slot = Slot.Face, rarity = Rarity.Rare, fish = 10, shape = "stars", a = H("FFD24A"), b = H("FFFFFF") },
            new Item { id = "face_teary", name = "Puppy Eyes", icon = "face_teary", slot = Slot.Face, coins = 140, shape = "teary", a = H("8FD3FF"), b = H("FFFFFF") },
            new Item { id = "face_determined", name = "Determined", icon = "face_determined", slot = Slot.Face, coins = 140, shape = "determined", a = H("3B2A22") },
            new Item { id = "face_monocle", name = "Gentlecat", icon = "face_monocle", slot = Slot.Face, rarity = Rarity.Epic, fish = 20, shape = "monocle", a = H("D9A33B"), b = H("4A2E1E") },
            new Item { id = "face_patch", name = "Pirate Patch", icon = "face_patch", slot = Slot.Face, rarity = Rarity.Rare, coins = 260, shape = "patch", a = H("231F20"), b = H("231F20") },
            new Item { id = "face_heartshades", name = "Heart Shades", icon = "face_heartshades", slot = Slot.Face, rarity = Rarity.Epic, fish = 18, shape = "heartshades", a = H("5A1F2C"), b = H("F27B8F") },
            new Item { id = "face_cat3", name = "Cat Smile", icon = "face_cat3", slot = Slot.Face, coins = 150, shape = "cat3", a = H("2B2024"), b = H("F59AAE") },
            new Item { id = "face_freckles", name = "Freckles", icon = "face_freckles", slot = Slot.Face, coins = 120, shape = "freckles", a = H("A3612F") },
            new Item { id = "face_bandage", name = "Brave Bandage", icon = "face_bandage", slot = Slot.Face, coins = 120, shape = "bandage", a = H("F2CC8F"), b = H("D9A86A") },
        };

        public static Item Get(string id) => Catalog.Find(i => i.id == id);
        public static bool IsDefault(string id) => id == "fur_orange" || id == "hat_none" || id == "col_red" || id == "trail_none" || id == "face_default";
        public static string DefaultFor(Slot s) => s == Slot.Fur ? "fur_orange" : s == Slot.Hat ? "hat_none" : s == Slot.Collar ? "col_red" : s == Slot.Trail ? "trail_none" : "face_default";
        public static string EquippedId(Slot s) => PawSave.Equipped(s.ToString(), DefaultFor(s));

        public static PawCosmetics Instance { get; private set; }

        GameObject hat, face, collar;
        readonly Dictionary<Slot, string> preview = new Dictionary<Slot, string>();

        /// <summary>Try an item on without equipping it (wardrobe preview). ClearPreview() goes back to the saved look.</summary>
        public void SetPreview(Slot s, string id) { preview[s] = id; Apply(); }
        public void ClearPreview() { preview.Clear(); Apply(); }
        string Look(Slot s) => preview.TryGetValue(s, out var id) ? id : EquippedId(s);
        ParticleSystem trail;
        readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        PawTownDemoMover mover;

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            mover = GetComponent<PawTownDemoMover>();
            Apply();
            PawSave.Changed += Apply;
        }

        void OnDisable() { PawSave.Changed -= Apply; }

        /// <summary>Instance (not shared) material of the cat renderer whose name ends with key.</summary>
        Material Mat(string key)
        {
            if (mats.TryGetValue(key, out var m) && m) return m;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                if (r.gameObject.name.EndsWith("_" + key)) { mats[key] = m = r.material; return m; }
            return null;
        }

        static void SetCol(Material m, Color c)
        {
            if (!m) return;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        public void Apply()
        {
            if (!this) return;
            var fur = Get(Look(Slot.Fur)) ?? Get("fur_orange");
            SetCol(Mat("CatOrange"), fur.a);
            SetCol(Mat("CatStripe"), fur.b);
            SetCol(Mat("CatCream"), fur.c);
            ApplyPattern(fur);
            var col = Get(Look(Slot.Collar)) ?? Get("col_red");
            SetCol(Mat("Red"), col.a);
            BuildCollar(col);
            BuildHat(Get(Look(Slot.Hat)));
            BuildTrail(Get(Look(Slot.Trail)));
            BuildFace(Get(Look(Slot.Face)));
        }

        // ------------------------------------------------------------------ fur patterns
        // The toon shader draws the pattern from each vertex's rest-pose position (UV channel 3), so patches stay put on
        // the fur while the skinned cat walks. The cat's meshes get that channel once, on a private copy of the mesh.
        static readonly string[] FurParts = { "CatOrange", "CatStripe", "CatCream" };
        bool restBaked;
        Vector3 restCenter; float restRadius = 1f;

        void BakeRest()
        {
            if (restBaked) return;
            var b = new Bounds(); bool any = false;
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                bool fur = false;
                foreach (var part in FurParts) if (r.gameObject.name.EndsWith("_" + part)) fur = true;
                if (!fur || !r.sharedMesh) continue;
                if (r.sharedMesh.name.EndsWith("_rest")) continue;               // already baked
                if (!r.sharedMesh.isReadable) { Debug.LogWarning("[PawCosmetics] cat mesh not readable, fur patterns off: " + r.sharedMesh.name); continue; }
                var m = Instantiate(r.sharedMesh);
                m.name = r.sharedMesh.name + "_rest";
                var v = m.vertices;
                m.SetUVs(3, v);
                r.sharedMesh = m;
                foreach (var p in v) { if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p); }
            }
            restBaked = true;                     // try once; a failure just leaves plain fur instead of breaking the screen
            if (!any) return;
            // colour points darken with distance from the middle of the body (towards ears, face, paws and tail)
            restCenter = b.center;
            restRadius = Mathf.Max(b.extents.x, Mathf.Max(b.extents.y, b.extents.z));
        }

        void ApplyPattern(Item fur)
        {
            if (fur.pattern != 0) BakeRest();
            foreach (var part in FurParts)
            {
                var m = Mat(part);
                if (!m || !m.HasProperty("_PatMode")) continue;
                m.SetFloat("_PatMode", fur.pattern);
                if (fur.pattern == 0) continue;
                m.SetColor("_PatColA", fur.pa);
                m.SetColor("_PatColB", fur.pb);
                // about four patches across the cat whatever units the model came in
                m.SetFloat("_PatScale", 2.2f / Mathf.Max(0.001f, restRadius));
                m.SetVector("_PatCenter", restCenter);
                m.SetFloat("_PatRadius", restRadius);
            }
        }

        // ------------------------------------------------------------------ hats
        static Material Toon(Color c)
        {
            var sh = Shader.Find("PawTown/Toon");
            var m = new Material(sh != null ? sh : Shader.Find("Universal Render Pipeline/Lit"));
            SetCol(m, c);
            return m;
        }

        static GameObject Part(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Color c)
        {
            var g = GameObject.CreatePrimitive(t);
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = Toon(c);
            g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        void BuildHat(Item it)
        {
            if (hat) { hat.SetActive(false); Destroy(hat); }   // hide now, Destroy only lands at frame end
            if (it == null || string.IsNullOrEmpty(it.shape)) return;
            var cat = GetComponentInChildren<Animator>();
            if (!cat) return;
            Transform head = null;
            foreach (var t in cat.GetComponentsInChildren<Transform>()) if (t.name == "Head") { head = t; break; }
            float s = cat.transform.lossyScale.x;            // 0.5 for the pet
            hat = new GameObject("Hat");
            hat.transform.SetParent(cat.transform, false);
            // head top in the cat's model space (from the cat model: head centre z 1.13, radius ~0.4, forward 0.34)
            hat.transform.localPosition = new Vector3(0f, 1.47f, 0.33f);
            hat.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);
            var root = hat.transform;
            if (Modelled(it.id, root, cat)) { }
            else if (it.shape == "cap")
            {
                Part(PrimitiveType.Sphere, root, new Vector3(0, 0.02f, 0), new Vector3(0.62f, 0.34f, 0.62f), it.a);
                Part(PrimitiveType.Cylinder, root, new Vector3(0, -0.05f, 0.3f), new Vector3(0.52f, 0.02f, 0.4f), it.a);
                Part(PrimitiveType.Sphere, root, new Vector3(0, 0.17f, 0), new Vector3(0.1f, 0.06f, 0.1f), it.b);
                Part(PrimitiveType.Sphere, root, new Vector3(0, 0.07f, 0.29f), new Vector3(0.14f, 0.12f, 0.03f), it.b);   // badge
            }
            else if (it.shape == "chef")
            {
                // tall puffy baker's hat: band + a cluster of soft puffs
                Part(PrimitiveType.Cylinder, root, new Vector3(0, 0.06f, 0), new Vector3(0.44f, 0.09f, 0.44f), it.a * 0.94f + Color.black * 0.06f);
                Part(PrimitiveType.Sphere, root, new Vector3(0, 0.27f, 0), new Vector3(0.5f, 0.36f, 0.5f), it.a);
                for (int i = 0; i < 4; i++)
                {
                    float ang = (i * 90f + 45f) * Mathf.Deg2Rad;
                    Part(PrimitiveType.Sphere, root, new Vector3(Mathf.Sin(ang) * 0.14f, 0.33f, Mathf.Cos(ang) * 0.14f), Vector3.one * 0.28f, it.a);
                }
                Part(PrimitiveType.Sphere, root, new Vector3(0, 0.07f, 0.22f), new Vector3(0.08f, 0.08f, 0.03f), it.b);   // little paw badge
            }
            else if (it.shape == "party")
            {
                // striped cone, tilted a little, with a pompom
                var cone = new GameObject("Cone");
                cone.transform.SetParent(root, false);
                cone.transform.localRotation = Quaternion.Euler(0, 0, 12f);
                cone.AddComponent<MeshFilter>().sharedMesh = ConeMesh();
                var mr = cone.AddComponent<MeshRenderer>();
                mr.sharedMaterial = Toon(it.a);
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                cone.transform.localScale = new Vector3(0.36f, 0.48f, 0.36f);
                for (int i = 0; i < 2; i++)
                    Part(PrimitiveType.Cylinder, cone.transform, new Vector3(0, 0.22f + i * 0.3f, 0), new Vector3(0.84f - i * 0.3f, 0.05f, 0.84f - i * 0.3f), it.b);
                Part(PrimitiveType.Sphere, cone.transform, new Vector3(0, 1.02f, 0), new Vector3(0.36f, 0.26f, 0.36f), it.b);
            }
            else if (BuildHatV2(it, root)) { }
            else if (it.shape == "flowers")
            {
                // leafy ring with little five-petal flowers around it
                for (int i = 0; i < 14; i++)
                {
                    float ang = i * (360f / 14f) * Mathf.Deg2Rad;
                    Part(PrimitiveType.Sphere, root, new Vector3(Mathf.Sin(ang) * 0.27f, -0.02f, Mathf.Cos(ang) * 0.25f), new Vector3(0.1f, 0.06f, 0.1f), it.a);
                }
                Color[] petals = { it.b, H("FFD65C"), H("FFFFFF"), it.b, H("FFD65C") };
                for (int f = 0; f < 5; f++)
                {
                    float ang = (-70f + f * 35f) * Mathf.Deg2Rad;     // across the front half
                    var c = new Vector3(Mathf.Sin(ang) * 0.27f, 0.02f, Mathf.Cos(ang) * 0.25f);
                    for (int k = 0; k < 5; k++)
                    {
                        float pa = k * 72f * Mathf.Deg2Rad;
                        Part(PrimitiveType.Sphere, root, c + new Vector3(Mathf.Cos(pa) * 0.045f, Mathf.Sin(pa) * 0.045f + 0.02f, 0.01f), Vector3.one * 0.06f, petals[f]);
                    }
                    Part(PrimitiveType.Sphere, root, c + new Vector3(0, 0.02f, 0.03f), Vector3.one * 0.045f, H("F5A93A"));
                }
            }
            else
            {
                Part(PrimitiveType.Cylinder, root, new Vector3(0, 0.03f, 0), new Vector3(0.5f, 0.07f, 0.5f), it.a);
                for (int i = 0; i < 5; i++)
                {
                    float ang = i * 72f * Mathf.Deg2Rad;
                    var p = new Vector3(Mathf.Sin(ang) * 0.21f, 0.15f, Mathf.Cos(ang) * 0.21f);
                    Part(PrimitiveType.Sphere, root, p, new Vector3(0.12f, 0.18f, 0.12f), it.a);
                    Part(PrimitiveType.Sphere, root, p + Vector3.up * 0.11f, Vector3.one * 0.07f, it.b);
                }
            }
            // follow the head bone (nods with the idle / walk) while keeping the placement made in rest space
            if (head) hat.transform.SetParent(head, true);
            MatchLayer(hat, cat);
        }

        /// <summary>New parts take the cat's render layer (the wardrobe preview camera only sees that layer).</summary>
        static void MatchLayer(GameObject g, Animator cat)
        {
            // the body's layer (not just the first renderer found: that can be another accessory still on the default layer)
            int layer = cat.gameObject.layer;
            foreach (var r in cat.GetComponentsInChildren<Renderer>(true))
                if (r.gameObject.name.EndsWith("_CatOrange")) { layer = r.gameObject.layer; break; }
            foreach (var t in g.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        static Mesh cone, torus;

        static Mesh ConeMesh()
        {
            if (cone) return cone;
            const int n = 24;
            var v = new List<Vector3>(); var tri = new List<int>();
            v.Add(new Vector3(0, 1, 0)); v.Add(Vector3.zero);
            for (int i = 0; i <= n; i++) { float a = i * Mathf.PI * 2f / n; v.Add(new Vector3(Mathf.Sin(a) * 0.5f, 0, Mathf.Cos(a) * 0.5f)); }
            for (int i = 0; i < n; i++) { tri.Add(0); tri.Add(2 + i); tri.Add(3 + i); tri.Add(1); tri.Add(3 + i); tri.Add(2 + i); }
            cone = new Mesh { name = "PartyCone" };
            cone.SetVertices(v); cone.SetTriangles(tri, 0); cone.RecalculateNormals(); cone.RecalculateBounds();
            return cone;
        }

        /// <summary>Unit ring in the XY plane (radius 1, tube 0.12) for glasses frames.</summary>
        static Mesh TorusMesh()
        {
            if (torus) return torus;
            const int seg = 32, side = 8; const float R = 1f, r = 0.12f;
            var v = new List<Vector3>(); var tri = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                var c = new Vector3(Mathf.Cos(a) * R, Mathf.Sin(a) * R, 0);
                for (int j = 0; j <= side; j++)
                {
                    float b = j * Mathf.PI * 2f / side;
                    v.Add(c + (c.normalized * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b)) * r);
                }
            }
            for (int i = 0; i < seg; i++)
                for (int j = 0; j < side; j++)
                {
                    int a0 = i * (side + 1) + j, a1 = a0 + side + 1;
                    tri.Add(a0); tri.Add(a1); tri.Add(a0 + 1); tri.Add(a0 + 1); tri.Add(a1); tri.Add(a1 + 1);
                }
            torus = new Mesh { name = "GlassesRing" };
            torus.SetVertices(v); torus.SetTriangles(tri, 0); torus.RecalculateNormals(); torus.RecalculateBounds();
            return torus;
        }

        // ------------------------------------------------------------------ face looks
        // Eye placement in the cat's model space (cat model: eyes at x +-0.175, z 1.03 + 0.15 lift, forward 0.69).
        static readonly float[] EyeX = { -0.175f, 0.175f };
        const float EyeY = 1.18f, EyeZ = 0.72f;

        void BuildFace(Item it)
        {
            if (face) { face.SetActive(false); Destroy(face); }
            var cat = GetComponentInChildren<Animator>();
            if (!cat) return;
            string look = it?.shape ?? "eyes";
            // looks that draw their own eyes (or cover them with dark lenses) hide the painted ones
            bool hideEyes = look == "happy" || look == "heart" || look == "shades" || look == "sleepy" || look == "wink"
                            || look == "stars" || look == "heartshades" || look == "cat3";
            foreach (var r in cat.GetComponentsInChildren<Renderer>(true))
                if (r.gameObject.name.EndsWith("_EyeBlack") || r.gameObject.name.EndsWith("_White")) r.enabled = !hideEyes;
            if (look == "eyes") return;

            Transform head = null;
            foreach (var t in cat.GetComponentsInChildren<Transform>()) if (t.name == "Head") { head = t; break; }
            face = new GameObject("Face");
            face.transform.SetParent(cat.transform, false);
            var root = face.transform;
            BuildFaceV2(it, look, root);
            foreach (float ex in EyeX)
            {
                var e = new Vector3(ex, EyeY, EyeZ);
                if (look == "happy" || look == "cat3")
                {
                    // ^ ^ : two little strokes meeting at the top
                    for (int k = -1; k <= 1; k += 2)
                    {
                        var st = Part(PrimitiveType.Capsule, root, e + new Vector3(k * 0.036f, -0.008f, 0.004f), new Vector3(0.032f, 0.048f, 0.032f), look == "cat3" ? H("2B2024") : it.a);
                        st.transform.localRotation = Quaternion.Euler(0, 0, k * 52f);
                    }
                }
                else if (look == "heart")
                {
                    for (int k = -1; k <= 1; k += 2)
                        Part(PrimitiveType.Sphere, root, e + new Vector3(k * 0.034f, 0.024f, 0.01f), new Vector3(0.085f, 0.085f, 0.04f), it.a);
                    var tip = Part(PrimitiveType.Cube, root, e + new Vector3(0, -0.012f, 0.008f), new Vector3(0.078f, 0.078f, 0.035f), it.a);
                    tip.transform.localRotation = Quaternion.Euler(0, 0, 45f);
                    Part(PrimitiveType.Sphere, root, e + new Vector3(-0.03f, 0.04f, 0.03f), new Vector3(0.025f, 0.025f, 0.01f), it.b);   // shine
                }
                else if (look == "specs")
                {
                    var ring = new GameObject("Lens");
                    ring.transform.SetParent(root, false);
                    ring.transform.localPosition = e + new Vector3(0, 0, 0.035f);
                    ring.transform.localScale = new Vector3(0.115f, 0.11f, 0.115f);
                    ring.AddComponent<MeshFilter>().sharedMesh = TorusMesh();
                    var mr = ring.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = Toon(it.a);
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                else if (look == "shades")
                {
                    Part(PrimitiveType.Sphere, root, e + new Vector3(0, 0.005f, 0.03f), new Vector3(0.22f, 0.16f, 0.05f), it.a);
                    Part(PrimitiveType.Sphere, root, e + new Vector3(-0.05f, 0.04f, 0.056f), new Vector3(0.05f, 0.025f, 0.008f), it.b);   // glint
                }
            }
            if (look == "specs" || look == "shades")
            {
                var bridge = Part(PrimitiveType.Cylinder, root, new Vector3(0, EyeY + 0.03f, EyeZ + 0.05f), new Vector3(0.02f, 0.05f, 0.02f), it.a);
                bridge.transform.localRotation = Quaternion.Euler(0, 0, 90f);
            }
            if (head) face.transform.SetParent(head, true);
            MatchLayer(face, cat);
        }

        // ------------------------------------------------------------------ modelled accessories
        // Modelled to match the icon sheets in Resources/Cosmetics/<id>.fbx. Their materials are named
        // M_C_<hex>: rebuilt here as toon materials of that colour; M_Outline uses the cat's own outline material.
        static readonly Dictionary<string, Material> toonCache = new Dictionary<string, Material>();
        /// <summary>The model's front (-Y) to the cat's front in Unity model space.</summary>
        public static Vector3 ModelFix = Vector3.zero;

        bool Modelled(string id, Transform parent, Animator cat, Vector3? pos = null, Quaternion? rot = null)
        {
            var prefab = Resources.Load<GameObject>("Cosmetics/" + id);
            if (!prefab) return false;
            var g = Instantiate(prefab, parent, false);
            g.name = id;
            g.transform.localPosition = pos ?? Vector3.zero;
            g.transform.localRotation = (rot ?? Quaternion.identity) * Quaternion.Euler(ModelFix);
            Material outline = null;
            foreach (var r in cat.GetComponentsInChildren<Renderer>(true))
                if (r.gameObject.name.EndsWith("_Outline")) { outline = r.sharedMaterial; break; }
            foreach (var r in g.GetComponentsInChildren<Renderer>(true))
            {
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++)
                {
                    string n = ms[i] ? ms[i].name : "";
                    int k = n.IndexOf("M_C_");
                    if (n.Contains("Outline") && outline) ms[i] = outline;
                    else if (k >= 0 && n.Length >= k + 10)
                    {
                        string hex = n.Substring(k + 4, 6);
                        if (!toonCache.TryGetValue(hex, out var m) || !m) toonCache[hex] = m = Toon(H(hex));
                        ms[i] = m;
                    }
                }
                r.sharedMaterials = ms;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return true;
        }

        // ------------------------------------------------------------------ v2 pieces (hats, faces, collars from the HD sheets)
        static GameObject Mesh(Transform parent, Mesh m, Vector3 pos, Quaternion rot, Vector3 scale, Color c)
        {
            var g = new GameObject(m.name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos; g.transform.localRotation = rot; g.transform.localScale = scale;
            g.AddComponent<MeshFilter>().sharedMesh = m;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = Toon(c);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        static GameObject Rot(GameObject g, float x, float y, float z) { g.transform.localRotation = Quaternion.Euler(x, y, z); return g; }

        /// <summary>The newer hats; returns false for shapes it does not know (handled by the older code).</summary>
        bool BuildHatV2(Item it, Transform root)
        {
            Color a = it.a, b = it.b, dark = it.a * 0.8f; dark.a = 1f;
            switch (it.shape)
            {
                case "beanie":
                    Part(PrimitiveType.Sphere, root, new Vector3(0, 0.05f, 0), new Vector3(0.64f, 0.44f, 0.64f), a);
                    Part(PrimitiveType.Cylinder, root, new Vector3(0, -0.06f, 0), new Vector3(0.63f, 0.07f, 0.63f), dark);
                    Part(PrimitiveType.Sphere, root, new Vector3(0, 0.3f, 0), Vector3.one * 0.22f, b);
                    return true;
                case "witch":
                    Part(PrimitiveType.Cylinder, root, new Vector3(0, -0.05f, 0), new Vector3(0.95f, 0.02f, 0.95f), a);
                    Mesh(root, ConeMesh(), new Vector3(0, -0.04f, 0), Quaternion.Euler(-8f, 0, -10f), new Vector3(0.52f, 0.8f, 0.52f), a);
                    Part(PrimitiveType.Cylinder, root, new Vector3(0, 0.0f, 0), new Vector3(0.47f, 0.05f, 0.47f), H("2A2628"));
                    Part(PrimitiveType.Sphere, root, new Vector3(0, 0.0f, 0.24f), new Vector3(0.09f, 0.09f, 0.03f), b);
                    return true;
                case "beret":
                    Rot(Part(PrimitiveType.Sphere, root, new Vector3(0.06f, 0.07f, 0), new Vector3(0.74f, 0.22f, 0.7f), a), 0, 0, 12f);
                    Part(PrimitiveType.Sphere, root, new Vector3(0.06f, 0.2f, 0), Vector3.one * 0.07f, a);
                    return true;
                case "straw":
                    Part(PrimitiveType.Cylinder, root, new Vector3(0, -0.05f, 0), new Vector3(1.05f, 0.02f, 1.05f), a);
                    Part(PrimitiveType.Sphere, root, new Vector3(0, 0.06f, 0), new Vector3(0.56f, 0.32f, 0.56f), a);
                    Part(PrimitiveType.Cylinder, root, new Vector3(0, -0.005f, 0), new Vector3(0.58f, 0.05f, 0.58f), b);
                    for (int i = 0; i < 5; i++)
                    {
                        float ang = i * 72f * Mathf.Deg2Rad;
                        Part(PrimitiveType.Sphere, root, new Vector3(0.2f + Mathf.Cos(ang) * 0.05f, 0.02f + Mathf.Sin(ang) * 0.05f, 0.22f), new Vector3(0.06f, 0.06f, 0.02f), Color.white);
                    }
                    Part(PrimitiveType.Sphere, root, new Vector3(0.2f, 0.02f, 0.23f), new Vector3(0.045f, 0.045f, 0.02f), H("F5B52E"));
                    return true;
                case "cowboy":
                    Part(PrimitiveType.Cylinder, root, new Vector3(0, -0.05f, 0), new Vector3(1.05f, 0.02f, 0.85f), a);
                    for (int k = -1; k <= 1; k += 2)
                        Rot(Part(PrimitiveType.Sphere, root, new Vector3(k * 0.47f, 0.0f, 0), new Vector3(0.14f, 0.18f, 0.7f), a), 0, 0, k * -25f);
                    Part(PrimitiveType.Sphere, root, new Vector3(0, 0.14f, 0), new Vector3(0.52f, 0.42f, 0.48f), a);
                    Part(PrimitiveType.Cylinder, root, new Vector3(0, 0.02f, 0), new Vector3(0.54f, 0.06f, 0.5f), b);
                    Part(PrimitiveType.Sphere, root, new Vector3(0, 0.02f, 0.25f), new Vector3(0.07f, 0.07f, 0.02f), H("F2C14E"));
                    return true;
                case "pirate":
                    Part(PrimitiveType.Sphere, root, new Vector3(0, 0.06f, 0), new Vector3(0.6f, 0.36f, 0.6f), a);
                    for (int i = 0; i < 3; i++)   // three folded-up brim sides
                    {
                        float ang = i * 120f;
                        var side = Part(PrimitiveType.Capsule, root, Quaternion.Euler(0, ang, 0) * new Vector3(0, 0.08f, 0.3f), new Vector3(0.18f, 0.32f, 0.08f), a);
                        side.transform.localRotation = Quaternion.Euler(0, ang, 90f) * Quaternion.Euler(0, 0, 0);
                    }
                    Part(PrimitiveType.Sphere, root, new Vector3(0, 0.12f, 0.31f), new Vector3(0.11f, 0.11f, 0.03f), Color.white);
                    Part(PrimitiveType.Cylinder, root, new Vector3(0, -0.04f, 0), new Vector3(0.62f, 0.03f, 0.62f), b);
                    return true;
                case "santa":
                    Part(PrimitiveType.Cylinder, root, new Vector3(0, -0.05f, 0), new Vector3(0.66f, 0.09f, 0.66f), b);
                    Mesh(root, ConeMesh(), new Vector3(0, -0.02f, 0), Quaternion.Euler(0, 0, -38f), new Vector3(0.6f, 0.62f, 0.6f), a);
                    Part(PrimitiveType.Sphere, root, new Vector3(0.38f, 0.47f, 0), Vector3.one * 0.17f, b);
                    return true;
                case "tophat":
                    Part(PrimitiveType.Cylinder, root, new Vector3(0, -0.05f, 0), new Vector3(0.78f, 0.02f, 0.78f), a);
                    Part(PrimitiveType.Cylinder, root, new Vector3(0, 0.2f, 0), new Vector3(0.46f, 0.24f, 0.46f), a);
                    Part(PrimitiveType.Cylinder, root, new Vector3(0, 0.02f, 0), new Vector3(0.47f, 0.05f, 0.47f), b);
                    for (int k = -1; k <= 1; k += 2)
                        Part(PrimitiveType.Sphere, root, new Vector3(0.12f + k * 0.06f, 0.03f, 0.2f), new Vector3(0.09f, 0.07f, 0.04f), b);
                    return true;
                case "propeller":
                    Part(PrimitiveType.Sphere, root, new Vector3(0, 0.02f, 0), new Vector3(0.62f, 0.34f, 0.62f), a);
                    Rot(Part(PrimitiveType.Sphere, root, new Vector3(0.12f, 0.06f, 0.05f), new Vector3(0.35f, 0.3f, 0.5f), H("E8473A")), 0, 0, 20f);
                    Rot(Part(PrimitiveType.Sphere, root, new Vector3(-0.12f, 0.06f, -0.05f), new Vector3(0.35f, 0.3f, 0.5f), H("4FB86A")), 0, 0, -20f);
                    Part(PrimitiveType.Cylinder, root, new Vector3(0, -0.05f, 0.3f), new Vector3(0.52f, 0.02f, 0.4f), a);
                    Part(PrimitiveType.Cylinder, root, new Vector3(0, 0.22f, 0), new Vector3(0.03f, 0.06f, 0.03f), b);
                    var prop = new GameObject("Propeller").transform;
                    prop.SetParent(root, false); prop.localPosition = new Vector3(0, 0.29f, 0);
                    Part(PrimitiveType.Capsule, prop, Vector3.zero, new Vector3(0.06f, 0.2f, 0.02f), b).transform.localRotation = Quaternion.Euler(90f, 0, 0);
                    Part(PrimitiveType.Capsule, prop, Vector3.zero, new Vector3(0.06f, 0.2f, 0.02f), H("3F7FD1")).transform.localRotation = Quaternion.Euler(90f, 90f, 0);
                    prop.gameObject.AddComponent<PawSpin>().speed = 400f;
                    return true;
                case "frog":
                    Part(PrimitiveType.Sphere, root, new Vector3(0, 0.05f, 0), new Vector3(0.68f, 0.44f, 0.66f), a);
                    for (int k = -1; k <= 1; k += 2)
                    {
                        Part(PrimitiveType.Sphere, root, new Vector3(k * 0.16f, 0.24f, 0.1f), Vector3.one * 0.18f, b);
                        Part(PrimitiveType.Sphere, root, new Vector3(k * 0.16f, 0.25f, 0.18f), Vector3.one * 0.08f, H("2B2024"));
                        Part(PrimitiveType.Sphere, root, new Vector3(k * 0.22f, 0.04f, 0.27f), new Vector3(0.08f, 0.05f, 0.02f), H("F59AAE"));
                    }
                    return true;
                case "mushroom":
                    Part(PrimitiveType.Sphere, root, new Vector3(0, 0.08f, 0), new Vector3(0.9f, 0.46f, 0.9f), a);
                    Part(PrimitiveType.Cylinder, root, new Vector3(0, -0.08f, 0), new Vector3(0.66f, 0.04f, 0.66f), b);
                    Vector3[] spots = { new Vector3(0, 0.3f, 0), new Vector3(0.24f, 0.2f, 0.16f), new Vector3(-0.26f, 0.19f, 0.12f), new Vector3(0.05f, 0.18f, 0.32f), new Vector3(-0.12f, 0.2f, -0.26f), new Vector3(0.3f, 0.12f, -0.15f) };
                    foreach (var sp in spots)
                    {
                        var dot = Part(PrimitiveType.Sphere, root, sp, new Vector3(0.12f, 0.04f, 0.12f), b);
                        dot.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (sp - new Vector3(0, -0.05f, 0)).normalized);
                    }
                    return true;
                case "viking":
                    Part(PrimitiveType.Sphere, root, new Vector3(0, 0.03f, 0), new Vector3(0.64f, 0.44f, 0.64f), a);
                    Part(PrimitiveType.Cylinder, root, new Vector3(0, -0.07f, 0), new Vector3(0.66f, 0.06f, 0.66f), b);
                    Part(PrimitiveType.Cylinder, root, new Vector3(0, 0.08f, 0.0f), new Vector3(0.08f, 0.2f, 0.66f), b).transform.localRotation = Quaternion.Euler(90f, 0, 0);
                    for (int k = -1; k <= 1; k += 2)
                        Mesh(root, ConeMesh(), new Vector3(k * 0.28f, 0.08f, 0), Quaternion.Euler(0, 0, k * -62f), new Vector3(0.15f, 0.34f, 0.15f), H("F4E7CC"));
                    return true;
                case "grad":
                    Part(PrimitiveType.Sphere, root, new Vector3(0, 0.0f, 0), new Vector3(0.56f, 0.3f, 0.56f), a);
                    Rot(Part(PrimitiveType.Cube, root, new Vector3(0, 0.13f, 0), new Vector3(0.64f, 0.03f, 0.64f), a), 0, 45f, 0);
                    Part(PrimitiveType.Sphere, root, new Vector3(0, 0.15f, 0), Vector3.one * 0.05f, b);
                    Part(PrimitiveType.Cylinder, root, new Vector3(0.36f, 0.04f, 0), new Vector3(0.02f, 0.1f, 0.02f), b);
                    Part(PrimitiveType.Sphere, root, new Vector3(0.36f, -0.07f, 0), new Vector3(0.05f, 0.08f, 0.05f), b);
                    return true;
                case "halo":
                    var halo = Mesh(root, TorusMesh(), new Vector3(0, 0.3f, 0), Quaternion.Euler(90f, 0, 0), Vector3.one * 0.26f, a);
                    halo.AddComponent<PawSpin>().bob = 0.03f;
                    return true;
                case "bunny":
                    Mesh(root, TorusMesh(), new Vector3(0, -0.3f, -0.02f), Quaternion.identity, new Vector3(0.45f, 0.45f, 0.4f), a);
                    for (int k = -1; k <= 1; k += 2)
                    {
                        Rot(Part(PrimitiveType.Capsule, root, new Vector3(k * 0.15f, 0.3f, -0.02f), new Vector3(0.14f, 0.32f, 0.08f), a), 0, 0, k * -14f);
                        Rot(Part(PrimitiveType.Capsule, root, new Vector3(k * 0.152f, 0.3f, 0.025f), new Vector3(0.07f, 0.24f, 0.03f), b), 0, 0, k * -14f);
                    }
                    return true;
                case "headphones":
                    Mesh(root, TorusMesh(), new Vector3(0, -0.3f, -0.02f), Quaternion.identity, new Vector3(0.46f, 0.46f, 0.5f), a);
                    for (int k = -1; k <= 1; k += 2)
                    {
                        Rot(Part(PrimitiveType.Cylinder, root, new Vector3(k * 0.43f, -0.24f, 0), new Vector3(0.24f, 0.06f, 0.24f), a), 0, 0, 90f);
                        Rot(Part(PrimitiveType.Cylinder, root, new Vector3(k * 0.47f, -0.24f, 0), new Vector3(0.17f, 0.03f, 0.17f), b), 0, 0, 90f);
                        Part(PrimitiveType.Sphere, root, new Vector3(k * 0.4f, 0.0f, 0), new Vector3(0.06f, 0.12f, 0.06f), a);
                    }
                    return true;
            }
            return false;
        }

        /// <summary>Point on the front of the head surface at model-space (x, y), pushed out by 'lift'.</summary>
        static Vector3 OnFace(float x, float y, float lift = 0.01f)
        {
            float dx = x / 0.47f, dy = (y - 1.13f) / 0.38f;
            float z = 0.34f + 0.40f * Mathf.Sqrt(Mathf.Max(0.05f, 1f - dx * dx - dy * dy));
            return new Vector3(x, y, z + lift);
        }

        void BuildFaceV2(Item it, string look, Transform root)
        {
            Color ink = H("2B2024");
            switch (look)
            {
                case "sleepy":
                    foreach (float ex in EyeX)
                        Rot(Part(PrimitiveType.Capsule, root, OnFace(ex, EyeY - 0.01f), new Vector3(0.028f, 0.06f, 0.02f), it.a), 0, 0, 90f);
                    break;
                case "wink":
                    // her right eye open (drawn), her left one a closed ^, little tongue out
                    var open = OnFace(-0.175f, EyeY);
                    Part(PrimitiveType.Sphere, root, open, new Vector3(0.17f, 0.2f, 0.06f), it.a);
                    Part(PrimitiveType.Sphere, root, open + new Vector3(0.03f, 0.04f, 0.03f), Vector3.one * 0.06f, Color.white);
                    var shut = OnFace(0.175f, EyeY);
                    for (int k = -1; k <= 1; k += 2)
                        Rot(Part(PrimitiveType.Capsule, root, shut + new Vector3(k * 0.036f, -0.008f, 0), new Vector3(0.03f, 0.048f, 0.03f), it.a), 0, 0, k * 52f);
                    Part(PrimitiveType.Sphere, root, OnFace(0.02f, 1.0f, 0.06f), new Vector3(0.07f, 0.08f, 0.04f), it.b);
                    break;
                case "stars":
                    foreach (float ex in EyeX)
                    {
                        var c = OnFace(ex, EyeY, 0.02f);
                        Rot(Part(PrimitiveType.Cube, root, c, new Vector3(0.12f, 0.12f, 0.02f), it.a), 0, 0, 45f);
                        Part(PrimitiveType.Cube, root, c + new Vector3(0, 0, 0.004f), new Vector3(0.12f, 0.12f, 0.02f), it.a);
                        Part(PrimitiveType.Sphere, root, c + new Vector3(-0.025f, 0.03f, 0.02f), Vector3.one * 0.03f, it.b);
                    }
                    break;
                case "teary":
                    foreach (float ex in EyeX)
                        Part(PrimitiveType.Sphere, root, OnFace(ex + Mathf.Sign(ex) * 0.05f, EyeY - 0.1f, 0.02f), new Vector3(0.045f, 0.07f, 0.03f), it.a);
                    break;
                case "determined":
                    foreach (float ex in EyeX)
                        Rot(Part(PrimitiveType.Capsule, root, OnFace(ex, EyeY + 0.12f, 0.02f), new Vector3(0.025f, 0.065f, 0.02f), it.a), 0, 0, 90f + Mathf.Sign(ex) * 18f);
                    break;
                case "monocle":
                    var mono = OnFace(0.175f, EyeY, 0.04f);
                    Mesh(root, TorusMesh(), mono, Quaternion.identity, Vector3.one * 0.12f, it.a);
                    Part(PrimitiveType.Cylinder, root, mono + new Vector3(0.1f, -0.13f, -0.02f), new Vector3(0.012f, 0.1f, 0.012f), it.a);
                    for (int k = -1; k <= 1; k += 2)
                        Rot(Part(PrimitiveType.Sphere, root, OnFace(k * 0.07f, 1.065f, 0.06f), new Vector3(0.13f, 0.05f, 0.04f), it.b), 0, 0, k * -18f);
                    break;
                case "patch":
                    var pc = OnFace(0.175f, EyeY, 0.02f);
                    Part(PrimitiveType.Sphere, root, pc, new Vector3(0.2f, 0.18f, 0.05f), it.a);
                    Rot(Part(PrimitiveType.Cube, root, OnFace(0.02f, 1.32f, 0.0f), new Vector3(0.5f, 0.025f, 0.02f), it.a), 0, 0, -28f);
                    break;
                case "heartshades":
                    foreach (float ex in EyeX)
                    {
                        var c = OnFace(ex, EyeY, 0.03f);
                        for (int k = -1; k <= 1; k += 2)
                            Part(PrimitiveType.Sphere, root, c + new Vector3(k * 0.04f, 0.028f, 0), new Vector3(0.11f, 0.11f, 0.03f), it.a);
                        Rot(Part(PrimitiveType.Cube, root, c + new Vector3(0, -0.015f, -0.002f), new Vector3(0.1f, 0.1f, 0.028f), it.a), 0, 0, 45f);
                        Rot(Part(PrimitiveType.Cube, root, c + new Vector3(0, -0.015f, -0.006f), new Vector3(0.125f, 0.125f, 0.02f), it.b), 0, 0, 45f);
                        for (int k = -1; k <= 1; k += 2)
                            Part(PrimitiveType.Sphere, root, c + new Vector3(k * 0.04f, 0.028f, -0.006f), new Vector3(0.135f, 0.135f, 0.02f), it.b);
                    }
                    Rot(Part(PrimitiveType.Cylinder, root, new Vector3(0, EyeY + 0.03f, EyeZ + 0.06f), new Vector3(0.02f, 0.05f, 0.02f), it.b), 0, 0, 90f);
                    break;
                case "cat3":
                    for (int k = -1; k <= 1; k += 2)
                        Part(PrimitiveType.Sphere, root, OnFace(k * 0.28f, 1.06f, 0.01f), new Vector3(0.11f, 0.06f, 0.03f), it.b);
                    break;
                case "freckles":
                    for (int k = -1; k <= 1; k += 2)
                        for (int d = 0; d < 3; d++)
                            Part(PrimitiveType.Sphere, root, OnFace(k * (0.2f + d * 0.035f), 1.06f + (d == 1 ? 0.025f : 0f), 0.005f), Vector3.one * 0.022f, it.a);
                    break;
                case "bandage":
                    var bp = OnFace(0.26f, 1.05f, 0.01f);
                    Rot(Part(PrimitiveType.Cube, root, bp, new Vector3(0.14f, 0.055f, 0.02f), it.a), 0, -25f, 28f);
                    Rot(Part(PrimitiveType.Cube, root, bp + new Vector3(0, 0, 0.006f), new Vector3(0.05f, 0.045f, 0.02f), it.b), 0, -25f, 28f);
                    break;
            }
        }

        /// <summary>Collar add-ons (bandana, bow tie, pearls, scarf) around the neck; the bell hides under them.</summary>
        void BuildCollar(Item it)
        {
            if (collar) { collar.SetActive(false); Destroy(collar); }
            var cat = GetComponentInChildren<Animator>();
            if (!cat) return;
            bool extra = it != null && !string.IsNullOrEmpty(it.shape);
            foreach (var r in cat.GetComponentsInChildren<Renderer>(true))
                if (r.gameObject.name.EndsWith("_Yellow")) r.enabled = !extra;
            if (!extra) return;
            Transform body = null;
            foreach (var t in cat.GetComponentsInChildren<Transform>()) if (t.name == "Body") { body = t; break; }
            collar = new GameObject("CollarExtra");
            collar.transform.SetParent(cat.transform, false);
            var root = collar.transform;
            // collar band in model space (centre (0,-0.27,0.70), radius 0.25, tilted 25 degrees, front down)
            Vector3 centre = new Vector3(0, 0.85f, 0.27f);
            Quaternion tilt = Quaternion.Euler(25f, 0, 0);
            Vector3 Around(float deg, float grow) => centre + tilt * new Vector3(Mathf.Sin(deg * Mathf.Deg2Rad) * (0.27f + grow), 0, Mathf.Cos(deg * Mathf.Deg2Rad) * (0.25f + grow));
            if (Modelled(it.id, root, cat, centre, tilt)) { if (body) collar.transform.SetParent(body, true); MatchLayer(collar, cat); return; }
            switch (it.shape)
            {
                case "bandana":
                    Mesh(root, ConeMesh(), Around(0, 0.02f) + new Vector3(0, 0.03f, 0), Quaternion.Euler(180f + 20f, 0, 0), new Vector3(0.42f, 0.26f, 0.12f), it.a);
                    Part(PrimitiveType.Sphere, root, Around(0, 0.06f) + new Vector3(0, -0.08f, 0), new Vector3(0.07f, 0.06f, 0.02f), Color.white);
                    break;
                case "bowtie":
                    var bt = Around(0, 0.03f);
                    for (int k = -1; k <= 1; k += 2)
                        Rot(Part(PrimitiveType.Sphere, root, bt + new Vector3(k * 0.08f, 0, 0), new Vector3(0.14f, 0.11f, 0.06f), it.a), 0, 0, k * 12f);
                    Part(PrimitiveType.Sphere, root, bt + new Vector3(0, 0, 0.02f), new Vector3(0.06f, 0.07f, 0.05f), it.a);
                    break;
                case "pearl":
                    for (int i = 0; i < 18; i++)
                        Part(PrimitiveType.Sphere, root, Around(i * 20f, 0.035f), Vector3.one * 0.065f, it.a);
                    Part(PrimitiveType.Sphere, root, Around(0, 0.05f) + new Vector3(0, -0.07f, 0), new Vector3(0.07f, 0.07f, 0.04f), H("F2B33D"));
                    break;
                case "scarf":
                    for (int i = 0; i < 16; i++)
                        Part(PrimitiveType.Sphere, root, Around(i * 22.5f, 0.05f), new Vector3(0.15f, 0.12f, 0.15f), it.a);
                    for (int d = 0; d < 3; d++)
                        Part(PrimitiveType.Sphere, root, Around(25f, 0.07f) + new Vector3(0, -0.07f - d * 0.08f, 0.02f), new Vector3(0.12f, 0.1f, 0.06f), d % 2 == 0 ? it.a : it.a * 0.85f + Color.black * 0.15f);
                    break;
            }
            if (body) collar.transform.SetParent(body, true);
            MatchLayer(collar, cat);
        }

        // ------------------------------------------------------------------ trails
        // Each trail is three particle layers that only flow while she moves: a soft coloured smoke, the trail's own
        // shapes from its icon (puffs, petals, flowers, clovers, bubbles, hearts, stars, notes) drifting and spinning,
        // and twinkling sparkles. Sprites live in Resources/Trails.
        class TrailLook { public string[] shapes; public string swirl = "smoke"; public Color smoke = Color.white; public float gravity = -0.06f, spin = 90f, size = 0.17f; public bool sparkleTint; }

        static TrailLook LookFor(string id)
        {
            // shapes and swirls are the painted pieces from the magic sticker sheet
            switch (id)
            {
                case "trail_yarn": return new TrailLook { shapes = new[] { "puff", "yarnball" }, swirl = "swirl_pink", gravity = -0.04f, spin = 40f, size = 0.2f };
                case "trail_flower": return new TrailLook { shapes = new[] { "marigold", "petal_orange", "petal_orange" }, swirl = "swirl_gold", gravity = 0.05f, spin = 140f };
                case "trail_blossom": return new TrailLook { shapes = new[] { "blossom", "petal_pink", "petal_pink" }, swirl = "swirl_pink", gravity = 0.04f, spin = 140f };
                case "trail_clover": return new TrailLook { shapes = new[] { "clover", "leaf" }, swirl = "swirl_green", gravity = -0.03f, spin = 120f, sparkleTint = true, smoke = H("A8F0B4") };
                case "trail_bubbles": return new TrailLook { shapes = new[] { "bubble", "bubble_small", "bubble_small" }, swirl = "swirl_blue", gravity = -0.12f, spin = 0f, size = 0.2f };
                case "trail_hearts": return new TrailLook { shapes = new[] { "heart", "heart_small" }, swirl = "swirl_pink", gravity = -0.08f, spin = 30f };
                case "trail_stars": return new TrailLook { shapes = new[] { "star", "sparkle4" }, swirl = "swirl_stars", gravity = -0.05f, spin = 120f, sparkleTint = true, smoke = H("FFE38A") };
                case "trail_notes": return new TrailLook { shapes = new[] { "note", "note_pink" }, swirl = "swirl_notes", gravity = -0.09f, spin = 25f };
                case "trail_butterfly": return new TrailLook { shapes = new[] { "butterfly_pink", "butterfly_yellow", "butterfly_blue" }, swirl = "swirl_butterfly", gravity = -0.1f, spin = 20f, size = 0.2f };
                case "trail_moon": return new TrailLook { shapes = new[] { "moon", "cloud", "sparkle4" }, swirl = "swirl_purple", gravity = -0.04f, spin = 30f, sparkleTint = true, smoke = H("D9C8FF") };
                case "trail_koi": return new TrailLook { shapes = new[] { "goldfish", "wave", "bubble_small" }, swirl = "swirl_blue", gravity = -0.06f, spin = 15f, size = 0.2f };
            }
            return new TrailLook { shapes = new[] { "sparkle4" } };
        }

        static readonly Dictionary<string, Material> trailMats = new Dictionary<string, Material>();

        static Material TrailMat(string tex, bool additive)
        {
            string key = tex + (additive ? "+" : "");
            if (trailMats.TryGetValue(key, out var m) && m) return m;
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) return null;
            m = new Material(sh) { name = "Trail_" + key };
            m.SetTexture("_BaseMap", Resources.Load<Texture2D>("Trails/" + tex));
            m.SetColor("_BaseColor", Color.white);
            // URP declares these as Float properties: SetInt does not reach them in Unity 6, which left the material
            // opaque (the white squares). SetFloat on each, plus the transparent keyword and queue.
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (additive) m.EnableKeyword("_BLENDMODE_ADD");
            m.renderQueue = 3000;
            return trailMats[key] = m;
        }

        static ParticleSystem Layer(Transform parent, string name, string tex, bool additive)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            var ps = g.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 120;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;
            var r = g.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = TrailMat(tex, additive);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingFudge = additive ? -2f : 0f;
            return ps;
        }

        static AnimationCurve Curve(params float[] kv)
        {
            var c = new AnimationCurve();
            for (int i = 0; i + 1 < kv.Length; i += 2) c.AddKey(new Keyframe(kv[i], kv[i + 1]));
            for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
            return c;
        }

        static Gradient Fade(Color c, float peak)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peak, 0.15f), new GradientAlphaKey(peak * 0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        GameObject trailRoot;

        void BuildTrail(Item it)
        {
            if (trailRoot) { trailRoot.SetActive(false); Destroy(trailRoot); }
            trail = null;
            if (it == null || it.id == "trail_none") return;
            var look = LookFor(it.id);
            trailRoot = new GameObject("Trail");
            trailRoot.transform.SetParent(transform, false);
            trailRoot.transform.localPosition = new Vector3(0, 0.18f, -0.18f);
            var root = trailRoot.transform;

            // 1. smoke: big soft tinted puffs that swell and fade
            var smoke = Layer(root, "Smoke", look.swirl, false);
            var m = smoke.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.3f);
            m.startSpeed = 0.04f;
            m.startSize = new ParticleSystem.MinMaxCurve(0.38f, 0.6f);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.gravityModifier = -0.02f;
            var e = smoke.emission; e.rateOverTime = 0f; e.rateOverDistance = 9f;
            var col = smoke.colorOverLifetime; col.enabled = true; col.color = Fade(look.smoke, 0.6f);
            var sol = smoke.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, Curve(0f, 0.6f, 1f, 1.5f));

            // 2. the trail's own shapes: pop in, drift (float up or flutter down), spin, pop out
            for (int i = 0; i < look.shapes.Length; i++)
            {
                var sp = Layer(root, "Shapes" + i, look.shapes[i], false);
                var sm = sp.main;
                sm.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.6f);
                sm.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.25f);
                string sh = look.shapes[i];
                float sz = sh.StartsWith("petal") || sh.StartsWith("sparkle") || sh.EndsWith("_small") ? look.size * 0.7f : look.size;
                sm.startSize = new ParticleSystem.MinMaxCurve(sz * 1.2f, sz * 1.9f);
                sm.startRotation = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
                sm.gravityModifier = look.gravity;
                var se = sp.emission; se.rateOverTime = 0f; se.rateOverDistance = 7f / look.shapes.Length;
                var ss = sp.sizeOverLifetime; ss.enabled = true; ss.size = new ParticleSystem.MinMaxCurve(1f, Curve(0f, 0f, 0.15f, 1.1f, 0.25f, 1f, 0.8f, 0.9f, 1f, 0f));
                var sc = sp.colorOverLifetime; sc.enabled = true; sc.color = Fade(Color.white, 1f);
                if (look.spin != 0f) { var rot = sp.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-look.spin * Mathf.Deg2Rad, look.spin * Mathf.Deg2Rad); }
                var nz = sp.noise; nz.enabled = true; nz.strength = 0.15f; nz.frequency = 0.6f; nz.scrollSpeed = 0.3f;
                if (i == 0) trail = sp;
            }

            // 3. sparkles: tiny twinkles, a few even while she stands still (magic aura)
            var tw = Layer(root, "Sparkles", "sparkle4", true);
            var tm = tw.main;
            tm.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            tm.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
            tm.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
            tm.startColor = new ParticleSystem.MinMaxGradient(Color.white, look.sparkleTint ? look.smoke : Color.white);
            tm.gravityModifier = -0.04f;
            var te = tw.emission; te.rateOverTime = 5f; te.rateOverDistance = 10f;
            var ts = tw.sizeOverLifetime; ts.enabled = true; ts.size = new ParticleSystem.MinMaxCurve(1f, Curve(0f, 0f, 0.2f, 1f, 0.45f, 0.4f, 0.7f, 1f, 1f, 0f));
            var tsh = tw.shape; tsh.radius = 0.25f;

            var catA = GetComponentInChildren<Animator>();
            if (catA) MatchLayer(trailRoot, catA);      // so the wardrobe preview camera sees the trail too
            foreach (var ps in trailRoot.GetComponentsInChildren<ParticleSystem>()) ps.Play();
        }
    }
}

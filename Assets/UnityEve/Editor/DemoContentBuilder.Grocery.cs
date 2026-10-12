using UnityEngine;
using UnityEve.Battle;
using UnityEve.Cameras;
using UnityEve.Interaction;

namespace UnityEve.Editor
{
    /// <summary>
    /// Inside Kingsport Foods: checkout lanes, four stocked aisles, a produce stand and freezers, and
    /// behind a locked staff door the storeroom and the manager's desk. Two fights.
    /// </summary>
    public static partial class DemoContentBuilder
    {
        private static readonly Vector3 GroceryOrigin = new Vector3(-120f, 0f, -20f);

        private static void BuildGrocery(SceneKit k)
        {
            var c = k.Content;
            var o = GroceryOrigin;
            var lino = SimpleMaterial("StoreLino", new Color(0.72f, 0.72f, 0.66f), Tex("tiles"), 0.4f);
            var storeWall = SimpleMaterial("StoreInterior", new Color(0.82f, 0.8f, 0.72f));
            var shelfMat = SimpleMaterial("ShelfMetal", new Color(0.55f, 0.58f, 0.6f), null, 0.5f);
            var counter = SimpleMaterial("CheckoutCounter", new Color(0.25f, 0.27f, 0.3f));
            var belt = SimpleMaterial("Belt", new Color(0.08f, 0.08f, 0.09f));
            var panel = SimpleMaterial("Fluorescent", new Color(0.85f, 0.95f, 1f), null, 0.5f, emissive: true);
            var coolerGlow = SimpleMaterial("CoolerGlass", new Color(0.3f, 0.5f, 0.55f), null, 0.9f, emissive: true);
            var signRed = SimpleMaterial("StoreSign", new Color(0.7f, 0.08f, 0.06f), null, 0.4f, emissive: true);
            var product = new[]
            {
                SimpleMaterial("ProductRed", new Color(0.75f, 0.18f, 0.12f)), SimpleMaterial("ProductYellow", new Color(0.9f, 0.75f, 0.2f)),
                SimpleMaterial("ProductBlue", new Color(0.18f, 0.35f, 0.7f)), SimpleMaterial("ProductGreen", new Color(0.25f, 0.55f, 0.3f)),
                SimpleMaterial("ProductWhite", new Color(0.88f, 0.86f, 0.8f)), SimpleMaterial("ProductOrange", new Color(0.9f, 0.48f, 0.15f)),
            };

            // ---- The hall (28 x 26 m, 4.2 m ceiling).
            RoomH(k, "Grocery", o + new Vector3(-14f, 0f, 0f), o + new Vector3(14f, 0f, 26f), lino, storeWall, 4.2f);
            var root = new GameObject("GroceryFittings").transform;
            root.SetParent(k.Level);
            root.position = o;
            Transform R() => root;

            // Ceiling light panels and flickering fluorescents.
            foreach (var x in new[] { -8f, 0f, 8f })
            foreach (var z in new[] { 6f, 14f, 22f })
            {
                LocalBox(R(), "LightPanel", new Vector3(x - 1.2f, 4.12f, z - 0.25f), new Vector3(x + 1.2f, 4.18f, z + 0.25f), panel, false);
                if ((x + z) % 16 != 0) PointLight(k, "Fluorescent", o + new Vector3(x, 3.9f, z), new Color(0.82f, 0.95f, 1f), 4f, 9f, flicker: (x * z) % 3 == 0);
            }

            // Entrance: the sliding doors (exit), mats, the customer service sign.
            LocalBox(R(), "SlidingDoors", new Vector3(-2f, 0f, 0f), new Vector3(2f, 3f, 0.1f), k.Window, false);
            LocalBox(R(), "Mat", new Vector3(-1.8f, 0f, 0.1f), new Vector3(1.8f, 0.02f, 1.6f), belt, false);
            LocalBox(R(), "ServiceDesk", new Vector3(8f, 0f, 1f), new Vector3(13f, 1.1f, 2.4f), counter);
            LocalBox(R(), "ServiceSign", new Vector3(8.5f, 2.6f, 0.05f), new Vector3(12.5f, 3.3f, 0.15f), signRed, false);
            SignText(R(), "CUSTOMER SERVICE", new Vector3(10.5f, 2.95f, 0.2f), 0f, 0.022f, Color.white);

            // Checkout lanes.
            foreach (var x in new[] { -10.5f, -7f, -3.5f })
            {
                LocalBox(R(), "Checkout", new Vector3(x - 0.45f, 0f, 3f), new Vector3(x + 0.45f, 0.95f, 5.6f), counter);
                LocalBox(R(), "Belt", new Vector3(x - 0.38f, 0.95f, 4f), new Vector3(x + 0.38f, 0.98f, 5.5f), belt, false);
                LocalBox(R(), "Register", new Vector3(x - 0.25f, 0.95f, 3.2f), new Vector3(x + 0.25f, 1.25f, 3.6f), shelfMat, false);
                LocalBox(R(), "LaneLight", new Vector3(x - 0.06f, 0.95f, 5.4f), new Vector3(x + 0.06f, 2.3f, 5.52f), shelfMat, false);
                LocalBox(R(), "LaneNumber", new Vector3(x - 0.2f, 2.3f, 5.4f), new Vector3(x + 0.2f, 2.6f, 5.52f), signRed, false);
            }

            // Aisles: double-sided shelving stocked in coloured rows, hanging aisle signs.
            var aisleNames = new[] { "CEREAL", "CANNED", "SNACKS", "HOUSEHOLD" };
            var aisleX = new[] { -9f, -4f, 1f, 6f };
            for (var a = 0; a < aisleX.Length; a++)
            {
                var x = aisleX[a];
                LocalBox(R(), "ShelfUnit", new Vector3(x - 0.6f, 0f, 8f), new Vector3(x + 0.6f, 0.15f, 20f), shelfMat);
                LocalBox(R(), "ShelfSpine", new Vector3(x - 0.05f, 0f, 8f), new Vector3(x + 0.05f, 2f, 20f), shelfMat);
                foreach (var y in new[] { 0.15f, 0.6f, 1.05f, 1.5f })
                {
                    LocalBox(R(), "Shelf", new Vector3(x - 0.6f, y, 8f), new Vector3(x + 0.6f, y + 0.04f, 20f), shelfMat, false);
                    for (var seg = 0; seg < 4; seg++)
                    foreach (var side in new[] { -1f, 1f })
                    {
                        if ((a * 7 + seg * 3 + (int)(y * 10) + (side > 0 ? 1 : 0)) % 9 == 0) continue; // a few emptied stretches
                        var m = product[(a + seg * 2 + (int)(y * 3) + (side > 0 ? 1 : 0)) % product.Length];
                        var z0 = 8.1f + seg * 3f;
                        LocalBox(R(), "Stock", new Vector3(side < 0 ? x - 0.56f : x + 0.08f, y + 0.04f, z0), new Vector3(side < 0 ? x - 0.08f : x + 0.56f, y + 0.38f, z0 + 2.8f), m, false);
                    }
                }
                LocalBox(R(), "AisleSign", new Vector3(x - 0.9f, 3.1f, 13.95f), new Vector3(x + 0.9f, 3.55f, 14.05f), signRed, false);
                // One face only: the text shader draws through the board, so a back copy would show mirrored.
                SignText(R(), aisleNames[a], new Vector3(x, 3.32f, 13.9f), 0f, 0.022f, Color.white);
            }
            // Spilled stock and a toppled cart in the aisles.
            var spill = new System.Random(5);
            for (var i = 0; i < 14; i++)
            {
                var px = (float)(spill.NextDouble() * 22 - 12);
                var pz = 8f + (float)spill.NextDouble() * 12f;
                if (System.Array.Exists(aisleX, ax => Mathf.Abs(ax - px) < 0.9f)) continue;
                LocalBox(R(), "Spilled", new Vector3(px - 0.15f, 0f, pz - 0.1f), new Vector3(px + 0.15f, 0.25f, pz + 0.1f), product[i % product.Length], false,
                    new Vector3(0f, (float)spill.NextDouble() * 90f, i % 3 == 0 ? 90f : 0f));
            }
            Cart(k, o + new Vector3(3.5f, 0f, 16f), 70f, tipped: true);

            // Produce stand and freezers.
            for (var i = 0; i < 4; i++)
            {
                var z = 6f + i * 2.2f;
                LocalBox(R(), "ProduceBin", new Vector3(10.5f, 0f, z), new Vector3(13f, 0.8f, z + 1.9f), k.Wood);
                LocalBox(R(), "Produce", new Vector3(10.6f, 0.8f, z + 0.1f), new Vector3(12.9f, 0.98f, z + 1.8f), product[new[] { 0, 3, 5, 1 }[i]], false);
            }
            for (var x = -13f; x < 4f; x += 2.6f)
            {
                LocalBox(R(), "Freezer", new Vector3(x, 0f, 24.6f), new Vector3(x + 2.4f, 2.4f, 26f), storeWall);
                LocalBox(R(), "FreezerGlass", new Vector3(x + 0.1f, 0.3f, 24.55f), new Vector3(x + 2.3f, 2.2f, 24.62f), coolerGlow, false);
            }
            PointLight(k, "FreezerGlow", o + new Vector3(-5f, 1.4f, 23.6f), new Color(0.55f, 0.85f, 1f), 3f, 7f, flicker: true);
            LocalBox(R(), "StaffDoor", new Vector3(8.4f, 0f, 25.9f), new Vector3(10.2f, 2.3f, 26f), k.DoorMat, false);
            SignText(R(), "STAFF ONLY", new Vector3(9.3f, 2.5f, 25.85f), 180f, 0.018f, new Color(1f, 0.85f, 0.4f));

            // ---- The storeroom (behind the staff door).
            var s = o + new Vector3(4f, 0f, 27f);
            RoomH(k, "Storeroom", s, s + new Vector3(10f, 0f, 9f), k.Sidewalk, k.Plaster, 3.2f);
            PointLight(k, "StoreroomBulb", s + new Vector3(5f, 2.9f, 4.5f), new Color(1f, 0.82f, 0.55f), 4f, 9f, flicker: true, shadows: true);
            foreach (var (px, pz) in new[] { (1.2f, 7.8f), (3.4f, 7.8f), (8.8f, 7.6f), (8.8f, 5f) })
            {
                Model(k.Level, Urban("pallet"), s + new Vector3(px, 0f, pz), 0f, size: 1.3f, name: "Pallet");
                Model(k.Level, Surv("box-large"), s + new Vector3(px, 0.15f, pz), 15f * px, size: 1f, name: "Boxes");
            }
            var desk = Model(k.Interact, Furn("desk"), s + new Vector3(1.4f, 0f, 2f), 90f, size: 1.6f, name: "Container_Storeroom_Desk");
            Attach<Container>(desk, s + new Vector3(2.6f, 0f, 2f), 1.6f)
                .Configure("manager's desk", c.Loot["drawer"], "Under a schedule taped with a church flyer...", c.Items["key_church"]);
            Locks.Key("Storeroom", "key_church");
            var log = Marker(k, "Readable_ManagersLog", s + new Vector3(1.4f, 0.9f, 3.4f), 90f);
            Attach<Readable>(log, s + new Vector3(2.6f, 0f, 3.4f), 1.4f, 0f).Configure("Read the manager's log",
                "'Frank - Father Ames left the side-door key with us for the food drive. Keep it in the desk. Whatever is in the back freezer is NOT spoiled meat. Don't open it.'");

            // ---- Doors: storefront <-> hall, hall <-> storeroom (locked).
            var outside = Marker(k, "Spawn_GroceryOutside", new Vector3(-51f, 0.14f, -94.6f), 0f).transform;
            var inside = Marker(k, "Spawn_GroceryInside", o + new Vector3(0f, 0f, 1.4f), 0f).transform;
            var enter = Attach<Door>(Marker(k, "Door_Grocery_Enter", new Vector3(-51f, 0.14f, -95.9f), 180f), new Vector3(-51f, 0.14f, -94.6f), 1.8f, 1.2f);
            enter.Configure("Enter Kingsport Foods", inside);
            var leave = Attach<Door>(Marker(k, "Door_Grocery_Exit", o + new Vector3(0f, 0f, 0.1f), 0f), o + new Vector3(0f, 0f, 1.2f), 1.6f, 1.2f);
            leave.Configure("Go back outside", outside);
            var roomIn = Marker(k, "Spawn_StoreroomInside", s + new Vector3(5.3f, 0f, 1.2f), 0f).transform;
            var roomOut = Marker(k, "Spawn_StoreroomOutside", o + new Vector3(9.3f, 0f, 24.6f), 180f).transform;
            var staff = Attach<Door>(Marker(k, "Door_Storeroom_Enter", o + new Vector3(9.3f, 0f, 25.8f), 0f), o + new Vector3(9.3f, 0f, 24.6f), 1.6f, 1.2f);
            staff.Configure("Open the staff door", roomIn, c.Items["key_storeroom"], "STAFF ONLY. Locked. The manager will have a key - or the registers will.");
            Locks.Edge("Street", "Storeroom", "key_storeroom");
            var staffBack = Attach<Door>(Marker(k, "Door_Storeroom_Exit", s + new Vector3(5.3f, 0f, 0.1f), 0f), s + new Vector3(5.3f, 0f, 1.1f), 1.6f, 1.2f);
            staffBack.Configure("Back to the shop floor", roomOut);

            // ---- Things to find on the shop floor.
            var register = Marker(k, "Container_Grocery_Register", o + new Vector3(-7f, 1.1f, 3.4f), 0f);
            Attach<Container>(register, o + new Vector3(-6.2f, 0f, 3.4f), 1.4f, 0f)
                .Configure("register", c.Loot["dumpster"], "The drawer is jammed with receipts.", c.Items["key_storeroom"]);
            Locks.Key("Street", "key_storeroom");
            var shelf = Marker(k, "Container_Grocery_Shelf", o + new Vector3(-4f, 1f, 11f), 0f);
            Attach<Container>(shelf, o + new Vector3(-2.8f, 0f, 11f), 1.4f, 0f).Configure("shelf", c.Loot["kitchen"], "Most of it is gone, but not all.");
            var freezer = Marker(k, "Container_Grocery_Freezer", o + new Vector3(-0.6f, 1f, 24.8f), 0f);
            Attach<Container>(freezer, o + new Vector3(-0.6f, 0f, 23.6f), 1.4f, 0f).Configure("freezer", c.Loot["kitchen"]);
            var pharmacy = Marker(k, "Container_Grocery_Service", o + new Vector3(10.5f, 1f, 1.7f), 0f);
            Attach<Container>(pharmacy, o + new Vector3(10.5f, 0f, 3f), 1.4f, 0f).Configure("service desk", c.Loot["bathroom"], "First-aid supplies behind the counter.");
            var shells = Marker(k, "Pickup_GroceryShells", o + new Vector3(8.5f, 0.05f, 18f), 0f);
            Attach<Pickup>(shells, o + new Vector3(8.5f, 0f, 17f), 1.3f, 0.2f).Configure(c.Items["ammo_9mm"], 2, Glint(k, shells.transform));

            // ---- Fights.
            Encounter(k, "9_Grocery", "Kingsport Foods", o + new Vector3(-12f, 0f, 7f), o + new Vector3(10f, 0f, 23f),
                ("crawler", new Vector3(-6f, 0f, 4f)), ("crawler", new Vector3(5f, 0f, -3f)), ("sporesac", new Vector3(0f, 0f, 5f)));
            Encounter(k, "10_Storeroom", "Storeroom", s + new Vector3(0.4f, 0f, 2.6f), s + new Vector3(9.6f, 0f, 6.8f),
                ("brute", new Vector3(2f, 0f, 1.2f)));

            // ---- Screens.
            var checkout = Shot(k, "GroceryCheckout", o + new Vector3(13.3f, 3.9f, 7.5f), o + new Vector3(-4f, 0.6f, 2f), ShotMode.Fixed, 60f);
            Zone(k, "GroceryCheckout", o + new Vector3(-14f, 0, 0f), o + new Vector3(14f, 0, 7.5f), checkout);
            var aisles = Shot(k, "GroceryAislesCCTV", o + new Vector3(-13.4f, 4f, 7.2f), o + new Vector3(2f, 0f, 16f), ShotMode.Cctv, 64f, 50f, 30f);
            Zone(k, "GroceryAisles", o + new Vector3(-14f, 0, 7f), o + new Vector3(14f, 0, 21f), aisles);
            var back = Shot(k, "GroceryFreezers", o + new Vector3(13.2f, 3.8f, 20.5f), o + new Vector3(-3f, 0.8f, 25f), ShotMode.Fixed, 58f);
            Zone(k, "GroceryFreezers", o + new Vector3(-14f, 0, 20.5f), o + new Vector3(14f, 0, 26f), back);
            var room = Shot(k, "Storeroom", s + new Vector3(9.6f, 2.9f, 0.4f), s + new Vector3(3f, 0.6f, 6f), ShotMode.Fixed, 66f);
            Zone(k, "Storeroom", s + new Vector3(0f, 0, -0.5f), s + new Vector3(10f, 0, 9f), room);

            Tour(k, "23_grocery_checkout", o + new Vector3(-1f, 0f, 3f), 0f);
            Tour(k, "24_grocery_aisles", o + new Vector3(-1.5f, 0f, 14f), 0f);
            Tour(k, "25_grocery_freezers", o + new Vector3(0f, 0f, 22.5f), 0f);
            Tour(k, "26_storeroom", s + new Vector3(5f, 0f, 3f), 0f);
        }
    }
}

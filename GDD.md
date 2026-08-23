# Terraforming Tendencies - Game Design Document

That sounds like a fantastic "Cold Start" mechanic! It adds a layer of mystery and urgency that fits perfectly with a casual but tense RTS. You aren't just building; you are scouting for your life.

By starting the player in a dark, unexplored map covered by a physical Hex-Grid Shroud with decaying buildings, you create an immediate resource race. If they don't find a Biomass vent or a Mineral vein within the first two minutes, their starting base will simply rot away.

The Gameplay Loop: Search & Secure

Here is how we can break that down into high-level systems:

1. The "Probe Droid" Automation

Instead of micro-managing every scout, the player hits a "Scan Sector" button.

Cost: 25 Biomass per Probe.

Behavior: The Probe spawns and picks a random "Unexplored" Hex on the map. As it moves, it permanently reveals the Hexes it travels through (removing the physical shroud tiles), exposing the 3D terrain and pinging any resources underneath.

Risk: The Probes themselves are fragile. If they fly into a "Toxic Storm" (a natural disaster), they are destroyed.

2. Hidden Resource Spawning (The "Treasure Map")

When the game starts, a ResourceGenerator script places resource nodes randomly, but disables their visuals and colliders.

The player sees nothing but a barren wasteland.

When a Probe "Reveals" a Hex, the Hex Manager checks for any hidden resources within that Hex coordinate and makes them permanently Visible and Selectable.

3. Transition to "Permanence"

Once a resource is found, the player has to move from "Scouting" to "Settling."

The Mining Station: A building that can only be placed on a discovered resource node.

The Life Support Link: Once the station is built, the player can build a Conduit or Spreader Node to connect it back to the main base, stopping the decay of that specific outpost.


### The Hex-Based Presentation System
While the game remains a 3D RTS on a continuous plane, the exploration and visibility are strictly Hex-based.
1. **The Shroud:** The map is covered in physical Hex-Tile GameObjects that obscure the terrain and resources.
2. **The Reveal:** When a player's unit enters a Hex coordinate, the Shroud tile is destroyed/animated away, revealing the 3D world underneath.
3. **Hex Coordinates:** The underlying 3D world space is mapped to a mathematical Hex Grid, ensuring units can accurately translate their XYZ positions into Hex Q/R/S coordinates to trigger reveals.

Implementation Hints: How to build this "Decay & Discovery" System

Since you’ve reset your code, we can build this in a modular way. Here are the three scripts you’ll eventually need:

1. The Global Decay Manager

A script that loops through every building on the map.

if (building.isInsideSpreaderRadius == false) building.Health -= decayRate * Time.deltaTime;

2. The Hidden Resource Spawner

A script that runs once at Start().

It uses Random.Range to place MineralPatch and GasVent prefabs across the map.

It sets gameObject.SetActive(false) until a Probe finds it.

3. The Probe Logic

A simple unit with a "Search" state.

It moves to a targetLocation.

It has a SphereCollider set to "Is Trigger."

OnTriggerEnter(Collider other): If "other" is a Hidden Resource, it calls other.GetComponent<Resource>().Discover().

The First Conflict: The "Starting Budget"

To make this work, the player needs to start with just enough resources to launch, say, 5 Probes.

If those 5 Probes find nothing, the player loses.

If they find a "Small Scrap Pile," they get enough Biomass to launch 5 more.

If they find a "Permanent Vein," they win the early game.

Does this "Search and Discovery" phase feel like the right level of "Casual" for you? Or should the probes be even more automated (like they just launch themselves every 30 seconds)?
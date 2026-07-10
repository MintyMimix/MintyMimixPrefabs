== HOW TO USE ==
1. Click and drag the "Zone Manager" prefab into your scene.
2. Duplicate "ZoneTrigger-Example1" in the hierarchy for as many zones as you want to have in your world.
 - Note: There is a maximum of 32767 zones. If for some reason you need more, change all instances of `short` in the scripts to `int`.
3. Modify each of these duplicate zone triggers to match the zones you want to have in your world, including their name and the space they occupy. 
  - WARNING: There MUST be at least 2.35 units of space between zones, otherwise a player may be marked as in "no zone" due to occupying two at the same time.
  - WARNING: Teleporting into/out of a zone will NOT work out of the box; you will need an Udon script to call the ZoneTrigger's ForceLocalEnter() and ForceLocalExit() methods if you wish to do this.
4. Inspect the ZoneManager. Add and remove all of the zone triggers you have just created/deleted. Be sure not to duplicate entries!
5. Repeat Steps 2-4 for ZoneMonitor.
  - Zone Monitors are screens that will report how many players are in the zone (if "Output Type" is set to "Player Count") and optionally which players are in the zone (if "Output Type" is set to "Player List".
  - A single Zone Monitor can report as many zones as you'd like. You can configure this in "List Mode", where you can set it to one zone, multiple zones, or all zones in the world.
  - If you wish to modify how the monitor looks, you may freely do so. Just ensure that the TMP Text objects "ZoneMonitorTitle" and "ZoneMonitorDescription" both exist as children and are not renamed.
  - If you do not want any zone monitors, you can safely set the array in ZoneManager to 0 indices.
6. Optionally modify "ZoneTrigger-Respawn" to occupy the area that players will spawn into when they hit "Respawn". 
 - If you do not want this behavior, you may delete the object from your hierarchy and remove the entry in Zone Manager's "Zone Trigger for Respawn" in the inspector.

== Prefab by MintyMimix ==
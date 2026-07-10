
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class ZoneManager : UdonSharpBehaviour
{
    [Tooltip("What is the zone that should be applied when a player respawns? (For no zone, leave this blank.)")]
    [SerializeField] private ZoneTrigger zoneTriggerForRespawn;
    [Tooltip("A list of the zone triggers that should be managed.")]
    [SerializeField] private ZoneTrigger[] zoneTriggers;
    [Tooltip("A list of zone monitors that should be managed. May be safely left with zero entries.")]
    [SerializeField] private ZoneMonitor[] zoneMonitors = new ZoneMonitor[1];
    // All lists of all players in every zone.
    // The index of the list corresponds to the zone ID, and the value within each DataList is a player ID int.
    private DataList[] playersInZones;
    // A mapping of every zone name (value) with every short zone ID (index).
    private string[] zoneIDMapping;
    // The zone ID derived from zoneNameForRespawn. If -1, no zone was found / specified.
    internal short zone_id_for_respawn;
    // The local player's zone tracker.
    private ZoneTrackerPlayer localZoneTracker;
    // Constants
    internal const short NO_ZONE = -1;
    internal const short RESPAWN_ZONE = -2;
    internal const short UNKNOWN_ZONE = -3;

    // Initialize runtime variables.
    private void Start()
    {
        playersInZones = new DataList[zoneTriggers.Length];
        zoneIDMapping = new string[zoneTriggers.Length];
        for (short i = 0; i < zoneTriggers.Length; i++)
        {
            // Create a datalist of player IDs for every zone.
            playersInZones[i] = new DataList();
            if (zoneTriggers[i] == null) { continue; }
            // Set each zone trigger's ID based on the listed index.
            zoneTriggers[i].zone_id = i;
            // Create a mapping of the zone's name to the ID.
            zoneIDMapping[i] = zoneTriggers[i].zoneName.Trim();
        }
        // Once the zone name mapping is complete, initialize the zone monitors.
        if (zoneMonitors != null)
        {
            for (int i = 0; i < zoneMonitors.Length; i++)
            {
                if (zoneMonitors[i] != null)
                {
                    zoneMonitors[i].InitializeZonePanels();
                }
            }
        }
        // And populate the respawn zone ID.
        if (zoneTriggerForRespawn != null) { zone_id_for_respawn = zoneTriggerForRespawn.zone_id; }
    }

    // When we have fully retrieved another player's data, we should update their zone ID accordingly.
    public override void OnPlayerRestored(VRCPlayerApi player)
    {
        base.OnPlayerRestored(player);
        if (playersInZones == null) { return; } // Somehow, setup is incomplete at this stage; do not go further.
        if (player == null) { return; }
        // Find that player's PlayerZoneTracker and make sure they're listed in the proper zone.
        GameObject[] playerObjects = Networking.GetPlayerObjects(player);
        for (int i = 0; i < playerObjects.Length; i++)
        {
            if (!Utilities.IsValid(playerObjects[i])) continue;
            ZoneTrackerPlayer tracker = playerObjects[i].GetComponent<ZoneTrackerPlayer>();
            if (tracker != null)
            {
                tracker.ApplyLocalZoneChange(true);
                if (player.isLocal) { localZoneTracker = tracker; }
            }
        }
        
    }

    // The local player is changing zones.
    internal void SendZoneChange(short new_zone_id, bool is_exit = false)
    {
        if (new_zone_id == RESPAWN_ZONE) { new_zone_id = zone_id_for_respawn; }
        if (localZoneTracker != null) { localZoneTracker.ChangeZone(new_zone_id, is_exit); }
    }

    // Received a zone change request from a player (both local and remote).
    internal void ReceiveZoneChange(short prev_zone_id, short new_zone_id, int player_id, bool force_update = false)
    {
        // Don't process if the zones match.
        if (prev_zone_id == new_zone_id && !force_update) { return; }
        // Zone ID of -2 means to use whatever we have assigned for our respawn.
        if (prev_zone_id == RESPAWN_ZONE) { prev_zone_id = zone_id_for_respawn; }
        if (new_zone_id == RESPAWN_ZONE) { new_zone_id = zone_id_for_respawn; }
        // If the previous Zone ID is in our list of zones, remove the player from that zone.
        if (prev_zone_id >= 0 && prev_zone_id < playersInZones.Length)
        {
            playersInZones[prev_zone_id].Remove(player_id);
        }
        // If the new Zone ID is in our list of zones, add the player to that zone.
        if (new_zone_id >= 0 && new_zone_id < playersInZones.Length)
        {
            if (!playersInZones[new_zone_id].Contains(player_id))
            {
                playersInZones[new_zone_id].Add(player_id);
            }
        }
        // Finalll, refresh the monitors.
        if (zoneMonitors != null)
        {
            for (int i = 0; i < zoneMonitors.Length; i++)
            {
                zoneMonitors[i].RefreshZonePanelAtID(prev_zone_id);
                if (new_zone_id != prev_zone_id) { zoneMonitors[i].RefreshZonePanelAtID(new_zone_id); }
            }
        }
        OnZoneChanged(new_zone_id, player_id);
    }

    // Various external getters for potential use by other scripts.
    internal bool IsPlayerInZone(int playerId, short zone)
    {
        int count = GetPlayerCountInZone(zone);
        for (int i = 0; i < count; i++)
        {
            // If the entry is invalid, skip it.
            if (playersInZones[zone][i].Error != DataError.None) { continue; }
            if (playersInZones[zone][i].Int == playerId) { return true; }
        }
        return false;
    }
    internal DataList GetPlayersInZone(short zone)
    {
        if (playersInZones == null || zone < 0 || zone >= playersInZones.Length) { return new DataList(); }
        return playersInZones[zone];
    }
    internal int GetPlayerCountInZone(short zone)
    {
        if (playersInZones == null || zone < 0 || zone >= playersInZones.Length) { return 0; }
        return playersInZones[zone].Count;
    }
    internal short GetZoneIDFromName(string name)
    {
        if (zoneIDMapping == null) { return NO_ZONE; }
        name = name.Trim();
        for (short i = 0; i < zoneIDMapping.Length; i++)
        {
            if (string.Equals(name, zoneIDMapping[i], System.StringComparison.OrdinalIgnoreCase)) { return i; }
        }
        return NO_ZONE;
    }
    internal string GetZoneNameFromID(short zone)
    {
        if (zone < 0 || zone >= zoneIDMapping.Length) { return null; }
        return zoneIDMapping[zone];
    }

    internal string[] GetAllZoneNames()
    {
        if (zoneIDMapping == null) { return null; }
        return (string[])zoneIDMapping.Clone();
    }

    internal ZoneTrigger[] GetAllZones()
    {
        if (zoneTriggers == null) { return null; }
        return (ZoneTrigger[])zoneTriggers.Clone();
    }

    // Publicly available callback method.
    public virtual void OnZoneChanged(short zone_id, int player_id) 
    {

    }

}

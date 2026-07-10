
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class ZoneTrackerPlayer : UdonSharpBehaviour
{
    [Tooltip("The zone manager used for this world. Make sure this gets linked in the inspector.")]
    [SerializeField] private ZoneManager zoneManager;
    // The zone ID the user is currently occupying.
    // If zone ID < 0, we assume they are not in any zone.
    // If you want to have more than thousands of zones, change the data type from short to int.
    // Though one must wonder about your implementation if you need THAT many zones...
    [UdonSynced] private short zone_id = ZoneManager.RESPAWN_ZONE;
    // A local version of the zone ID that the client has cached.
    // If there is a difference between this and the networked zone ID, we can alert the ZoneManager.
    private short local_zone_id = ZoneManager.UNKNOWN_ZONE;
    // The networked owner of this player object.
    private VRCPlayerApi owner = null;

    // At this object's instantiation, get the network owner.
    // We can do it at this stage instead of OnOwnershipTransferred() since this is a Player Object.
    private void Start()
    {
        owner = Networking.GetOwner(gameObject);
    }

    // The client is receiving a network update from this script.
    public override void OnDeserialization()
    {
        // If the zone ID has not changed, do nothing.
        if (zone_id == local_zone_id) { return; }
        ApplyLocalZoneChange();
    }

    // Updates the Zone Manager and recaches the zone ID.
    // Typically called by OnDeserialization(), but is also called when a player's data is restored on ZoneManager.
    internal void ApplyLocalZoneChange(bool force_update = false)
    {
        // If we can't find the owner, do nothing.
        if (owner == null) { owner = Networking.GetOwner(gameObject); if (owner == null) { return; } }
        // Locally alert the ZoneManager that this player's zone has changed.
        zoneManager.ReceiveZoneChange(local_zone_id, zone_id, owner.playerId, force_update);
        // Cache the new value.
        local_zone_id = zone_id;
    }

    // Called by the ZoneManager when the local player enters a new zone.
    internal void ChangeZone(short new_zone_id, bool is_exit = false)
    {
        // This script should only be called by its owner.
        if (owner == null || !owner.isLocal) { return; }
        // If the new zone is respawn, get the corresponding ID.
        if (new_zone_id == ZoneManager.RESPAWN_ZONE) { new_zone_id = zoneManager.zone_id_for_respawn; }
        // If we are exitting a trigger, ensure we aren't overwriting a new zone's value already.
        if (is_exit && new_zone_id == zone_id) { new_zone_id = ZoneManager.NO_ZONE; }
        else if (is_exit && new_zone_id != zone_id) { return; }
        // Set the networked Zone ID to the new one, and synchronize it across the network.
        zone_id = new_zone_id;
        RequestSerialization();
        if (zone_id != local_zone_id) { ApplyLocalZoneChange(); }
    }

    // If the owner disconnects, send a message to the zone manager accordingly.
    public override void OnPlayerLeft(VRCPlayerApi player)
    {
        base.OnPlayerLeft(player);
        if (owner == null || player != owner) { return; }
        zoneManager.ReceiveZoneChange(zone_id, ZoneManager.NO_ZONE, player.playerId);
    }

    // If the owner respawns, send a message to the zone manager accordingly.
    public override void OnPlayerRespawn(VRCPlayerApi player)
    {
        base.OnPlayerRespawn(player);
        if (owner == null || player != owner) { return; }
        ChangeZone(ZoneManager.RESPAWN_ZONE);
    }

}

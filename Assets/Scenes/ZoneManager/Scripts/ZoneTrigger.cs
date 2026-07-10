
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class ZoneTrigger : UdonSharpBehaviour
{
    [Tooltip("The zone manager used for this world. Make sure this gets linked in the inspector.")]
    [SerializeField] private ZoneManager zoneManager;
    // For some reason, getters/setters break this script, so we'll just expose their variables outright.
    [Tooltip("The Zone name assigned to this zone. Make sure no two names match.")]
	[SerializeField] internal string zoneName = "";
    // The actual zone ID used for processing. 
    internal short zone_id;

    // A callback method to force the local player to enter this trigger.
    public void ForceLocalEnter()
    {
		zoneManager.SendZoneChange(zone_id, false);
	}

	// A callback method to force the local player to exit this trigger.
	public void ForceLocalExit()
	{
		zoneManager.SendZoneChange(zone_id, true);
	}

	// When a player enters the zone, relay the information to the zone manager.
	public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        // Due to unreliability of remote player colliders, we will only have the local player fire this event.
        if (!player.isLocal) { return; }
        base.OnPlayerTriggerEnter(player);
        zoneManager.SendZoneChange(zone_id, false);
    }

	// When a player leaves the zone, relay the information to the zone manager.
	public override void OnPlayerTriggerExit(VRCPlayerApi player)
	{
		// Due to unreliability of remote player colliders, we will only have the local player fire this event.
		if (!player.isLocal) { return; }
		base.OnPlayerTriggerExit(player);
		zoneManager.SendZoneChange(zone_id, true);
	}

}

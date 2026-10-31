
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class VRCStationSharp : UdonSharpBehaviour
{
    /// <summary>
    /// UdonSharp version of the VRCStation default UdonBehaviour.
    /// </summary>

    internal VRC.SDKBase.VRCStation _station;
    public bool occupied = false;
    public string playerName = string.Empty;
    public VRCPlayerApi playerInStation = null;

    private void Start()
    {
        _station = GetComponent<VRC.SDKBase.VRCStation>();
        _station.transform.TweenPosition(_station.transform.position + new Vector3(0, 5, 0), 2f, VRCTweenEase.OutQuad)
            .From()
            .SetDelay(0.5f)
            .SetLoops(-1, VRCTweenLoopType.Yoyo);
    }

    public override void Interact()
    {
        _station.PlayerMobility = VRC.SDKBase.VRCStation.Mobility.Mobile;
        Networking.LocalPlayer.UseAttachedStation();
    }

    public override void OnStationEntered(VRCPlayerApi player)
    {
        playerInStation = player;
        playerName = player.displayName;
        occupied = true;
        if (!player.isLocal)
        {
            _station.PlayerMobility = VRC.SDKBase.VRCStation.Mobility.Immobilize;
        }
    }

    public override void OnStationExited(VRCPlayerApi player)
    {
        playerInStation = null;
        playerName = string.Empty;
        occupied = false;
    }

    public void _EnterStation()
    {
        // Make the seated location upright and aligned with the player.
        // This is to circumvent issues when entering in VR when the station is rotated on the x or z axes.
        _station.stationEnterPlayerLocation.rotation = Quaternion.Euler(Networking.LocalPlayer.GetRotation().eulerAngles.x, Networking.LocalPlayer.GetRotation().eulerAngles.y, 0);
        _station.UseStation(Networking.LocalPlayer);

        // Return the seat to its correct orientation, bringing the player with us in the process.
        // This fixes the seated position when getting in a rolled/pitched vehicle in VR.
        _station.stationEnterPlayerLocation.localRotation = Quaternion.identity;
    }

    public void _ForceRemoteIntoStation()
    {
        VRCPlayerApi[] players = VRCPlayerApi.GetPlayers();
        if (players.Length < 1) { return; }
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i].isLocal) { continue; }
            _station.UseStation(players[i]);
        }
    }
}


using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class GravityButton : UdonSharpBehaviour
{
    public VRCStation station_attached;

    private void Start()
    {
    }

    public override void Interact()
    {
        station_attached.UseStation(Networking.LocalPlayer);
    }

    public override void OnStationEntered(VRCPlayerApi player)
    {
        Debug.Log($"{player.displayName} Entered");
        station_attached.PlayerMobility = VRCStation.Mobility.Mobile;
        if (transform.rotation.eulerAngles.z < 180) { transform.rotation = Quaternion.Euler(0, 0, 180); }
        Networking.LocalPlayer.SetGravityStrength(-Mathf.Abs(Networking.LocalPlayer.GetGravityStrength()));
        SendCustomEventDelayedFrames("SendGravity", 4);
    }

    public override void OnStationExited(VRCPlayerApi player)
    {
        Debug.Log($"{player.displayName} Exited");
        if (transform.rotation.eulerAngles.z > 0) { transform.rotation = Quaternion.Euler(0, 0, 0); }
        Networking.LocalPlayer.SetGravityStrength(Mathf.Abs(Networking.LocalPlayer.GetGravityStrength()));
        Networking.LocalPlayer.Immobilize(false);
    }

    public void SendGravity()
    {
        station_attached.PlayerMobility = VRCStation.Mobility.ImmobilizeForVehicle;
        SendCustomEventDelayedFrames("ReleaseGravity", 4);
    }

    public void ReleaseGravity()
    {
        station_attached.PlayerMobility = VRCStation.Mobility.Mobile;
        Networking.LocalPlayer.Immobilize(false);
    }

    public override void OnPlayerRespawn(VRCPlayerApi player)
    {
        Networking.LocalPlayer.SetGravityStrength(Mathf.Abs(Networking.LocalPlayer.GetGravityStrength()));
        Networking.LocalPlayer.Immobilize(false);
    }
}

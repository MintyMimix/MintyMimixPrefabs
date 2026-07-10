
using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;

public enum ZoneMonitorListMode { SingleZone, MultipleZones, AllZones }
public enum ZoneMonitorOutputType { PlayerCount, PlayerList, PlayerCountAndList }

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class ZoneMonitor : UdonSharpBehaviour
{
    [Header("Configuration")]
    [Tooltip("Should this monitor only display one zone, multiple zones, or all zones?")]
    [SerializeField] private ZoneMonitorListMode listMode;
    [Tooltip("Should this monitor only display player counts, or should it list the players within the zone?")]
    [SerializeField] private ZoneMonitorOutputType outputType;
    [Tooltip("What are the zone triggers that should be monitored?")]
    [SerializeField] private ZoneTrigger[] zoneTriggers = new ZoneTrigger[2];
    [Tooltip("What is the zone trigger that should be monitored?")]
    [SerializeField] private ZoneTrigger zoneTrigger;
    [Header("References")]
    [Tooltip("The template panel that will be instantiated based on the number of zones selected.")]
    [SerializeField] private RectTransform templatePanel;
    [Tooltip("The zone manager used for this world. Make sure this gets linked in the inspector.")]
    [SerializeField] private ZoneManager zoneManager;

    private RectTransform[] zonePanels = new RectTransform[0]; // The list of template panels per zone.
    private TMP_Text[] zoneTitles = new TMP_Text[0]; // The names of each zone.
    private TMP_Text[] zoneDescriptions = new TMP_Text[0]; // The player counts or list of player names in each zone.
    private short[] zoneIDs = new short[0]; // The list of zone IDs retrieved from the list of names.

    internal void InitializeZonePanels()
    {
        // Clean up the list of zone names.
        if (listMode == ZoneMonitorListMode.SingleZone) { zoneTriggers = new ZoneTrigger[1]; zoneTriggers[0] = zoneTrigger; }
        else if (listMode == ZoneMonitorListMode.AllZones) { zoneTriggers = zoneManager.GetAllZones(); }
        else if (zoneTriggers == null) { zoneTriggers = new ZoneTrigger[0]; }
        // Create a list of IDs based on the zone names, validating as needed.
        short[] tempZoneIDs = new short[zoneTriggers.Length];
        int true_zone_count = 0;
        for (int i = 0; i < zoneTriggers.Length; i++)
        {
            short zone_id = zoneTriggers[i].zone_id;
            if (zone_id != ZoneManager.NO_ZONE)
            {
                tempZoneIDs[true_zone_count++] = zone_id;
            }
        }
        zoneIDs = new short[true_zone_count];
        zonePanels = new RectTransform[true_zone_count];
        zoneTitles = new TMP_Text[true_zone_count];
        zoneDescriptions = new TMP_Text[true_zone_count];
        for (int i = 0; i < true_zone_count; i++)
        {
            zoneIDs[i] = tempZoneIDs[i];
            zonePanels[i] = Instantiate(templatePanel.gameObject, templatePanel.parent).GetComponent<RectTransform>();
            Transform title_transform = GetChildTransformByName(zonePanels[i], "ZoneMonitorTitle", false, true);
            if (title_transform != null) { zoneTitles[i] = title_transform.GetComponent<TMP_Text>(); }
            Transform desc_transform = GetChildTransformByName(zonePanels[i], "ZoneMonitorDescription", false, true);
            if (desc_transform != null) { zoneDescriptions[i] = desc_transform.GetComponent<TMP_Text>(); }
            RefreshZonePanelAtIndex(i);
        }
        // Disable the template once it's all been setup.
        templatePanel.gameObject.SetActive(false);
    }

    // Refresh a zone panel for a given zone ID.
    internal void RefreshZonePanelAtID(short zone_id)
    {
        if (zoneIDs == null || zone_id < 0) { return; }
        for (int i = 0; i < zoneIDs.Length; i++)
        {
            if (zone_id == zoneIDs[i]) { RefreshZonePanelAtIndex(i); return; }
        }
    }

    // Refresh a zone panel at a given index.
    private void RefreshZonePanelAtIndex(int index)
    {
        if (zoneIDs == null || index < 0 || index >= zoneIDs.Length
            || zoneTitles == null || index < 0 || index >= zoneTitles.Length
            || zoneDescriptions == null || index < 0 || index >= zoneDescriptions.Length
            ) { return; }
        zoneTitles[index].text = zoneManager.GetZoneNameFromID(zoneIDs[index]);
        DataList player_list = zoneManager.GetPlayersInZone(zoneIDs[index]);
        if (player_list == null) { zoneDescriptions[index].text = outputType == ZoneMonitorOutputType.PlayerList ? "" : "0"; return; }
        int player_count = player_list.Count;
        if (player_count <= 0) { zoneDescriptions[index].text = outputType == ZoneMonitorOutputType.PlayerList ? "" : "0"; return; }
        string player_names = player_count.ToString();
        if (outputType == ZoneMonitorOutputType.PlayerList || outputType == ZoneMonitorOutputType.PlayerCountAndList)
        {
            for (int i = 0; i < player_count; i++)
            {
                if (player_list[i].Error != DataError.None) { continue; }
                VRCPlayerApi player = VRCPlayerApi.GetPlayerById(player_list[i].Int);
                if (player == null) { continue; }
                player_names += $"\n{player.displayName}";
            }
            if (outputType == ZoneMonitorOutputType.PlayerList) { player_names = player_names.Replace($"{player_count.ToString()}\n", ""); }
        }
        zoneDescriptions[index].text = player_names;
    }

    // Return a child transform of a parent based on name.
    internal static Transform GetChildTransformByName(Transform parent_tranform, string name, bool is_substring = false, bool is_recursive = false)
    {
        Transform transform_out = null;
        foreach (Transform child in parent_tranform)
        {
            if ((!is_substring && string.Equals(child.name.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase))
                || (is_substring && child.name.ToLower().Trim().Contains(name.ToLower().Trim())))
            {
                transform_out = child;
                break;
            }
            else if (is_recursive && child.childCount > 0)
            {
                Transform result = GetChildTransformByName(child, name, is_substring, is_recursive); ;
                if (result != null) { return result; }
            }
        }
        return transform_out;
    }

}

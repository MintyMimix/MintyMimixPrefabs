
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDK3.Persistence;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PlayerTagManager : UdonSharpBehaviour
{
    /// <summary>
    /// While there is a built-in Player Tag system in Udon, it's primitive and unfinished.
    /// The goal of this script is to provide a way for world creators to dynamically add sets of tags that can be applied to players.
    /// These tags can be placed in mutually exclusive groups, such as if you want to say, "Likes Ice Cream / Neutral Ice Cream / Dislikes Ice Cream" as one tag with three options.
    /// The world creator can also optionally have the tag display over the head of the player, if they pick an option.
    /// Finally, any player can locally choose whether or not to show all tags in the world in their game.
    /// 
    /// The way we accomplish this is through several objects:
    /// - PlayerTagManager, a local, non-synchronized object to handle UI events and local preferences.
    /// - PlayerTagData, a player-owned, manually synchronized object that handles what tags a player currently has in which state(s).
    /// - PlayerTagGroup, which defines any mutually exclusive groups of tags the world creators wants to have available.
    /// - PlayerTagButton, which locally sends an event to the manager to toggle a single tag/iterate through the options of a group tag.
    /// To prevent network thrashing, we have each player handle their own tags via PlayerTagSync.
    /// When then locally build player tag arrays based on what they see when a player joins/leaves/sends a serialization event in PlayerTagManager.
    /// </summary>

    // References
    private const float PERSISTENCE_SYNC_DELAY = 5.0f;
    private const string PERSISTENCE_TAGS_KEY = "PTM_Tags";
    private const string PERSISTENCE_PREFS_KEY = "PTM_Pref";
    [SerializeField] internal PlayerTagGroup[] tagGroups;
    [SerializeField] internal PlayerTagCluster[] tagClusters;
    [SerializeField] internal Sprite tagFallbackImage;
    [SerializeField] internal GameObject[] prefIndicatorDisplayTagsGlobal;
    [SerializeField] internal GameObject[] prefIndicatorDisplayTagsPlayer;
    [SerializeField] internal GameObject[] prefIndicatorDisplayOwnTags;

    // Runtime
    [NonSerialized] public bool prefDisplayTagsGlobal = true; // Does the local player want to display all player tags?
    [NonSerialized] public bool prefDisplayOwnTags = true; // Does the local player want to display their own tags over their head?
    internal VRCPlayerApi localPlayer; // Who is the local player?
    internal PlayerTagData localTagData; // What is the PlayerTagData of the local player?
    internal VRCPlayerApi.TrackingData localHeadData; // What is the head position/rotation of the local player?
    internal DataList allTagData = new DataList(); // The PlayerTagData for all players.
    
    // If the output of a tag changes for any player, we should send callback events to listeners to refresh their data.
    private DataDictionary tagListenersGroup = new DataDictionary();

    // Persistence - Restore = OnPlayerRestored()
    private DataDictionary syncDictPreferences = new DataDictionary();
    private DataDictionary syncDictTags = new DataDictionary();
    private DataDictionary syncDictTagIndices = new DataDictionary();
    private int persistenceRequests = 0;
    private bool waitingForRestore = true;
    private bool waitingForStart = true;

    private void Start()
    {
        localPlayer = Networking.LocalPlayer;
        localHeadData = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);

        for (byte i = 0; i < tagGroups.Length; i++)
        {
            if (tagGroups[i] == null) { continue; }
            if (!tagGroups[i].tagPersist) { continue; }
            syncDictTags.Add($"{tagGroups[i].tagGroupName}", (byte)0);
            syncDictTagIndices.Add($"{tagGroups[i].tagGroupName}", i);
        }

        syncDictPreferences.Add("DisplayTagsGlobal", true);
        syncDictPreferences.Add("DisplayOwnTags", true);
        syncDictPreferences.Add("DisplayTagsPlayer", true);

        for (int i = 0; i < prefIndicatorDisplayTagsGlobal.Length; i++)
        {
            if (prefIndicatorDisplayTagsGlobal[i] == null) { continue; }
            prefIndicatorDisplayTagsGlobal[i].gameObject.SetActive(false);
        }
        for (int i = 0; i < prefIndicatorDisplayOwnTags.Length; i++)
        {
            if (prefIndicatorDisplayOwnTags[i] == null) { continue; }
            prefIndicatorDisplayOwnTags[i].SetActive(false);
        }
        for (int i = 0; i < prefIndicatorDisplayTagsPlayer.Length; i++)
        {
            if (prefIndicatorDisplayTagsPlayer[i] == null) { continue; }
            prefIndicatorDisplayTagsPlayer[i].SetActive(false);
        }

        waitingForStart = false;
    }

    public void RestoreLocalPlayerData()
    {
        OnPlayerRestored(Networking.LocalPlayer);
    }

    public override void OnPlayerRestored(VRCPlayerApi player)
    {
        base.OnPlayerRestored(player);
        if (!player.isLocal) { return; }
        // Failsafe in case this runs before Start()
        if (waitingForStart) { SendCustomEventDelayedSeconds("RestoreLocalPlayerData", 0.3f); return; }

        // Failsafe in case this runs before PlayerTagData.OnPlayerJoined() for local player
        if (localTagData == null) 
        {
            GameObject localTagObj = FindPlayerOwnedObject(player, nameof(PlayerTagData));
            if (localTagObj != null) { localTagData = localTagObj.GetComponent<PlayerTagData>(); }
        }
        if (localTagData == null) { return; }

        DataDictionary loadedTagDict = GetDictFromPersist(player, PERSISTENCE_TAGS_KEY);
        DataDictionary loadedPrefDict = GetDictFromPersist(player, PERSISTENCE_PREFS_KEY);

        // Load the tag states for synchronized tags
        if (loadedTagDict != null)
        {
            DataList tagKeys = loadedTagDict.GetKeys();
            int tagKeyCount = tagKeys.Count;
            for (int i = 0; i < tagKeyCount ; i++) 
            {
                if (tagKeys[i].Error != DataError.None) { continue; }
                string tagKey = tagKeys[i].String;
                if (syncDictTags[tagKey].Error != DataError.None) { continue; }
                double stateAsDouble = loadedTagDict[tagKey].Double;
                byte stateAsByte = (byte)Mathf.Clamp(Mathf.RoundToInt((float)stateAsDouble), 0 , 255);
                syncDictTags[tagKey] = stateAsByte;
                if (syncDictTagIndices[tagKey].Error != DataError.None) { continue; }
                SetTagGroup(syncDictTagIndices[tagKey].Byte, stateAsByte);
            }
        }

        // Load the player preferences
        if (loadedPrefDict != null)
        {
            if (loadedPrefDict["DisplayTagsGlobal"].Error == DataError.None) 
            {
                if (loadedPrefDict["DisplayTagsGlobal"].Boolean != prefDisplayTagsGlobal) { ToggleDisplayPanelsGlobal(); }
                if (syncDictPreferences.ContainsKey("DisplayTagsGlobal")) { syncDictPreferences["DisplayTagsGlobal"] = loadedPrefDict["DisplayTagsGlobal"].Boolean; }
                else { syncDictPreferences.Add("DisplayTagsGlobal", loadedPrefDict["DisplayTagsGlobal"].Boolean); }
            }
            if (loadedPrefDict["DisplayOwnTags"].Error == DataError.None) 
            {
                if (loadedPrefDict["DisplayOwnTags"].Boolean != prefDisplayOwnTags) { ToggleDisplayPanelsLocal(); }
                if (syncDictPreferences.ContainsKey("DisplayOwnTags")) { syncDictPreferences["DisplayOwnTags"] = loadedPrefDict["DisplayOwnTags"].Boolean; }
                else { syncDictPreferences.Add("DisplayOwnTags", loadedPrefDict["DisplayOwnTags"].Boolean); }
            }
            if (loadedPrefDict["DisplayTagsPlayer"].Error == DataError.None) 
            {
                if (loadedPrefDict["DisplayTagsPlayer"].Boolean != localTagData.prefDisplayTagsPlayer) { ToggleDisplayPanelsPersonal(); }
                if (syncDictPreferences.ContainsKey("DisplayTagsPlayer")) { syncDictPreferences["DisplayTagsPlayer"] = loadedPrefDict["DisplayTagsPlayer"].Boolean; }
                else { syncDictPreferences.Add("DisplayTagsPlayer", loadedPrefDict["DisplayTagsPlayer"].Boolean); }
            }
        }

        waitingForRestore = false;
    }

    internal static GameObject FindPlayerOwnedObject(VRCPlayerApi player, string objName)
    {
        var objects = Networking.GetPlayerObjects(player);
        for (int i = 0; i < objects.Length; i++)
        {
            if (!Utilities.IsValid(objects[i])) continue;
            if (!objects[i].name.Contains(objName)) continue;
            if (Utilities.IsValid(objects[i]))
            {
                return objects[i];
            }
        }
        return null;
    }

    public void ToggleDisplayPanelsGlobal()
    {
        prefDisplayTagsGlobal = !prefDisplayTagsGlobal;
        int tagDataSize = allTagData.Count;
        for (int i = 0; i < tagDataSize; i++)
        {
            if (allTagData[i].Error != DataError.None) { allTagData.RemoveAt(i); i--; tagDataSize--; continue; }
            PlayerTagData tagData = (PlayerTagData)allTagData[i].Reference;
            if (tagData.owner != null) { tagData.RefreshPanels(true); }
        }
        for (int i = 0; i < prefIndicatorDisplayTagsGlobal.Length; i++)
        {
            if (prefIndicatorDisplayTagsGlobal[i] == null) { continue; }
            prefIndicatorDisplayTagsGlobal[i].SetActive(!prefDisplayTagsGlobal);
        }
        if (syncDictPreferences.ContainsKey("DisplayTagsGlobal")) { syncDictPreferences["DisplayTagsGlobal"] = prefDisplayTagsGlobal; }
        else { syncDictPreferences.Add("DisplayTagsGlobal", prefDisplayTagsGlobal); }
        QueueDataSyncRequest();
    }

    public void ToggleDisplayPanelsLocal()
    {
        if (localTagData == null) { return; }
        prefDisplayOwnTags = !prefDisplayOwnTags;
        localTagData.RefreshPanels(true);
        for (int i = 0; i < prefIndicatorDisplayOwnTags.Length; i++)
        {
            if (prefIndicatorDisplayOwnTags[i] == null) { continue; }
            prefIndicatorDisplayOwnTags[i].SetActive(!prefDisplayOwnTags);
        }
        if (syncDictPreferences.ContainsKey("DisplayOwnTags")) { syncDictPreferences["DisplayOwnTags"] = prefDisplayOwnTags; }
        else { syncDictPreferences.Add("DisplayOwnTags", prefDisplayOwnTags); }
        QueueDataSyncRequest();
    }

    public void ToggleDisplayPanelsPersonal()
    {
        if (localTagData == null) { return; }
        localTagData.prefDisplayTagsPlayer = !localTagData.prefDisplayTagsPlayer;
        localTagData.QueueSerialization();
        for (int i = 0; i < prefIndicatorDisplayTagsPlayer.Length; i++)
        {
            if (prefIndicatorDisplayTagsPlayer[i] == null) { continue; }
            prefIndicatorDisplayTagsPlayer[i].SetActive(!localTagData.prefDisplayTagsPlayer);
        }
        if (syncDictPreferences.ContainsKey("DisplayTagsPlayer")) { syncDictPreferences["DisplayTagsPlayer"] = localTagData.prefDisplayTagsPlayer; }
        else { syncDictPreferences.Add("DisplayTagsPlayer", localTagData.prefDisplayTagsPlayer); }
        QueueDataSyncRequest();
    }

    // Updates every frame. Use sparingly for billboarding.
    // To-do: remove this method and replace with VRCBillboard when that feature is released.
    public override void PostLateUpdate()
    {
        base.PostLateUpdate();
        if (!Utilities.IsValid(localPlayer)) { return; }
        localHeadData = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
    }

    internal byte GetNextStateInTagGroup(byte indexDesired)
    {
        if (indexDesired >= tagGroups.Length || localTagData == null) { return 0; }
        byte valueDesired = localTagData.GetTagState(indexDesired);
        if ((valueDesired + 1) >= tagGroups[indexDesired].size() || (valueDesired + 1) > byte.MaxValue) { valueDesired = 0; }
        else { valueDesired = (byte)(valueDesired + 1); }
        return valueDesired;
    }

    internal void SetTagGroup(byte indexDesired, PlayerTagButton callbackButton = null)
    {
        if (indexDesired >= tagGroups.Length || localTagData == null) { return; }
        SetTagGroup(indexDesired, GetNextStateInTagGroup(indexDesired), callbackButton);
    }

    internal void SetTagGroup(byte indexDesired, byte valueDesired, PlayerTagButton callbackButton = null)
    {
        if (indexDesired >= tagGroups.Length || localTagData == null) { return; }
        if (valueDesired >= tagGroups[indexDesired].size() || valueDesired > byte.MaxValue) { valueDesired = 0; }
        localTagData.SetTagState(indexDesired, valueDesired);

        if (callbackButton != null) { callbackButton.UpdateValueCallback(valueDesired); }
    }

    internal void SetTagGroup(PlayerTagGroup tagGroup, PlayerTagButton callbackButton = null)
    {
        int tagGroupIndex = GetTagGroupIndex(tagGroup);
        if (tagGroupIndex < 0 || tagGroupIndex > byte.MaxValue) { return; }
        SetTagGroup((byte)tagGroupIndex, callbackButton);
    }

    internal void SetTagGroup(PlayerTagGroup tagGroup, byte valueDesired, PlayerTagButton callbackButton = null)
    {
        int tagGroupIndex = GetTagGroupIndex(tagGroup);
        if (tagGroupIndex < 0 || tagGroupIndex > byte.MaxValue) { return; }
        SetTagGroup((byte)tagGroupIndex, valueDesired, callbackButton);
    }

    internal int GetTagGroupIndex(PlayerTagGroup tagGroup)
    {
        for (int i = 0; i < tagGroups.Length; i++)
        {
            if (tagGroup == tagGroups[i])
            {
                return i;
            }
        }
        return -1;
    }

    // Store and retrieve player-owned objects, in this case, PlayerTagData.
    // We want to have two methods: a safe one that is much more efficient since it only searches a list of PlayerTagData,
    // And a manual one for when we need to search all player-owned objects in general, which can be expensive, depending on the world.
    // The manual one should only be called upon player join or as a failsafe for error-catching.

    public override void OnPlayerLeft(VRCPlayerApi player)
    {
        base.OnPlayerLeft(player);
        allTagData.Remove(GetPlayerTagDataSafe(player.playerId));
        CallbackListenersAll(); 
    }

    public PlayerTagData GetPlayerTagDataSafe(int playerId)
    {
        int tagDataSize = allTagData.Count;
        if (tagDataSize <= 0) { return GetPlayerTagDataManual(playerId); }
        for (int i = 0; i < tagDataSize; i++)
        {
            if (allTagData[i].Error != DataError.None) { allTagData.RemoveAt(i); i--; tagDataSize--; continue; }
            PlayerTagData tagData = (PlayerTagData)allTagData[i].Reference;
            if (Utilities.IsValid(tagData.owner) && tagData.owner.playerId == playerId) { return tagData; }
        }
        return GetPlayerTagDataManual(playerId);
    }

    public PlayerTagData GetPlayerTagDataSafe(VRCPlayerApi player)
    {
        if (!Utilities.IsValid(player)) { return null; }
        return GetPlayerTagDataSafe(player.playerId);
    }

    public PlayerTagData GetPlayerTagDataManual(VRCPlayerApi player)
    {
        if (!Utilities.IsValid(player)) { return null; }
        GameObject[] objects = Networking.GetPlayerObjects(player);
        for (int i = 0; i < objects.Length; i++)
        {
            if (!Utilities.IsValid(objects[i])) continue;
            PlayerTagData foundScript = objects[i].GetComponentInChildren<PlayerTagData>();
            if (Utilities.IsValid(foundScript)) return foundScript;
        }
        return null;
    }

    public PlayerTagData GetPlayerTagDataManual(int playerId)
    {
        return GetPlayerTagDataManual(VRCPlayerApi.GetPlayerById(playerId));
    }


    // Sometimes we want to have screens and other objects read in a list of names that the tags apply to.
    // This is where the PlayerTagListeners come in. Every time a player's tag is updated, the listeners receive a callback.
    internal void RegisterListenerGroup(PlayerTagListener listener, byte tagGrouptoMonitor)
    {
        if (!Utilities.IsValid(listener)) { return; }
        int index = tagGrouptoMonitor;
        if (!tagListenersGroup.ContainsKey(index)) { tagListenersGroup.Add(index, new DataList()); }
        tagListenersGroup[index].DataList.Add(listener);
        listener.SetGroupIndex(index);
    }

    internal void CallbackListeners(int tagIndex)
    {
        DataDictionary listenerDict = tagListenersGroup;
        if (!Utilities.IsValid(listenerDict) || !listenerDict.ContainsKey(tagIndex)) { return; }
        DataList listenerList = listenerDict[tagIndex].DataList;
        if (!Utilities.IsValid(listenerList)) { return; }
        int listenerCnt = listenerList.Count;
        for (int i = 0; i < listenerCnt; i++)
        {
            if (listenerList[i].Error != DataError.None) { listenerList.RemoveAt(i); i--; listenerCnt--; continue; }
            ((PlayerTagListener)listenerList[i].Reference).UpdateOutput();
        } 
    }

    internal void CallbackListenersAll()
    {
        DataDictionary listenerDict = tagListenersGroup;
        if (!Utilities.IsValid(listenerDict)) { return; }
        DataList keys = listenerDict.GetKeys();
        int keyCount = keys.Count;
        for (int i = 0; i < keyCount; i++) 
        {
            if (keys[i].Error != DataError.None) { keys.RemoveAt(i); i--; keyCount--; continue; }
            int key = keys[i].Int;
            CallbackListeners(key);
        }
    }

    internal void TrySyncTag(byte tagGroupIndex, byte tagState)
    {
        if (tagGroupIndex < 0 || tagGroupIndex >= tagGroups.Length || tagGroups[tagGroupIndex] == null) { return; }
        PlayerTagGroup tagGroup = tagGroups[tagGroupIndex];
        if (!tagGroup.tagPersist) { return; }
        if (!syncDictTags.ContainsKey(tagGroup.tagGroupName))
        {
            syncDictTags.Add(tagGroup.tagGroupName, tagState);
            syncDictTagIndices.Add(tagGroup.tagGroupName, tagGroupIndex);
        }
        else { syncDictTags[tagGroup.tagGroupName] = tagState; }
        QueueDataSyncRequest();
    }

    public void QueueDataSyncRequest()
    {
        if (waitingForRestore) { return; }
        if (persistenceRequests < 0) { persistenceRequests = 0; }
        persistenceRequests++;
        SendCustomEventDelayedSeconds(nameof(TryPostQueueDataSyncRequest), PERSISTENCE_SYNC_DELAY);
    }

    public void TryPostQueueDataSyncRequest()
    {
        persistenceRequests--;
        if (persistenceRequests <= 0) 
        {
            Debug.Log($"[PlayerTagManager] Persistence result: {SendDataSyncRequest(syncDictTags, PERSISTENCE_TAGS_KEY)}");
            Debug.Log($"[PlayerTagManager] Persistence result: {SendDataSyncRequest(syncDictPreferences, PERSISTENCE_PREFS_KEY)}");
        }
    }

    // Send a request to VRChat's servers to synchronize our preferences.
    private string SendDataSyncRequest(DataDictionary dictionary, string json_key)
    {
        if (VRCJson.TrySerializeToJson(dictionary, JsonExportType.Minify, out DataToken result))
        {
            if (result.Error != DataError.None) { return $"Data dictionary {json_key} returned as Error type"; } // Failed to retrieve a result
            else if (result.TokenType != TokenType.String) { return $"Data dictionary {json_key} stored as a non-string, somehow"; } // Stored a non-string, somehow.
            PlayerData.SetString(json_key, result.String);
            return $"Successfully synchronized {json_key}"; // Success!
        }
        return $"Failed to serialize JSON for {json_key}"; // Failed to serialize JSON.
    }

    private DataDictionary GetDictFromPersist(VRCPlayerApi player, string persistenceDictName)
    {
        if (PlayerData.HasKey(player, persistenceDictName))
        {
            if (PlayerData.TryGetString(player, persistenceDictName, out string prefs_str))
            {
                if (VRCJson.TryDeserializeFromJson(prefs_str, out DataToken result))
                {
                    // Deserialization succeeded! Let's figure out what we've got.
                    if (result.TokenType == TokenType.DataDictionary)
                    {
                        Debug.Log($"[PlayerTagManager] Successfully deserialized {persistenceDictName} as a dictionary with {result.DataDictionary.Count} items.");
                        return result.DataDictionary;
                    }
                    else if (result.TokenType == TokenType.DataList)
                    {
                        Debug.Log($"[PlayerTagManager] Successfully deserialized {persistenceDictName} as a list with {result.DataList.Count} items.");
                        DataDictionary listAsDict = new DataDictionary();
                        listAsDict.Add("out", result.DataList);
                        return listAsDict;
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    // Deserialization failed. Let's see what the error was.
                    Debug.LogError($"[PlayerTagManager] {gameObject.name}: Failed to Deserialize json {prefs_str} - {result.ToString()}");
                    return null;
                }
            }
        }
        Debug.Log($"[PlayerTagManager] Failed to find persistence data for {persistenceDictName}");
        return null;
    }

}

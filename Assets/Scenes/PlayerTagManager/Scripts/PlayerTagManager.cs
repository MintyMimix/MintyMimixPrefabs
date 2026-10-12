
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
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
    [SerializeField] internal PlayerTagGroup[] tagGroups;
    [SerializeField] internal string[] tagSingleNames;
    [SerializeField] internal Color[] tagSingleColors;
    [SerializeField] internal bool[] tagSingleDisplayAboveHead;

    // Runtime
    [NonSerialized] public bool prefDisplayTagsGlobal = true; // Does the local player want to display all player tags?
    [NonSerialized] public bool prefDisplayOwnTags = true; // Does the local player want to display their own tags over their head?
    internal VRCPlayerApi localPlayer; // Who is the local player?
    internal PlayerTagData localTagData; // What is the PlayerTagData of the local player?
    internal VRCPlayerApi.TrackingData localHeadData; // What is the head position/rotation of the local player?
    internal DataList allTagData = new DataList(); // The PlayerTagData for all players.
    
    // If the output of a tag changes for any player, we should send callback events to listeners to refresh their data.
    private DataDictionary tagListenersSingle = new DataDictionary();
    private DataDictionary tagListenersGroup = new DataDictionary();

    private void Start()
    {
        localPlayer = Networking.LocalPlayer;
        localHeadData = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
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
    }

    public void ToggleDisplayPanelsLocal()
    {
        if (localTagData == null) { return; }
        prefDisplayOwnTags = !prefDisplayOwnTags;
        localTagData.RefreshPanels(true);
    }

    public void ToggleDisplayPanelsPersonal()
    {
        if (localTagData == null) { return; }
        localTagData.prefDisplayTagsPlayer = !localTagData.prefDisplayTagsPlayer;
        localTagData.QueueSerialization();
    }

    // Updates every frame. Use sparingly for billboarding.
    // To-do: remove this method and replace with VRCBillboard when that feature is released.
    public override void PostLateUpdate()
    {
        base.PostLateUpdate();
        if (!Utilities.IsValid(localPlayer)) { return; }
        localHeadData = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
    }

    internal void SetTagSingle(byte indexDesired, PlayerTagButton callbackButton = null)
    {
        if (indexDesired >= tagSingleNames.Length || localTagData == null) { return; }
        bool valueDesired = !localTagData.GetTagStateSingle(indexDesired);
        byte newValue = (byte)(valueDesired ? 1 : 0);
        localTagData.SetTagState(true, indexDesired, newValue);

        if (callbackButton != null) { callbackButton.UpdateValueCallback(true, newValue); }
    }

    internal byte GetNextStateInTagGroup(byte indexDesired)
    {
        if (indexDesired >= tagGroups.Length || localTagData == null) { return 0; }
        byte valueDesired = localTagData.GetTagStateGroup(indexDesired);
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
        localTagData.SetTagState(false, indexDesired, valueDesired);

        if (callbackButton != null) { callbackButton.UpdateValueCallback(false, valueDesired); }
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
        CallbackListenersAll(true); 
        CallbackListenersAll(false);
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
    internal void RegisterListenerSingle(PlayerTagListener listener, byte tagSingleToMonitor)
    {
        if (!Utilities.IsValid(listener)) { return; }
        int index = tagSingleToMonitor;
        if (!tagListenersSingle.ContainsKey(index)) { tagListenersSingle.Add(index, new DataList()); }
        tagListenersSingle[index].DataList.Add(listener);
    }

    internal void RegisterListenerGroup(PlayerTagListener listener, byte tagGrouptoMonitor)
    {
        if (!Utilities.IsValid(listener)) { return; }
        int index = tagGrouptoMonitor;
        if (!tagListenersGroup.ContainsKey(index)) { tagListenersGroup.Add(index, new DataList()); }
        tagListenersGroup[index].DataList.Add(listener);
        listener.SetGroupIndex(index);
    }

    internal void CallbackListeners(bool isSingle, int tagIndex)
    {
        DataDictionary listenerDict = isSingle ? tagListenersSingle : tagListenersGroup;
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

    internal void CallbackListenersAll(bool isSingle)
    {
        DataDictionary listenerDict = isSingle ? tagListenersSingle : tagListenersGroup;
        if (!Utilities.IsValid(listenerDict)) { return; }
        DataList keys = listenerDict.GetKeys();
        int keyCount = keys.Count;
        for (int i = 0; i < keyCount; i++) 
        {
            if (keys[i].Error != DataError.None) { keys.RemoveAt(i); i--; keyCount--; continue; }
            int key = keys[i].Int;
            CallbackListeners(isSingle, key);
        }
    }

}

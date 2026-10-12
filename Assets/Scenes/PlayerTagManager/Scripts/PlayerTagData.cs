
using UdonSharp;
using UnityEngine;
using VRC.SDK3.UdonNetworkCalling;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class PlayerTagData : UdonSharpBehaviour
{
    /// <summary>
    /// A player-owned, manually synchronized object that handles what tags a player currently has in which state(s).
    /// To prevent network thrashing, we have each player handle their own tags and then serialize accordingly.
    /// </summary>

    // Constants
    private float NETWORK_SYNC_DELAY = 0.7f;

    // References
    [SerializeField] private PlayerTagManager playerTagManager; // The global tag manager.
    [SerializeField] private RectTransform tagCanvas; // The canvas that gets displayed for tags.
    [SerializeField] private RectTransform tagDisplayPanelTemplate; // Gets instantatied based on the number of single & group tags. Used for displaying tag elements.
    [SerializeField] private float tagCanvasHeightOffset; // How high above the player's head should this canvas be?

    // Runtime
    private bool processingReady = false; // Is our data fully initialized?
    internal VRCPlayerApi owner; // Who is the owner of this PlayerTagData? (One spawns per player.)
    private VRCPlayerApi.TrackingData headData; // What is the head position/rotation of this player?
    // Serialization events act on a delayed timer and will not fire until after a certain period of time to prevent network spam.
    private int queuedNetworkRequests = 0;
    // Instantiated tagPanelTemplate and child elements.
    private RectTransform[] tagDisplayPanels;
    private UnityEngine.UI.Image[] tagDisplayImg;
    private TMPro.TMP_Text[] tagDisplayTxt;

    [UdonSynced] internal bool prefDisplayTagsPlayer = true; // Does the owner want to display their own player tags?
    private bool localDisplayTags = false; // Non-networked version of the above.

    // Since we can't sync a dict, we'll store an array for every value possible tag state.
    // byte is used since we will never have a negative value, and it's unlikely a world creator will ever create more than 255 tags / states.
    // While it's theoretically possible, I would question their implementation.
    [UdonSynced] private bool[] tagSingleStates = new bool[0];
    [UdonSynced] private byte[] tagGroupStates = new byte[0];
    private bool[] localSingleStates = new bool[0];
    private byte[] localGroupStates = new byte[0];
    private int totalTagsActive = 0;

    private void Start()
    {
        owner = Networking.GetOwner(gameObject);
        tagSingleStates = new bool[playerTagManager.tagSingleNames.Length];
        tagGroupStates = new byte[playerTagManager.tagGroups.Length];
        localSingleStates = new bool[tagSingleStates.Length];
        localGroupStates = new byte[tagGroupStates.Length];
        // Initialize display panels.
        int totalTags = playerTagManager.tagSingleNames.Length + playerTagManager.tagGroups.Length;
        tagDisplayPanels = new RectTransform[totalTags];
        tagDisplayImg = new UnityEngine.UI.Image[totalTags];
        tagDisplayTxt = new TMPro.TMP_Text[totalTags];
        for (int i = 0; i < totalTags; i++)
        {
            // Don't create panels for tags that will never display anything.
            if (i < playerTagManager.tagSingleNames.Length && !playerTagManager.tagSingleDisplayAboveHead[i]) { continue; }
            else if (i >= playerTagManager.tagSingleNames.Length)
            {
                int groupIndex = i - playerTagManager.tagSingleNames.Length;
                bool shouldDisplay = false;
                for (int j = 0; j < playerTagManager.tagGroups[groupIndex].size(); j++)
                {
                    if (playerTagManager.tagGroups[groupIndex].GetDisplayAboveHead(j)) { shouldDisplay = true; break; }
                }
                if (!shouldDisplay) { continue; }
            }
            tagDisplayPanels[i] = Instantiate(tagDisplayPanelTemplate.gameObject, tagDisplayPanelTemplate.parent).GetComponent<RectTransform>();
            tagDisplayImg[i] = tagDisplayPanels[i].GetComponent<UnityEngine.UI.Image>();
            tagDisplayTxt[i] = tagDisplayPanels[i].GetComponentInChildren<TMPro.TMP_Text>();
            tagDisplayPanels[i].gameObject.SetActive(false);
        }
        tagDisplayPanelTemplate.gameObject.SetActive(false);

        processingReady = true;

        // Register this to the manager. Assign to localTagData if we are the owner.
        playerTagManager.allTagData.Add(this);
        if (owner.isLocal) { playerTagManager.localTagData = this; }
        else { SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, nameof(RequestForceSerialization)); }
        
    }

    internal void SetTagState(bool isSingle, byte tagIndex, byte tagState)
    {
        // Owner-only.
        if (owner == null || !owner.isLocal) { return; }
        if (isSingle)
        {
            if (tagState > 1) { return; } // False (0) / True (1) states only
            if (tagIndex >= tagSingleStates.Length) { return; }
            tagSingleStates[tagIndex] = tagState == 1;
        }
        else
        {
            if (tagIndex >= tagGroupStates.Length || tagState >= playerTagManager.tagGroups[tagIndex].size()) { return; }
            tagGroupStates[tagIndex] = tagState;
        }
        QueueSerialization();
    }

    internal bool GetTagStateSingle(byte tagIndex)
    {
        if (tagIndex >= tagSingleStates.Length) { return false; }
        return tagSingleStates[tagIndex];
    }

    internal byte GetTagStateGroup(byte tagIndex)
    {
        if (tagIndex >= tagGroupStates.Length) { return 0; }
        return tagGroupStates[tagIndex];
    }

	// Toggling a tag does not immediately send a network request. We place a short delay to prevent network spam.
	public void QueueSerialization()
    {
        // Owner-only.
        if (owner == null || !owner.isLocal) { return; }
        if (queuedNetworkRequests < 0) { queuedNetworkRequests = 0; }
        queuedNetworkRequests++;
        OnDeserialization();
        SendCustomEventDelayedSeconds(nameof(TryPostQueueSerialization), NETWORK_SYNC_DELAY);
    }

    public void TryPostQueueSerialization()
    {
        queuedNetworkRequests--;
        if (queuedNetworkRequests <= 0) { RequestSerialization(); }
    }

    public override void OnDeserialization()
    {
        base.OnDeserialization();
        if (!processingReady) { return; }
        bool refreshAll = false;
        if (localDisplayTags != prefDisplayTagsPlayer) 
        {
            refreshAll = true;
            localDisplayTags = prefDisplayTagsPlayer;
        }
        RefreshPanels(refreshAll);
    }

    // If we have a particularly slow joiner, we might want to request a refresh of data.
    [NetworkCallable]
    public void RequestForceSerialization()
    {
        // Owner-only.
        if (owner == null || !owner.isLocal) { return; }
        QueueSerialization();
    }

    internal void RefreshPanels(bool refreshAll)
    {
        // Detect changes to the values.
        for (int i = 0; i < tagSingleStates.Length; i++)
        {
            if (tagSingleStates[i] != localSingleStates[i] || refreshAll)
            {
                if (tagDisplayPanels[i] != null)
                {
                    bool isActive = ShouldDisplayTagSingle(i);
                    if (tagDisplayPanels[i].gameObject.activeSelf != isActive) { tagDisplayPanels[i].gameObject.SetActive(isActive); if (isActive) { totalTagsActive++; } else { totalTagsActive--; } }
                    tagDisplayImg[i].color = playerTagManager.tagSingleColors[i];
                    tagDisplayTxt[i].text = playerTagManager.tagSingleNames[i];
                }
                playerTagManager.CallbackListeners(true, i);
                localSingleStates[i] = tagSingleStates[i];
            }
        }
        for (int i = 0; i < tagGroupStates.Length; i++)
        {
            if (tagGroupStates[i] != localGroupStates[i] || refreshAll)
            {
                int displayIndex = tagSingleStates.Length + i;
                if (tagDisplayPanels[displayIndex] != null)
                {
                    bool isActive = ShouldDisplayTagGroup(i, tagGroupStates[i]);
                    if (tagDisplayPanels[displayIndex].gameObject.activeSelf != isActive) { tagDisplayPanels[displayIndex].gameObject.SetActive(isActive); if (isActive) { totalTagsActive++; } else { totalTagsActive--; } }
                    tagDisplayImg[displayIndex].color = playerTagManager.tagGroups[i].GetColor(tagGroupStates[i]);
                    tagDisplayTxt[displayIndex].text = playerTagManager.tagGroups[i].GetStateName(tagGroupStates[i]);
                }
                playerTagManager.CallbackListeners(false, i);
                localGroupStates[i] = tagGroupStates[i];
            }
        }
    }

    // Check if a tag should be displayed, based on both the local player's, this player's, and the tag's preferences.
    private bool ShouldDisplayTagSingle(int tagSingleIndex)
    {
        if (tagSingleIndex >= tagSingleStates.Length) { return false; }
        if (!playerTagManager.prefDisplayTagsGlobal || !prefDisplayTagsPlayer) { return false; }
        if (owner != null && owner.isLocal && !playerTagManager.prefDisplayOwnTags) { return false; }
        if (tagDisplayPanels == null) { return tagSingleStates[tagSingleIndex]; }
        else { return tagSingleStates[tagSingleIndex] && tagDisplayPanels[tagSingleIndex] != null; }
    }

    private bool ShouldDisplayTagGroup(int tagGroupIndex, byte tagGroupState)
    {
        if (tagGroupIndex >= tagGroupStates.Length) { return false; }
        if (tagGroupStates[tagGroupIndex] == 0 || !playerTagManager.prefDisplayTagsGlobal || !prefDisplayTagsPlayer)  { return false; }
		if (owner != null && owner.isLocal && !playerTagManager.prefDisplayOwnTags) { return false; }
		if (playerTagManager.tagGroups[tagGroupIndex].GetDisplayAboveHead(tagGroupState) && tagDisplayPanels[tagSingleStates.Length + tagGroupIndex] != null) { return true; }
        return false; 
    }

    // Updates every frame. Use sparingly for billboarding.
    // To-do: remove this method and replace with VRCBillboard when that feature is released.
    public override void PostLateUpdate()
    {
        base.PostLateUpdate();
        // Don't billboard if we aren't showing anything.
        if (owner == null || playerTagManager.localPlayer == null || !Utilities.IsValid(owner)) { return; }

        headData = owner.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        float calcHeightOffset = tagCanvasHeightOffset + ((totalTagsActive - 1) * (0.2f / 3f));
		tagCanvas.transform.SetPositionAndRotation(headData.position + (calcHeightOffset * Vector3.up), playerTagManager.localHeadData.rotation);
    }

    // Old implementation. Only use if network clogging seems to be a major issue.
    /*internal void SetTagState(bool isSingle, byte tagIndex, byte tagState)
   {
       // Owner-only.
       if (owner == null || !owner.isLocal) { return; }
       if (isSingle)
       {
           if (tagState > 1) { return; } // False (0) / True (1) states only
           int inArrayIndex = FindTag(tagIndex, tagSingleIndices);
           if (inArrayIndex != -1) { tagSingleIndices[inArrayIndex] = tagState; }
           else { AddTagSingle(tagIndex, tagState == 1, ref tagSingleIndices, ref tagSingleStates); }
       }
       else
       {
           int inArrayIndex = FindTag(tagIndex, tagGroupIndices);
           if (inArrayIndex != -1) { tagGroupIndices[inArrayIndex] = tagState; }
           else { AddTagGroup(tagIndex, tagState, ref tagGroupIndices, ref tagGroupStates); }
       }
       QueueSerialization();
   }

   private int FindTag(byte indexDesired, byte[] indexArr)
   {
       if (indexArr == null || indexArr.Length == 0) { return -1; }
       for (byte i = 0; i < indexArr.Length; i++) { if (indexArr[i] == indexDesired) return i; }
       return -1;
   }

   private void AddTagSingle(byte indexDesired, bool valueDesired, ref byte[] indexArr, ref bool[] valueArr)
   {
       if (indexArr == null || valueArr == null) { return; }
       byte[] newIndexArr = new byte[indexArr.Length + 1]; 
       bool[] newValueArr = new bool[valueArr.Length + 1];
       for (int i = 0; i < indexArr.Length; i++) { newIndexArr[i] = indexArr[i]; newValueArr[i] = valueArr[i]; }
       newIndexArr[indexArr.Length] = indexDesired;
       newValueArr[valueArr.Length] = valueDesired;
       indexArr = newIndexArr;
       valueArr = newValueArr;
   }

   private void AddTagGroup(byte indexDesired, byte valueDesired, ref byte[] indexArr, ref byte[] valueArr)
   {
       if (indexArr == null || valueArr == null) { return; }
       byte[] newIndexArr = new byte[indexArr.Length + 1];
       byte[] newValueArr = new byte[valueArr.Length + 1];
       for (int i = 0; i < indexArr.Length; i++) { newIndexArr[i] = indexArr[i]; newValueArr[i] = valueArr[i]; }
       newIndexArr[indexArr.Length] = indexDesired;
       newValueArr[valueArr.Length] = valueDesired;
       indexArr = newIndexArr;
       valueArr = newValueArr;
   }*/

}

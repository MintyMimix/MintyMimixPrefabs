
using UdonSharp;
using UnityEngine;
using UnityEngine.Analytics;
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
    [SerializeField] private RectTransform tagDisplayClusterTemplate; // Gets instantatied based on the number of single & group tags. Used for displaying tag elements.
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
    private RectTransform[] tagClusterPanels;

    [UdonSynced] internal bool prefDisplayTagsPlayer = true; // Does the owner want to display their own player tags?
    private bool localDisplayTags = false; // Non-networked version of the above.

    // Since we can't sync a dict, we'll store an array for every value possible tag state.
    // byte is used since we will never have a negative value, and it's unlikely a world creator will ever create more than 255 tags / states.
    // While it's theoretically possible, I would question their implementation.
    [UdonSynced] private byte[] tagGroupStates = new byte[0];
    private byte[] localGroupStates = new byte[0];
    private int totalTagsActive = 0;

    private void Start()
    {
        owner = Networking.GetOwner(gameObject);
        tagGroupStates = new byte[playerTagManager.tagGroups.Length];
        localGroupStates = new byte[tagGroupStates.Length];
        // Initialize display panels.
        int totalTags = playerTagManager.tagGroups.Length;
        tagDisplayPanels = new RectTransform[totalTags];
        tagDisplayImg = new UnityEngine.UI.Image[totalTags];
        tagDisplayTxt = new TMPro.TMP_Text[totalTags];
        for (int i = 0; i < totalTags; i++)
        {
            // Don't create panels for tags that will never display anything.
            bool shouldDisplay = false;
            for (int j = 0; j < playerTagManager.tagGroups[i].size(); j++)
            {
                if (playerTagManager.tagGroups[i].GetDisplayAboveHead(j)) { shouldDisplay = true; break; }
            }
            if (!shouldDisplay) { continue; }
            tagDisplayPanels[i] = Instantiate(tagDisplayPanelTemplate.gameObject, tagDisplayPanelTemplate.parent).GetComponent<RectTransform>();
            tagDisplayPanels[i].sizeDelta = new Vector2(tagDisplayPanels[i].sizeDelta.x, tagDisplayPanels[i].sizeDelta.y * playerTagManager.tagGroups[i].scaleInPanel);
            tagDisplayImg[i] = tagDisplayPanels[i].GetComponent<UnityEngine.UI.Image>();
            tagDisplayTxt[i] = tagDisplayPanels[i].GetComponentInChildren<TMPro.TMP_Text>();
            tagDisplayPanels[i].gameObject.SetActive(false);
        }

        int ClusterCount = playerTagManager.tagClusters.Length;
        tagClusterPanels = new RectTransform[ClusterCount];
        for (int i = 0; i < ClusterCount; i++)
        {
            if (playerTagManager.tagClusters == null || ClusterCount == 0) { continue; }
            tagClusterPanels[i] = Instantiate(tagDisplayClusterTemplate.gameObject, tagDisplayClusterTemplate.parent).GetComponent<RectTransform>();
            tagClusterPanels[i].sizeDelta = new Vector2(tagClusterPanels[i].sizeDelta.x, tagClusterPanels[i].sizeDelta.y * playerTagManager.tagClusters[i].scaleInPanel);
        }

        // Parent groups in their Clusters
        for (int i = 0; i < ClusterCount; i++)
        {
            PlayerTagCluster Cluster = playerTagManager.tagClusters[i];
            if (Cluster == null || tagClusterPanels[i] == null) { continue; }
            for (int j = 0; j < Cluster.tagsInCluster.Length; j++)
            {
                int groupIndex = -1;
                for (int k = 0; k < totalTags; k++)
                {
                    if (Cluster.tagsInCluster[j] == playerTagManager.tagGroups[k])
                    {
                        groupIndex = k; break;
                    }
                }
                if (groupIndex >= 0 && tagDisplayPanels[groupIndex] != null)
                {
                    tagDisplayPanels[groupIndex].transform.SetParent(tagClusterPanels[i].transform);
                }
            }
        }
        tagDisplayPanelTemplate.gameObject.SetActive(false);
        tagDisplayClusterTemplate.gameObject.SetActive(false);

        processingReady = true;

        // Register this to the manager. Assign to localTagData if we are the owner.
        playerTagManager.allTagData.Add(this);
        if (owner.isLocal) { playerTagManager.localTagData = this; }
        else { SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, nameof(RequestForceSerialization)); }
        
    }

    internal void SetTagState(byte tagIndex, byte tagState)
    {
        // Owner-only.
        if (owner == null || !owner.isLocal) { return; }
        if (tagIndex >= tagGroupStates.Length || tagState >= playerTagManager.tagGroups[tagIndex].size()) { return; }
        tagGroupStates[tagIndex] = tagState;
        QueueSerialization();

        if (playerTagManager.tagGroups[tagIndex].tagPersist) { playerTagManager.TrySyncTag(tagIndex, tagState); }
    }

    internal byte GetTagState(byte tagIndex)
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
        for (int i = 0; i < tagGroupStates.Length; i++)
        {
            if (tagGroupStates[i] != localGroupStates[i] || refreshAll)
            {
                if (tagDisplayPanels[i] != null)
                {
                    bool isActive = ShouldDisplayTag(i, tagGroupStates[i]);
                    if (tagDisplayPanels[i].gameObject.activeSelf != isActive) { tagDisplayPanels[i].gameObject.SetActive(isActive); if (isActive) { totalTagsActive++; } else { totalTagsActive--; } }
                    if (playerTagManager.tagGroups[i].tagUseImage)
                    {
                        Sprite groupImage = playerTagManager.tagGroups[i].GetImage(tagGroupStates[i]);
                        if (tagDisplayImg[i] != null)
                        {
                            tagDisplayImg[i].sprite = groupImage == null ? playerTagManager.tagFallbackImage : groupImage;
                            tagDisplayImg[i].preserveAspect = groupImage != null;
                            tagDisplayImg[i].color = Color.white;
                        }
                    }
                    else
                    {
                        if (tagDisplayImg[i] != null)
                        {
                            tagDisplayImg[i].color = playerTagManager.tagGroups[i].GetColor(tagGroupStates[i]);
                            tagDisplayImg[i].preserveAspect = false;
                        }
                    }
                    if (tagDisplayTxt[i] != null)
                    {
                        tagDisplayTxt[i].text = playerTagManager.tagGroups[i].tagDisplayText ? playerTagManager.tagGroups[i].GetStateName(tagGroupStates[i]) : "";
                    }
                }
                playerTagManager.CallbackListeners(i);
                localGroupStates[i] = tagGroupStates[i];
            }
        }
        for (int i = 0; i < tagClusterPanels.Length; i++)
        {
            if (tagClusterPanels[i] == null) { continue; }
            bool shouldBeActive = false;
            for (int j = 0; j < tagClusterPanels[i].childCount; j++)
            {
                Transform childPanel = tagClusterPanels[i].GetChild(j);
                if (childPanel == null) { continue; }
                if (childPanel.gameObject.activeSelf) { shouldBeActive = true; break; }
            }
            if (tagClusterPanels[i].gameObject.activeSelf != shouldBeActive) { tagClusterPanels[i].gameObject.SetActive(shouldBeActive); }
        }
    }

    // Check if a tag should be displayed, based on both the local player's, this player's, and the tag's preferences.
    private bool ShouldDisplayTag(int tagGroupIndex, byte tagGroupState)
    {
        if (tagGroupIndex >= tagGroupStates.Length) { return false; }
        if (tagGroupStates[tagGroupIndex] == 0 || !playerTagManager.prefDisplayTagsGlobal || !prefDisplayTagsPlayer)  { return false; }
		if (owner != null && owner.isLocal && !playerTagManager.prefDisplayOwnTags) { return false; }
		if (playerTagManager.tagGroups[tagGroupIndex].GetDisplayAboveHead(tagGroupState) && tagDisplayPanels[tagGroupIndex] != null) { return true; }
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
        Quaternion headRot = playerTagManager.localHeadData.rotation;
        headRot = Quaternion.Euler(new Vector3(0, headRot.eulerAngles.y, 0));
        tagCanvas.transform.SetPositionAndRotation(headData.position + (calcHeightOffset * Vector3.up), headRot);
    }

   
}


using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;

public enum GroupOutputType
{
    OutputNamesInState, OutputNamesInActiveStates, OutputNamesAndTagsInActiveStates, OutputNamesAndTagsInAllStates
}

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PlayerTagListener : UdonSharpBehaviour
{
    /// <summary>
    /// Outputs all players that are either assigned to a tag or have a specific state for a tag.
    /// </summary>

    // Expect redundant code; this used to be two scripts that got merged into one. (PlayerTagListenerSingle/PlayerTagListenerGroup -> PlayerTagListener)

    [SerializeField] private PlayerTagManager playerTagManager;

    [SerializeField] private byte tagGroupToMonitor;
    [SerializeField] private GroupOutputType outputGroupType;
    [SerializeField] private byte[] statesToListenFor; // If outputType=OutputNamesInState, which state(s) should we listen for?
    [SerializeField] private bool outputStateCount; // If outputType=OutputNamesInState, should we output the count of players in that state as well?
    
    [SerializeField] private string outputNameSeparator = "\n";
    [SerializeField] private string outputTagSeparator = ": ";
    [SerializeField] private TMPro.TMP_Text optionalOutputTextElement = null;

    private int tagGroupIndex = -1;
    public string outputText = "";
    public int outputCount = 0; // Only updates if outputType=OutputNamesInState


    private void Start()
    {
        if (playerTagManager == null) { Destroy(gameObject); }
        playerTagManager.RegisterListenerGroup(this, tagGroupToMonitor);
    }

    internal void SetGroupIndex(int index)
    {
        tagGroupIndex = index;
    }

    internal void UpdateOutput()
    {
		if (tagGroupIndex < 0 || tagGroupIndex >= playerTagManager.tagGroups.Length || tagGroupIndex > byte.MaxValue) { return; }
		outputText = "";
		outputCount = 0;
		int plyCnt = playerTagManager.allTagData.Count;
		byte groupIndex = (byte)tagGroupIndex;
		if (plyCnt <= 0) { return; }
		// Before starting, sort tags by owner names, alphabetically
		DataDictionary tagByOwner = new DataDictionary();
		for (int i = 0; i < plyCnt; i++)
		{
			if (playerTagManager.allTagData[i].Error != VRC.SDK3.Data.DataError.None) { continue; }
			PlayerTagData tagData = (PlayerTagData)playerTagManager.allTagData[i].Reference;
			if (!Utilities.IsValid(tagData.owner)) { continue; }
			tagByOwner.Add(tagData.owner.displayName, tagData);
		}
		int ownerCnt = tagByOwner.Count;
		if (ownerCnt == 0) { if (optionalOutputTextElement != null) { optionalOutputTextElement.text = outputText; } return; }
		DataList ownerNames = tagByOwner.GetKeys();
		ownerNames.Sort();

		// Then output accordingly
		for (int i = 0; i < ownerCnt; i++)
		{
			if (ownerNames[i].Error != VRC.SDK3.Data.DataError.None) { continue; }
			PlayerTagData tagData = (PlayerTagData)tagByOwner[ownerNames[i]].Reference;
			byte state = tagData.GetTagState(groupIndex);

			if (outputGroupType == GroupOutputType.OutputNamesAndTagsInAllStates
				|| (outputGroupType == GroupOutputType.OutputNamesAndTagsInActiveStates && state > 0))
			{
				if (outputText.Length > 0) { outputText += outputNameSeparator; }
				outputText += tagData.owner.displayName + outputTagSeparator + playerTagManager.tagGroups[groupIndex].GetStateName(state);
			}
			else if (outputGroupType == GroupOutputType.OutputNamesInActiveStates && state > 0)
			{
				if (outputText.Length > 0) { outputText += outputNameSeparator; }
				outputText += tagData.owner.displayName;
			}
			else if (outputGroupType == GroupOutputType.OutputNamesInState)
			{
				bool inState = false;
				for (int j = 0; j < statesToListenFor.Length; j++) { if (state == statesToListenFor[j]) { inState = true; break; } }
				if (inState)
				{
					if (outputText.Length > 0) { outputText += outputNameSeparator; }
					outputText += tagData.owner.displayName;
					if (outputStateCount) { outputCount++; }
				}
			}
		}
		if (optionalOutputTextElement != null) { optionalOutputTextElement.text = outputText; }
	}

}

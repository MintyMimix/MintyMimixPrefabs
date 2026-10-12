
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public enum ListenerType
{
    Single, Group
}
public enum SingleOutputType
{
    TagIsActive, TagIsNOTActive, NameAndTag
}
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
    [SerializeField] private ListenerType listenerType;

    [SerializeField] private byte tagSingleToMonitor;
    [SerializeField] private SingleOutputType outputSingleType;

    [SerializeField] private byte tagGroupToMonitor;
    [SerializeField] private GroupOutputType outputGroupType;
    [SerializeField] private byte stateToListenFor; // If outputType=OutputNamesInState, which state should we listen for?
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
        if (listenerType == ListenerType.Single) { playerTagManager.RegisterListenerSingle(this, tagSingleToMonitor); }
        else if (listenerType == ListenerType.Group) { playerTagManager.RegisterListenerGroup(this, tagGroupToMonitor); }
    }

    internal void SetGroupIndex(int index)
    {
        tagGroupIndex = index;
    }

    internal void UpdateOutput()
    {
        if (listenerType == ListenerType.Single) { UpdateOutputSingle(); }
        else if (listenerType == ListenerType.Group) { UpdateOutputGroup(); }
    }

    private void UpdateOutputSingle()
    {
        if (tagSingleToMonitor >= playerTagManager.tagSingleNames.Length) { return; }
        int plyCnt = playerTagManager.allTagData.Count;
        if (plyCnt <= 0) { return; }
        outputText = "";
        for (int i = 0; i < plyCnt; i++)
        {
            if (playerTagManager.allTagData[i].Error != VRC.SDK3.Data.DataError.None) { continue; }
            PlayerTagData tagData = (PlayerTagData)playerTagManager.allTagData[i].Reference;
            if (!Utilities.IsValid(tagData.owner)) { continue; }
            bool state = tagData.GetTagStateSingle(tagSingleToMonitor);

            if (outputSingleType == SingleOutputType.NameAndTag)
            {
                if (outputText.Length > 0) { outputText += outputNameSeparator; }
                outputText += tagData.owner.displayName + outputTagSeparator + (state ? "O" : "X");
            }
            else if ((outputSingleType == SingleOutputType.TagIsActive && state)
                || (outputSingleType == SingleOutputType.TagIsNOTActive && !state))
            {
                if (outputText.Length > 0) { outputText += outputNameSeparator; }
                outputText += tagData.owner.displayName;
            }
        }
        if (optionalOutputTextElement != null) { optionalOutputTextElement.text = outputText; }
    }

    private void UpdateOutputGroup()
    {
        if (tagGroupIndex < 0 || tagGroupIndex >= playerTagManager.tagGroups.Length || tagGroupIndex > byte.MaxValue) { return; }
        outputText = "";
        outputCount = 0;
        int plyCnt = playerTagManager.allTagData.Count;
        byte groupIndex = (byte)tagGroupIndex;
        if (plyCnt <= 0) { return; }
        for (int i = 0; i < plyCnt; i++)
        {
            if (playerTagManager.allTagData[i].Error != VRC.SDK3.Data.DataError.None) { continue; }
            PlayerTagData tagData = (PlayerTagData)playerTagManager.allTagData[i].Reference;
            if (!Utilities.IsValid(tagData.owner)) { continue; }
            byte state = tagData.GetTagStateGroup(groupIndex);

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
            else if (outputGroupType == GroupOutputType.OutputNamesInState && state == stateToListenFor)
            {
                if (outputText.Length > 0) { outputText += outputNameSeparator; }
                outputText += tagData.owner.displayName;
                if (outputStateCount) { outputCount++; }
            }
        }
        if (optionalOutputTextElement != null) { optionalOutputTextElement.text = outputText; }
    }
}

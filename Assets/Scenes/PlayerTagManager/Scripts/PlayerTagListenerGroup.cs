
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public enum GroupOutputType
{
    OutputNamesInState
    , OutputNamesInActiveStates, OutputNamesInAllStates
    , OutputNamesAndTagsInActiveStates, OutputNamesAndTagsInAllStates
}

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PlayerTagListenerGroup : UdonSharpBehaviour
{
    [SerializeField] private PlayerTagManager playerTagManager;
    [SerializeField] private byte tagGroupToMonitor;
    [SerializeField] private GroupOutputType outputType;
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
        for (int i = 0; i < plyCnt; i++)
        {
            if (playerTagManager.allTagData[i].Error != VRC.SDK3.Data.DataError.None) { continue; }
            PlayerTagData tagData = (PlayerTagData)playerTagManager.allTagData[i].Reference;
            if (!Utilities.IsValid(tagData.owner)) { continue; }
            byte state = tagData.GetTagStateGroup(groupIndex);

            if (outputType == GroupOutputType.OutputNamesAndTagsInAllStates
                || (outputType == GroupOutputType.OutputNamesAndTagsInActiveStates && state > 0))
            {
                if (outputText.Length > 0) { outputText += outputNameSeparator; }
                outputText += tagData.owner.displayName + outputTagSeparator + playerTagManager.tagGroups[groupIndex].GetStateName(state);
            }
            else if (outputType == GroupOutputType.OutputNamesInAllStates
                || (outputType == GroupOutputType.OutputNamesInActiveStates && state > 0))
            {
                if (outputText.Length > 0) { outputText += outputNameSeparator; }
                outputText += tagData.owner.displayName;
            }
            else if (outputType == GroupOutputType.OutputNamesInState && state == stateToListenFor)
            {
                if (outputText.Length > 0) { outputText += outputNameSeparator; }
                outputText += tagData.owner.displayName;
                if (outputStateCount) { outputCount++; }
            }
        }
        if (optionalOutputTextElement != null) { optionalOutputTextElement.text = outputText; }
    }
}

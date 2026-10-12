
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public enum SingleOutputType
{
    TagIsActive, TagIsNOTActive, NameAndTag
}

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PlayerTagListenerSingle : UdonSharpBehaviour
{
    [SerializeField] private PlayerTagManager playerTagManager;
    [SerializeField] private byte tagSingleToMonitor;
    [SerializeField] private SingleOutputType outputType;
    [SerializeField] private string outputNameSeparator = "\n";
    [SerializeField] private string outputTagSeparator = ": ";
    [SerializeField] private TMPro.TMP_Text optionalOutputTextElement = null;

    public string outputText = "";

    private void Start()
    {
        if (playerTagManager == null) { Destroy(gameObject); } 
        playerTagManager.RegisterListenerSingle(this, tagSingleToMonitor);
    }

    internal void UpdateOutput()
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

            if (outputType == SingleOutputType.NameAndTag)
            {
                if (outputText.Length > 0) { outputText += outputNameSeparator; }
                outputText += tagData.owner.displayName + outputTagSeparator + (state ? "O" : "X");
            }
            else if ((outputType == SingleOutputType.TagIsActive && state)
                || (outputType == SingleOutputType.TagIsNOTActive && !state)) 
            {
                if (outputText.Length > 0) { outputText += outputNameSeparator; }
                outputText += tagData.owner.displayName;
            }
        }
        if (optionalOutputTextElement != null) { optionalOutputTextElement.text = outputText; }
    }
}

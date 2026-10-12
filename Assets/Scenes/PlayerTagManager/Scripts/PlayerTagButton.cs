
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public enum TagButtonType
{
    Single, GroupSetValue, GroupNextValue
}

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PlayerTagButton : UdonSharpBehaviour
{
    /// <summary>
    /// Locally sends an event to the manager to toggle a single tag/iterate through the options of a group tag.
    /// </summary>

    [SerializeField] private PlayerTagManager playerTagManager;
    [SerializeField] private TagButtonType tagButtonType;
    [SerializeField] private byte tagSingleToToggle;
    [SerializeField] private byte tagGroupToToggle;
    [SerializeField] private byte setTagGroupValue;
    private string inspectorInteractText = string.Empty;

    private void Start()
    {
        if (playerTagManager == null) { gameObject.SetActive(false); return; }
        inspectorInteractText = InteractionText;
        RefreshInteractText();
    }

    private void RefreshInteractText()
    {
        if (tagButtonType == TagButtonType.Single && tagSingleToToggle < playerTagManager.tagSingleNames.Length)
        {
            InteractionText = inspectorInteractText.Replace("$TAG", playerTagManager.tagSingleNames[tagSingleToToggle]);
        }
        else if (tagButtonType == TagButtonType.GroupNextValue && tagGroupToToggle < playerTagManager.tagGroups.Length)
        {
            InteractionText = inspectorInteractText.Replace("$TAG", playerTagManager.tagGroups[tagGroupToToggle].tagGroupName)
                .Replace("$STATE", playerTagManager.tagGroups[tagGroupToToggle].GetStateName(playerTagManager.GetNextStateInTagGroup(tagGroupToToggle)));
        }
        else if (tagButtonType == TagButtonType.GroupSetValue && tagGroupToToggle < playerTagManager.tagGroups.Length)
        {
            InteractionText = inspectorInteractText.Replace("$TAG", playerTagManager.tagGroups[tagGroupToToggle].tagGroupName)
                .Replace("$STATE", playerTagManager.tagGroups[tagGroupToToggle].GetStateName(setTagGroupValue));
        }
    }

    public void OnPush()
    {
        Interact();
    }

    public override void Interact()
    {
        if (tagButtonType == TagButtonType.Single) { playerTagManager.SetTagSingle(tagSingleToToggle, this); }
        else if (tagButtonType == TagButtonType.GroupNextValue) { playerTagManager.SetTagGroup(tagGroupToToggle, this); }
        else if (tagButtonType == TagButtonType.GroupSetValue) { playerTagManager.SetTagGroup(tagGroupToToggle, setTagGroupValue, this); }
    }

    internal void UpdateValueCallback(bool isSingle, int newValue)
    {
        if (tagButtonType == TagButtonType.GroupNextValue) { RefreshInteractText(); }
    }
}

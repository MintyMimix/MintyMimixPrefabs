
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public enum TagButtonType
{
    SetValue, NextValue
}

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PlayerTagButton : UdonSharpBehaviour
{
    /// <summary>
    /// Locally sends an event to the manager to toggle a single tag/iterate through the options of a group tag.
    /// </summary>

    [SerializeField] private PlayerTagManager playerTagManager;
    [SerializeField] private TagButtonType tagButtonType;
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
        if (tagButtonType == TagButtonType.NextValue && tagGroupToToggle < playerTagManager.tagGroups.Length)
        {
            InteractionText = inspectorInteractText.Replace("$TAG", playerTagManager.tagGroups[tagGroupToToggle].tagGroupName)
                .Replace("$STATE", playerTagManager.tagGroups[tagGroupToToggle].GetStateName(playerTagManager.GetNextStateInTagGroup(tagGroupToToggle)));
        }
        else if (tagButtonType == TagButtonType.SetValue && tagGroupToToggle < playerTagManager.tagGroups.Length)
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
        if (tagButtonType == TagButtonType.NextValue) { playerTagManager.SetTagGroup(tagGroupToToggle, this); }
        else if (tagButtonType == TagButtonType.SetValue) { playerTagManager.SetTagGroup(tagGroupToToggle, setTagGroupValue, this); }
    }

    internal void UpdateValueCallback(int newValue)
    {
        if (tagButtonType == TagButtonType.NextValue) { RefreshInteractText(); }
    }
}

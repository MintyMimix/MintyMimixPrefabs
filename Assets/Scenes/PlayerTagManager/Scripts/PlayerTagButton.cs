
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public enum TagButtonType
{
    Single, GroupNextValue, GroupSetValue
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

    public void OnClick()
    {
        if (tagButtonType == TagButtonType.Single) { playerTagManager.SetTagSingle(tagSingleToToggle, this); }
        else if (tagButtonType == TagButtonType.GroupNextValue) { playerTagManager.SetTagGroup(tagGroupToToggle, this); }
        else if (tagButtonType == TagButtonType.GroupSetValue) { playerTagManager.SetTagGroup(tagGroupToToggle, setTagGroupValue, this); }
    }

    internal void UpdateValueCallback(bool isSingle, int newValue)
    {
        // Feel free to do things here
    }
}

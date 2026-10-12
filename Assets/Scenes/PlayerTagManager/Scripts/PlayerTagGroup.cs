
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PlayerTagGroup : UdonSharpBehaviour
{
    /// <summary>
    /// Defines any mutually exclusive groups of tags the world creators wants to have available.
    /// </summary>

    [SerializeField] public string tagGroupName;
    [SerializeField] private string[] tagStateNames;
    [SerializeField] private Color[] tagColors;
    [SerializeField] private bool[] tagDisplayAboveHead;

    public byte size() 
    {
        if (tagStateNames == null || tagStateNames.Length >= byte.MaxValue || tagStateNames.Length != tagColors.Length || tagStateNames.Length != tagDisplayAboveHead.Length) { return 0; }
        return (byte)(tagStateNames.Length + 1);
    }
    public string GetStateName(int state)
    {
        if (state == 0 || state > tagStateNames.Length) { return "None"; }
        return tagStateNames[state - 1];
    }
    internal Color GetColor(int state)
    {
        if (state == 0 || state > tagColors.Length) { return Color.black; }
        return tagColors[state - 1];
    }
    internal bool GetDisplayAboveHead(int state)
    {
        if (state == 0 || state > tagDisplayAboveHead.Length) { return false; }
        return tagDisplayAboveHead[state - 1];
    }
}

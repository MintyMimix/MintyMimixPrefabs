
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PlayerTagCluster : UdonSharpBehaviour
{
    [SerializeField] internal PlayerTagGroup[] tagsInCluster;
    [SerializeField] internal float scaleInPanel = 1.0f;
}

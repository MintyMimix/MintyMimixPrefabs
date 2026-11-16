
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class TriviaRunOnInteract : UdonSharpBehaviour
{
    [SerializeField] private UdonSharpBehaviour referenceBehaviour;
    [SerializeField] private string PublicEventToRun;
    [SerializeField] private GameObject[] toggleObjects;

    public override void Interact()
    {
        referenceBehaviour.SendCustomEvent(PublicEventToRun);
        if (Utilities.IsValid(toggleObjects) && toggleObjects.Length > 0)
        {
            for (int i = 0; i < toggleObjects.Length; i++)
            {
                if (!Utilities.IsValid(toggleObjects[i])) { continue; }
                toggleObjects[i].SetActive(!toggleObjects[i].activeInHierarchy);
            }
        }
    }
}

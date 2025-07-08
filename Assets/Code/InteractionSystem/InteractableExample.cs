using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using TMPro;

namespace Code.InteractionSystem
{
    public class InteractableExample : Interactable {
        public override string InteractionPrompt => "Pless E to interact";
        
        protected override void OnInteract(NetworkConnection conn) {
            if (IsServer) {
                Debug.Log("Interacting...");
            }
        }
    }
}                        

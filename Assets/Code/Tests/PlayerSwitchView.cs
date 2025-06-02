using System;
using UnityEngine;
using UnityEngine.Serialization;

public class PlayerSwitchView : MonoBehaviour
{
    [SerializeField] private Transform playerFPV, playerTPV;
    [FormerlySerializedAs("fpv")] [SerializeField] private bool thirdPersonView;

    private void Awake()
    {
        SwitchWiev(thirdPersonView);
    }

    private void Update()
    {
        if (Input.GetKeyUp(KeyCode.C))
        {
            SwitchWiev(!thirdPersonView);
        }
    }

    private void SwitchWiev(bool view)
    {
        thirdPersonView = view;
        if (playerFPV.gameObject.activeSelf)
        {
            playerTPV.SetPositionAndRotation(playerFPV.position, playerFPV.rotation);
            playerFPV.gameObject.SetActive(!view);
            playerTPV.gameObject.SetActive(view);
        }
        else
        {
            playerFPV.SetPositionAndRotation(playerTPV.position, playerTPV.rotation);
            playerTPV.gameObject.SetActive(view);
            playerFPV.gameObject.SetActive(!view);
        }
    }
}

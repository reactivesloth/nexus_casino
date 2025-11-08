using System;
using Code.Player;
using Code.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerUIFPV : MonoBehaviour
{
    [SerializeField] private GameObject playerUIFPVPanel;
    [SerializeField] private TextMeshProUGUI playerName;
    [SerializeField] private TextMeshProUGUI playerRole;
    [SerializeField] private Image voiceImage;
    [SerializeField] private GameObject hostIndicator;

    private bool inited = false;
    
    private PlayerUI _playerUI;
    private PlayerMovementController  _playerMovementController;

    private void Update()
    {
        if (!inited)
        {
            _playerMovementController = FindLocalOwnerMovement();
            if (_playerMovementController != null)
            {
                _playerUI = _playerMovementController.GetComponent<PlayerUI>();
                if (_playerUI != null)
                {
                    inited = true;
                }
            }
        }

        if (inited && playerUIFPVPanel != null)
        {
            playerUIFPVPanel.SetActive(_playerMovementController.FirstPersonView);
            
            if (playerUIFPVPanel.activeSelf)
            {
                if (playerName != null) playerName.text = _playerUI.PlayerName;
                if (playerRole != null) playerRole.text = _playerUI.PlayerRole;
                if (hostIndicator != null) hostIndicator.SetActive(_playerUI.IsHost);
                if (voiceImage != null)
                {
                    voiceImage.color = _playerUI.IsVoiceMuted.Value ? Color.red : _playerUI.IsVoiceHeld.Value ? Color.white : Color.clear;
                    voiceImage.gameObject.SetActive(_playerUI.IsVoiceHeld.Value || _playerUI.IsVoiceMuted.Value);
                }        
            }
        }
        else
        {
            return;
        }
    }
    
    private PlayerMovementController FindLocalOwnerMovement()
    {
        var all = FindObjectsByType<PlayerMovementController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            var m = all[i];
            if (m != null && m.Owner.IsLocalClient)
                return m;
        }

        return null;
    }
}

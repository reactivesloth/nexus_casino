using System.Linq;
using Code.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI
{
    public class PlayerUIFPV : MonoBehaviour
    {
        [SerializeField] private GameObject playerUIFPVPanel;
        [SerializeField] private TextMeshProUGUI playerName;
        [SerializeField] private TextMeshProUGUI playerRole;
        [SerializeField] private Image voiceImage;
        [SerializeField] private GameObject hostIndicator;

        private bool _inited;
    
        private PlayerUI _playerUI;
        private PlayerMovementController  _playerMovementController;

        private void Update()
        {
            if (!_inited)
            {
                _playerMovementController = FindLocalOwnerMovement();
                if (_playerMovementController != null)
                {
                    _playerUI = _playerMovementController.GetComponent<PlayerUI>();
                    if (_playerUI != null)
                    {
                        _inited = true;
                    }
                }
            }

            if (!_inited || playerUIFPVPanel == null) return;
            if (_playerUI != null)
            {
                playerUIFPVPanel.SetActive(_playerMovementController.FirstPersonView);

                if (playerUIFPVPanel.activeSelf)
                {
                    if (playerName != null) playerName.text = _playerUI.PlayerName;
                    if (playerRole != null) playerRole.text = _playerUI.PlayerRole;
                    if (hostIndicator != null) hostIndicator.SetActive(_playerUI.IsHost);
                    if (voiceImage != null)
                    {
                        voiceImage.color = _playerUI.IsVoiceMuted.value ? Color.red :
                            _playerUI.IsVoiceHeld.value ? Color.white : Color.clear;
                        voiceImage.gameObject.SetActive(_playerUI.IsVoiceHeld.value || _playerUI.IsVoiceMuted.value);
                    }
                }
            }
            else
            {
                _inited = false;
            }
        }
    
        private static PlayerMovementController FindLocalOwnerMovement()
        {
            var all = FindObjectsByType<PlayerMovementController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return all.FirstOrDefault(m => m != null && m.isOwner);
        }
    }
}

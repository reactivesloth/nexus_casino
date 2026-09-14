using System.Linq;
using Code.Network.Player;
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

        private void Update()
        {
            if (!_inited)
            {
                if (PlayerMovementController.LocalInstance != null)
                {
                    _playerUI = PlayerMovementController.LocalInstance.GetComponent<PlayerUI>();
                    if (_playerUI != null)
                    {
                        _inited = true;
                    }
                }
            }

            if (!_inited || playerUIFPVPanel == null) return;
            if (_playerUI != null)
            {
                playerUIFPVPanel.SetActive(PlayerMovementController.LocalInstance.FirstPersonView);

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
    }
}

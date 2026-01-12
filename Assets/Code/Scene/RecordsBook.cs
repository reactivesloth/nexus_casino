using System.Collections.Generic;
using Code.API;
using Code.API.Models;
using Code.Network;
using Code.Player;
using Proyecto26;
using TMPro;
using UnityEngine;

namespace Code.Scene
{
    public class RecordsBook : MonoBehaviour
    {
                private static readonly int Open = Animator.StringToHash("Open");
        private static readonly int Close = Animator.StringToHash("Close");

        [SerializeField] private Animator bookAnimator;
        [SerializeField] private float activateDistance = 5f;

        [Header("Table")]
        [SerializeField] private GameObject uiRoot;
        [SerializeField] private Transform depositContainer;
        [SerializeField] private Transform withdrawContainer;
        [SerializeField] private GameObject recordItemPrefab;

        private bool _isOpen = false;
        private PlayerMovementController _localPlayer;

        private void Update()
        {
            _localPlayer ??= PlayerMovementController.Own;
            if (_localPlayer == null) return;

            var distance = Vector3.Distance(_localPlayer.transform.position, transform.position);
            if (distance <= activateDistance && !_isOpen) OpenBook();
            else if (_isOpen && distance > activateDistance) CloseBook();
        }

        private void OpenBook()
        {
            _isOpen = true;
            bookAnimator.SetTrigger(Open);
            OpenUI();
        }

        private void CloseBook()
        {
            _isOpen = false;
            bookAnimator.SetTrigger(Close);
            CloseUI();
        }

        private void OpenUI()
        {
            uiRoot.SetActive(true);
            UpdateTable();
        }

        private void CloseUI()
        {
            uiRoot.SetActive(false);
        }

        private void UpdateTable()
        {
            ClearContainer(depositContainer);
            ClearContainer(withdrawContainer);

            var topDepositsRequest = new RequestHelper
            {
                Uri = ApiRoutes.DOMAIN.TrimEnd('/') + "/api/client/records/top-deposits",
                Headers = ClientDataStorage.GetJwtHeader(),
                Params = new Dictionary<string, string> { { "limit", "3" } }
            };

            var topWithdrawalsRequest = new RequestHelper
            {
                Uri = ApiRoutes.DOMAIN.TrimEnd('/') + "/api/client/records/top-withdrawals",
                Headers = ClientDataStorage.GetJwtHeader(),
                Params = new Dictionary<string, string> { { "limit", "3" } }
            };

            RestClient.Get(topDepositsRequest).Then(res =>
            {
                if (res.StatusCode != 200)
                    return;
                
                var data = JsonUtility.FromJson<TopSchema>(res.Text);
                FillTable(depositContainer, data.records, true);
            });

            RestClient.Get(topWithdrawalsRequest).Then(res =>
            {
                if (res.StatusCode != 200) 
                    return;
                
                var data = JsonUtility.FromJson<TopSchema>(res.Text);
                FillTable(withdrawContainer, data.records, false);
            });
        }

        private void FillTable(Transform parent, List<TopRecord> records, bool isDeposit)
        {
            for (int i = 0; i < records.Count; i++)
            {
                var item = Instantiate(recordItemPrefab, parent);
                var texts = item.GetComponentsInChildren<TMP_Text>();

                // Ожидаем порядок: [0] – номер, [1] – ник, [2] – сумма
                if (texts.Length >= 3)
                {
                    texts[0].text = (i + 1).ToString();
                    texts[1].text = records[i].username;
                    texts[2].text = records[i].total_amount.ToString("N0"); // формат с разделителями
                }
            }
        }

        private void ClearContainer(Transform container)
        {
            for (int i = container.childCount - 1; i >= 0; i--)
                Destroy(container.GetChild(i).gameObject);
        }
    }
}
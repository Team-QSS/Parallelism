using System;
using TMPro;
using UnityEngine;

public class UIController : MonoBehaviour
{
   [SerializeField] private TextMeshProUGUI _tmp;
   [SerializeField] private NetworkController _networkController;

   [SerializeField] private GameObject startBtn;
   [SerializeField] private GameObject settingBtn;
   
   private Team team;
   private PlayerStatus status;

   public void ChangeText()
   {
      team = _networkController.m_LocalUser.Team.Value;
      status = _networkController.m_LocalUser.UserStatus.Value;
      _tmp.text = $@"진영 : {team switch {
                             Team.None => "없음",
                             Team.Red  => "빨강",
                             Team.Blue => "파랑",
                             _         => throw new ArgumentOutOfRangeException() }}
" +
                  $"준비 상태 : {(status == PlayerStatus.Ready ? "완료" : "대기중") }";
   }

   private void Update()
   {
      OnlyShowHost();
   }

   public void OnlyShowHost()
   {
      if (_networkController.m_LocalUser.IsHost.Value)
      {
         settingBtn.SetActive(true);
         startBtn.SetActive(true);
      }
      else
      {
         settingBtn.SetActive(false);
         startBtn.SetActive(false);
      }
   }
}

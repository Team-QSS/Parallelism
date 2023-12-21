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
      _tmp.text = $"Team : {team}\n" +
                  $"Ready : {status == PlayerStatus.Ready}";
   }
   
   public void ChangeTeam()
   {
      team = _networkController.m_LocalUser.Team.Value;
      ChangeText();
   }
   
   public void ChangeReady()
   {
      status = _networkController.m_LocalUser.UserStatus.Value;
      ChangeText();
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

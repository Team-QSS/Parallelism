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
      _tmp.text = $"Team : {team}\n" +
                  $"Ready : {status == PlayerStatus.Ready}";
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

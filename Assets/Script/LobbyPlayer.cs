using TMPro;
using UnityEngine;

public class LobbyPlayer : MonoBehaviour
{
    [SerializeField] private TextMeshPro _playerName;
    [SerializeField] private Renderer _isReadyRenderer;

    private MaterialPropertyBlock _propertyBlock;

    // public void SetData(data)
    // {
    //     _data = data;
    //     _playerName.text = _data.Gamertag;
    //
    //     if (_data.IsReady)
    //     {
    //         if (_isReadyRenderer != null)
    //         {
    //             _propertyBlock = new MaterialPropertyBlock();
    //             _isReadyRenderer.GetPropertyBlock(_propertyBlock);
    //             _propertyBlock.SetColor("_BaseColor", Color.green);
    //             _isReadyRenderer.SetPropertyBlock(_propertyBlock);
    //         }
    //     }
    //
    //     gameObject.SetActive(true);
    // }
}

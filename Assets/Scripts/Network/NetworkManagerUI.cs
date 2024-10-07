using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class NetworkManagerUI : MonoBehaviour
{
    public Button hostButton;
    public Button clientButton;
    // public Button serverButton;

    void Start()
    {
        Cursor.visible = true;
        // Assign the button click events to their corresponding methods
        hostButton.onClick.AddListener(OnHostButtonClicked);
        clientButton.onClick.AddListener(OnClientButtonClicked);
        // serverButton.onClick.AddListener(OnServerButtonClicked);
    }

    void OnHostButtonClicked()
    {
        NetworkManager.Singleton.StartHost();
    }

    void OnClientButtonClicked()
    {
        NetworkManager.Singleton.StartClient();
    }

    // void OnServerButtonClicked()
    // {
    //     NetworkManager.Singleton.StartServer();
    // }
}

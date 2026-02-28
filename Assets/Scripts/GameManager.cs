using UnityEngine;
using UnityEngine.XR.Management;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public GameObject vrPlayer;
    public GameObject pcPlayer;

    IEnumerator Start()
    {
        yield return CheckXR();
    }

    IEnumerator CheckXR()
    {
        if (XRGeneralSettings.Instance.Manager.activeLoader == null)
        {
            Debug.Log("Initializing XR...");

            yield return XRGeneralSettings.Instance.Manager.InitializeLoader();

            if (XRGeneralSettings.Instance.Manager.activeLoader != null)
            {
                XRGeneralSettings.Instance.Manager.StartSubsystems();
                Debug.Log("XR started");

                vrPlayer.SetActive(true);
                pcPlayer.SetActive(false);
            }
            else
            {
                Debug.Log("XR failed. Using PC mode.");

                vrPlayer.SetActive(false);
                pcPlayer.SetActive(true);
            }
        }
        else
        {
            Debug.Log("XR already running");
        }
    }
}

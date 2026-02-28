using UnityEngine;
using UnityEngine.SceneManagement;

public class BackButton : MonoBehaviour
{
    // Name of the scene to load (your Start Menu)
    public string startMenuSceneName = "StartMenu";

    // This function is called when the button is clicked
    public void GoBackToStartMenu()
    {
        Debug.Log("Back button pressed, loading Start Menu");
        SceneManager.LoadScene(startMenuSceneName);
    }
}

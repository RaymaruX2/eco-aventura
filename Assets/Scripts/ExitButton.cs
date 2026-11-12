using UnityEngine;

public class ExitButton : MonoBehaviour
{
    public void ExitGame()
    {
        Debug.Log("Saliendo del juego...");
        Application.Quit();

#if UNITY_EDITOR
        // Para detener el juego si estás en el editor de Unity
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}

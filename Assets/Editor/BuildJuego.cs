using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Compilacion del juego.
//  - Menu:  Juego > Compilar para Windows
//  - Linea de comandos: Unity -batchmode -quit -projectPath <proyecto> -executeMethod BuildJuego.Windows
// Genera Builds/Windows/<nombre del producto>.exe con todas las escenas activas de Build Settings.
public static class BuildJuego
{
    [MenuItem("Juego/Compilar para Windows")]
    public static void Windows()
    {
        string[] escenas = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        string carpeta = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Builds", "Windows");
        Directory.CreateDirectory(carpeta);
        string exe = Path.Combine(carpeta, PlayerSettings.productName + ".exe");

        BuildPlayerOptions op = new BuildPlayerOptions
        {
            scenes = escenas,
            locationPathName = exe,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };
        BuildReport r = BuildPipeline.BuildPlayer(op);
        Debug.Log("BUILD_RESULTADO: " + r.summary.result + " | errores: " + r.summary.totalErrors + " | avisos: " + r.summary.totalWarnings +
                  " | tamano: " + (r.summary.totalSize / (1024f * 1024f)).ToString("0.0") + " MB | " + exe);
        if (Application.isBatchMode) EditorApplication.Exit(r.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}

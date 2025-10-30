using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace BinPickingAI
{
    public class PythonExecutor : MonoBehaviour
{
    //public string condaPath = @"C:\Users\dudrj\anaconda3\Scripts"; // adjust path
    public string condaPath = @"/home/dudrjs/anaconda3/Scripts";
    public string envName = "mlagents";
    //public string scriptPath = @"D:\unityworkspace\BinpickingAI\py\grasp_train.py";
    public string scriptPath = @"py/grasp_train.py";

    private Process pythonProcess;

    void Start()
    {
        RunPythonScript();
    }

    void RunPythonScript()
    {
        ProcessStartInfo psi = new ProcessStartInfo();
        psi.FileName = "/bin/bash";
        psi.RedirectStandardInput = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;

        pythonProcess = new Process();
        pythonProcess.StartInfo = psi;
        pythonProcess.OutputDataReceived += (sender, e) => {
            if (!string.IsNullOrEmpty(e.Data))
                UnityEngine.Debug.Log("[Python STDOUT] " + e.Data);
        };
        pythonProcess.ErrorDataReceived += (sender, e) => {
            if (!string.IsNullOrEmpty(e.Data))
                UnityEngine.Debug.LogError("[Python STDERR] " + e.Data);
        };
        pythonProcess.Start();
        pythonProcess.BeginOutputReadLine();
        pythonProcess.BeginErrorReadLine();
        StreamWriter sw = pythonProcess.StandardInput;
        if (sw.BaseStream.CanWrite)
        {
            sw.WriteLine($"source ~/anaconda3/etc/profile.d/conda.sh");
            sw.WriteLine($"conda activate {envName}");
            sw.WriteLine($"python \"{scriptPath}\"");
        }
    }

    // Unity ���� �� ȣ��
    void OnApplicationQuit()
    {
        if (pythonProcess != null && !pythonProcess.HasExited)
        {
            pythonProcess.CloseMainWindow();
            if (!pythonProcess.WaitForExit(2000))
            {
                pythonProcess.Kill();
            }
        }
    }
}

}

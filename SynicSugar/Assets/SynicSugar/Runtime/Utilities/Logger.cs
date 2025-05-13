using System;
using System.Diagnostics;

namespace SynicSugar
{
    internal class Logger
    {
        [Conditional("SYNICSUGAR_LOG")] 
        internal static void Log(string methodName, string message)
        {
            UnityEngine.Debug.Log($"[SynicSugar INFO] [{DateTime.Now.ToString("HH:mm:ss.fff")}] {methodName}: {message}");
        }
        [Conditional("SYNICSUGAR_LOG")] 
        internal static void Log(string methodName, Result result)
        {
            UnityEngine.Debug.Log($"[SynicSugar INFO] [{DateTime.Now.ToString("HH:mm:ss.fff")}] {methodName} Result: {result}");
        }
        [Conditional("SYNICSUGAR_LOG")] 
        internal static void Log(string methodName, string message, Result result)
        {
            UnityEngine.Debug.Log($"[SynicSugar INFO] [{DateTime.Now.ToString("HH:mm:ss.fff")}] {methodName}: {message} Result: {result}");
        }

        internal static void LogWarning(string methodName, string message)
        {
            UnityEngine.Debug.LogWarning($"[SynicSugar WARN] [{DateTime.Now.ToString("HH:mm:ss.fff")}] {methodName}: {message}");
        }
        internal static void LogWarning(string methodName, Result result)
        {
            UnityEngine.Debug.LogWarning($"[SynicSugar WARN] [{DateTime.Now.ToString("HH:mm:ss.fff")}] {methodName} Result: {result}");
        }
        internal static void LogWarning(string methodName, string message, Result result)
        {
            UnityEngine.Debug.LogWarning($"[SynicSugar WARN] [{DateTime.Now.ToString("HH:mm:ss.fff")}] {methodName}: {message} Result: {result}");
        }
        
        internal static void LogError(string methodName, string message)
        {
            UnityEngine.Debug.LogError($"[SynicSugar ERROR] [{DateTime.Now.ToString("HH:mm:ss.fff")}] {methodName}: {message}");
        }
        internal static void LogError(string methodName, Result result)
        {
            UnityEngine.Debug.LogError($"[SynicSugar ERROR] [{DateTime.Now.ToString("HH:mm:ss.fff")}] {methodName} Result: {result}");
        }
        internal static void LogError(string methodName, string message, Result result)
        {
            UnityEngine.Debug.LogError($"[SynicSugar ERROR] [{DateTime.Now.ToString("HH:mm:ss.fff")}] {methodName}: {message} Result: {result}");
        }
    }
}
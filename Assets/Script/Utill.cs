using System;
using System.Collections;
using UnityEngine;

// public static class Utill
// {
//     private static readonly Executor Executor = new GameObject().AddComponent<Executor>();
//
//     public static void ExecuteLink(Action<float> action, float a, float b, float t)
//     {
//         Executor.ExecuteLink(action, a, b, t);
//     }
//     
//     public static void ExecuteLink(Action<Vector3> action, Vector3 a, Vector3 b, float t)
//     {
//         Executor.ExecuteLink(action, a, b, t);
//     }
//     
//     public static void ExecuteLink(Action start, Func<bool> condition, Action end)
//     {
//         Executor.ExecuteLink(start, condition, end);
//     }
//     
//     public static void ExecuteLink(Func<bool> condition, Action end)
//     {
//         Executor.ExecuteLink(condition, end);
//     }
//         
//     public static void ExecuteLink(Func<bool> preCondition, Func<bool> condition, Action end)
//     {
//         Executor.ExecuteLink(preCondition, condition, end);
//     }
//     
//     public static void ExecuteLink(float t, Action end)
//     {
//         Executor.ExecuteLink(t, end);
//     }
// }

public static class Utill
{
    public static IEnumerator Execute(Action<float> action, float a, float b, float t)
    {
        float timer = 0;
        while (timer < t)
        {
            timer += Time.smoothDeltaTime;
            action(Mathf.Lerp(a, b, Mathf.Clamp01(timer / t)));
            yield return null;
        }
    }
    
    public static IEnumerator Execute(Action<Vector3> action, Vector3 a, Vector3 b, float t)
    {
        float timer = 0;
        while (timer < t)
        {
            timer += Time.smoothDeltaTime;
            action(Vector3.Lerp(a, b, Mathf.Clamp01(timer / t)));
            yield return null;
        }
    }
    
    public static IEnumerator Execute(Action<Quaternion> action, Quaternion a, Quaternion b, float t)
    {
        float timer = 0;
        while (timer < t)
        {
            timer += Time.smoothDeltaTime;
            action(Quaternion.Lerp(a, b, Mathf.Clamp01(timer / t)));
            yield return null;
        }
    }
    
    public static IEnumerator Execute(Action start, Func<bool> condition, Action end)
    {
        start();
        yield return new WaitUntil(condition);
        end();
    }

    public static IEnumerator Execute(Func<bool> condition, Action end)
    {
        yield return new WaitUntil(condition);
        end();
    }
    
    public static IEnumerator Execute(Func<bool> preCondition, Func<bool> condition, Action end)
    {
        yield return new WaitUntil(preCondition);
        yield return new WaitUntil(condition);
        end();
    }
    
    public static IEnumerator Execute(float t, Action end)
    {
        yield return new WaitForSeconds(t);
        end();
    }

}
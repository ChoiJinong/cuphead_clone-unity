using UnityEngine;

public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T instance;
    public static T Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<T>();
                if (instance == null)
                {
                    var singletonObject = new GameObject($"@{typeof(T).Name}");
                    instance = singletonObject.AddComponent<T>();
                }
                DontDestroyOnLoad(instance.gameObject);
            }
            return instance;
        }
    }
}

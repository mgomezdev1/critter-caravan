using UnityEngine;

#nullable enable
public class PersistentSingletonBehaviour<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T? instance;
    protected static T Instance { 
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<T>();
                DontDestroyOnLoad(instance.gameObject);
            }
            return instance;
        }
    }

    protected virtual void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
        } 
        else
        {
            instance = this as T;
            DontDestroyOnLoad(instance!.gameObject);
        }
    }
}
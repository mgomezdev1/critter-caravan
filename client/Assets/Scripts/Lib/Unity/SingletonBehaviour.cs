using UnityEngine;

#nullable enable
public class SingletonBehaviour<T> : MonoBehaviour where T : Object
{
    private static T? instance;
    protected static T Instance { 
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<T>();
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
        }
    }
}
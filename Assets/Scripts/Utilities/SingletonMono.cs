using UnityEngine;

public class SingletonMono<T> : MonoBehaviour where T : Component
{
    private static T singleton = null;
    private static bool m_bAppClosed = false;

    public static T Singleton
    {
        get
        {
            if (m_bAppClosed) return null;
            if (singleton != null) return singleton;

            singleton = FindFirstObjectByType<T>();

            if (singleton == null)
            {
                GameObject go = new GameObject(typeof(T).Name);
                singleton = go.AddComponent<T>();
            }

            DontDestroyOnLoad(singleton.gameObject);
            return singleton;
        }
        protected set
        {
            if (null != singleton) return;
            singleton = value;
            DontDestroyOnLoad(singleton.gameObject);
        }
    }

    protected virtual void Awake()
    {
        if (singleton == null)
        {
            singleton = this as T;
            DontDestroyOnLoad(gameObject);
        }
        else if (singleton != this)
        {
            Destroy(gameObject);
        }
    }

    protected virtual void OnDestroy()
    {
        if (singleton == this) singleton = null;
    }

    protected virtual void OnApplicationQuit()
    {
        m_bAppClosed = true;
    }

    public static T Create()
    {
        return SingletonMono<T>.Singleton;
    }

    public static bool IsCreated()
    {
        return singleton != null;
    }

    private static void CreateSingleton()
    {
        string strID = (typeof(T)).ToString();
        Singleton = new GameObject(strID).AddComponent<T>();
    }
}
using UnityEngine;

public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
    private static bool _hasInstance;
    protected bool IsUsed = false;

    void Awake()
    {
        CheckDuplication();
        if (IsUsed && CheckDontDestroyOnLoad())
        {
            DontDestroyOnLoad(gameObject);
        }

        OnAwake();
    }

    void OnDestroy()
    {
        if (IsUsed && _instance == (this as T))
        {
            _hasInstance = false;
            _instance = null;
            IsUsed = false;
        }

        OnDestroyed();
    }

    protected virtual void OnAwake()
    {
    }

    protected virtual void OnDestroyed()
    {
    }

    protected virtual bool CheckDontDestroyOnLoad()
    {
        return true;
    }

    private void CheckDuplication()
    {
        T[] array = FindObjectsByType<T>(FindObjectsInactive.Include);

        if (array.Length < 2)
        {
            if (_instance == null)
            {
                SetInstance(this as T);
            }
            else
            {
                Destroy(gameObject);
            }
            return;
        }

        Singleton<T> survivor = null;
        foreach (T obj in array)
        {
            Singleton<T> s = obj as Singleton<T>;
            if (s != null && s.IsUsed)
            {
                survivor = s;
                break;
            }
        }

        if (survivor == null)
        {
            survivor = this;
        }

        foreach (T obj in array)
        {
            Singleton<T> s = obj as Singleton<T>;
            if (s != null && s != survivor)
            {
                Destroy(s.gameObject);
            }
        }

        if (survivor == this)
        {
            if (_instance == null)
            {
                SetInstance(this as T);
            }
        }
        else
        {
            Destroy(gameObject);
            if (_instance == null)
            {
                SetInstance(survivor as T);
            }
        }
    }

    private static void FindInstance()
    {
        T[] array = FindObjectsByType<T>(FindObjectsInactive.Include);
        if (array.Length >= 1)
        {
            SetInstance(array[0]);
        }
    }

    private static void FindOrCreateInstance()
    {
        T[] array = FindObjectsByType<T>(FindObjectsInactive.Include);
        if (array.Length >= 1)
        {
            SetInstance(array[0]);
            return;
        }

        GameObject prefab = Resources.Load<GameObject>("Singleton/" + typeof(T).Name);
        if (prefab != null)
        {
            Create(prefab);
        }
        else
        {
            Create(typeof(T).Name);
        }
    }

    protected static void SetInstance(T instance)
    {
        _instance = instance;
        _hasInstance = _instance != null;
        if (_instance is Singleton<T> singleton)
        {
            singleton.IsUsed = true;
        }
    }

    public static bool Exist()
    {
        if (!_hasInstance)
        {
            FindInstance();
        }

        return _hasInstance;
    }

    public static T Create(string name)
    {
        if (_hasInstance)
        {
            return _instance;
        }

        GameObject instance = new GameObject(name);
        if (!Application.isPlaying)
        {
            instance.hideFlags = HideFlags.HideAndDontSave;
        }

        T component = instance.AddComponent<T>();
        if (!_hasInstance)
        {
            SetInstance(component);
        }

        return _instance;
    }

    public static T Create(GameObject prefab)
    {
        if (_hasInstance)
        {
            return _instance;
        }

        GameObject instance = Instantiate(prefab);
        if (!Application.isPlaying)
        {
            instance.hideFlags = HideFlags.HideAndDontSave;
        }

        if (!_hasInstance)
        {
            if (instance.TryGetComponent(out T component))
            {
                SetInstance(component);
            }
        }

        return _instance;
    }

    public static T Instance()
    {
        if (!_hasInstance)
        {
            FindOrCreateInstance();
        }

        return _instance;
    }
}

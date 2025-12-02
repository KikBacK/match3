using System.Collections.Generic;
using UnityEngine;

public class ScGemPoolService
{
    private readonly Transform _parent;
    private readonly Dictionary<ScGem, Queue<ScGem>> _gemPools = new Dictionary<ScGem, Queue<ScGem>>();
    private readonly Dictionary<ScGem, ScGem> _instanceToPrefab = new Dictionary<ScGem, ScGem>();

    public ScGemPoolService(Transform parent)
    {
        _parent = parent;
    }

    // Prewarm pool with a specified amount of gems for each prefab
    public void Prewarm(ScGem prefab, int count)
    {
        if (!_gemPools.TryGetValue(prefab, out var pool))
        {
            pool = new Queue<ScGem>();
            _gemPools[prefab] = pool;
        }

        for (var i = 0; i < count; i++)
        {
            var gem = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
            gem.gameObject.SetActive(false);
            _instanceToPrefab[gem] = prefab;
            if (_parent != null)
                gem.transform.SetParent(_parent);
            pool.Enqueue(gem);
        }
    }

    private ScGem Get(ScGem prefab)
    {
        if (!_gemPools.TryGetValue(prefab, out var pool))
        {
            pool = new Queue<ScGem>();
            _gemPools[prefab] = pool;
        }

        ScGem gem;
        if (pool.Count > 0)
        {
            gem = pool.Dequeue();
        }
        else
        {
            gem = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
            _instanceToPrefab[gem] = prefab;
        }

        gem.gameObject.SetActive(true);
        // ensure pooled objects are parented correctly
        if (_parent != null)
            gem.transform.SetParent(_parent);

        return gem;
    }
    
    public ScGem Spawn(ScGem prefab, Vector3 position)
    {
        var gem = Get(prefab);
        gem.transform.position = position;
        return gem;
    }

    public void Release(ScGem gem)
    {
        if (gem == null)
            return;

        if (!_instanceToPrefab.TryGetValue(gem, out var prefab) || prefab == null)
        {
            // Fallback: if we don't know the prefab for this instance, destroy it to avoid leaking objects
            Object.Destroy(gem.gameObject);
            return;
        }

        if (!_gemPools.TryGetValue(prefab, out var pool))
        {
            pool = new Queue<ScGem>();
            _gemPools[prefab] = pool;
        }

        gem.gameObject.SetActive(false);
        pool.Enqueue(gem);
    }
}
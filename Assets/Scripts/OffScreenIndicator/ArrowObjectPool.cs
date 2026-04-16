using System.Collections.Generic;
using UnityEngine;

class ArrowObjectPool : MonoBehaviour
{
    public static ArrowObjectPool current;

    [Tooltip("Assign the arrow prefab.")]
    public Indicator pooledObject;
    [Tooltip("Initial pooled amount.")]
    public int pooledAmount = 1;
    [Tooltip("Should the pooled amount increase.")]
    public bool willGrow = true;

    List<Indicator> pooledObjects;

    void Awake()
    {
        current = this;
    }

    void Start()
    {
        pooledObjects = new List<Indicator>();

        for (int i = 0; i < pooledAmount; i++)
        {
            Indicator arrow = Instantiate(pooledObject);
            arrow.transform.SetParent(transform, false);
            arrow.Activate(false);
            pooledObjects.Add(arrow);
        }
    }

    /// <summary>
    /// Gets pooled objects from the pool.
    /// </summary>
    /// <returns></returns>
    public Indicator GetPooledObject()
    {
        for (int i = 0; i < pooledObjects.Count; i++)
        {
            if (!pooledObjects[i].Active)
            {
                return pooledObjects[i];
            }
        }
        if (willGrow)
        {
            Indicator arrow = Instantiate(pooledObject);
            arrow.transform.SetParent(transform, false);
            arrow.Activate(false);
            pooledObjects.Add(arrow);
            return arrow;
        }
        return null;
    }

    /// <summary>
    /// Deactive all the objects in the pool.
    /// </summary>
    public void DeactivateAllPooledObjects()
    {
        foreach (Indicator arrow in pooledObjects)
        {
            arrow.Activate(false);
        }
    }

    /// <summary>
    /// 활성 인디케이터 목록 반환 (orphan 감지용)
    /// </summary>
    public List<Indicator> GetAllActiveIndicators()
    {
        var result = new List<Indicator>();
        foreach (var ind in pooledObjects)
        {
            if (ind != null && ind.Active)
                result.Add(ind);
        }
        return result;
    }
}

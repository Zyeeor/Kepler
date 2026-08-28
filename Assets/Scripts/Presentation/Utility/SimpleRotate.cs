using UnityEngine;

/// <summary>
/// Continuously rotates the object. Optionally randomizes the rotation speed on start
/// so multiple instances (e.g. dropped gems) don't spin in lockstep.
/// </summary>
public class SimpleRotate : MonoBehaviour
{
    [Tooltip("基础旋转速度（度/秒）。")]
    public Vector3 rotationSpeed = new Vector3(0, 45f, 0);

    [Header("Randomize")]
    [Tooltip("启用后，Start 时在基础速度上叠加每轴 [-range, +range] 的随机偏移。")]
    public bool randomizeSpeed = false;
    [Tooltip("每轴速度的随机偏移范围（度/秒）；偏移可能为负，从而随机反向旋转。")]
    public Vector3 randomSpeedRange = new Vector3(0, 20f, 0);

    Vector3 _speed;

    void Start()
    {
        _speed = rotationSpeed;
        if (randomizeSpeed)
        {
            _speed.x += Random.Range(-randomSpeedRange.x, randomSpeedRange.x);
            _speed.y += Random.Range(-randomSpeedRange.y, randomSpeedRange.y);
            _speed.z += Random.Range(-randomSpeedRange.z, randomSpeedRange.z);
        }
    }

    void Update()
    {
        transform.Rotate(_speed * Time.deltaTime);
    }
}

using UnityEngine;

/// <summary>
/// Continuously rotates the object at a constant speed.
/// </summary>
public class SimpleRotate : MonoBehaviour
{
    public Vector3 rotationSpeed = new Vector3(0, 45f, 0);

    void Update()
    {
        transform.Rotate(rotationSpeed * Time.deltaTime);
    }
}

using UnityEngine;

public class SpinnerRotator : MonoBehaviour
{
    public float speed = 200f;
    void Update()
    {
        transform.Rotate(Vector3.up * speed * Time.deltaTime);
    }
}

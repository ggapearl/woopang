using UnityEngine;

public class SpinnerRotator : MonoBehaviour
{
    public float speed = 200f;
    public Vector3 axis = Vector3.back; // 기본값: 반시계 방향 (또는 Vector3.forward)

    void Update()
    {
        transform.Rotate(axis * speed * Time.deltaTime);
    }
}

using UnityEngine;

[RequireComponent(typeof(Rigidbody))] // Rigidbodyが必須であることを示す
public class ConstantMovement : MonoBehaviour
{
    public float moveSpeed = 5.0f;

    private Rigidbody rb;

    void Awake()
    {
        // 最初に一回だけRigidbodyコンポーネントを取得しておく
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        // 現在位置から、前方に移動した後の新しい位置を計算
        Vector3 newPosition = rb.position + (transform.forward * moveSpeed * Time.fixedDeltaTime);

        // Rigidbodyを新しい位置へ移動させる
        rb.MovePosition(newPosition);
    }
}
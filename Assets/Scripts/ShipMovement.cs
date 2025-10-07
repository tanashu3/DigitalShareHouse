using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ShipMovement : MonoBehaviour
{
    public float moveSpeed = 5.0f;
    private Rigidbody rb;
    private bool isMoving = false; // 船が動いて良いかの状態を管理するフラグ

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    // WorldManagerからこの命令が呼ばれると、船が動き出す
    public void StartMoving()
    {
        isMoving = true;
        Debug.Log("Ship movement has been started.");
    }

    void FixedUpdate()
    {
        // isMovingがtrueになるまで、この先の処理は実行されない
        if (!isMoving)
        {
            return;
        }

        Vector3 newPosition = rb.position + (transform.forward * moveSpeed * Time.fixedDeltaTime);
        rb.MovePosition(newPosition);
    }
}
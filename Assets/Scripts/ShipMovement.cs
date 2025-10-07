using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ShipMovement : MonoBehaviour
{
    public float moveSpeed = 5.0f;
    private Rigidbody rb;
    private bool isMoving = false; // �D�������ėǂ����̏�Ԃ��Ǘ�����t���O

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    // WorldManager���炱�̖��߂��Ă΂��ƁA�D�������o��
    public void StartMoving()
    {
        isMoving = true;
        Debug.Log("Ship movement has been started.");
    }

    void FixedUpdate()
    {
        // isMoving��true�ɂȂ�܂ŁA���̐�̏����͎��s����Ȃ�
        if (!isMoving)
        {
            return;
        }

        Vector3 newPosition = rb.position + (transform.forward * moveSpeed * Time.fixedDeltaTime);
        rb.MovePosition(newPosition);
    }
}
using UnityEngine;

[RequireComponent(typeof(Rigidbody))] // Rigidbody���K�{�ł��邱�Ƃ�����
public class ConstantMovement : MonoBehaviour
{
    public float moveSpeed = 5.0f;

    private Rigidbody rb;

    void Awake()
    {
        // �ŏ��Ɉ�񂾂�Rigidbody�R���|�[�l���g���擾���Ă���
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        // ���݈ʒu����A�O���Ɉړ�������̐V�����ʒu���v�Z
        Vector3 newPosition = rb.position + (transform.forward * moveSpeed * Time.fixedDeltaTime);

        // Rigidbody��V�����ʒu�ֈړ�������
        rb.MovePosition(newPosition);
    }
}
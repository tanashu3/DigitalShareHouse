using UnityEngine;

public class DragonCastleCamera : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("追従する対象（Ship_BigDracoを指定）")]
    public Transform target;

    [Tooltip("対象からの位置オフセット（城のテラスのような位置に調整）")]
    public Vector3 offset = new Vector3(0, 15f, -5f);

    [Header("Mouse Settings")]
    [Tooltip("マウス感度")]
    public float mouseSensitivity = 2.0f;
    [Tooltip("上下の視点制限（角度）")]
    public Vector2 pitchLimits = new Vector2(-60f, 80f);

    // 内部変数
    private float yaw = 0f;
    private float pitch = 0f;

    void Start()
    {
        // 最初の角度を現在のカメラの向きに合わせる
        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = angles.x;

        // カーソルを消してロックする（ゲームプレイ用）
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 1. マウス操作による視点回転（周囲を見渡す）
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        yaw += mouseX;
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, pitchLimits.x, pitchLimits.y);

        // 2. カメラの回転を適用
        // 船が回転しても、プレイヤーが見ている方角を維持したい場合は target.rotation を外す
        // 船の進行方向と一緒に視界も回したい場合は target.rotation を掛ける
        // ここでは「船に乗っている感」を出すため、船の回転に合わせて視界も回します
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);

        // 船がカーブしたときにカメラも追従して回るようにする計算
        // target.rotation（船の向き）を基準に、マウスの回転（rotation）を加える
        transform.rotation = target.rotation * rotation;

        // 3. 位置の追従（ドラゴンの動きに合わせて移動）
        // LateUpdateを使うことで、船が移動し終わった後の位置にカメラを置く（ガタつき防止）

        // targetの現在位置 + targetの向きに基づいたオフセット
        Vector3 desiredPosition = target.position + target.TransformDirection(offset);
        transform.position = desiredPosition;
    }
}
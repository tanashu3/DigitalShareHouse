using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ShipMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("船の移動速度")]
    public float moveSpeed = 5.0f;
    [Tooltip("移動を開始するためのキー")]
    public KeyCode startKey = KeyCode.H;

    [Tooltip("進行方向を180度反転させるか")]
    public bool invertDirection = true; // ★ここをONにすると逆走します

    [Header("Animation Settings")]
    [Tooltip("ドラゴンのAnimatorコンポーネント")]
    public Animator dragonAnimator;
    [Tooltip("Animatorで作成したBool型パラメータの名前")]
    public string movingParameterName = "IsMoving";

    [Tooltip("アニメーション速度が1.0（標準）に見えるときの移動速度")]
    public float baseAnimationSpeed = 5.0f; // ★この速度を基準にアニメ再生速度を伸縮します

    // 内部変数
    private Rigidbody rb;
    private bool isMoving = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (dragonAnimator == null)
        {
            dragonAnimator = GetComponentInChildren<Animator>();
        }

        // 初期化
        isMoving = false;
        if (dragonAnimator != null)
        {
            dragonAnimator.SetBool(movingParameterName, false);
            dragonAnimator.speed = 1.0f; // 待機モーションは標準速度で
        }
    }

    void Update()
    {
        // キー入力待ち
        if (!isMoving && Input.GetKeyDown(startKey))
        {
            StartMovingSequence();
        }

        // 移動中の場合、移動速度を変えたらリアルタイムでアニメ速度も変える処理
        if (isMoving && dragonAnimator != null && baseAnimationSpeed > 0)
        {
            // 現在の速度 / 基準速度 = アニメーション倍率
            // 例: 速度10で走るなら、アニメも2倍速(10/5)にする
            dragonAnimator.speed = moveSpeed / baseAnimationSpeed;
        }
    }

    private void StartMovingSequence()
    {
        Debug.Log("キー入力検知: 船の移動を開始します。");
        isMoving = true;

        if (dragonAnimator != null)
        {
            dragonAnimator.SetBool(movingParameterName, true);
        }
    }

    void FixedUpdate()
    {
        if (!isMoving) return;

        // ★進行方向の決定（invertDirectionがONなら -forward、OFFなら forward）
        Vector3 forwardDir = invertDirection ? -transform.forward : transform.forward;

        Vector3 newPosition = rb.position + (forwardDir * moveSpeed * Time.fixedDeltaTime);
        rb.MovePosition(newPosition);
    }
}
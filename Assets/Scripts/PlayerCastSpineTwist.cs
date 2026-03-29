using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerCastSpineTwist : MonoBehaviour
{
    [Tooltip("Chest yaw during a cast. Sign auto-flips for L vs R so each arm swings toward screen centre.")]
    [Range(0f, 60f)] public float twistDegrees = 30f;

    [Tooltip("Ease in/out time, in seconds.")]
    [Range(0.01f, 1f)] public float smoothTime = 0.2f;

    private Animator animator;
    private int upperBodyLayerIndex = -1;
    private int castLHash;
    private int castRHash;
    private float currentTwist;
    private float twistVelocity;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        castLHash = Animator.StringToHash("CastL");
        castRHash = Animator.StringToHash("CastR");
    }

    private void Start()
    {
        if (animator == null) return;
        upperBodyLayerIndex = animator.GetLayerIndex("UpperBody");
        if (upperBodyLayerIndex < 0 && animator.layerCount > 1) upperBodyLayerIndex = 1;
    }

    private void LateUpdate()
    {
        if (animator == null || upperBodyLayerIndex <= 0) return;

        float w = animator.GetLayerWeight(upperBodyLayerIndex);
        float sign = ResolveSign();
        float target = (w > 0.001f && sign != 0f) ? twistDegrees * sign * w : 0f;

        currentTwist = Mathf.SmoothDamp(currentTwist, target, ref twistVelocity, smoothTime);

        if (Mathf.Abs(currentTwist) < 0.05f) return;

        Transform bone = animator.GetBoneTransform(HumanBodyBones.UpperChest);
        if (bone == null) bone = animator.GetBoneTransform(HumanBodyBones.Chest);
        if (bone == null) bone = animator.GetBoneTransform(HumanBodyBones.Spine);
        if (bone == null) return;

        bone.rotation = Quaternion.AngleAxis(currentTwist, transform.up) * bone.rotation;
    }

    private float ResolveSign()
    {
        AnimatorStateInfo cur = animator.GetCurrentAnimatorStateInfo(upperBodyLayerIndex);
        if (cur.shortNameHash == castLHash) return 1f;
        if (cur.shortNameHash == castRHash) return -1f;
        AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(upperBodyLayerIndex);
        if (next.shortNameHash == castLHash) return 1f;
        if (next.shortNameHash == castRHash) return -1f;
        return 0f;
    }
}

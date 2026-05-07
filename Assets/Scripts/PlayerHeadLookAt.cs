using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerHeadLookAt : MonoBehaviour
{
    [Tooltip("Distance ahead of the camera the head aims. Larger = subtler vertical tilt.")]
    public float lookAheadDistance = 15f;

    [Tooltip("Overall look-at IK blend. 0 = off, 1 = full.")]
    [Range(0f, 1f)] public float lookAtWeight = 1f;

    [Tooltip("Body twist toward the look point. Keep low so locomotion isn't distorted.")]
    [Range(0f, 1f)] public float bodyWeight = 0f;

    [Range(0f, 1f)] public float headWeight = 1f;

    [Tooltip("Eye tracking. Only works if the avatar has eye bones mapped.")]
    [Range(0f, 1f)] public float eyesWeight = 1f;

    [Tooltip("Max allowed deviation. 0 = no clamp, 1 = strong clamp.")]
    [Range(0f, 1f)] public float clampWeight = 0.5f;

    private Animator animator;
    private Camera cam;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        cam = Camera.main;
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (animator == null) return;
        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null) return;
        }

        Vector3 lookPoint = cam.transform.position + cam.transform.forward * lookAheadDistance;
        animator.SetLookAtPosition(lookPoint);
        animator.SetLookAtWeight(lookAtWeight, bodyWeight, headWeight, eyesWeight, clampWeight);
    }
}

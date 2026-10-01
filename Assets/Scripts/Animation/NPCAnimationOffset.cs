using UnityEngine;

public class NPCAnimationOffset : MonoBehaviour
{
    void Start()
    {
        Animator animator = GetComponent<Animator>();

        // Controller state
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

        animator.Update(0); // initialize animator
        float offset = Random.Range(0f, 1f);

        
        animator.Play(stateInfo.fullPathHash, 0, offset);
    }
}
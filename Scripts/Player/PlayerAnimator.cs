using UnityEngine;

public class PlayerAnimator : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private Transform runnersParent;

    public void Run()
    {
        for (int i = 0; i < runnersParent.childCount; i++)
        {
            Transform runner = runnersParent.GetChild(i);
            if (!runner.gameObject.activeSelf)
                continue;

            Animator runnerAnimator = runner.GetComponent<Runner>().GetAnimator();
            runnerAnimator.Play("Run");
        }
    }

    public void Idle()
    {
        for (int i = 0; i < runnersParent.childCount; i++)
        {
            Transform runner = runnersParent.GetChild(i);
            if (!runner.gameObject.activeSelf)
                continue;

            Animator runnerAnimator = runner.GetComponent<Runner>().GetAnimator();

            runnerAnimator.Play("Idle");
        }
    }
}

using UnityEngine;

[RequireComponent(typeof(Animator))]
public abstract class BossBase : MonoBehaviour
{
    public enum BossState { Idle, Attack }

    private bool hasState;
    public BossState CurrentState { get; private set; }
    protected float StateElapsed { get; private set; }
    protected Animator BossAnimator { get; private set; }

    protected virtual void Awake() => BossAnimator = GetComponent<Animator>();
    protected virtual void OnEnable() => ChangeState(BossState.Idle);
    private void Update() => Tick(Time.deltaTime);

    private void Tick(float deltaTime)
    {
        if (!hasState) return;
        StateElapsed += Mathf.Max(0f, deltaTime);
        UpdateState();
    }

    protected void ChangeState(BossState nextState)
    {
        if (hasState && CurrentState == nextState) return;
        if (hasState) ExitState(CurrentState);
        CurrentState = nextState;
        StateElapsed = 0f;
        hasState = true;
        EnterState(nextState);
    }

    protected void PlayAnimation(int stateHash)
    {
        BossAnimator.Play(stateHash, 0, 0f);
        BossAnimator.Update(0f);
    }

    protected abstract void EnterState(BossState state);
    protected abstract void UpdateState();
    protected abstract void ExitState(BossState state);

    protected virtual void OnDisable()
    {
        // 비활성화를 공격 완료로 처리해서 사이클이 진행되지 않게 합니다.
        hasState = false;
        StateElapsed = 0f;
    }
}
